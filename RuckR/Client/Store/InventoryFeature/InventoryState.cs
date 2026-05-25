using Fluxor;
using RuckR.Shared.Models;

namespace RuckR.Client.Store.InventoryFeature;

/// <summary>
/// Inventory feature state for captured recruit collection.
/// </summary>
[FeatureState]
public record InventoryState
{
    /// <summary>
    /// Indicates inventory loading status.
    /// </summary>
    public bool IsLoading { get; init; }

    /// <summary>
    /// All captured recruits in the current user's collection.
    /// </summary>
    public IReadOnlyList<CollectionModel> CollectedPlayers { get; init; } = Array.Empty<CollectionModel>();

    /// <summary>
    /// Timestamp of last successful collection sync.
    /// </summary>
    public DateTime? LastSynced { get; init; }

    /// <summary>
    /// Last collection-related error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates an empty inventory state.
    /// </summary>
    public InventoryState() { }
}
