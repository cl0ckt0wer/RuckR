using RuckR.Shared.Models;

namespace RuckR.Client.Store.InventoryFeature;

/// <summary>
/// Triggers inventory fetch from API.
/// </summary>
public record FetchInventoryAction;

/// <summary>
/// Provides player list result for inventory fetch.
/// </summary>
/// <param name="Players">Fetched collection entries.</param>
public record FetchInventoryResultAction(IReadOnlyList<CollectionModel> Players);

/// <summary>
/// Adds a captured player to local inventory.
/// </summary>
/// <param name="Player">Captured player payload.</param>
public record AddPlayerAction(CollectionModel Player);

/// <summary>
/// Removes a player from inventory.
/// </summary>
/// <param name="PlayerId">Inventory player id.</param>
public record RemovePlayerAction(int PlayerId);

/// <summary>
/// Toggles favorite state for a collection row.
/// </summary>
/// <param name="CollectionId">Collection row identifier.</param>
public record ToggleFavoriteAction(int CollectionId);

/// <summary>
/// Reports an inventory error.
/// </summary>
/// <param name="ErrorMessage">Error text.</param>
public record InventoryErrorAction(string ErrorMessage);
