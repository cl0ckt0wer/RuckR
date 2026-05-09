using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RuckR.Shared.Models;

namespace RuckR.Client.Services;

public class TelemetryLoggerProvider : ILoggerProvider, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentQueue<ClientLogEntry> _queue = new();
    private readonly string _sessionId = Guid.NewGuid().ToString("N")[..8];
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private Task? _flushLoop;

    public TelemetryLoggerProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public ILogger CreateLogger(string categoryName)
        => new TelemetryLogger(categoryName, this);

    internal void Enqueue(ClientLogEntry entry)
    {
        if (_queue.Count < 1000)
            _queue.Enqueue(entry);
        _flushLoop ??= FlushLoopAsync(_cts.Token);

        if (string.Equals(entry.LogLevel, nameof(LogLevel.Error), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.LogLevel, nameof(LogLevel.Critical), StringComparison.OrdinalIgnoreCase))
        {
            _ = FlushAsync();
        }
    }

    private async Task FlushLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                await FlushAsync();
            }
            catch (OperationCanceledException) { break; }
            catch { /* swallow — bridge failures must not crash app */ }
        }
    }

    private async Task FlushAsync()
    {
        if (_queue.IsEmpty) return;

        if (!await _flushLock.WaitAsync(0)) return;

        try
        {
            var batch = new ClientLogBatch { SessionId = _sessionId };
            while (batch.Entries.Count < 50 && _queue.TryDequeue(out var entry))
                batch.Entries.Add(entry);

            if (batch.Entries.Count == 0) return;

            using var response = await _httpClient.PostAsJsonAsync("api/telemetry", batch);

            if (!response.IsSuccessStatusCode)
            {
                foreach (var entry in batch.Entries)
                    _queue.Enqueue(entry);
            }
        }
        catch { /* network down — bridge failures must not crash app */ }
        finally
        {
            _flushLock.Release();
        }
    }

    void IDisposable.Dispose()
    {
        // Non-blocking — DI may call this; actual cleanup in DisposeAsync
        _cts.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        if (_flushLoop is not null)
            await _flushLoop; // wait for loop to exit
        await FlushAsync(); // final flush
        _cts.Dispose();
    }
}

internal class TelemetryLogger : ILogger
{
    private readonly string _category;
    private readonly TelemetryLoggerProvider _provider;

    public TelemetryLogger(string category, TelemetryLoggerProvider provider)
    {
        _category = category;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        _provider.Enqueue(new ClientLogEntry
        {
            Timestamp = DateTime.UtcNow,
            LogLevel = logLevel.ToString(),
            Category = _category,
            Message = formatter(state, exception),
            Exception = exception?.ToString()
        });
    }
}
