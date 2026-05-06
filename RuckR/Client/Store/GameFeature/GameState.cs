using Fluxor;

namespace RuckR.Client.Store.GameFeature;

[FeatureState]
public record GameState
{
    public bool IsAuthenticated { get; init; }
    public string? Username { get; init; }
    public bool IsSignalRConnected { get; init; }
    public string? ConnectionError { get; init; }

    public GameState() { }
}
