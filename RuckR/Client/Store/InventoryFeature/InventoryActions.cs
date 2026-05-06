using RuckR.Shared.Models;

namespace RuckR.Client.Store.InventoryFeature;

public record FetchInventoryAction;
public record FetchInventoryResultAction(IReadOnlyList<CollectionModel> Players);
public record AddPlayerAction(CollectionModel Player);
public record RemovePlayerAction(int PlayerId);
public record ToggleFavoriteAction(int CollectionId);
public record InventoryErrorAction(string ErrorMessage);
