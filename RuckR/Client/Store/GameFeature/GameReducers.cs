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

    [ReducerMethod]
    public static GameState ReduceSetBrowserOnlineState(GameState state, SetBrowserOnlineStateAction action) =>
        state with { IsBrowserOnline = action.IsOnline };

    [ReducerMethod]
    public static GameState ReduceSetConnectionMetrics(GameState state, SetConnectionMetricsAction action) =>
        state with { ConnectionLatencyMs = action.LatencyMs, PendingActionCount = action.PendingActionCount };
}
