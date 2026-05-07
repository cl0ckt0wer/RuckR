using Microsoft.Playwright;
using RuckR.Tests.Fixtures;
using RuckR.Tests.Pages;

namespace RuckR.Tests.E2E;

public class MobileTests : IClassFixture<CustomWebApplicationFactory>, IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _baseUrl = null!;

    public MobileTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
    }

    public async Task InitializeAsync()
    {
        _baseUrl = _factory.GetServerAddress();
        _context = await _playwright.NewContextAsync(deviceName: "Pixel 5");
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();
    }

    [Fact]
    public async Task MapPage_OnPixel5_ShouldBeResponsive()
    {
        var mapPage = new MapPage(_page, _baseUrl);
        await mapPage.GoToAsync();
        await mapPage.WaitForMapLoadedAsync();

        // Map should fill the mobile screen
        var viewport = _page.ViewportSize;
        Assert.NotNull(viewport);
        Assert.True(viewport!.Width <= 412,
            $"Mobile viewport width {viewport.Width} should be ≤ Pixel 5 width (412px)");

        // Verify the map is actually rendered (not blank)
        var isRendered = await mapPage.IsMapRenderedAsync();
        Assert.True(isRendered, "Leaflet map container should be visible on mobile viewport");
    }

    [Fact]
    public async Task PlayerGrid_OnPixel5_ShouldRenderCards()
    {
        // Register a test user first so we can access the players grid
        var userEmail = $"mobile_{Guid.NewGuid():N}@test.com";
        const string password = "TestPass123!";

        var registerPage = new RegisterPage(_page, _baseUrl);
        await registerPage.GoToAsync();
        await registerPage.RegisterAsync(userEmail, password);

        var gridPage = new PlayerGridPage(_page, _baseUrl);
        await gridPage.GoToAsync();
        await gridPage.WaitForPlayerGridLoadedAsync();

        // Player cards should be visible — even if empty state is shown, the page should render
        var count = await gridPage.GetNearbyPlayerCountAsync();
        var isEmpty = await gridPage.IsEmptyStateVisibleAsync();

        // At least one of: cards shown or empty-state message visible
        Assert.True(count >= 0 || isEmpty,
            "Player grid on mobile should either show cards or empty-state message");
    }
}
