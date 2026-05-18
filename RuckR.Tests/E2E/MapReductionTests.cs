using Microsoft.Playwright;
using RuckR.Tests.Fixtures;
using RuckR.Tests.Pages;

namespace RuckR.Tests.E2E;

/// <summary>
/// Regression coverage for the GeoBlazor map reduction flags and mobile marker interaction.
/// </summary>
[Collection(nameof(TestCollection))]
public class MapReductionTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private readonly List<IConsoleMessage> _consoleErrors = [];
    private readonly List<string> _pageErrors = [];
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _baseUrl = null!;

    private static readonly string[] KnownBenignConsoleErrors =
    {
        "AggregateException_ctor_DefaultMessage",
        "Arg_PlatformNotSupported",
        "401 (Unauthorized)",
        "Failed to load resource",
        // The deployment ArcGIS key can be referrer-restricted to the public host.
        // E2E runs from a localhost Kestrel origin, so styled basemap auth may fail
        // while the GeoBlazor view and RuckR overlay behavior are still testable.
        "Token Invalid.: Failed to load basemap"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="MapReductionTests"/> class.
    /// </summary>
    public MapReductionTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
    }

    /// <summary>
    /// Creates a mobile browser context with geolocation permissions near the seeded London pitch.
    /// </summary>
    public async Task InitializeAsync()
    {
        _baseUrl = _factory.ServerBaseUrl;
        _context = await _playwright.NewContextAsync(
            deviceName: "Pixel 5",
            grantGeolocation: true,
            latitude: 51.5074,
            longitude: -0.1278);
        _page = await _context.NewPageAsync();
        _page.Console += OnConsole;
        _page.PageError += OnPageError;
    }

    /// <summary>
    /// Disposes the browser page and context.
    /// </summary>
    public async Task DisposeAsync()
    {
        _page.Console -= OnConsole;
        _page.PageError -= OnPageError;
        await _page.CloseAsync();
        await _context.CloseAsync();
    }

    /// <summary>
    /// Verifies each planned reduction flag combination renders a non-blank GeoBlazor surface on mobile.
    /// </summary>
    [Theory]
    [InlineData("basemap=styled&mapGraphics=true&autoGps=false")]
    [InlineData("basemap=styled&mapGraphics=false&autoGps=true")]
    [InlineData("basemap=styled&mapGraphics=true&autoGps=true")]
    public async Task MapReductionFlags_OnMobile_RenderGeoBlazorSurface(string queryString)
    {
        var mapPage = new MapPage(_page, _baseUrl);

        await mapPage.GoToAsync(queryString);

        Assert.True(await mapPage.WaitForMapLoadedAsync(30_000), "Map shell should load.");
        Assert.True(await mapPage.WaitForGeoBlazorSurfaceAsync(45_000), "GeoBlazor should attach a visible ArcGIS drawing surface.");

        var (width, height) = await mapPage.GetMapContainerSizeAsync();
        Assert.True(width >= 320, $"Map width should fit a mobile viewport; actual width was {width}.");
        Assert.True(height >= 480, $"Map height should fill the mobile map shell; actual height was {height}.");
        AssertNoUnexpectedBrowserErrors();
    }

    /// <summary>
    /// Verifies a pitch marker tap opens the RuckR overlay without also showing an ArcGIS native popup.
    /// </summary>
    [Fact]
    public async Task PitchMarkerTap_OnMobile_OpensRuckROverlayWithoutNativePopup()
    {
        var mapPage = new MapPage(_page, _baseUrl);

        await mapPage.GoToAsync("basemap=styled&mapGraphics=true&autoGps=true");

        Assert.True(await mapPage.WaitForMapLoadedAsync(30_000), "Map shell should load.");
        Assert.True(await mapPage.WaitForGeoBlazorSurfaceAsync(45_000), "GeoBlazor should attach a visible ArcGIS drawing surface.");

        await mapPage.CenterOnNearestAvailablePitchAsync();
        await mapPage.ClickMapCenterAsync();
        await mapPage.WaitForPitchOverlayAsync();

        Assert.True(await mapPage.IsPitchOverlayVisibleAsync(), "Pitch marker tap should open the RuckR pitch overlay.");
        Assert.False(await mapPage.IsNativePopupVisibleAsync(), "Pitch marker tap should not also show the ArcGIS native popup.");
        AssertNoUnexpectedBrowserErrors();
    }

    private void OnConsole(object? _, IConsoleMessage msg)
    {
        if (msg.Type == "error")
            _consoleErrors.Add(msg);
    }

    private void OnPageError(object? _, string err)
    {
        _pageErrors.Add(err);
    }

    private void AssertNoUnexpectedBrowserErrors()
    {
        var unexpectedConsoleErrors = _consoleErrors
            .Where(error => !KnownBenignConsoleErrors.Any(known =>
                error.Text.Contains(known, StringComparison.OrdinalIgnoreCase)))
            .Select(error => error.Text)
            .ToList();

        Assert.True(unexpectedConsoleErrors.Count == 0,
            $"Unexpected browser console errors:\n{string.Join("\n", unexpectedConsoleErrors)}");
        Assert.True(_pageErrors.Count == 0,
            $"Unhandled page exceptions:\n{string.Join("\n", _pageErrors)}");
    }
}
