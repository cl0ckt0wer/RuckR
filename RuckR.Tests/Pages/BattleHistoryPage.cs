using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

    /// <summary>
    /// Provides access to :.
    /// </summary>
public class BattleHistoryPage : BasePage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="""BattleHistoryPage"""/> class.
    /// </summary>
    /// <param name="page">The page to use.</param>
    /// <param name="baseUrl">The baseUrl to use.</param>
    public BattleHistoryPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    /// <summary>
    /// Verifies go To Async.
    /// </summary>
    public async Task GoToAsync() => await NavigateToAsync("/battles/history");

    /// <summary>
    /// Wait for the history page to finish loading (entries visible or empty state).
    /// </summary>
    public async Task WaitForHistoryLoadedAsync(int timeoutMs = 15000)
    {
        try
        {
            await Task.WhenAny(
                Page.WaitForSelectorAsync("[data-testid='history-item']", new() { Timeout = timeoutMs }),
                Page.WaitForSelectorAsync("[data-testid='history-empty']", new() { Timeout = timeoutMs })
            );
        }
        catch { }
    }

    /// <summary>Return the number of battle entry elements on the page.</summary>
    public async Task<int> GetBattleEntryCountAsync()
    {
        var entries = await Page.QuerySelectorAllAsync("[data-testid='history-item']");
        return entries.Count;
    }

    /// <summary>
    /// Get the result badge text for a battle entry at the given zero-based index.
    /// Returns one of the result labels or null if out of range.
    /// </summary>
    public async Task<string?> GetResultBadgeAsync(int index)
    {
        var entries = await Page.QuerySelectorAllAsync("[data-testid='history-item']");
        if (index < 0 || index >= entries.Count)
            return null;

        var badge = await entries[index].QuerySelectorAsync("[data-testid='result-badge']");
        if (badge is null) return null;

        return (await badge.TextContentAsync())?.Trim();
    }

    /// <summary>Check whether a battle entry at the given index has an expand/collapse toggle.</summary>
    public async Task<bool> IsEntryExpandableAsync(int index)
    {
        var entries = await Page.QuerySelectorAllAsync("[data-testid='history-item']");
        if (index < 0 || index >= entries.Count)
            return false;

        var toggle = await entries[index].QuerySelectorAsync("[data-testid='expand-indicator']");
        return toggle is not null;
    }

    /// <summary>
    /// Click the expand toggle on a battle entry to reveal detailed information.
    /// </summary>
    public async Task ExpandEntryAsync(int index)
    {
        var entries = await Page.QuerySelectorAllAsync("[data-testid='history-item']");
        if (index >= 0 && index < entries.Count)
        {
            var toggle = await entries[index].QuerySelectorAsync("[data-testid='expand-indicator']");
            if (toggle is not null)
                await toggle.ClickAsync();
        }
    }

    // ── State checks ────────────────────────────────────────────────

    /// <summary>
    /// Verifies is Empty State Visible Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<bool> IsEmptyStateVisibleAsync()
    {
        return await ExistsAsync("[data-testid='history-empty']");
    }

    /// <summary>
    /// Verifies is Loading Visible Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<bool> IsLoadingVisibleAsync()
    {
        return await ExistsAsync("[data-testid='history-loading']");
    }

    /// <summary>
    /// Verifies is Error Visible Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<bool> IsErrorVisibleAsync()
    {
        return await ExistsAsync("[data-testid='history-error']");
    }
}


