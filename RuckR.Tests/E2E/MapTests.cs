using Microsoft.Playwright;
using RuckR.Tests.Fixtures;
using RuckR.Tests.Pages;

namespace RuckR.Tests.E2E;

[Collection(nameof(TestCollection))]
public class MapTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _baseUrl = null!;

    public MapTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
    }

    public async Task InitializeAsync()
    {
        _context = await _playwright.NewContextAsync(grantGeolocation: true, latitude: 51.5074, longitude: -0.1278);
        _page = await _context.NewPageAsync();
        _baseUrl = _factory.ServerBaseUrl;
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();
    }

    [Fact]
    public async Task MapPage_Loads_ShowsGeoBlazorMap()
    {
        var mapPage = new MapPage(_page, _baseUrl);
        await mapPage.GoToAsync();

        // Verify map container exists in DOM
        var isRendered = await mapPage.IsMapRenderedAsync();
        Assert.True(isRendered, "Map container should be rendered in the DOM");

        // Verify map shell loaded
        var mapLoaded = await mapPage.WaitForMapLoadedAsync();
        Assert.True(mapLoaded, "Map shell should load within timeout");
    }

    [Fact]
    public async Task MapPage_ShowsOnboardingBanner_CanDismiss()
    {
        var mapPage = new MapPage(_page, _baseUrl);
        await mapPage.GoToAsync();
        await mapPage.WaitForMapLoadedAsync();

        // Onboarding should appear on first visit
        var hasOnboarding = await mapPage.IsOnboardingBannerVisibleAsync();
        Assert.True(hasOnboarding, "Onboarding banner should appear on first visit");

        // Dismiss it
        await mapPage.DismissOnboardingAsync();

        // Verify banner is hidden after dismiss
        var stillVisible = await mapPage.IsOnboardingBannerVisibleAsync();
        Assert.False(stillVisible, "Onboarding banner should be hidden after dismiss");

        // Refresh and verify onboarding does NOT reappear (localStorage flag persisted)
        await _page.ReloadAsync();
        await mapPage.WaitForMapLoadedAsync();
        var hasOnboardingAfterReload = await mapPage.IsOnboardingBannerVisibleAsync();
        Assert.False(hasOnboardingAfterReload, "Onboarding banner should not reappear after reload");
    }
}
