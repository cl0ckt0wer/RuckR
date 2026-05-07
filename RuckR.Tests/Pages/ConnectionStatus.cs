using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

/// <summary>
/// Page Object for the ConnectionStatus shared component — displays the
/// real-time SignalR connection state (Connected / Reconnecting... / Disconnected).
/// </summary>
public class ConnectionStatus : BasePage
{
    public ConnectionStatus(IPage page, string baseUrl) : base(page, baseUrl) { }

    // ── Connection state text ──────────────────────────────────────────

    /// <summary>
    /// Get the current connection state as one of three string values:
    /// "Connected", "Reconnecting...", or "Disconnected".
    /// Falls back to reading the status-dot CSS class if the text element
    /// is not available (e.g., component not yet rendered).
    /// </summary>
    public async Task<string> GetConnectionStateAsync()
    {
        // Prefer the explicit status text
        var textEl = await Page.QuerySelectorAsync(".connection-status .status-text");
        if (textEl is not null)
        {
            var text = await textEl.TextContentAsync();
            if (!string.IsNullOrWhiteSpace(text))
                return text.Trim();
        }

        // Fall back to CSS class on the dot
        var dot = await Page.QuerySelectorAsync(".connection-status .status-dot");
        if (dot is not null)
        {
            var classList = await dot.GetAttributeAsync("class");
            if (!string.IsNullOrEmpty(classList))
            {
                if (classList.Contains("connected"))
                    return "Connected";
                if (classList.Contains("reconnecting"))
                    return "Reconnecting...";
                if (classList.Contains("disconnected"))
                    return "Disconnected";
            }
        }

        return "Disconnected";
    }

    // ── Boolean state checks ───────────────────────────────────────────

    /// <summary>
    /// Check whether the connection is in the "Connected" state.
    /// Looks for the green dot class or "Connected" text.
    /// </summary>
    public async Task<bool> IsConnectedAsync()
    {
        var state = await GetConnectionStateAsync();
        return state == "Connected";
    }

    /// <summary>
    /// Check whether the connection is in the "Reconnecting..." state.
    /// Looks for the yellow dot class or "Reconnecting..." text.
    /// </summary>
    public async Task<bool> IsReconnectingAsync()
    {
        var state = await GetConnectionStateAsync();
        return state == "Reconnecting...";
    }

    /// <summary>
    /// Check whether the connection is in the "Disconnected" state.
    /// Looks for the red dot class or "Disconnected" text.
    /// </summary>
    public async Task<bool> IsDisconnectedAsync()
    {
        var state = await GetConnectionStateAsync();
        return state == "Disconnected";
    }

    // ── Reconnect ──────────────────────────────────────────────────────

    /// <summary>
    /// Click the "Reconnect" button shown when the connection is disconnected.
    /// </summary>
    public async Task ClickReconnectAsync()
    {
        var reconnectBtn = Page.Locator(".connection-status button", new() { HasText = "Reconnect" });
        if (await reconnectBtn.IsVisibleAsync())
        {
            await reconnectBtn.ClickAsync();
        }
    }
}
