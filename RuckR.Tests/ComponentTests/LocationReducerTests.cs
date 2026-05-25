using RuckR.Client.Store.LocationFeature;

namespace RuckR.Tests.ComponentTests;

public class LocationReducerTests
{
    [Fact]
    public void ReduceStartLocationSearch_ClearsErrorsAndMarksWatching()
    {
        var startedAt = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
        var state = new LocationState
        {
            ErrorMessage = "Location permission is off.",
            LastErrorCode = 1,
            LastErrorAtUtc = startedAt.AddSeconds(-2),
            PermissionStatus = GeolocationPermissionStatus.Denied
        };

        var next = LocationReducers.ReduceStartLocationSearch(
            state,
            new StartLocationSearchAction(startedAt));

        Assert.True(next.IsWatching);
        Assert.Null(next.ErrorMessage);
        Assert.Null(next.LastErrorCode);
        Assert.Null(next.LastErrorAtUtc);
        Assert.Equal(startedAt, next.SearchStartedAtUtc);
    }

    [Fact]
    public void ReduceLocationError_WithPermissionDenied_TurnsOffWatchingAndStoresPermissionState()
    {
        var errorAt = new DateTime(2026, 5, 25, 12, 0, 1, DateTimeKind.Utc);
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
                GeolocationPermissionStatus.Denied,
                errorAt));

        Assert.False(next.IsWatching);
        Assert.Equal("Location permission is off.", next.ErrorMessage);
        Assert.Equal(1, next.LastErrorCode);
        Assert.Equal(errorAt, next.LastErrorAtUtc);
        Assert.Equal(GeolocationPermissionStatus.Denied, next.PermissionStatus);
    }

    [Fact]
    public void ReduceUpdatePosition_ClearsPermissionErrorsAndMarksGranted()
    {
        var state = new LocationState
        {
            ErrorMessage = "Location permission is off.",
            LastErrorCode = 1,
            SearchStartedAtUtc = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc),
            LastErrorAtUtc = new DateTime(2026, 5, 25, 12, 0, 1, DateTimeKind.Utc),
            PermissionStatus = GeolocationPermissionStatus.Denied
        };

        var next = LocationReducers.ReduceUpdatePosition(
            state,
            new UpdatePositionAction(51.5074, -0.1278, 18));

        Assert.Null(next.ErrorMessage);
        Assert.Null(next.LastErrorCode);
        Assert.Null(next.SearchStartedAtUtc);
        Assert.Null(next.LastErrorAtUtc);
        Assert.Equal(GeolocationPermissionStatus.Granted, next.PermissionStatus);
        Assert.Equal(51.5074, next.UserLatitude);
        Assert.Equal(-0.1278, next.UserLongitude);
    }

    [Fact]
    public void ReduceLocationSearchExpired_WithErrorAndNoFix_EndsProvisionalSearch()
    {
        var startedAt = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
        var state = new LocationState
        {
            SearchStartedAtUtc = startedAt,
            ErrorMessage = "Location permission is off.",
            LastErrorAtUtc = startedAt.AddSeconds(1)
        };

        var next = LocationReducers.ReduceLocationSearchExpired(
            state,
            new LocationSearchExpiredAction(startedAt.Add(LocationSearchPolicy.PendingErrorGracePeriod)));

        Assert.Null(next.SearchStartedAtUtc);
        Assert.Equal("Location permission is off.", next.ErrorMessage);
    }

    [Fact]
    public void ReduceLocationSearchExpired_WithPosition_DoesNotOverwritePositionState()
    {
        var startedAt = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
        var state = new LocationState
        {
            UserLatitude = 51.5074,
            UserLongitude = -0.1278,
            SearchStartedAtUtc = startedAt,
            ErrorMessage = "Location permission is off."
        };

        var next = LocationReducers.ReduceLocationSearchExpired(
            state,
            new LocationSearchExpiredAction(startedAt.Add(LocationSearchPolicy.PendingErrorGracePeriod)));

        Assert.Equal(startedAt, next.SearchStartedAtUtc);
    }
}
