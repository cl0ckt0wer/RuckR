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
        if (toggler != null && await toggler.IsVisibleAsync())
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
        await Page.GetByTestId("nav-map").ClickAsync();
        await WaitForBlazorAsync();
    }

    public async Task NavigateToCatalogAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.GetByTestId("nav-catalog").ClickAsync();
        await WaitForBlazorAsync();
    }

    public async Task NavigateToCollectionAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.GetByTestId("nav-collection").ClickAsync();
        await WaitForBlazorAsync();
    }

    public async Task NavigateToNearbyPlayersAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.GetByTestId("nav-nearby").ClickAsync();
        await WaitForBlazorAsync();
    }

    public async Task NavigateToBattlesAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.GetByTestId("nav-battles").ClickAsync();
        await WaitForBlazorAsync();
    }

    public async Task NavigateToBattleHistoryAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.GetByTestId("nav-history").ClickAsync();
        await WaitForBlazorAsync();
    }

    public async Task NavigateToCreatePitchAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.GetByTestId("nav-create-pitch").ClickAsync();
        await WaitForBlazorAsync();
    }

    /// <summary>
    /// Detect authentication state from the nav menu.
    /// Returns (isLoggedIn, username).
    /// </summary>
    public async Task<(bool isLoggedIn, string username)> GetAuthStateAsync()
    {
        var loginLink = await Page.QuerySelectorAsync("[data-testid='nav-login']");
        if (loginLink != null)
        {
            return (false, "");
        }

        var logoutLink = await Page.QuerySelectorAsync("[data-testid='nav-logout']");
        if (logoutLink != null)
        {
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
            return (true, "");
        }

        return (false, "");
    }

    public async Task ClickLoginAsync()
    {
        await DismissErrorUiAsync();
        await EnsureNavExpandedAsync();
        await Page.GetByTestId("nav-login").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task ClickLogoutAsync()
    {
        await DismissErrorUiAsync();
        await EnsureNavExpandedAsync();
        await Page.GetByTestId("nav-logout").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Perform a full logout: click the nav logout link, complete the
    /// Identity logout confirmation form, and wait for redirect back to
    /// the Blazor app.
    /// </summary>
    public async Task FullLogoutAsync()
    {
        await Page.Context.ClearCookiesAsync();
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForBlazorAsync();
        await DismissErrorUiAsync();
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
