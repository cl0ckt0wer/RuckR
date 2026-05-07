using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

public class CatalogPage : BasePage
{
    public CatalogPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    public async Task GoToAsync() => await NavigateToAsync("/catalog");

    // Loading state
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
    public async Task FilterByPositionAsync(string position)
    {
        await Page.GetByTestId("position-filter").SelectOptionAsync(position);
        await Page.WaitForTimeoutAsync(500);
    }

    public async Task FilterByRarityAsync(string rarity)
    {
        await Page.GetByTestId("rarity-filter").SelectOptionAsync(rarity);
        await Page.WaitForTimeoutAsync(500);
    }

    public async Task SearchByNameAsync(string name)
    {
        await Page.GetByTestId("name-search").FillAsync(name);
        await Page.WaitForTimeoutAsync(500);
    }

    public async Task ClearFiltersAsync()
    {
        await Page.GetByTestId("catalog-clear-btn").ClickAsync();
        await Page.WaitForTimeoutAsync(300);
    }

    // Player cards
    public async Task<int> GetPlayerCardCountAsync()
    {
        var cards = await Page.QuerySelectorAllAsync("[data-testid='catalog-card']");
        return cards.Count;
    }

    public async Task<string?> GetPlayerCardNameAsync(int index)
    {
        var cards = await Page.QuerySelectorAllAsync("[data-testid='catalog-card']");
        if (index >= 0 && index < cards.Count)
        {
            return await cards[index].TextContentAsync();
        }
        return null;
    }

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
    public async Task<bool> IsEmptyStateVisibleAsync()
    {
        return await ExistsAsync("[data-testid='catalog-empty']");
    }

    public async Task<bool> IsLoadingVisibleAsync()
    {
        return await ExistsAsync("[data-testid='catalog-loading']");
    }

    public async Task<bool> IsErrorVisibleAsync()
    {
        return await ExistsAsync("[data-testid='catalog-error']");
    }
}
