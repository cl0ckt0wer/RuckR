using RuckR.Shared.Models;

namespace RuckR.Client.Store.MapFeature;

/// <summary>
/// Indicates that map initialization has completed.
/// </summary>
public record MapInitializedAction;

/// <summary>
/// Replaces the currently visible pitches.
/// </summary>
/// <param name="Pitches">Visible pitch collection.</param>
public record SetPitchesAction(IReadOnlyList<PitchModel> Pitches);

/// <summary>
/// Replaces the currently visible encounters.
/// </summary>
/// <param name="Encounters">Visible encounter collection.</param>
public record SetEncountersAction(IReadOnlyList<PlayerEncounterDto> Encounters);

/// <summary>
/// Selects a pitch in UI state.
/// </summary>
/// <param name="PitchId">Selected pitch id.</param>
public record SelectPitchAction(int? PitchId);

/// <summary>
/// Selects an encounter in UI state.
/// </summary>
/// <param name="EncounterId">Selected encounter id.</param>
public record SelectEncounterAction(Guid? EncounterId);

/// <summary>
/// Clears map selection state.
/// </summary>
public record ClearSelectionAction;
