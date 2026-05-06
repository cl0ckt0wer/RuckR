using Fluxor;
using RuckR.Shared.Models;

namespace RuckR.Client.Store.MapFeature;

[FeatureState]
public record MapState
{
    public bool IsMapInitialized { get; init; }
    public bool IsMapReady { get; init; }
    public IReadOnlyList<PitchModel> VisiblePitches { get; init; } = Array.Empty<PitchModel>();
    public int? SelectedPitchId { get; init; }

    public MapState() { }
}
