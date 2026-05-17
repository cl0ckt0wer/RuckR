using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

    /// <summary>
    /// Provides access to :.
    /// </summary>
public class NavMenu : BasePage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="""NavMenu"""/> class.
    /// </summary>
    /// <param name="page">The page to use.</param>
    /// <param name="baseUrl">The baseUrl to use.</param>
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

    /// <summary>
    /// Verifies navigate To Map Async.
    /// </summary>
    public async Task NavigateToMapAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.GetByTestId("nav-map").ClickAsync();
        await WaitForBlazorAsync();
    }

    /// <summary>
    /// Verifies navigate To Catalog Async.
    /// </summary>
    public async Task NavigateToCatalogAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.GetByTestId("nav-catalog").ClickAsync();
        await WaitForBlazorAsync();
    }

    /// <summary>
    /// Verifies navigate To Collection Async.
    /// </summary>
    public async Task NavigateToCollectionAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.GetByTestId("nav-collection").ClickAsync();
        await WaitForBlazorAsync();
    }

    /// <summary>
    /// Verifies navigate To Nearby Players Async.
    /// </summary>
    public async Task NavigateToNearbyPlayersAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.GetByTestId("nav-nearby").ClickAsync();
        await WaitForBlazorAsync();
    }

    /// <summary>
    /// Verifies navigate To Battles Async.
    /// </summary>
    public async Task NavigateToBattlesAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.GetByTestId("nav-battles").ClickAsync();
        await WaitForBlazorAsync();
    }

    /// <summary>
    /// Verifies navigate To Battle History Async.
    /// </summary>
    public async Task NavigateToBattleHistoryAsync()
    {
        await EnsureNavExpandedAsync();
        await Page.GetByTestId("nav-history").ClickAsync();
        await WaitForBlazorAsync();
    }

    /// <summary>
    /// Verifies navigate To Create Pitch Async.
    /// </summary>
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

    /// <summary>
    /// Verifies click Login Async.
    /// </summary>
    public async Task ClickLoginAsync()
    {
        await DismissErrorUiAsync();
        await EnsureNavExpandedAsync();
        await Page.GetByTestId("nav-login").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Verifies click Logout Async.
    /// </summary>
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
        await ClickLogoutAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Click here to Logout" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForBlazorAsync();
        await DismissErrorUiAsync();
    }

    /// <summary>
    /// Verifies is Visible Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<bool> IsVisibleAsync()
    {
        return await ExistsAsync(".navbar");
    }

    /// <summary>
    /// Verifies get Nav Links Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
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


