using RuckR.Client.Store.LocationFeature;

namespace RuckR.Tests.ComponentTests;

public class LocationReducerTests
{
    [Fact]
    public void ReduceLocationError_WithPermissionDenied_TurnsOffWatchingAndStoresPermissionState()
    {
        var state = new LocationState
        {
            IsWatching = true,
            PermissionStatus = GeolocationPermissionStatus.Prompt
        };

        var next = LocationReducers.ReduceLocationError(
            state,
            new LocationErrorAction(
                "Location permission is off.",
                1,
                GeolocationPermissionStatus.Denied));

        Assert.False(next.IsWatching);
        Assert.Equal("Location permission is off.", next.ErrorMessage);
        Assert.Equal(1, next.LastErrorCode);
        Assert.Equal(GeolocationPermissionStatus.Denied, next.PermissionStatus);
    }

    [Fact]
    public void ReduceUpdatePosition_ClearsPermissionErrorsAndMarksGranted()
    {
        var state = new LocationState
        {
            ErrorMessage = "Location permission is off.",
            LastErrorCode = 1,
            PermissionStatus = GeolocationPermissionStatus.Denied
        };

        var next = LocationReducers.ReduceUpdatePosition(
            state,
            new UpdatePositionAction(51.5074, -0.1278, 18));

        Assert.Null(next.ErrorMessage);
        Assert.Null(next.LastErrorCode);
        Assert.Equal(GeolocationPermissionStatus.Granted, next.PermissionStatus);
        Assert.Equal(51.5074, next.UserLatitude);
        Assert.Equal(-0.1278, next.UserLongitude);
    }
}
