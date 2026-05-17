using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

    /// <summary>
    /// Provides access to :.
    /// </summary>
public class CatalogPage : BasePage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="""CatalogPage"""/> class.
    /// </summary>
    /// <param name="page">The page to use.</param>
    /// <param name="baseUrl">The baseUrl to use.</param>
    public CatalogPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    /// <summary>
    /// Verifies go To Async.
    /// </summary>
    public async Task GoToAsync() => await NavigateToAsync("/catalog");

    // Loading state
    /// <summary>
    /// Verifies wait For Catalog Loaded Async.
    /// </summary>
    /// <param name="timeoutMs">The timeoutMs to use.</param>
    public async Task WaitForCatalogLoadedAsync(int timeoutMs = 15000)
    {
        await WaitForBlazorAsync(timeoutMs);
        try
        {
            await Task.WhenAny(
                Page.WaitForSelectorAsync("[data-testid='catalog-card']", new() { Timeout = timeoutMs }),
                Page.WaitForSelectorAsync("[data-testid='catalog-empty']", new() { Timeout = timeoutMs })
            );
        }
        catch { }
    }

    // Filters
    /// <summary>
    /// Verifies filter By Position Async.
    /// </summary>
    /// <param name="position">The position to use.</param>
    public async Task FilterByPositionAsync(string position)
    {
        await Page.GetByTestId("position-filter").SelectOptionAsync(position);
        await Page.WaitForTimeoutAsync(500);
    }

    /// <summary>
    /// Verifies filter By Rarity Async.
    /// </summary>
    /// <param name="rarity">The rarity to use.</param>
    public async Task FilterByRarityAsync(string rarity)
    {
        await Page.GetByTestId("rarity-filter").SelectOptionAsync(rarity);
        await Page.WaitForTimeoutAsync(500);
    }

    /// <summary>
    /// Verifies search By Name Async.
    /// </summary>
    /// <param name="name">The name to use.</param>
    public async Task SearchByNameAsync(string name)
    {
        await Page.GetByTestId("name-search").FillAsync(name);
        await Page.WaitForTimeoutAsync(500);
    }

    /// <summary>
    /// Verifies clear Filters Async.
    /// </summary>
    public async Task ClearFiltersAsync()
    {
        await Page.GetByTestId("catalog-clear-btn").ClickAsync();
        await Page.WaitForTimeoutAsync(300);
    }

    // Player cards
    /// <summary>
    /// Verifies get Player Card Count Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<int> GetPlayerCardCountAsync()
    {
        var cards = await Page.QuerySelectorAllAsync("[data-testid='catalog-card']");
        return cards.Count;
    }

    /// <summary>
    /// Verifies get Player Card Name Async.
    /// </summary>
    /// <param name="index">The index to use.</param>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<string?> GetPlayerCardNameAsync(int index)
    {
        var cards = await Page.QuerySelectorAllAsync("[data-testid='catalog-card']");
        if (index >= 0 && index < cards.Count)
        {
            return await cards[index].TextContentAsync();
        }
        return null;
    }

    /// <summary>
    /// Verifies is Collected Badge Visible Async.
    /// </summary>
    /// <param name="cardIndex">The cardIndex to use.</param>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<bool> IsCollectedBadgeVisibleAsync(int cardIndex)
    {
        var cards = await Page.QuerySelectorAllAsync("[data-testid='catalog-card']");
        if (cardIndex >= 0 && cardIndex < cards.Count)
        {
            var badge = await cards[cardIndex].QuerySelectorAsync("[data-testid='collected-badge']");
            return badge != null;
        }
        return false;
    }

    // State checks
    /// <summary>
    /// Verifies is Empty State Visible Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<bool> IsEmptyStateVisibleAsync()
    {
        return await ExistsAsync("[data-testid='catalog-empty']");
    }

    /// <summary>
    /// Verifies is Loading Visible Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<bool> IsLoadingVisibleAsync()
    {
        return await ExistsAsync("[data-testid='catalog-loading']");
    }

    /// <summary>
    /// Verifies is Error Visible Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<bool> IsErrorVisibleAsync()
    {
        return await ExistsAsync("[data-testid='catalog-error']");
    }
}


