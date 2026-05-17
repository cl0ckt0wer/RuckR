using RuckR.Shared.Models;
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
            maxActionAccuracyMeters: 50);

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
                Accuracy = 18
            },
            maxActionAccuracyMeters: 50);

        Assert.Equal(MapPage.GpsStatusKind.Ready, status.Kind);
        Assert.Equal("GPS on", status.Label);
        Assert.Equal("18m", status.AccuracyText);
        Assert.Null(MapPage.BuildGpsActionHint(status, 50));
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
                Accuracy = 82
            },
            maxActionAccuracyMeters: 50);

        Assert.Equal(MapPage.GpsStatusKind.Weak, status.Kind);
        Assert.Equal("Weak GPS", status.Label);
        Assert.Equal("82m", status.AccuracyText);
        Assert.Contains("50m", MapPage.BuildGpsActionHint(status, 50));
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
        Assert.False(options.EnableArcGisWidgets);
        Assert.True(options.EnableMapDiagnostics);
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
                ["Map:EnableGameGraphics"] = "false",
                ["Map:EnableAutoGpsWatch"] = "false"
            }),
            "https://example.test/map");

        Assert.Equal(MapPage.MapBasemapMode.Empty, options.BasemapMode);
        Assert.True(options.EnableArcGisWidgets);
        Assert.False(options.EnableMapDiagnostics);
        Assert.False(options.EnableGameGraphics);
        Assert.False(options.EnableAutoGpsWatch);
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
                ["Map:EnableGameGraphics"] = "true",
                ["Map:EnableAutoGpsWatch"] = "true"
            }),
            "https://example.test/map?basemap=empty&arcGisWidgets=true&mapDiagnostics=false&mapGraphics=false&autoGps=false");

        Assert.Equal(MapPage.MapBasemapMode.Empty, options.BasemapMode);
        Assert.True(options.EnableArcGisWidgets);
        Assert.False(options.EnableMapDiagnostics);
        Assert.False(options.EnableGameGraphics);
        Assert.False(options.EnableAutoGpsWatch);
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

    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? values = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();
    }
}


