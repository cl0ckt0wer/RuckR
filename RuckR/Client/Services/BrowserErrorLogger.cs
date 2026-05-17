using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;
using RuckR.Shared.Models;

namespace RuckR.Client.Services;

/// <summary>
/// Bridges browser-side JavaScript errors into server-side telemetry logging.
/// </summary>
public sealed class BrowserErrorLogger : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly TelemetryLoggerProvider _telemetryLoggerProvider;
    private IJSObjectReference? _module;
    private DotNetObjectReference<BrowserErrorLogger>? _dotNetObjectReference;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    /// <summary>
    /// Creates a new browser error logger bridge.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime used to import and invoke error-logging script.</param>
    /// <param name="telemetryLoggerProvider">Telemetry logger to enqueue browser logs.</param>
    [DynamicDependency(nameof(LogBrowserError))]
    public BrowserErrorLogger(IJSRuntime jsRuntime, TelemetryLoggerProvider telemetryLoggerProvider)
#pragma warning restore CS8618
    {
        _jsRuntime = jsRuntime;
        _telemetryLoggerProvider = telemetryLoggerProvider;
    }

    /// <summary>
    /// Initializes the JavaScript logger bridge and starts forwarding browser errors.
    /// </summary>
    public async Task InitializeAsync()
    {
        _dotNetObjectReference = DotNetObjectReference.Create(this);
        _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/browser-logging.module.js");
        await _module.InvokeVoidAsync("start", _dotNetObjectReference);
    }

    /// <summary>
    /// Receives a browser error and forwards it to telemetry batching.
    /// </summary>
    /// <param name="message">Error message text.</param>
    /// <param name="stack">Error stack trace, if any.</param>
    /// <param name="url">URL where the error occurred.</param>
    /// <param name="userAgent">Browser user-agent string.</param>
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

    /// <summary>
    /// Stops the JS logger and releases interop resources.
    /// </summary>
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
