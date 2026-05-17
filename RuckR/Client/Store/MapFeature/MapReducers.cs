using Fluxor;

namespace RuckR.Client.Store.MapFeature;

/// <summary>
/// Reducers for map-related state updates.
/// </summary>
public static class MapReducers
{
    [ReducerMethod]
    /// <summary>
    /// Marks the map as initialized and ready.
    /// </summary>
    /// <param name="state">Current map state.</param>
    /// <param name="action">Initialization action.</param>
    /// <returns>Updated map state.</returns>
    public static MapState ReduceMapInitialized(MapState state, MapInitializedAction action)
    {
        return state with { IsMapInitialized = true, IsMapReady = true };
    }

    [ReducerMethod]
    /// <summary>
    /// Replaces the visible pitch list.
    /// </summary>
    /// <param name="state">Current map state.</param>
    /// <param name="action">Action containing pitches.</param>
    /// <returns>Updated map state.</returns>
    public static MapState ReduceSetPitches(MapState state, SetPitchesAction action)
    {
        return state with { VisiblePitches = action.Pitches };
    }

    [ReducerMethod]
    /// <summary>
    /// Replaces encounters and clears stale selected encounter if no longer visible.
    /// </summary>
    /// <param name="state">Current map state.</param>
    /// <param name="action">Action containing encounters.</param>
    /// <returns>Updated map state.</returns>
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

    [ReducerMethod]
    /// <summary>
    /// Selects an active pitch and clears encounter selection.
    /// </summary>
    /// <param name="state">Current map state.</param>
    /// <param name="action">Action containing selected pitch id.</param>
    /// <returns>Updated map state.</returns>
    public static MapState ReduceSelectPitch(MapState state, SelectPitchAction action)
    {
        return state with
        {
            SelectedPitchId = action.PitchId,
            SelectedEncounterId = null
        };
    }

    [ReducerMethod]
    /// <summary>
    /// Selects an active encounter and clears pitch selection.
    /// </summary>
    /// <param name="state">Current map state.</param>
    /// <param name="action">Action containing selected encounter id.</param>
    /// <returns>Updated map state.</returns>
    public static MapState ReduceSelectEncounter(MapState state, SelectEncounterAction action)
    {
        return state with
        {
            SelectedPitchId = null,
            SelectedEncounterId = action.EncounterId
        };
    }

    [ReducerMethod]
    /// <summary>
    /// Clears both pitch and encounter selections.
    /// </summary>
    /// <param name="state">Current map state.</param>
    /// <param name="action">Clear-selection action.</param>
    /// <returns>Updated map state.</returns>
    public static MapState ReduceClearSelection(MapState state, ClearSelectionAction action)
    {
        return state with
        {
            SelectedPitchId = null,
            SelectedEncounterId = null
        };
    }
}
