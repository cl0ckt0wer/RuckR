using RuckR.Shared.Models;
using MapPage = RuckR.Client.Pages.Map;

namespace RuckR.Tests.ComponentTests;

public class MapGpsNoticeTests
{
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

    [Fact]
    public void ShouldShowGpsNoticeForState_StaysHiddenAfterDismissal()
    {
        Assert.False(MapPage.ShouldShowGpsNoticeForState(
            hasApiKey: true,
            gpsNoticeDismissed: true,
            lastKnownPosition: null));
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    public void ShouldDismissGpsNoticeAfterRetry_HidesOnFifthRetry(
        int retryAttempts,
        bool expected)
    {
        Assert.Equal(expected, MapPage.ShouldDismissGpsNoticeAfterRetry(retryAttempts));
    }
}
