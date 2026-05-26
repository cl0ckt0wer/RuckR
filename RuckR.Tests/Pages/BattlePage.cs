using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

/// <summary>
/// Page Object for the /battle page — the Battle Arena with tabbed interface
/// for sending challenges and managing active battles.
/// </summary>
public class BattlePage : BasePage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="""BattlePage"""/> class.
    /// </summary>
    /// <param name="page">The page to use.</param>
    /// <param name="baseUrl">The baseUrl to use.</param>
    public BattlePage(IPage page, string baseUrl) : base(page, baseUrl) { }

    /// <summary>
    /// Navigate to /battle with optional opponent query param.
    /// </summary>
    public async Task GoToAsync(string? opponent = null, int? playerId = null)
    {
        var path = "/battle";
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(opponent))
            queryParams.Add($"opponent={Uri.EscapeDataString(opponent)}");
        if (queryParams.Count > 0)
            path += "?" + string.Join("&", queryParams);
        await NavigateToAsync(path);
    }

    // ── Page loading ───────────────────────────────────────────────────

    /// <summary>
    /// Wait for the battle page tabs to render, indicating the page has loaded.
    /// </summary>
    public async Task WaitForBattlePageLoadedAsync(int timeoutMs = 15000)
    {
        await Page.WaitForSelectorAsync("[data-testid='battle-tabs']", new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs,
            State = WaitForSelectorState.Visible
        });
    }

    // ── Tab navigation ─────────────────────────────────────────────────

    /// <summary>Click the "New Challenge" tab and wait for the form to appear.</summary>
    public async Task SelectChallengeTabAsync()
    {
        await Page.GetByTestId("challenge-tab").ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='challenge-card']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });
    }

    /// <summary>Click the incoming challenges tab and wait for content.</summary>
    public async Task SelectActiveChallengesTabAsync()
    {
        await Page.GetByTestId("incoming-tab").ClickAsync();
        try
        {
            await Task.WhenAny(
                Page.WaitForSelectorAsync("[data-testid='incoming-challenges']", new() { Timeout = 5000 }),
                Page.WaitForSelectorAsync("[data-testid='battle-empty']", new() { Timeout = 5000 })
            );
        }
        catch { }
    }

    // ── New Challenge form ─────────────────────────────────────────────

    /// <summary>
    /// Selection is no longer part of challenge creation.
    /// </summary>
    public async Task SelectPlayerForChallengeAsync(int index)
    {
        await Task.CompletedTask;
    }

    /// <summary>Fill the opponent username input field.</summary>
    public async Task EnterOpponentUsernameAsync(string username)
    {
        await Page.GetByTestId("opponent-input").FillAsync(username);
    }

    /// <summary>Click the "Send Challenge" submit button.</summary>
    public async Task ClickSendChallengeAsync()
    {
        await Page.GetByTestId("send-challenge-btn").ClickAsync();
    }

    /// <summary>
    /// Get the rate limit text showing how many challenges have been sent this hour.
    /// </summary>
    public async Task<string?> GetRateLimitTextAsync()
    {
        return await Page.GetByTestId("rate-limit-info").TextContentAsync();
    }

    // ── Active Challenges ──────────────────────────────────────────────

    /// <summary>
    /// Count the number of incoming challenge cards visible on the page.
    /// Assumes the "Active Challenges" tab is selected.
    /// </summary>
    public async Task<int> GetIncomingChallengeCountAsync()
    {
        var cards = await Page.QuerySelectorAllAsync("[data-testid='incoming-challenges'] [data-testid='pending-card']");
        return cards.Count;
    }

    /// <summary>
    /// Accept an incoming challenge by zero-based index, then submit the first recruit and move.
    /// </summary>
    public async Task AcceptChallengeAsync(int challengeIndex)
    {
        var acceptButtons = Page.Locator("[data-testid='incoming-challenges'] [data-testid='accept-btn']");
        var count = await acceptButtons.CountAsync();
        if (challengeIndex >= 0 && challengeIndex < count)
        {
            await acceptButtons.Nth(challengeIndex).ClickAsync();
            await Page.WaitForSelectorAsync("[data-testid='selection-modal']", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5000
            });
            var modalSelect = Page.GetByTestId("selection-player-select");
            await modalSelect.SelectOptionAsync(new SelectOptionValue { Index = 1 });
            var moveSelect = Page.GetByTestId("selection-move-select");
            await moveSelect.SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await Page.GetByTestId("selection-submit-btn").ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }
    }

    /// <summary>
    /// Decline an incoming challenge by zero-based index.
    /// </summary>
    public async Task DeclineChallengeAsync(int challengeIndex)
    {
        var declineButtons = Page.Locator("[data-testid='incoming-challenges'] [data-testid='decline-btn']");
        var count = await declineButtons.CountAsync();
        if (challengeIndex >= 0 && challengeIndex < count)
        {
            await declineButtons.Nth(challengeIndex).ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }
    }

    // ── Battle Result Modal ────────────────────────────────────────────

    /// <summary>
    /// Wait for the battle result modal ("Victory!" or "Defeat") to appear.
    /// </summary>
    public async Task WaitForBattleResultModalAsync(int timeoutMs = 10000)
    {
        await Page.WaitForSelectorAsync("[data-testid='result-modal']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = timeoutMs
        });
    }

    /// <summary>Get the full text content from the battle result modal.</summary>
    public async Task<string?> GetBattleResultTextAsync()
    {
        var modal = await Page.QuerySelectorAsync("[data-testid='result-modal']");
        return modal is not null ? await modal.TextContentAsync() : null;
    }

    /// <summary>Close the battle result modal by clicking the Close button.</summary>
    public async Task CloseBattleResultModalAsync()
    {
        await Page.GetByTestId("result-close-btn").ClickAsync();
        await Page.WaitForTimeoutAsync(300);
    }

    // ── State checks ───────────────────────────────────────────────────

    /// <summary>Check whether the empty state ("No active challenges") is visible.</summary>
    public async Task<bool> IsEmptyStateVisibleAsync()
    {
        return await ExistsAsync("[data-testid='battle-empty']");
    }

    /// <summary>Check whether the loading spinner is visible.</summary>
    public async Task<bool> IsLoadingVisibleAsync()
    {
        return await ExistsAsync("[data-testid='battle-loading']");
    }

    /// <summary>Check whether the error alert is visible.</summary>
    public async Task<bool> IsErrorVisibleAsync()
    {
        return await ExistsAsync("[data-testid='battle-error']");
    }
}


