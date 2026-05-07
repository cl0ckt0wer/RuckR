using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

public class BattleHistoryPage : BasePage
{
    private const string BattleEntry = ".battle-entry";
    private const string ResultBadge = ".result-badge";
    private const string OpponentName = ".opponent-name";
    private const string MethodText = ".method-text";
    private const string ExpandToggle = ".expand-toggle";

    public BattleHistoryPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    public async Task GoToAsync() => await NavigateToAsync("/battles/history");

    /// <summary>
    /// Wait for the history page to finish loading (entries visible or empty state).
    /// </summary>
    public async Task WaitForHistoryLoadedAsync(int timeoutMs = 15000)
    {
        try
        {
            await Task.WhenAny(
                Page.WaitForSelectorAsync(BattleEntry, new() { Timeout = timeoutMs }),
                Page.WaitForSelectorAsync("text=No battles", new() { Timeout = timeoutMs })
            );
        }
        catch { }
    }

    /// <summary>Return the number of battle entry elements on the page.</summary>
    public async Task<int> GetBattleEntryCountAsync()
    {
        var entries = await Page.QuerySelectorAllAsync(BattleEntry);
        return entries.Count;
    }

    /// <summary>
    /// Get the result badge text for a battle entry at the given zero-based index.
    /// Returns one of: "Victory", "Defeat", "Expired", "Declined", or null if out of range.
    /// </summary>
    public async Task<string?> GetResultBadgeAsync(int index)
    {
        var entries = await Page.QuerySelectorAllAsync(BattleEntry);
        if (index < 0 || index >= entries.Count)
            return null;

        var badge = await entries[index].QuerySelectorAsync(ResultBadge);
        if (badge is null) return null;

        return (await badge.TextContentAsync())?.Trim();
    }

    /// <summary>
    /// Get the opponent name text (e.g. "vs JohnDoe") for a battle entry at the given index.
    /// </summary>
    public async Task<string?> GetOpponentNameAsync(int index)
    {
        var entries = await Page.QuerySelectorAllAsync(BattleEntry);
        if (index < 0 || index >= entries.Count)
            return null;

        var opponent = await entries[index].QuerySelectorAsync(OpponentName);
        if (opponent is null) return null;

        return (await opponent.TextContentAsync())?.Trim();
    }

    /// <summary>
    /// Get the method/resolution text (e.g. "Won by TKO", "Lost by decision") for a battle entry.
    /// </summary>
    public async Task<string?> GetMethodTextAsync(int index)
    {
        var entries = await Page.QuerySelectorAllAsync(BattleEntry);
        if (index < 0 || index >= entries.Count)
            return null;

        var method = await entries[index].QuerySelectorAsync(MethodText);
        if (method is null) return null;

        return (await method.TextContentAsync())?.Trim();
    }

    /// <summary>Check whether a battle entry at the given index has an expand/collapse toggle.</summary>
    public async Task<bool> IsEntryExpandableAsync(int index)
    {
        var entries = await Page.QuerySelectorAllAsync(BattleEntry);
        if (index < 0 || index >= entries.Count)
            return false;

        var toggle = await entries[index].QuerySelectorAsync(ExpandToggle);
        return toggle is not null;
    }

    /// <summary>
    /// Click the expand toggle on a battle entry to reveal detailed information.
    /// </summary>
    public async Task ExpandEntryAsync(int index)
    {
        var entries = await Page.QuerySelectorAllAsync(BattleEntry);
        if (index >= 0 && index < entries.Count)
        {
            var toggle = await entries[index].QuerySelectorAsync(ExpandToggle);
            if (toggle is not null)
                await toggle.ClickAsync();
        }
    }

    // ── State checks ────────────────────────────────────────────────

    public async Task<bool> IsEmptyStateVisibleAsync()
    {
        return await ExistsAsync("text=No battles");
    }

    public async Task<bool> IsLoadingVisibleAsync()
    {
        return await ExistsAsync(".spinner-border");
    }

    public async Task<bool> IsErrorVisibleAsync()
    {
        return await ExistsAsync(".alert-danger");
    }
}
