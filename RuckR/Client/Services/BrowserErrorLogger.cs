using Microsoft.JSInterop;
using RuckR.Shared.Models;

namespace RuckR.Client.Services;

public sealed class BrowserErrorLogger : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly TelemetryLoggerProvider _telemetryLoggerProvider;
    private IJSObjectReference? _module;
    private DotNetObjectReference<BrowserErrorLogger>? _dotNetObjectReference;

    public BrowserErrorLogger(IJSRuntime jsRuntime, TelemetryLoggerProvider telemetryLoggerProvider)
    {
        _jsRuntime = jsRuntime;
        _telemetryLoggerProvider = telemetryLoggerProvider;
    }

    public async Task InitializeAsync()
    {
        _dotNetObjectReference = DotNetObjectReference.Create(this);
        _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/client-logging.module.js");
        await _module.InvokeVoidAsync("start", _dotNetObjectReference);
    }

    [JSInvokable]
    public void LogBrowserError(string message, string? stack, string? url, string? userAgent)
    {
        _telemetryLoggerProvider.Enqueue(new ClientLogEntry
        {
            Timestamp = DateTime.UtcNow,
            LogLevel = nameof(LogLevel.Error),
            Category = "Browser",
            Message = $"Browser client error: {message}",
            Exception = stack,
            Url = url,
            UserAgent = userAgent
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("stop");
            await _module.DisposeAsync();
        }

        _dotNetObjectReference?.Dispose();
    }
}
