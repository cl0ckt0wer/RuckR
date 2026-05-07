using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

/// <summary>
/// Page Object for the /players/nearby page — a grid of nearby player cards
/// with fuzzy distance display, Challenge/Scout actions, radius filter, and
/// GPS-disabled / empty / loading / error states.
/// </summary>
public class PlayerGridPage : BasePage
{
    public PlayerGridPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    /// <summary>Navigate to the nearby players grid and wait for Blazor to render.</summary>
    public async Task GoToAsync() => await NavigateToAsync("/players/nearby");

    // ── Grid loading ────────────────────────────────────────────────────

    /// <summary>
    /// Wait for the player grid to finish loading — either player cards appear
    /// or the empty-state message is shown.
    /// </summary>
    public async Task WaitForPlayerGridLoadedAsync(int timeoutMs = 15000)
    {
        await WaitForBlazorAsync(timeoutMs);
        try
        {
            await Task.WhenAny(
                Page.WaitForSelectorAsync(".card", new() { Timeout = timeoutMs }),
                Page.WaitForSelectorAsync("text=No nearby players", new() { Timeout = timeoutMs })
            );
        }
        catch { }
    }

    // ── Player card count ───────────────────────────────────────────────

    /// <summary>Return the total number of player cards currently visible.</summary>
    public async Task<int> GetNearbyPlayerCountAsync()
    {
        var cards = await Page.QuerySelectorAllAsync(".card");
        return cards.Count;
    }

    // ── Card content accessors ──────────────────────────────────────────

    /// <summary>Get the player name from a card at the given zero-based index.</summary>
    public async Task<string?> GetPlayerNameAsync(int index)
    {
        var cards = await Page.QuerySelectorAllAsync(".card");
        if (index < 0 || index >= cards.Count)
            return null;
        var heading = await cards[index].QuerySelectorAsync("h5, .card-title");
        return heading is not null ? await heading.TextContentAsync() : null;
    }

    /// <summary>Get the owner username from a card at the given zero-based index.</summary>
    public async Task<string?> GetOwnerUsernameAsync(int index)
    {
        var cards = await Page.QuerySelectorAllAsync(".card");
        if (index < 0 || index >= cards.Count)
            return null;
        var subtitle = await cards[index].QuerySelectorAsync(".card-subtitle, .text-muted");
        return subtitle is not null ? await subtitle.TextContentAsync() : null;
    }

    /// <summary>
    /// Get the fuzzy distance text from a card (e.g. "~1.2km away").
    /// Returns null if the index is out of range or no distance text is found.
    /// </summary>
    public async Task<string?> GetFuzzyDistanceTextAsync(int index)
    {
        var cards = await Page.QuerySelectorAllAsync(".card");
        if (index < 0 || index >= cards.Count)
            return null;
        var distance = await cards[index].QuerySelectorAsync(".distance, .fuzzy-distance");
        if (distance is not null)
            return await distance.TextContentAsync();
        // Fallback: search for any element containing "km away"
        var text = await cards[index].TextContentAsync();
        var match = System.Text.RegularExpressions.Regex.Match(text ?? "", @"~\d+\.?\d*km away");
        return match.Success ? match.Value : null;
    }

    // ── Card actions ────────────────────────────────────────────────────

    /// <summary>
    /// Click the "Challenge" button on the player card at the given index.
    /// </summary>
    public async Task ClickChallengeButtonAsync(int index)
    {
        var cards = await Page.QuerySelectorAllAsync(".card");
        if (index < 0 || index >= cards.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var challengeBtn = await cards[index].QuerySelectorAsync("button:has-text('Challenge')");
        if (challengeBtn is not null)
            await challengeBtn.ClickAsync();
    }

    /// <summary>
    /// Click the "Scout" button on the player card at the given index
    /// (shown for wild/unowned players).
    /// </summary>
    public async Task ClickScoutButtonAsync(int index)
    {
        var cards = await Page.QuerySelectorAllAsync(".card");
        if (index < 0 || index >= cards.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var scoutBtn = await cards[index].QuerySelectorAsync("button:has-text('Scout')");
        if (scoutBtn is not null)
            await scoutBtn.ClickAsync();
    }

    // ── Radius filter ───────────────────────────────────────────────────

    /// <summary>
    /// Select a radius value from the radius dropdown, if it exists.
    /// </summary>
    public async Task ChangeRadiusAsync(string radius)
    {
        var select = await Page.QuerySelectorAsync("select[id*='radius']");
        if (select is not null)
            await select.SelectOptionAsync(radius);
        await Page.WaitForTimeoutAsync(500);
    }

    // ── GPS disabled banner ─────────────────────────────────────────────

    /// <summary>
    /// Check whether the "Enable GPS" banner is visible (shown when the
    /// browser denies or blocks geolocation permission).
    /// </summary>
    public async Task<bool> HasGPSDisabledMessageAsync()
    {
        return await Page.GetByText("Enable GPS").IsVisibleAsync();
    }

    // ── State checks ────────────────────────────────────────────────────

    /// <summary>Check whether the empty-state message is visible.</summary>
    public async Task<bool> IsEmptyStateVisibleAsync()
    {
        return await ExistsAsync("text=No nearby players");
    }

    /// <summary>Check whether the loading spinner is visible.</summary>
    public async Task<bool> IsLoadingVisibleAsync()
    {
        return await ExistsAsync(".spinner-border");
    }

    /// <summary>Check whether an error alert is visible.</summary>
    public async Task<bool> IsErrorVisibleAsync()
    {
        return await ExistsAsync(".alert-danger");
    }
}
