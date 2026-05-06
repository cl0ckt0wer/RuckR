using RuckR.Shared.Models;

namespace RuckR.Client.Store.MapFeature;

public record MapInitializedAction;
public record SetPitchesAction(IReadOnlyList<PitchModel> Pitches);
public record SelectPitchAction(int? PitchId);
public record ClearSelectionAction;
