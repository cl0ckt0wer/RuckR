using System.Linq;
using Fluxor;

namespace RuckR.Client.Store.InventoryFeature;

/// <summary>
/// Reducers for inventory state updates.
/// </summary>
public static class InventoryReducers
{
    /// <summary>
    /// Starts inventory fetch and marks state as loading.
    /// </summary>
    /// <param name="state">Current inventory state.</param>
    /// <param name="_">Fetch action.</param>
    /// <returns>Updated inventory state.</returns>
    [ReducerMethod]
    public static InventoryState ReduceFetchInventory(InventoryState state, FetchInventoryAction _) =>
        state with { IsLoading = true, ErrorMessage = null };

    /// <summary>
    /// Applies fetched collection and updates the last sync time.
    /// </summary>
    /// <param name="state">Current inventory state.</param>
    /// <param name="action">Fetch-result action.</param>
    /// <returns>Updated inventory state.</returns>
    [ReducerMethod]
    public static InventoryState ReduceFetchInventoryResult(InventoryState state, FetchInventoryResultAction action) =>
        state with { IsLoading = false, CollectedPlayers = action.Players, LastSynced = DateTime.UtcNow };

    /// <summary>
    /// Adds a newly captured player to inventory.
    /// </summary>
    /// <param name="state">Current inventory state.</param>
    /// <param name="action">Add player action.</param>
    /// <returns>Updated inventory state.</returns>
    [ReducerMethod]
    public static InventoryState ReduceAddPlayer(InventoryState state, AddPlayerAction action) =>
        state with { CollectedPlayers = state.CollectedPlayers.Append(action.Player).ToList() };

    /// <summary>
    /// Toggles favorite flag on the selected collection entry.
    /// </summary>
    /// <param name="state">Current inventory state.</param>
    /// <param name="action">Favorite toggle action.</param>
    /// <returns>Updated inventory state.</returns>
    [ReducerMethod]
    public static InventoryState ReduceToggleFavorite(InventoryState state, ToggleFavoriteAction action)
    {
        var list = state.CollectedPlayers.ToList();
        var idx = list.FindIndex(c => c.Id == action.CollectionId);
        if (idx >= 0)
        {
            var item = list[idx];
            item.IsFavorite = !item.IsFavorite;
        }
        return state with { CollectedPlayers = list };
    }

    /// <summary>
    /// Marks inventory request as failed and stores message.
    /// </summary>
    /// <param name="state">Current inventory state.</param>
    /// <param name="action">Error action.</param>
    /// <returns>Updated inventory state.</returns>
    [ReducerMethod]
    public static InventoryState ReduceInventoryError(InventoryState state, InventoryErrorAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };
}

