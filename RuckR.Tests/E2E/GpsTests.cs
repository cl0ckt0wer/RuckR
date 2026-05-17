using Microsoft.Playwright;
using RuckR.Tests.Fixtures;
using RuckR.Tests.Pages;

namespace RuckR.Tests.E2E;

[Collection(nameof(TestCollection))]
    /// <summary>
    /// Provides access to i Class Fixture<Playwright Fixture>,.
    /// </summary>
public class GpsTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _baseUrl = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="""GpsTests"""/> class.
    /// </summary>
    /// <param name="factory">The factory to use.</param>
    /// <param name="playwright">The playwright to use.</param>
    public GpsTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
    }

    /// <summary>
    /// Verifies initialize Async.
    /// </summary>
    public async Task InitializeAsync()
    {
        _baseUrl = _factory.GetServerAddress();
        _context = await _playwright.NewContextAsync(
            grantGeolocation: true,
            latitude: 51.5074,
            longitude: -0.1278);
        _page = await _context.NewPageAsync();
    }

    /// <summary>
    /// Verifies dispose Async.
    /// </summary>
    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();
    }

    [Fact]
    /// <summary>
    /// Verifies gps Enabled Map Centers On User Location.
    /// </summary>
    public async Task GpsEnabled_MapCentersOnUserLocation()
    {
        var mapPage = new MapPage(_page, _baseUrl);
        await mapPage.GoToAsync();
        await mapPage.WaitForMapLoadedAsync();

        // User marker should be visible (blue pulsing circle)
        var hasUserMarker = await mapPage.IsUserMarkerVisibleAsync();
        Assert.True(hasUserMarker, "User location marker should appear when GPS is enabled");
    }

    [Fact]
    /// <summary>
    /// Verifies gps Disabled Shows Enable Prompt.
    /// </summary>
    public async Task GpsDisabled_ShowsEnablePrompt()
    {
        // Create context WITHOUT geolocation permission
        await using var noGpsContext = await _playwright.NewContextAsync(grantGeolocation: false);
        var noGpsPage = await noGpsContext.NewPageAsync();

        var mapPage = new MapPage(noGpsPage, _baseUrl);
        await mapPage.GoToAsync();
        await mapPage.WaitForMapLoadedAsync();

        // The "Enable GPS" banner should be visible when geolocation is denied
        var hasGpsBanner = await mapPage.IsGpsDisabledBannerVisibleAsync();
        Assert.True(hasGpsBanner, "GPS disabled banner should appear when geolocation is denied");
    }
}


