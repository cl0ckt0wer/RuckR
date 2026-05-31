using RuckR.Shared.Models;
using RuckR.Client.Store.LocationFeature;
using Microsoft.Extensions.Configuration;
using MapPage = RuckR.Client.Pages.GameMap;

namespace RuckR.Tests.ComponentTests;

    /// <summary>
    /// Provides access to class.
    /// </summary>
public class MapGpsNoticeTests
{
    /// <summary>
    /// Verifies should Show Gps Notice For State Shows Only When Gps Is Unavailable.
    /// </summary>
    [Fact]
    public void ShouldShowGpsNoticeForState_ShowsOnlyWhenGpsIsUnavailable()
    {
        Assert.True(MapPage.ShouldShowGpsNoticeForState(
            hasApiKey: true,
            gpsNoticeDismissed: false,
            lastKnownPosition: null));

        Assert.False(MapPage.ShouldShowGpsNoticeForState(
            hasApiKey: true,
            gpsNoticeDismissed: false,
            lastKnownPosition: new GeoPosition
            {
                Latitude = 51.5074,
                Longitude = -0.1278,
                Accuracy = 500
            }));
    }

    /// <summary>
    /// Verifies should Show Gps Notice For State Stays Hidden After Dismissal.
    /// </summary>
    [Fact]
    public void ShouldShowGpsNoticeForState_StaysHiddenAfterDismissal()
    {
        Assert.False(MapPage.ShouldShowGpsNoticeForState(
            hasApiKey: true,
            gpsNoticeDismissed: true,
            lastKnownPosition: null));
    }

    /// <summary>
    /// Verifies permission denial still surfaces even if an old location exists.
    /// </summary>
    [Fact]
    public void ShouldShowGpsNoticeForState_ShowsPermissionDenialWithStaleLocation()
    {
        Assert.True(MapPage.ShouldShowGpsNoticeForState(
            hasApiKey: true,
            gpsNoticeDismissed: false,
            lastKnownPosition: new GeoPosition
            {
                Latitude = 51.5074,
                Longitude = -0.1278,
                Accuracy = 18
            },
            errorMessage: "Location permission is off."));
    }

    /// <summary>
    /// Verifies should Show Map Unavailable Fallback Only When Map Key Is Missing.
    /// </summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("configured-api-key", false)]
    public void ShouldShowMapUnavailableFallback_OnlyWhenMapKeyIsMissing(
        string? apiKey,
        bool expected)
    {
        Assert.Equal(expected, MapPage.ShouldShowMapUnavailableFallback(apiKey));
    }

    /// <summary>
    /// Verifies map Unavailable Copy Gives Player Next Actions Instead Of Configuration Instructions.
    /// </summary>
    [Fact]
    public void MapUnavailableCopy_GivesPlayerNextActionsInsteadOfConfigurationInstructions()
    {
        Assert.Contains("browse players", MapPage.MapUnavailableBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("environment variable", MapPage.MapUnavailableBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API key", MapPage.MapUnavailableTitle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies build Gps Status When Waiting For Location Returns Searching.
    /// </summary>
    [Fact]
    public void BuildGpsStatus_WhenWaitingForLocation_ReturnsSearching()
    {
        var status = MapPage.BuildGpsStatus(
            hasApiKey: true,
            isWatching: true,
            errorMessage: null,
            lastKnownPosition: null,
            maxActionAccuracyMeters: 200);

        Assert.Equal(MapPage.GpsStatusKind.Searching, status.Kind);
        Assert.Equal("Searching", status.Label);
        Assert.Null(status.AccuracyText);
    }

    /// <summary>
    /// Verifies build Gps Status When Accuracy Is Good Returns Ready With Accuracy.
    /// </summary>
    [Fact]
    public void BuildGpsStatus_WhenAccuracyIsGood_ReturnsReadyWithAccuracy()
    {
        var status = MapPage.BuildGpsStatus(
            hasApiKey: true,
            isWatching: true,
            errorMessage: null,
            lastKnownPosition: new GeoPosition
            {
                Latitude = 51.5074,
                Longitude = -0.1278,
                Accuracy = 150
            },
            maxActionAccuracyMeters: 200);

        Assert.Equal(MapPage.GpsStatusKind.Ready, status.Kind);
        Assert.Equal("GPS on", status.Label);
        Assert.Equal("150m", status.AccuracyText);
        Assert.Null(MapPage.BuildGpsActionHint(status, 200));
    }

    /// <summary>
    /// Verifies build Gps Status When Accuracy Is Poor Returns Weak With Action Hint.
    /// </summary>
    [Fact]
    public void BuildGpsStatus_WhenAccuracyIsPoor_ReturnsWeakWithActionHint()
    {
        var status = MapPage.BuildGpsStatus(
            hasApiKey: true,
            isWatching: true,
            errorMessage: null,
            lastKnownPosition: new GeoPosition
            {
                Latitude = 51.5074,
                Longitude = -0.1278,
                Accuracy = 250
            },
            maxActionAccuracyMeters: 200);

        Assert.Equal(MapPage.GpsStatusKind.Weak, status.Kind);
        Assert.Equal("Weak GPS", status.Label);
        Assert.Equal("250m", status.AccuracyText);
        Assert.Contains("200m", MapPage.BuildGpsActionHint(status, 200));
    }

    /// <summary>
    /// Verifies build Gps Status When Location Errors Returns Blocked.
    /// </summary>
    [Fact]
    public void BuildGpsStatus_WhenLocationErrors_ReturnsBlocked()
    {
        var status = MapPage.BuildGpsStatus(
            hasApiKey: true,
            isWatching: false,
            errorMessage: "Permission denied",
            lastKnownPosition: null,
            maxActionAccuracyMeters: 50);

        Assert.Equal(MapPage.GpsStatusKind.Blocked, status.Kind);
        Assert.Equal("GPS blocked", status.Label);
        Assert.Contains("location permission", MapPage.BuildGpsActionHint(status, 50), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Location permission is off", MapPage.BuildGpsNoticeTitle(status));
        Assert.Contains("browser location permission", MapPage.BuildGpsNoticeBody(status), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies early permission errors are treated as an active GPS search.
    /// </summary>
    [Fact]
    public void BuildGpsStatus_WhenPermissionErrorDuringSearchGrace_ReturnsSearching()
    {
        var startedAt = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);

        var status = MapPage.BuildGpsStatus(
            hasApiKey: true,
            isWatching: false,
            errorMessage: "Location permission is off.",
            lastKnownPosition: null,
            maxActionAccuracyMeters: 50,
            searchStartedAtUtc: startedAt,
            lastErrorAtUtc: startedAt.AddSeconds(1),
            nowUtc: startedAt.AddSeconds(3));

        Assert.Equal(MapPage.GpsStatusKind.Searching, status.Kind);
        Assert.Equal("Checking GPS", status.Label);
        Assert.Null(status.AccuracyText);
        Assert.Equal("Waiting for GPS before checking range.", MapPage.BuildGpsActionHint(status, 50));
    }

    /// <summary>
    /// Verifies permission errors become blocked after the browser search grace window expires.
    /// </summary>
    [Fact]
    public void BuildGpsStatus_WhenPermissionErrorAfterSearchGrace_ReturnsBlocked()
    {
        var startedAt = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);

        var status = MapPage.BuildGpsStatus(
            hasApiKey: true,
            isWatching: false,
            errorMessage: "Location permission is off.",
            lastKnownPosition: null,
            maxActionAccuracyMeters: 50,
            searchStartedAtUtc: startedAt,
            lastErrorAtUtc: startedAt.AddSeconds(1),
            nowUtc: startedAt.Add(LocationSearchPolicy.PendingErrorGracePeriod).AddMilliseconds(1));

        Assert.Equal(MapPage.GpsStatusKind.Blocked, status.Kind);
        Assert.Equal("GPS blocked", status.Label);
    }

    /// <summary>
    /// Verifies location permission helper recognizes browser denial text.
    /// </summary>
    [Theory]
    [InlineData("Location permission is off.", true)]
    [InlineData("User denied Geolocation.", true)]
    [InlineData("Location request timed out.", false)]
    [InlineData(null, false)]
    public void IsLocationPermissionDenied_ClassifiesPermissionErrors(string? message, bool expected)
    {
        Assert.Equal(expected, MapPage.IsLocationPermissionDenied(message));
    }

    /// <summary>
    /// Verifies should Dismiss Gps Notice After Retry Hides On Fifth Retry.
    /// </summary>
    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    public void ShouldDismissGpsNoticeAfterRetry_HidesOnFifthRetry(
        int retryAttempts,
        bool expected)
    {
        Assert.Equal(expected, MapPage.ShouldDismissGpsNoticeAfterRetry(retryAttempts));
    }

    /// <summary>
    /// Verifies parse Basemap Mode Handles Known Values.
    /// </summary>
    [Theory]
    [InlineData("styled", MapPage.MapBasemapMode.Styled)]
    [InlineData("empty", MapPage.MapBasemapMode.Empty)]
    [InlineData(" EMPTY ", MapPage.MapBasemapMode.Empty)]
    public void ParseBasemapMode_HandlesKnownValues(
        string value,
        MapPage.MapBasemapMode expected)
    {
        Assert.Equal(expected, MapPage.ParseBasemapMode(value));
    }

    /// <summary>
    /// Verifies parse Basemap Mode Returns Null For Unknown Values.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("satellite")]
    public void ParseBasemapMode_ReturnsNullForUnknownValues(string? value)
    {
        Assert.Null(MapPage.ParseBasemapMode(value));
    }

    /// <summary>
    /// Verifies resolve Map Reduction Options Uses Defaults When Config And Query Are Missing.
    /// </summary>
    [Fact]
    public void ResolveMapReductionOptions_UsesDefaults_WhenConfigAndQueryAreMissing()
    {
        var options = MapPage.ResolveMapReductionOptions(
            BuildConfiguration(),
            "https://example.test/map");

        Assert.Equal(MapPage.MapBasemapMode.Styled, options.BasemapMode);
        Assert.True(options.EnableArcGisWidgets);
        Assert.False(options.EnableMapDiagnostics);
        Assert.True(options.EnableMapPerformanceSummary);
        Assert.True(options.EnableGameGraphics);
        Assert.True(options.EnableAutoGpsWatch);
    }

    /// <summary>
    /// Verifies resolve Map Reduction Options Reads Config Values.
    /// </summary>
    [Fact]
    public void ResolveMapReductionOptions_ReadsConfigValues()
    {
        var options = MapPage.ResolveMapReductionOptions(
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["Map:BasemapMode"] = "empty",
                ["Map:EnableArcGisWidgets"] = "true",
                ["Map:EnableMapDiagnostics"] = "false",
                ["Map:EnableMapPerformanceSummary"] = "false",
                ["Map:EnableGameGraphics"] = "false",
                ["Map:EnableAutoGpsWatch"] = "false"
            }),
            "https://example.test/map");

        Assert.Equal(MapPage.MapBasemapMode.Empty, options.BasemapMode);
        Assert.True(options.EnableArcGisWidgets);
        Assert.False(options.EnableMapDiagnostics);
        Assert.False(options.EnableMapPerformanceSummary);
        Assert.False(options.EnableGameGraphics);
        Assert.False(options.EnableAutoGpsWatch);
    }

    /// <summary>
    /// Verifies local development defaults to the token-free basemap mode.
    /// </summary>
    [Fact]
    public void DevelopmentAppSettings_DefaultsToEmptyBasemap()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "RuckR",
            "Client",
            "wwwroot",
            "appsettings.Development.json"));

        var options = MapPage.ResolveMapReductionOptions(
            new ConfigurationBuilder()
                .AddJsonFile(path, optional: false)
                .Build(),
            "https://example.test/map");

        Assert.Equal(MapPage.MapBasemapMode.Empty, options.BasemapMode);
    }

    /// <summary>
    /// Verifies resolve Map Reduction Options Lets Query Override Config.
    /// </summary>
    [Fact]
    public void ResolveMapReductionOptions_LetsQueryOverrideConfig()
    {
        var options = MapPage.ResolveMapReductionOptions(
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["Map:BasemapMode"] = "styled",
                ["Map:EnableArcGisWidgets"] = "false",
                ["Map:EnableMapDiagnostics"] = "true",
                ["Map:EnableMapPerformanceSummary"] = "true",
                ["Map:EnableGameGraphics"] = "true",
                ["Map:EnableAutoGpsWatch"] = "true"
            }),
            "https://example.test/map?basemap=empty&arcGisWidgets=true&mapDiagnostics=false&mapPerfSummary=false&mapGraphics=false&autoGps=false");

        Assert.Equal(MapPage.MapBasemapMode.Empty, options.BasemapMode);
        Assert.True(options.EnableArcGisWidgets);
        Assert.False(options.EnableMapDiagnostics);
        Assert.False(options.EnableMapPerformanceSummary);
        Assert.False(options.EnableGameGraphics);
        Assert.False(options.EnableAutoGpsWatch);
    }

    /// <summary>
    /// Verifies deferred ArcGIS widgets render only after their delayed enable state is reached.
    /// </summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void ShouldRenderArcGisWidgetsForState_RequiresEnabledAndReady(
        bool enableArcGisWidgets,
        bool widgetsReady,
        bool expected)
    {
        Assert.Equal(expected, MapPage.ShouldRenderArcGisWidgetsForState(enableArcGisWidgets, widgetsReady));
    }

    /// <summary>
    /// Verifies resolve Map Reduction Options Treats Bare Basemap Query As Empty.
    /// </summary>
    [Fact]
    public void ResolveMapReductionOptions_TreatsBareBasemapQueryAsEmpty()
    {
        var options = MapPage.ResolveMapReductionOptions(
            BuildConfiguration(),
            "https://example.test/map?basemap");

        Assert.Equal(MapPage.MapBasemapMode.Empty, options.BasemapMode);
    }

    /// <summary>
    /// Verifies first map load waits briefly for GPS before falling back to the default map center.
    /// </summary>
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void ShouldWaitForInitialGpsPosition_OnlyWaitsWhenAutoGpsNeedsFirstPosition(
        bool enableAutoGpsWatch,
        bool hasLastKnownPosition,
        bool expected)
    {
        var position = hasLastKnownPosition
            ? new GeoPosition { Latitude = 51.5074, Longitude = -0.1278 }
            : null;

        Assert.Equal(expected, MapPage.ShouldWaitForInitialGpsPosition(enableAutoGpsWatch, position));
    }

    /// <summary>
    /// Verifies a nearby in-flight map data load suppresses duplicate GPS-triggered refreshes.
    /// </summary>
    [Fact]
    public void ShouldRefreshPitchesForState_SuppressesNearbyInFlightLoad()
    {
        var now = new DateTime(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);
        var current = new GeoPosition { Latitude = 51.5074, Longitude = -0.1278 };
        var inFlight = new GeoPosition { Latitude = 51.50741, Longitude = -0.12781 };

        Assert.False(MapPage.ShouldRefreshPitchesForState(
            current,
            lastFetchPosition: null,
            lastFetchAtUtc: now.AddMinutes(-5),
            nowUtc: now,
            inFlightPosition: inFlight,
            inFlightStartedAtUtc: now.AddSeconds(-1)));
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? values = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();
    }
}


