using System.Linq;
using Fluxor;

namespace RuckR.Client.Store.InventoryFeature;

public static class InventoryReducers
{
    [ReducerMethod]
    public static InventoryState ReduceFetchInventory(InventoryState state, FetchInventoryAction _) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static InventoryState ReduceFetchInventoryResult(InventoryState state, FetchInventoryResultAction action) =>
        state with { IsLoading = false, CollectedPlayers = action.Players, LastSynced = DateTime.UtcNow };

    [ReducerMethod]
    public static InventoryState ReduceAddPlayer(InventoryState state, AddPlayerAction action) =>
        state with { CollectedPlayers = state.CollectedPlayers.Append(action.Player).ToList() };

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

    [ReducerMethod]
    public static InventoryState ReduceInventoryError(InventoryState state, InventoryErrorAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };
}
