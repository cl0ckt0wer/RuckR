using Fluxor;

namespace RuckR.Client.Store.MapFeature;

public static class MapReducers
{
    [ReducerMethod]
    public static MapState ReduceMapInitialized(MapState state, MapInitializedAction action)
    {
        return state with { IsMapInitialized = true, IsMapReady = true };
    }

    [ReducerMethod]
    public static MapState ReduceSetPitches(MapState state, SetPitchesAction action)
    {
        return state with { VisiblePitches = action.Pitches };
    }

    [ReducerMethod]
    public static MapState ReduceSetEncounters(MapState state, SetEncountersAction action)
    {
        // Keep the selected encounter if it's still in the list, otherwise clear it
        var selectedEncounterId = state.SelectedEncounterId;
        if (selectedEncounterId.HasValue && !action.Encounters.Any(e => e.EncounterId == selectedEncounterId.Value))
        {
            selectedEncounterId = null;
        }
        return state with { VisibleEncounters = action.Encounters, SelectedEncounterId = selectedEncounterId };
    }
}
