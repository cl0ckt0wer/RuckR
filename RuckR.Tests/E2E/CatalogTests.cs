using Microsoft.Playwright;
using RuckR.Tests.Fixtures;
using RuckR.Tests.Pages;

namespace RuckR.Tests.E2E;

public class CatalogTests : IClassFixture<CustomWebApplicationFactory>, IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _baseUrl = null!;

    public CatalogTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
    }

    public async Task InitializeAsync()
    {
        _context = await _playwright.NewContextAsync();
        _page = await _context.NewPageAsync();
        _baseUrl = _factory.ServerBaseUrl;

        // Register a test user so API calls (PlayersController requires [Authorize]) succeed
        var username = $"catalogtest_{Guid.NewGuid():N}@test.com";
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
            throw new InvalidOperationException("Failed to authenticate test user for Catalog tests");
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();
    }

    [Fact]
    public async Task CatalogPage_Loads_ShowsPlayerCards()
    {
        var catalogPage = new CatalogPage(_page, _baseUrl);
        await catalogPage.GoToAsync();
        await catalogPage.WaitForCatalogLoadedAsync();

        // Should have player cards (from seed data: 500 players)
        var cardCount = await catalogPage.GetPlayerCardCountAsync();
        Assert.True(cardCount > 0, "Catalog should show player cards");
    }

    [Fact]
    public async Task CatalogPage_Filter_ByPosition()
    {
        var catalogPage = new CatalogPage(_page, _baseUrl);
        await catalogPage.GoToAsync();
        await catalogPage.WaitForCatalogLoadedAsync();

        var initialCount = await catalogPage.GetPlayerCardCountAsync();

        // Filter by position
        await catalogPage.FilterByPositionAsync("Prop");
        await _page.WaitForTimeoutAsync(500);

        var filteredCount = await catalogPage.GetPlayerCardCountAsync();
        Assert.True(filteredCount <= initialCount, "Filter should reduce results");
        Assert.True(filteredCount > 0, "Should have Props in seed data");
    }
}
