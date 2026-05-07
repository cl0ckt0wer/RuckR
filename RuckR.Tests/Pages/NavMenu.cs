using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

public class NavMenu : BasePage
{
    public NavMenu(IPage page, string baseUrl) : base(page, baseUrl) { }

    /// <summary>
    /// Ensure the Bootstrap navbar is expanded before clicking a nav link.
    /// On mobile/small viewports, the navbar is collapsed by default.
    /// </summary>
    private async Task EnsureNavExpandedAsync()
    {
        var toggler = await Page.QuerySelectorAsync(".navbar-toggler");
        if (toggler != null)
        {
            var navScrollable = await Page.QuerySelectorAsync(".nav-scrollable.collapse");
            if (navScrollable != null)
            {
                await toggler.ClickAsync();
                await Page.WaitForSelectorAsync(".nav-scrollable:not(.collapse)", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 3000
                });
            }
        }
    }

    public async Task NavigateToMapAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.ClickAsync("a[href='map']");
        await WaitForBlazorAsync();
    }

    public async Task NavigateToCatalogAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.ClickAsync("a[href='catalog']");
        await WaitForBlazorAsync();
    }

    public async Task NavigateToCollectionAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.ClickAsync("a[href='collection']");
        await WaitForBlazorAsync();
    }

    public async Task NavigateToNearbyPlayersAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.ClickAsync("a[href='players/nearby']");
        await WaitForBlazorAsync();
    }

    public async Task NavigateToBattlesAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.ClickAsync("a[href='battle']");
        await WaitForBlazorAsync();
    }

    public async Task NavigateToBattleHistoryAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.ClickAsync("a[href='battles/history']");
        await WaitForBlazorAsync();
    }

    public async Task NavigateToCreatePitchAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.ClickAsync("a[href='pitches/create']");
        await WaitForBlazorAsync();
    }

    /// <summary>
    /// Detect authentication state from the nav menu.
    /// Returns (isLoggedIn, username).
    /// </summary>
    public async Task<(bool isLoggedIn, string username)> GetAuthStateAsync()
    {
        var loginLink = await Page.QuerySelectorAsync("a[href*='Login']");
        if (loginLink != null)
        {
            return (false, "");
        }

        var usernameSpan = await Page.QuerySelectorAsync(".nav-link-text");
        if (usernameSpan != null)
        {
            var text = await usernameSpan.TextContentAsync();
            if (!string.IsNullOrWhiteSpace(text) && text.StartsWith("Hello,"))
            {
                var username = text.Replace("Hello,", "").Replace("!", "").Trim();
                return (true, username);
            }
        }

        var logoutLink = await Page.QuerySelectorAsync("a[href*='LogOut']");
        if (logoutLink != null)
        {
            return (true, "");
        }

        return (false, "");
    }

    public async Task ClickLoginAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.ClickAsync("a[href*='Login']");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task ClickLogoutAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.ClickAsync("a[href*='LogOut']");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> IsVisibleAsync()
    {
        return await ExistsAsync(".navbar");
    }

    public async Task<string[]> GetNavLinksAsync()
    {
        var links = await Page.QuerySelectorAllAsync(".nav-link");
        var texts = new List<string>();
        foreach (var link in links)
        {
            var text = await link.TextContentAsync();
            if (!string.IsNullOrWhiteSpace(text))
                texts.Add(text.Trim());
        }
        return texts.ToArray();
    }
}
