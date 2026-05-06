using Fluxor;

namespace RuckR.Client.Store.GameFeature;

public static class GameReducers
{
    [ReducerMethod]
    public static GameState ReduceSetAuthState(GameState state, SetAuthStateAction action) =>
        state with { IsAuthenticated = action.IsAuthenticated, Username = action.Username };

    [ReducerMethod]
    public static GameState ReduceSetConnectionState(GameState state, SetConnectionStateAction action) =>
        state with { IsSignalRConnected = action.IsConnected, ConnectionError = action.Error };
}
