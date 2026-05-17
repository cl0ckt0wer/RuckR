using Fluxor;
using RuckR.Shared.Models;

namespace RuckR.Client.Store.MapFeature;

/// <summary>
/// Map feature state for pitch and encounter visibility.
/// </summary>
[FeatureState]
public record MapState
{
    /// <summary>
    /// Indicates map bootstrap has been triggered.
    /// </summary>
    public bool IsMapInitialized { get; init; }

    /// <summary>
    /// Indicates map is ready to render.
    /// </summary>
    public bool IsMapReady { get; init; }

    /// <summary>
    /// Pitches currently visible in map bounds.
    /// </summary>
    public IReadOnlyList<PitchModel> VisiblePitches { get; init; } = Array.Empty<PitchModel>();

    /// <summary>
    /// Player encounters currently visible in map bounds.
    /// </summary>
    public IReadOnlyList<PlayerEncounterDto> VisibleEncounters { get; init; } = Array.Empty<PlayerEncounterDto>();

    /// <summary>
    /// Selected pitch identifier, if any.
    /// </summary>
    public int? SelectedPitchId { get; init; }

    /// <summary>
    /// Selected encounter identifier, if any.
    /// </summary>
    public Guid? SelectedEncounterId { get; init; }

    /// <summary>
    /// Creates an empty map state.
    /// </summary>
    public MapState() { }
}
