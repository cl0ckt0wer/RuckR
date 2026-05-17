using Microsoft.Playwright;

namespace RuckR.Tests.Helpers;

    /// <summary>
    /// Provides access to class.
    /// </summary>
public static class BlazorWaitHelper
{
    /// <summary>
    /// Wait for a Blazor-rendered element containing the specified text.
    /// </summary>
    public static async Task<IElementHandle?> WaitForBlazorTextAsync(
        this IPage page, string text, int timeoutMs = 15000)
    {
        return await page.WaitForSelectorAsync($"text={text}", new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs,
            State = WaitForSelectorState.Visible
        });
    }

    /// <summary>
    /// Wait for the Blazor WASM initial load by polling for the reconnect modal to disappear.
    /// </summary>
    public static async Task WaitForBlazorLoadAsync(this IPage page, int timeoutMs = 45000)
    {
        try
        {
            await page.WaitForSelectorAsync("#components-reconnect-modal", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = timeoutMs
            });
        }
        catch (TimeoutException)
        {
            // The modal never appeared or already disappeared
        }

        // Small buffer for initial rendering
        await page.WaitForTimeoutAsync(500);
    }

    /// <summary>
    /// Wait for the page to be fully loaded: network idle, Blazor content visible,
    /// and no loading spinner present.
    /// </summary>
    public static async Task WaitForPageFullyLoadedAsync(this IPage page, int timeoutMs = 30000)
    {
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
        {
            Timeout = timeoutMs
        });

        await page.WaitForSelectorAsync("h1, h3, .alert, .card", new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs,
            State = WaitForSelectorState.Visible
        });

        try
        {
            await page.WaitForSelectorAsync(".spinner-border", new PageWaitForSelectorOptions
            {
                Timeout = 10000,
                State = WaitForSelectorState.Hidden
            });
        }
        catch (TimeoutException)
        {
            // Spinner may already be gone
        }
    }

    /// <summary>
    /// Wait for Fluxor store initialization by detecting the SignalR connection status dot.
    /// The .status-dot element appears inside ConnectionStatus after SignalR connects,
    /// which implies the Fluxor store has been initialized.
    /// </summary>
    public static async Task WaitForFluxorReadyAsync(this IPage page, int timeoutMs = 15000)
    {
        await page.WaitForSelectorAsync(".status-dot", new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs,
            State = WaitForSelectorState.Visible
        });
    }

    /// <summary>
    /// Wait for a toast notification containing the specified text to appear.
    /// Throws TimeoutException if the toast is not found within the timeout.
    /// </summary>
    public static async Task ExpectToastAsync(this IPage page, string text, int timeoutMs = 10000)
    {
        var toast = await page.WaitForSelectorAsync($".toast:has-text('{text}')", new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs,
            State = WaitForSelectorState.Visible
        });
        if (toast is null)
            throw new TimeoutException($"Toast with text '{text}' not found within {timeoutMs}ms");
    }
}


