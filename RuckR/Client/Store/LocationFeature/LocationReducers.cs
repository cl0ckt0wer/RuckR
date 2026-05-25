using Fluxor;

namespace RuckR.Client.Store.LocationFeature;

/// <summary>
/// Reducers for location state updates.
/// </summary>
public static class LocationReducers
{
    /// <summary>
    /// Updates position coordinates and clears previous errors.
    /// </summary>
    /// <param name="state">Current location state.</param>
    /// <param name="action">Position action payload.</param>
    /// <returns>Updated location state.</returns>
    [ReducerMethod]
    public static LocationState ReduceUpdatePosition(LocationState state, UpdatePositionAction action)
    {
        return state with
        {
            UserLatitude = action.Latitude,
            UserLongitude = action.Longitude,
            AccuracyMeters = action.Accuracy,
            ErrorMessage = null,
            LastErrorCode = null,
            SearchStartedAtUtc = null,
            LastErrorAtUtc = null,
            PermissionStatus = GeolocationPermissionStatus.Granted
        };
    }

    /// <summary>
    /// Marks a fresh browser geolocation search.
    /// </summary>
    /// <param name="state">Current location state.</param>
    /// <param name="action">Search start action.</param>
    /// <returns>Updated location state.</returns>
    [ReducerMethod]
    public static LocationState ReduceStartLocationSearch(LocationState state, StartLocationSearchAction action)
    {
        return state with
        {
            IsWatching = true,
            ErrorMessage = null,
            LastErrorCode = null,
            SearchStartedAtUtc = action.StartedAtUtc,
            LastErrorAtUtc = null
        };
    }

    /// <summary>
    /// Updates watch status.
    /// </summary>
    /// <param name="state">Current location state.</param>
    /// <param name="action">Watch status action.</param>
    /// <returns>Updated location state.</returns>
    [ReducerMethod]
    public static LocationState ReduceSetGpsWatching(LocationState state, SetGpsWatchingAction action)
    {
        return state with { IsWatching = action.IsWatching };
    }

    /// <summary>
    /// Applies location error text and turns off watching.
    /// </summary>
    /// <param name="state">Current location state.</param>
    /// <param name="action">Error action.</param>
    /// <returns>Updated location state.</returns>
    [ReducerMethod]
    public static LocationState ReduceLocationError(LocationState state, LocationErrorAction action)
    {
        var permissionStatus = action.PermissionStatus == GeolocationPermissionStatus.Unknown
            ? state.PermissionStatus
            : action.PermissionStatus;

        return state with
        {
            ErrorMessage = action.ErrorMessage,
            LastErrorCode = action.ErrorCode,
            IsWatching = false,
            LastErrorAtUtc = action.ErrorAtUtc ?? DateTime.UtcNow,
            PermissionStatus = permissionStatus
        };
    }

    /// <summary>
    /// Ends the provisional search window after an early error.
    /// </summary>
    /// <param name="state">Current location state.</param>
    /// <param name="action">Search expiration action.</param>
    /// <returns>Updated location state.</returns>
    [ReducerMethod]
    public static LocationState ReduceLocationSearchExpired(LocationState state, LocationSearchExpiredAction action)
    {
        if (state.UserLatitude.HasValue && state.UserLongitude.HasValue)
        {
            return state;
        }

        if (state.SearchStartedAtUtc is null || string.IsNullOrWhiteSpace(state.ErrorMessage))
        {
            return state;
        }

        return state with
        {
            SearchStartedAtUtc = null
        };
    }

    /// <summary>
    /// Updates the browser permission state.
    /// </summary>
    /// <param name="state">Current location state.</param>
    /// <param name="action">Permission action.</param>
    /// <returns>Updated location state.</returns>
    [ReducerMethod]
    public static LocationState ReduceSetLocationPermission(LocationState state, SetLocationPermissionAction action)
    {
        return state with { PermissionStatus = action.PermissionStatus };
    }
}
