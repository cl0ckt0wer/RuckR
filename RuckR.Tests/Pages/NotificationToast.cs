using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

/// <summary>
/// Page Object for the NotificationToast shared component — fixed-position
/// toast notifications for challenge invites and battle results.
/// </summary>
public class NotificationToast : BasePage
{
    public NotificationToast(IPage page, string baseUrl) : base(page, baseUrl) { }

    // ── Challenge toast ────────────────────────────────────────────────

    /// <summary>
    /// Wait for a challenge toast ("⚔️ Challenge!") to appear within the timeout.
    /// Returns true if the toast appeared; false on timeout.
    /// </summary>
    public async Task<bool> WaitForChallengeToastAsync(int timeoutMs = 15000)
    {
        try
        {
            await Page.WaitForSelectorAsync(".toast:has-text('⚔️ Challenge!')", new PageWaitForSelectorOptions
            {
                Timeout = timeoutMs,
                State = WaitForSelectorState.Visible
            });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Click the "Accept" button inside the challenge toast.
    /// </summary>
    public async Task AcceptChallengeFromToastAsync()
    {
        var acceptBtn = Page.Locator(".toast:has-text('Challenge!') .btn-success", new() { HasText = "Accept" });
        await acceptBtn.ClickAsync();
    }

    /// <summary>
    /// Click the "Decline" button inside the challenge toast.
    /// </summary>
    public async Task DeclineChallengeFromToastAsync()
    {
        var declineBtn = Page.Locator(".toast:has-text('Challenge!') .btn-outline-danger", new() { HasText = "Decline" });
        await declineBtn.ClickAsync();
    }

    // ── Battle result toast ────────────────────────────────────────────

    /// <summary>
    /// Wait for a battle result toast ("🏆 Battle Complete!") to appear within the timeout.
    /// Returns true if the toast appeared; false on timeout.
    /// </summary>
    public async Task<bool> WaitForBattleResultToastAsync(int timeoutMs = 15000)
    {
        try
        {
            await Page.WaitForSelectorAsync(".toast:has-text('🏆 Battle Complete!')", new PageWaitForSelectorOptions
            {
                Timeout = timeoutMs,
                State = WaitForSelectorState.Visible
            });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    // ── General toast helpers ──────────────────────────────────────────

    /// <summary>
    /// Get the combined text content of the first visible toast.
    /// Returns null if no toast is visible.
    /// </summary>
    public async Task<string?> GetToastTextAsync()
    {
        var toast = Page.Locator(".toast.show").First;
        if (await toast.IsVisibleAsync())
            return await toast.TextContentAsync();
        return null;
    }

    /// <summary>
    /// Check whether any toast is currently visible on screen.
    /// </summary>
    public async Task<bool> IsToastVisibleAsync()
    {
        var toast = Page.Locator(".toast.show");
        return await toast.IsVisibleAsync();
    }

    /// <summary>
    /// Dismiss (close) the first visible toast by clicking its close button.
    /// Does nothing if no toast is visible.
    /// </summary>
    public async Task DismissToastAsync()
    {
        var closeBtn = Page.Locator(".toast.show .btn-close").First;
        if (await closeBtn.IsVisibleAsync())
        {
            await closeBtn.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
        }
    }
}
