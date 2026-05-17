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
            ErrorMessage = null
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
        return state with { ErrorMessage = action.ErrorMessage, IsWatching = false };
    }
}
