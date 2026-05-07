using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

public abstract class BasePage
{
    protected readonly IPage Page;
    protected readonly string BaseUrl;

    protected BasePage(IPage page, string baseUrl)
    {
        Page = page;
        BaseUrl = baseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Navigate to a relative path on the app, then wait for Blazor to finish rendering.
    /// </summary>
    protected async Task NavigateToAsync(string relativePath)
    {
        await Page.GotoAsync($"{BaseUrl}{relativePath}");
        await DismissReconnectionModalAsync();
        await WaitForBlazorAsync();
    }

    /// <summary>
    /// Wait for Blazor WASM to finish rendering by detecting known DOM elements
    /// (headings, map container, or page content).
    /// Falls back to waiting for #app to have non-empty inner HTML.
    /// </summary>
    protected async Task WaitForBlazorAsync(int timeoutMs = 30000)
    {
        try
        {
            await Page.WaitForSelectorAsync("h1, h3, #ruckr-map, .page", new PageWaitForSelectorOptions
            {
                Timeout = timeoutMs,
                State = WaitForSelectorState.Visible
            });
        }
        catch (TimeoutException)
        {
            // Fallback: wait for #app to have non-empty inner HTML
            await Page.WaitForFunctionAsync(@"() => {
                const app = document.getElementById('app');
                return app && app.children.length > 0 && app.innerHTML.trim().length > 0;
            }", null, new PageWaitForFunctionOptions { Timeout = 5000 });
        }
    }

    /// <summary>
    /// Wait for the Bootstrap loading spinner to disappear (hidden or detached).
    /// </summary>
    protected async Task WaitForLoadingSpinnerToDisappearAsync(int timeoutMs = 10000)
    {
        try
        {
            await Page.WaitForSelectorAsync(".spinner-border", new PageWaitForSelectorOptions
            {
                Timeout = timeoutMs,
                State = WaitForSelectorState.Hidden
            });
        }
        catch (TimeoutException)
        {
            // Spinner may already be gone or was never present
        }
    }

    /// <summary>
    /// Remove the Blazor reconnect modal from the DOM if it exists.
    /// This prevents it from blocking headless test interactions.
    /// </summary>
    protected async Task DismissReconnectionModalAsync()
    {
        try
        {
            await Page.EvaluateAsync("() => document.getElementById('components-reconnect-modal')?.remove()");
        }
        catch
        {
            // Modal may not exist
        }
    }

    /// <summary>
    /// Take a screenshot for debugging purposes. Saved to the screenshots/ directory.
    /// </summary>
    public async Task<string> ScreenshotAsync(string name)
    {
        var directory = "screenshots";
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        var path = $"{directory}/{name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";
        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
        return path;
    }

    /// <summary>
    /// Get the current page title.
    /// </summary>
    public async Task<string> GetTitleAsync() => await Page.TitleAsync() ?? string.Empty;

    /// <summary>
    /// Wait for an element with the given data-testid to become visible.
    /// </summary>
    protected async Task<IElementHandle?> WaitForTestIdAsync(string testId, int timeoutMs = 10000)
    {
        return await Page.WaitForSelectorAsync($"[data-testid='{testId}']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = timeoutMs
        });
    }

    /// <summary>
    /// Public wrapper for Blazor readiness detection. Exposed for test orchestration
    /// where a test needs to await Blazor rendering before interacting with nav/page objects.
    /// </summary>
    public async Task WaitForBlazorReadyAsync(int timeoutMs = 30000)
    {
        await WaitForBlazorAsync(timeoutMs);
    }

    /// <summary>
    /// Check whether an element matching the data-testid exists in the DOM.
    /// </summary>
    protected async Task<bool> ExistsByTestIdAsync(string testId)
    {
        var element = await Page.QuerySelectorAsync($"[data-testid='{testId}']");
        return element != null;
    }

    /// <summary>
    /// Check whether an element matching the selector exists in the DOM.
    /// </summary>
    protected async Task<bool> ExistsAsync(string selector)
    {
        var element = await Page.QuerySelectorAsync(selector);
        return element != null;
    }
}
