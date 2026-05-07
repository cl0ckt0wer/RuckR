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
        // Wait for either cards or empty state
        try
        {
            await Task.WhenAny(
                Page.WaitForSelectorAsync(".card", new() { Timeout = timeoutMs }),
                Page.WaitForSelectorAsync("text=No players", new() { Timeout = timeoutMs })
            );
        }
        catch { }
    }

    // Filters
    public async Task FilterByPositionAsync(string position)
    {
        // Find the position dropdown and select
        var select = await Page.QuerySelectorAsync("select[id*='position']");
        if (select != null)
            await select.SelectOptionAsync(position);
        await Page.WaitForTimeoutAsync(500);
    }

    public async Task FilterByRarityAsync(string rarity)
    {
        var select = await Page.QuerySelectorAsync("select[id*='rarity']");
        if (select != null)
            await select.SelectOptionAsync(rarity);
        await Page.WaitForTimeoutAsync(500);
    }

    public async Task SearchByNameAsync(string name)
    {
        var input = await Page.QuerySelectorAsync("input[type='search'], input[id*='name'], input[placeholder*='search' i], input[placeholder*='name' i]");
        if (input != null)
        {
            await input.FillAsync(name);
            await Page.WaitForTimeoutAsync(500);
        }
    }

    public async Task ClearFiltersAsync()
    {
        var clearBtn = await Page.QuerySelectorAsync("button:has-text('Clear')");
        if (clearBtn != null)
            await clearBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(300);
    }

    // Player cards
    public async Task<int> GetPlayerCardCountAsync()
    {
        var cards = await Page.QuerySelectorAllAsync(".card");
        return cards.Count;
    }

    public async Task<string?> GetPlayerCardNameAsync(int index)
    {
        var cards = await Page.QuerySelectorAllAsync(".card");
        if (index >= 0 && index < cards.Count)
        {
            return await cards[index].TextContentAsync();
        }
        return null;
    }

    public async Task<bool> IsCollectedBadgeVisibleAsync(int cardIndex)
    {
        var cards = await Page.QuerySelectorAllAsync(".card");
        if (cardIndex >= 0 && cardIndex < cards.Count)
        {
            var badge = await cards[cardIndex].QuerySelectorAsync("text=Collected");
            return badge != null;
        }
        return false;
    }

    // State checks
    public async Task<bool> IsEmptyStateVisibleAsync()
    {
        return await ExistsAsync("text=No players");
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
