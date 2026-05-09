using Microsoft.Playwright;
using RuckR.Tests.Fixtures;
using RuckR.Tests.Pages;

namespace RuckR.Tests.E2E;

[Collection(nameof(TestCollection))]
public class NavMenuTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _baseUrl = null!;

    public NavMenuTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
    }

    public async Task InitializeAsync()
    {
        _context = await _playwright.NewContextAsync();
        _page = await _context.NewPageAsync();
        _baseUrl = _factory.ServerBaseUrl;

        // Register a test user so auth-required pages (Collection, Battles, History, Create Pitch)
        // load their content rather than redirecting to login.
        var username = $"navtest_{Guid.NewGuid():N}@test.com";
        var password = "TestPass123!";

        var registerPage = new RegisterPage(_page, _baseUrl);
        await registerPage.GoToAsync();
        await registerPage.RegisterAsync(username, password);

        // After registration, should be redirected to the app
        var nav = new NavMenu(_page, _baseUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await nav.WaitForBlazorReadyAsync();

        var (isLoggedIn, _) = await nav.GetAuthStateAsync();
        if (!isLoggedIn)
            throw new InvalidOperationException("Failed to authenticate test user for NavMenu tests");
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();
    }

    [Fact]
    public async Task NavMenu_AllLinks_ShouldNavigateCorrectly()
    {
        var nav = new NavMenu(_page, _baseUrl);
        await _page.GotoAsync(_baseUrl);
        await nav.WaitForBlazorReadyAsync();

        // 1. Map
        await nav.NavigateToMapAsync();
        Assert.True(await _page.QuerySelectorAsync(".navbar-brand") != null, "Navbar brand should be visible on Map");

        // 2. Catalog
        await nav.NavigateToCatalogAsync();
        Assert.True(await _page.QuerySelectorAsync(".navbar-brand") != null, "Navbar brand should be visible on Catalog");

        // 3. Collection
        await nav.NavigateToCollectionAsync();
        Assert.True(await _page.QuerySelectorAsync(".navbar-brand") != null, "Navbar brand should be visible on Collection");

        // 4. Nearby Players
        await nav.NavigateToNearbyPlayersAsync();
        Assert.True(await _page.QuerySelectorAsync(".navbar-brand") != null, "Navbar brand should be visible on Nearby Players");

        // 5. Battles
        await nav.NavigateToBattlesAsync();
        Assert.True(await _page.QuerySelectorAsync(".navbar-brand") != null, "Navbar brand should be visible on Battles");

        // 6. Battle History
        await nav.NavigateToBattleHistoryAsync();
        Assert.True(await _page.QuerySelectorAsync(".navbar-brand") != null, "Navbar brand should be visible on Battle History");

        // 7. Create Pitch
        await nav.NavigateToCreatePitchAsync();
        Assert.True(await _page.QuerySelectorAsync(".navbar-brand") != null, "Navbar brand should be visible on Create Pitch");
    }
}
