using Microsoft.Playwright;
using RuckR.Tests.Fixtures;
using RuckR.Tests.Pages;

namespace RuckR.Tests.E2E;

[Collection(nameof(TestCollection))]
public class AuthTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _baseUrl = null!;

    public AuthTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
    }

    public async Task InitializeAsync()
    {
        _context = await _playwright.NewContextAsync();
        _page = await _context.NewPageAsync();
        _baseUrl = _factory.ServerBaseUrl;
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();
    }

    [Fact]
    public async Task Register_NewUser_CanLoginAndSeeNavbar()
    {
        var username = $"testuser_{Guid.NewGuid():N}@test.com";
        var password = "TestPass123!";

        // Register
        var registerPage = new RegisterPage(_page, _baseUrl);
        await registerPage.GoToAsync();
        await registerPage.RegisterAsync(username, password);

        // After registration, should be redirected to the app
        var nav = new NavMenu(_page, _baseUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await nav.WaitForBlazorReadyAsync();

        // Verify logged in state
        var (isLoggedIn, _) = await nav.GetAuthStateAsync();
        Assert.True(isLoggedIn, "User should be logged in after registration");

        // Logout — full flow: click nav link, confirm on Identity page, redirect back
        await nav.FullLogoutAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await nav.WaitForBlazorReadyAsync();

        // Verify logged out
        var (stillLoggedIn, _) = await nav.GetAuthStateAsync();
        Assert.False(stillLoggedIn, "User should be logged out");

        // Login again
        var loginPage = new LoginPage(_page, _baseUrl);
        await loginPage.GoToAsync();
        await loginPage.LoginAsync(username, password);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await nav.WaitForBlazorReadyAsync();

        // Verify logged in again
        var (loggedInAgain, _) = await nav.GetAuthStateAsync();
        Assert.True(loggedInAgain, "User should be logged in after login");
    }

    [Fact]
    public async Task Root_Login_Root_Logout_CompletesSuccessfully()
    {
        var username = $"loginflow_{Guid.NewGuid():N}@test.com";
        var password = "TestPass123!";
        await _factory.CreateTestUserAsync(username, password);

        var nav = new NavMenu(_page, _baseUrl);

        await _page.GotoAsync(_baseUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await nav.WaitForBlazorReadyAsync();
        Assert.Contains(_baseUrl, _page.Url, StringComparison.OrdinalIgnoreCase);

        var loginPage = new LoginPage(_page, _baseUrl);
        await loginPage.GoToAsync();
        Assert.Contains("/Identity/Account/Login", _page.Url, StringComparison.OrdinalIgnoreCase);

        await loginPage.LoginAsync(username, password);

        await _page.GotoAsync(_baseUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await nav.WaitForBlazorReadyAsync();

        var (isLoggedIn, loggedInUsername) = await nav.GetAuthStateAsync();
        Assert.True(isLoggedIn, "User should be logged in after returning to root");
        Assert.Equal(username, loggedInUsername);

        await nav.FullLogoutAsync();

        var (isStillLoggedIn, _) = await nav.GetAuthStateAsync();
        Assert.False(isStillLoggedIn, "User should be logged out after logout");
    }

    [Fact]
    public async Task Logout_ThenLogin_UsesNormalNavigationLinks()
    {
        var username = $"normalnav_{Guid.NewGuid():N}@test.com";
        var password = "TestPass123!";

        var registerPage = new RegisterPage(_page, _baseUrl);
        await registerPage.GoToAsync();
        await registerPage.RegisterAsync(username, password);

        var nav = new NavMenu(_page, _baseUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await nav.WaitForBlazorReadyAsync();

        await nav.ClickLogoutAsync();

        Assert.Contains("/Identity/Account/Logout", _page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("RuckR", (await _page.Locator(".navbar-brand").TextContentAsync())?.Trim());

        await _page.GetByRole(AriaRole.Button, new() { Name = "Click here to Logout" }).ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await nav.WaitForBlazorReadyAsync();

        var loginLink = await _page.QuerySelectorAsync("a[href*='Login']");
        Assert.NotNull(loginLink);
        Assert.Contains("/Identity/Account/Login", await loginLink.GetAttributeAsync("href"));

        await _page.GotoAsync($"{_baseUrl.TrimEnd('/')}/Identity/Account/Login");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.Equal("RuckR", (await _page.Locator(".navbar-brand").TextContentAsync())?.Trim());
        Assert.Equal("Register", (await _page.GetByRole(AriaRole.Link, new() { Name = "Register", Exact = true }).TextContentAsync())?.Trim());

        var loginPage = new LoginPage(_page, _baseUrl);
        await loginPage.LoginAsync(username, password);

        var (loggedInAgain, loggedInUsername) = await nav.GetAuthStateAsync();
        Assert.True(loggedInAgain, "User should be logged in again after normal login navigation");
        Assert.Equal(username, loggedInUsername);
    }

    [Fact]
    public async Task Logout_RemovesAccessToAuthenticatedPages()
    {
        var username = $"logout_{Guid.NewGuid():N}@test.com";
        var password = "TestPass123!";

        var registerPage = new RegisterPage(_page, _baseUrl);
        await registerPage.GoToAsync();
        await registerPage.RegisterAsync(username, password);

        var nav = new NavMenu(_page, _baseUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await nav.WaitForBlazorReadyAsync();

        var (isLoggedIn, _) = await nav.GetAuthStateAsync();
        Assert.True(isLoggedIn, "User should be logged in before logout");

        await nav.FullLogoutAsync();

        var (isLoggedOut, _) = await nav.GetAuthStateAsync();
        Assert.False(isLoggedOut, "User should be logged out after logout");

        await _page.GotoAsync($"{_baseUrl.TrimEnd('/')}/collection");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForURLAsync(
            url => url.Contains("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });

        Assert.Contains("/Identity/Account/Login", _page.Url, StringComparison.OrdinalIgnoreCase);
    }
}
