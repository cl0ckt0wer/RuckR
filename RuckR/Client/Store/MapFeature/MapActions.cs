using RuckR.Shared.Models;

namespace RuckR.Client.Store.MapFeature;

public record MapInitializedAction;
public record SetPitchesAction(IReadOnlyList<PitchModel> Pitches);
public record SetEncountersAction(IReadOnlyList<PlayerEncounterDto> Encounters);
public record SelectPitchAction(int? PitchId);
public record SelectEncounterAction(Guid? EncounterId);
public record ClearSelectionAction;
