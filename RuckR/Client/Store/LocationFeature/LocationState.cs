using Fluxor;

namespace RuckR.Client.Store.LocationFeature;

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
    /// Creates an empty location state.
    /// </summary>
    public LocationState() { }
}
