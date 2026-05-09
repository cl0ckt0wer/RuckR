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
        return state with { VisibleEncounters = action.Encounters };
    }

    [ReducerMethod]
    public static MapState ReduceSelectPitch(MapState state, SelectPitchAction action)
    {
        return state with { SelectedPitchId = action.PitchId };
    }

    [ReducerMethod]
    public static MapState ReduceSelectEncounter(MapState state, SelectEncounterAction action)
    {
        return state with { SelectedEncounterId = action.EncounterId };
    }

    [ReducerMethod]
    public static MapState ReduceClearSelection(MapState state, ClearSelectionAction action)
    {
        return state with { SelectedPitchId = null, SelectedEncounterId = null };
    }
}
