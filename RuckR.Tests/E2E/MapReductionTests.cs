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
    private const double TestGpsLatitude = 51.5074;
    private const double TestGpsLongitude = -0.1278;

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
            latitude: TestGpsLatitude,
            longitude: TestGpsLongitude);
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

        await mapPage.GoToAsync("basemap=empty&mapGraphics=true&autoGps=true");

        Assert.True(await mapPage.WaitForMapLoadedAsync(30_000), "Map shell should load.");
        Assert.True(await mapPage.WaitForGeoBlazorSurfaceAsync(45_000), "GeoBlazor should attach a visible ArcGIS drawing surface.");

        await mapPage.CenterOnNearestAvailablePitchAsync();
        await mapPage.ClickMapCenterAsync();
        await mapPage.WaitForPitchOverlayAsync(10_000);

        Assert.True(await mapPage.IsPitchOverlayVisibleAsync(), "Pitch marker tap should open the RuckR pitch overlay.");
        Assert.False(await mapPage.IsNativePopupVisibleAsync(), "Pitch marker tap should not also show the ArcGIS native popup.");
        AssertNoUnexpectedBrowserErrors();
    }

    /// <summary>
    /// Verifies the RuckR shortcut controls render as compact ArcGIS-style buttons on mobile.
    /// </summary>
    [Fact]
    public async Task MapShortcutButtons_OnMobile_RenderAsCompactMapControls()
    {
        var mapPage = new MapPage(_page, _baseUrl);

        await mapPage.GoToAsync("basemap=empty&mapGraphics=true&autoGps=true");

        Assert.True(await mapPage.WaitForMapLoadedAsync(30_000), "Map shell should load.");
        Assert.True(await mapPage.WaitForGeoBlazorSurfaceAsync(45_000), "GeoBlazor should attach a visible ArcGIS drawing surface.");
        await mapPage.WaitForShortcutButtonsAsync();

        var expectedLabels = new Dictionary<string, string>
        {
            [MapPage.GpsCenterButtonTestId] = "Return to GPS location",
            [MapPage.NearestStadiumButtonTestId] = "Nearest stadium",
            [MapPage.NearestStandardButtonTestId] = "Nearest pitch",
            [MapPage.NearestTrainingButtonTestId] = "Nearest training ground",
            [MapPage.CandidatePlacesToggleTestId] = "Hide candidate places"
        };

        foreach (var testId in MapPage.ShortcutButtonTestIds)
        {
            var (width, height) = await mapPage.GetShortcutButtonSizeAsync(testId);
            var backgroundColor = await mapPage.GetShortcutButtonCssAsync(testId, "background-color");
            var ariaLabel = await mapPage.GetShortcutButtonAttributeAsync(testId, "aria-label");
            var title = await mapPage.GetShortcutButtonAttributeAsync(testId, "title");

            Assert.InRange(width, 30, 36);
            Assert.InRange(height, 30, 36);
            if (testId == MapPage.CandidatePlacesToggleTestId)
            {
                Assert.True(
                    backgroundColor.Contains("255, 255, 255", StringComparison.Ordinal)
                    || backgroundColor.Contains("233, 221, 245", StringComparison.Ordinal),
                    $"Candidate places button should use either the default or active map-control background; actual was '{backgroundColor}'.");
            }
            else
            {
                Assert.Contains("255, 255, 255", backgroundColor);
            }

            Assert.Equal(expectedLabels[testId], ariaLabel);
            Assert.Equal(expectedLabels[testId], title);
        }

        AssertNoUnexpectedBrowserErrors();
    }

    /// <summary>
    /// Verifies the candidate place shortcut updates both pressed state and layer visibility.
    /// </summary>
    [Fact]
    public async Task CandidatePlacesButton_OnMobile_TogglesPressedStateAndLayerVisibility()
    {
        var mapPage = new MapPage(_page, _baseUrl);

        await mapPage.GoToAsync("basemap=empty&mapGraphics=true&autoGps=true");

        Assert.True(await mapPage.WaitForMapLoadedAsync(30_000), "Map shell should load.");
        Assert.True(await mapPage.WaitForGeoBlazorSurfaceAsync(45_000), "GeoBlazor should attach a visible ArcGIS drawing surface.");
        await mapPage.WaitForShortcutButtonsAsync();

        Assert.NotNull(await mapPage.GetShortcutButtonAttributeAsync(MapPage.CandidatePlacesToggleTestId, "aria-pressed"));

        await mapPage.ClickShortcutButtonAsync(MapPage.CandidatePlacesToggleTestId);

        Assert.Null(await mapPage.GetShortcutButtonAttributeAsync(MapPage.CandidatePlacesToggleTestId, "aria-pressed"));
        Assert.False(await mapPage.IsCandidatePlacesLayerVisibleAsync(), "Candidate places layer should hide after the shortcut is clicked.");

        await mapPage.ClickShortcutButtonAsync(MapPage.CandidatePlacesToggleTestId);

        Assert.NotNull(await mapPage.GetShortcutButtonAttributeAsync(MapPage.CandidatePlacesToggleTestId, "aria-pressed"));
        Assert.True(await mapPage.IsCandidatePlacesLayerVisibleAsync(), "Candidate places layer should show after the shortcut is clicked again.");
        AssertNoUnexpectedBrowserErrors();
    }

    /// <summary>
    /// Verifies the GPS shortcut recenters the ArcGIS view to the last known browser geolocation.
    /// </summary>
    [Fact]
    public async Task GpsCenterButton_OnMobile_RecentersMapToLastKnownGpsLocation()
    {
        var mapPage = new MapPage(_page, _baseUrl);

        await mapPage.GoToAsync("basemap=empty&mapGraphics=true&autoGps=true");

        Assert.True(await mapPage.WaitForMapLoadedAsync(30_000), "Map shell should load.");
        Assert.True(await mapPage.WaitForGeoBlazorSurfaceAsync(45_000), "GeoBlazor should attach a visible ArcGIS drawing surface.");
        await mapPage.WaitForShortcutButtonsAsync();
        await mapPage.WaitForShortcutButtonEnabledAsync(MapPage.GpsCenterButtonTestId, 20_000);

        await mapPage.SetArcGisViewCenterAsync(40.7128, -74.0060, 4);
        var displacedCenter = await mapPage.GetArcGisViewCenterAsync();

        Assert.True(Math.Abs(displacedCenter.Latitude - TestGpsLatitude) > 1,
            "The map should be displaced from the mocked GPS location before pressing the GPS shortcut.");

        await mapPage.ClickShortcutButtonAsync(MapPage.GpsCenterButtonTestId);
        await mapPage.WaitForArcGisViewCenterNearAsync(TestGpsLatitude, TestGpsLongitude, toleranceDegrees: 0.01);

        var gpsCenter = await mapPage.GetArcGisViewCenterAsync();
        Assert.InRange(Math.Abs(gpsCenter.Latitude - TestGpsLatitude), 0, 0.01);
        Assert.InRange(Math.Abs(gpsCenter.Longitude - TestGpsLongitude), 0, 0.01);
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
