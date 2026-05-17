using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

    /// <summary>
    /// Provides access to :.
    /// </summary>
public class CollectionPage : BasePage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="""CollectionPage"""/> class.
    /// </summary>
    /// <param name="page">The page to use.</param>
    /// <param name="baseUrl">The baseUrl to use.</param>
    public CollectionPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    /// <summary>
    /// Verifies go To Async.
    /// </summary>
    public async Task GoToAsync() => await NavigateToAsync("/collection");

    // ── Page loading ───────────────────────────────────────────────────

    /// <summary>
    /// Wait for the collection page to finish loading — either player cards
    /// or the empty state message appears.
    /// </summary>
    public async Task WaitForCollectionLoadedAsync(int timeoutMs = 15000)
    {
        await WaitForBlazorAsync(timeoutMs);
        try
        {
            await Task.WhenAny(
                Page.WaitForSelectorAsync("[data-testid='collection-card']", new() { Timeout = timeoutMs }),
                Page.WaitForSelectorAsync("[data-testid='empty-state']", new() { Timeout = timeoutMs })
            );
        }
        catch { }
    }

    // ── Player cards ───────────────────────────────────────────────────

    /// <summary>
    /// Return the number of collected player cards currently visible.
    /// </summary>
    public async Task<int> GetCollectedPlayerCountAsync()
    {
        var cards = await Page.QuerySelectorAllAsync("[data-testid='collection-card']");
        return cards.Count;
    }

    /// <summary>
    /// Get the player name from the card at the given zero-based index.
    /// Returns null if the index is out of range.
    /// </summary>
    public async Task<string?> GetPlayerNameAsync(int index)
    {
        var cards = await Page.QuerySelectorAllAsync("[data-testid='collection-card']");
        if (index >= 0 && index < cards.Count)
        {
            var title = await cards[index].QuerySelectorAsync("[data-testid='collection-player-name']");
            if (title is not null)
                return await title.TextContentAsync();
        }
        return null;
    }

    // ── Favorite toggle ────────────────────────────────────────────────

    /// <summary>
    /// Click the star button on the card at the given index to toggle
    /// favorite status.
    /// </summary>
    public async Task ToggleFavoriteAsync(int index)
    {
        var cards = await Page.QuerySelectorAllAsync("[data-testid='collection-card']");
        if (index >= 0 && index < cards.Count)
        {
            var starBtn = await cards[index].QuerySelectorAsync("[data-testid='favorite-btn']");
            if (starBtn is not null)
                await starBtn.ClickAsync();
        }
    }

    /// <summary>
    /// Check whether the card at the given index is currently favorited
    /// (filled star "⭐" vs empty star "☆").
    /// </summary>
    public async Task<bool> IsFavoriteAsync(int index)
    {
        var cards = await Page.QuerySelectorAllAsync("[data-testid='collection-card']");
        if (index >= 0 && index < cards.Count)
        {
            var starBtn = await cards[index].QuerySelectorAsync("[data-testid='favorite-btn']");
            if (starBtn is not null)
            {
                var text = await starBtn.TextContentAsync();
                return text?.Contains('⭐') == true;
            }
        }
        return false;
    }

    // ── Expanded detail ────────────────────────────────────────────────

    /// <summary>
    /// Click the card body at the given index to expand the player detail
    /// section.
    /// </summary>
    public async Task ExpandPlayerDetailAsync(int index)
    {
        var cards = await Page.QuerySelectorAllAsync("[data-testid='collection-card']");
        if (index >= 0 && index < cards.Count)
        {
            var cardBody = await cards[index].QuerySelectorAsync(".card-body");
            if (cardBody is not null)
            {
                await cardBody.ClickAsync();
                await Page.WaitForTimeoutAsync(300);
            }
        }
    }

    /// <summary>
    /// Check whether any player detail panel (card footer with stats) is
    /// currently expanded and visible.
    /// </summary>
    public async Task<bool> IsPlayerDetailExpandedAsync()
    {
        return await ExistsAsync("[data-testid='collection-detail']");
    }

    // ── State checks ───────────────────────────────────────────────────

    /// <summary>
    /// Verifies is Empty State Visible Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<bool> IsEmptyStateVisibleAsync()
    {
        return await ExistsAsync("[data-testid='empty-state']");
    }

    /// <summary>
    /// Verifies is Loading Visible Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<bool> IsLoadingVisibleAsync()
    {
        return await ExistsAsync("[data-testid='collection-loading']");
    }

    /// <summary>
    /// Verifies is Error Visible Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<bool> IsErrorVisibleAsync()
    {
        return await ExistsAsync("[data-testid='collection-error']");
    }
}


