namespace RuckR.Client.Store.GameFeature;

/// <summary>
/// Sets current authentication status in game state.
/// </summary>
/// <param name="IsAuthenticated">Whether user is authenticated.</param>
/// <param name="Username">Authenticated user name.</param>
public record SetAuthStateAction(bool IsAuthenticated, string? Username);

/// <summary>
/// Sets SignalR connection state and optional error message.
/// </summary>
/// <param name="IsConnected">Whether SignalR is connected.</param>
/// <param name="Error">Connection error text if any.</param>
public record SetConnectionStateAction(bool IsConnected, string? Error);

/// <summary>
/// Updates browser online state.
/// </summary>
/// <param name="IsOnline">Whether browser is online.</param>
public record SetBrowserOnlineStateAction(bool IsOnline);

/// <summary>
/// Updates connection metrics in state.
/// </summary>
/// <param name="LatencyMs">Observed latency in milliseconds.</param>
/// <param name="PendingActionCount">Queued outbound actions count.</param>
public record SetConnectionMetricsAction(int? LatencyMs, int PendingActionCount);
