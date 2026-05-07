using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

/// <summary>
/// Page Object for the /battle page — the Battle Arena with tabbed interface
/// for sending challenges and managing active battles.
/// </summary>
public class BattlePage : BasePage
{
    public BattlePage(IPage page, string baseUrl) : base(page, baseUrl) { }

    /// <summary>
    /// Navigate to /battle with optional opponent and playerId query params.
    /// </summary>
    public async Task GoToAsync(string? opponent = null, int? playerId = null)
    {
        var path = "/battle";
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(opponent))
            queryParams.Add($"opponent={Uri.EscapeDataString(opponent)}");
        if (playerId.HasValue)
            queryParams.Add($"playerId={playerId.Value}");
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
        await Page.WaitForSelectorAsync(".nav-tabs", new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs,
            State = WaitForSelectorState.Visible
        });
    }

    // ── Tab navigation ─────────────────────────────────────────────────

    /// <summary>Click the "New Challenge" tab and wait for the form to appear.</summary>
    public async Task SelectChallengeTabAsync()
    {
        await Page.Locator(".nav-link", new() { HasText = "New Challenge" }).ClickAsync();
        // Wait for the "Send a Challenge" card to render in this tab
        await Page.WaitForSelectorAsync("text=Send a Challenge", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });
    }

    /// <summary>Click the "Active Challenges" tab and wait for the section heading.</summary>
    public async Task SelectActiveChallengesTabAsync()
    {
        await Page.Locator(".nav-link", new() { HasText = "Active Challenges" }).ClickAsync();
        // Wait for either "No active challenges" (empty) or "Incoming Challenges" heading
        try
        {
            await Task.WhenAny(
                Page.WaitForSelectorAsync("text=Incoming Challenges", new() { Timeout = 5000 }),
                Page.WaitForSelectorAsync("text=No active challenges", new() { Timeout = 5000 })
            );
        }
        catch { }
    }

    // ── New Challenge form ─────────────────────────────────────────────

    /// <summary>
    /// Select a player from the "Your Player" dropdown by zero-based index.
    /// Index 0 selects the first actual player (skipping the placeholder option).
    /// </summary>
    public async Task SelectPlayerForChallengeAsync(int index)
    {
        var select = Page.Locator(".card:has-text('Send a Challenge') select.form-select");
        // Option at index 0 is "-- Select your fighter --" placeholder; actual players start at index 1
        await select.SelectOptionAsync(new SelectOptionValue { Index = index + 1 });
    }

    /// <summary>Fill the opponent username input field.</summary>
    public async Task EnterOpponentUsernameAsync(string username)
    {
        await Page.GetByPlaceholder("Enter opponent's username").FillAsync(username);
    }

    /// <summary>Click the "Send Challenge" submit button.</summary>
    public async Task ClickSendChallengeAsync()
    {
        await Page.Locator(".card:has-text('Send a Challenge') button", new() { HasText = "Send Challenge" }).ClickAsync();
    }

    /// <summary>
    /// Get the rate limit text showing how many challenges have been sent this hour.
    /// Returns the "X of 10 challenges sent this hour" string.
    /// </summary>
    public async Task<string?> GetRateLimitTextAsync()
    {
        var el = await Page.QuerySelectorAsync(".card:has-text('Send a Challenge') small.text-muted");
        return el is not null ? await el.TextContentAsync() : null;
    }

    // ── Active Challenges ──────────────────────────────────────────────

    /// <summary>
    /// Count the number of incoming challenge cards visible on the page.
    /// Assumes the "Active Challenges" tab is selected.
    /// </summary>
    public async Task<int> GetIncomingChallengeCountAsync()
    {
        var cards = await Page.Locator(".card:has(button:has-text('Accept'))").AllAsync();
        return cards.Count;
    }

    /// <summary>
    /// Accept an incoming challenge by zero-based index. Clicks the Accept button
    /// on the challenge card, then selects the first available player in the
    /// "Pick Your Fighter" modal and confirms with "Fight!".
    /// Assumes the "Active Challenges" tab is selected.
    /// </summary>
    public async Task AcceptChallengeAsync(int challengeIndex)
    {
        var acceptButtons = Page.Locator(".card:has(button:has-text('Accept')) button", new() { HasText = "Accept" });
        var count = await acceptButtons.CountAsync();
        if (challengeIndex >= 0 && challengeIndex < count)
        {
            await acceptButtons.Nth(challengeIndex).ClickAsync();
            // Wait for the accept modal to appear
            await Page.WaitForSelectorAsync("text=Pick Your Fighter", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5000
            });
            // Select first player in the modal dropdown (skip placeholder at option index 0)
            var modalSelect = Page.Locator(".modal:has-text('Pick Your Fighter') select.form-select");
            await modalSelect.SelectOptionAsync(new SelectOptionValue { Index = 1 });
            // Confirm by clicking "Fight!" button
            await Page.GetByRole(AriaRole.Button, new() { Name = "Fight! ⚔️" }).ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }
    }

    /// <summary>
    /// Decline an incoming challenge by zero-based index. Clicks the Decline
    /// button on the challenge card. Assumes the "Active Challenges" tab is selected.
    /// </summary>
    public async Task DeclineChallengeAsync(int challengeIndex)
    {
        var declineButtons = Page.Locator(".card:has(button:has-text('Decline')) button", new() { HasText = "Decline" });
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
        await Page.WaitForSelectorAsync("text=Victory!, text=Defeat", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = timeoutMs
        });
    }

    /// <summary>Get the full text content from the battle result modal.</summary>
    public async Task<string?> GetBattleResultTextAsync()
    {
        var modal = await Page.QuerySelectorAsync(".modal:has-text('Victory!'), .modal:has-text('Defeat')");
        return modal is not null ? await modal.TextContentAsync() : null;
    }

    /// <summary>Close the battle result modal by clicking the Close button.</summary>
    public async Task CloseBattleResultModalAsync()
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
        await Page.WaitForTimeoutAsync(300);
    }

    // ── State checks ───────────────────────────────────────────────────

    /// <summary>Check whether the empty state ("No active challenges") is visible.</summary>
    public async Task<bool> IsEmptyStateVisibleAsync()
    {
        return await Page.GetByText("No active challenges").IsVisibleAsync();
    }

    /// <summary>Check whether the loading spinner is visible.</summary>
    public async Task<bool> IsLoadingVisibleAsync()
    {
        return await ExistsAsync(".spinner-border");
    }

    /// <summary>Check whether the error alert is visible.</summary>
    public async Task<bool> IsErrorVisibleAsync()
    {
        return await ExistsAsync(".alert-danger");
    }
}
