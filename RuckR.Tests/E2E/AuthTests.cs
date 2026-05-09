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

        await nav.ClickLoginAsync();
        Assert.Contains("/Identity/Account/Login", _page.Url, StringComparison.OrdinalIgnoreCase);

        var loginPage = new LoginPage(_page, _baseUrl);
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
}
