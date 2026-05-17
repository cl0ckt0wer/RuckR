using Fluxor;

namespace RuckR.Client.Store.GameFeature;

/// <summary>
/// Reducers for game/session state updates.
/// </summary>
public static class GameReducers
{
    [ReducerMethod]
    /// <summary>
    /// Updates authentication state and username.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="action">Action carrying auth status.</param>
    /// <returns>Updated game state.</returns>
    public static GameState ReduceSetAuthState(GameState state, SetAuthStateAction action) =>
        state with { IsAuthenticated = action.IsAuthenticated, Username = action.Username };

    [ReducerMethod]
    /// <summary>
    /// Updates SignalR connection state.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="action">Action carrying SignalR status and optional error.</param>
    /// <returns>Updated game state.</returns>
    public static GameState ReduceSetConnectionState(GameState state, SetConnectionStateAction action) =>
        state with { IsSignalRConnected = action.IsConnected, ConnectionError = action.Error };

    [ReducerMethod]
    /// <summary>
    /// Updates browser online indicator.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="action">Action carrying browser online status.</param>
    /// <returns>Updated game state.</returns>
    public static GameState ReduceSetBrowserOnlineState(GameState state, SetBrowserOnlineStateAction action) =>
        state with { IsBrowserOnline = action.IsOnline };

    [ReducerMethod]
    /// <summary>
    /// Updates connection latency and pending action count.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="action">Action carrying metrics.</param>
    /// <returns>Updated game state.</returns>
    public static GameState ReduceSetConnectionMetrics(GameState state, SetConnectionMetricsAction action) =>
        state with { ConnectionLatencyMs = action.LatencyMs, PendingActionCount = action.PendingActionCount };
}
