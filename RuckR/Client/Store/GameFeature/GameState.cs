using Fluxor;

namespace RuckR.Client.Store.GameFeature;

/// <summary>
/// Core game/session state maintained by the client.
/// </summary>
[FeatureState]
public record GameState
{
    /// <summary>
    /// Indicates whether the user is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// Current username, when authenticated.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Indicates SignalR connection status.
    /// </summary>
    public bool IsSignalRConnected { get; init; }

    /// <summary>
    /// Last connection error text, if any.
    /// </summary>
    public string? ConnectionError { get; init; }

    /// <summary>
    /// Browser online/offline status.
    /// </summary>
    public bool IsBrowserOnline { get; init; } = true;

    /// <summary>
    /// Latest measured SignalR latency in milliseconds.
    /// </summary>
    public int? ConnectionLatencyMs { get; init; }

    /// <summary>
    /// Number of outbound actions currently queued while offline/reconnect.
    /// </summary>
    public int PendingActionCount { get; init; }

    /// <summary>
    /// Creates an empty game state.
    /// </summary>
    public GameState() { }
}
