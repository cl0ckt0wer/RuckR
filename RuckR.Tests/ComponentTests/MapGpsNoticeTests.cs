using RuckR.Shared.Models;
using MapPage = RuckR.Client.Pages.Map;

namespace RuckR.Tests.ComponentTests;

    /// <summary>
    /// Provides access to class.
    /// </summary>
public class MapGpsNoticeTests
{
    [Fact]
    /// <summary>
    /// Verifies should Show Gps Notice For State Shows Only When Gps Is Unavailable.
    /// </summary>
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

    [Fact]
    /// <summary>
    /// Verifies should Show Gps Notice For State Stays Hidden After Dismissal.
    /// </summary>
    public void ShouldShowGpsNoticeForState_StaysHiddenAfterDismissal()
    {
        Assert.False(MapPage.ShouldShowGpsNoticeForState(
            hasApiKey: true,
            gpsNoticeDismissed: true,
            lastKnownPosition: null));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("configured-api-key", false)]
    /// <summary>
    /// Verifies should Show Map Unavailable Fallback Only When Map Key Is Missing.
    /// </summary>
    public void ShouldShowMapUnavailableFallback_OnlyWhenMapKeyIsMissing(
        string? apiKey,
        bool expected)
    {
        Assert.Equal(expected, MapPage.ShouldShowMapUnavailableFallback(apiKey));
    }

    [Fact]
    /// <summary>
    /// Verifies map Unavailable Copy Gives Player Next Actions Instead Of Configuration Instructions.
    /// </summary>
    public void MapUnavailableCopy_GivesPlayerNextActionsInsteadOfConfigurationInstructions()
    {
        Assert.Contains("browse players", MapPage.MapUnavailableBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("environment variable", MapPage.MapUnavailableBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API key", MapPage.MapUnavailableTitle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    /// <summary>
    /// Verifies build Gps Status When Waiting For Location Returns Searching.
    /// </summary>
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

    [Fact]
    /// <summary>
    /// Verifies build Gps Status When Accuracy Is Good Returns Ready With Accuracy.
    /// </summary>
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

    [Fact]
    /// <summary>
    /// Verifies build Gps Status When Accuracy Is Poor Returns Weak With Action Hint.
    /// </summary>
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

    [Fact]
    /// <summary>
    /// Verifies build Gps Status When Location Errors Returns Blocked.
    /// </summary>
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

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    /// <summary>
    /// Verifies should Dismiss Gps Notice After Retry Hides On Fifth Retry.
    /// </summary>
    public void ShouldDismissGpsNoticeAfterRetry_HidesOnFifthRetry(
        int retryAttempts,
        bool expected)
    {
        Assert.Equal(expected, MapPage.ShouldDismissGpsNoticeAfterRetry(retryAttempts));
    }
}


