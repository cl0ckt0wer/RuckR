using Fluxor;

namespace RuckR.Client.Store.LocationFeature;

/// <summary>
/// Browser location permission state as understood by the client.
/// </summary>
public enum GeolocationPermissionStatus
{
    /// <summary>The browser did not expose a permission state.</summary>
    Unknown,

    /// <summary>The user has granted location access.</summary>
    Granted,

    /// <summary>The browser may still prompt the user for location access.</summary>
    Prompt,

    /// <summary>The user or browser policy denied location access.</summary>
    Denied,

    /// <summary>The browser or device does not expose geolocation.</summary>
    Unavailable
}

/// <summary>
/// Shared timing policy for browser geolocation state transitions.
/// </summary>
public static class LocationSearchPolicy
{
    /// <summary>
    /// Time to treat early permission errors as provisional while the browser may still resolve a GPS fix.
    /// </summary>
    public static readonly TimeSpan PendingErrorGracePeriod = TimeSpan.FromSeconds(8);
}

/// <summary>
/// Location feature state (watch status and latest user coordinates).
/// </summary>
[FeatureState]
public record LocationState
{
    /// <summary>
    /// Latest user latitude in decimal degrees.
    /// </summary>
    public double? UserLatitude { get; init; }

    /// <summary>
    /// Latest user longitude in decimal degrees.
    /// </summary>
    public double? UserLongitude { get; init; }

    /// <summary>
    /// Last known positional accuracy in meters.
    /// </summary>
    public double? AccuracyMeters { get; init; }

    /// <summary>
    /// Indicates whether geolocation watch is active.
    /// </summary>
    public bool IsWatching { get; init; }

    /// <summary>
    /// Last error message from location resolution.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Last browser geolocation error code, when available.
    /// </summary>
    public int? LastErrorCode { get; init; }

    /// <summary>
    /// UTC time when the latest browser location search started.
    /// </summary>
    public DateTime? SearchStartedAtUtc { get; init; }

    /// <summary>
    /// UTC time when the latest browser geolocation error was observed.
    /// </summary>
    public DateTime? LastErrorAtUtc { get; init; }

    /// <summary>
    /// Current browser location permission state.
    /// </summary>
    public GeolocationPermissionStatus PermissionStatus { get; init; } = GeolocationPermissionStatus.Unknown;

    /// <summary>
    /// Creates an empty location state.
    /// </summary>
    public LocationState() { }
}
