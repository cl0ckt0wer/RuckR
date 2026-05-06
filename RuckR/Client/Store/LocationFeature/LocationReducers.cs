using Fluxor;

namespace RuckR.Client.Store.LocationFeature;

public static class LocationReducers
{
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

    [ReducerMethod]
    public static LocationState ReduceSetGpsWatching(LocationState state, SetGpsWatchingAction action)
    {
        return state with { IsWatching = action.IsWatching };
    }

    [ReducerMethod]
    public static LocationState ReduceLocationError(LocationState state, LocationErrorAction action)
    {
        return state with { ErrorMessage = action.ErrorMessage, IsWatching = false };
    }
}
