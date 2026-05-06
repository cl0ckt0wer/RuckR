using Fluxor;
using RuckR.Shared.Models;

namespace RuckR.Client.Store.InventoryFeature;

[FeatureState]
public record InventoryState
{
    public bool IsLoading { get; init; }
    public IReadOnlyList<CollectionModel> CollectedPlayers { get; init; } = Array.Empty<CollectionModel>();
    public DateTime? LastSynced { get; init; }
    public string? ErrorMessage { get; init; }

    public InventoryState() { }
}
