using Fluxor;

namespace RuckR.Client.Store.LocationFeature;

[FeatureState]
public record LocationState
{
    public double? UserLatitude { get; init; }
    public double? UserLongitude { get; init; }
    public double? AccuracyMeters { get; init; }
    public bool IsWatching { get; init; }
    public string? ErrorMessage { get; init; }

    public LocationState() { }
}
