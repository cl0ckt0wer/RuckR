using RuckR.Shared.Models;

namespace RuckR.Client.Services;

public interface IGeolocationService : IAsyncDisposable
{
    Task<GeoPosition?> GetCurrentPositionAsync();
    Task StartWatchAsync();
    void StopWatch();
    event Action<GeoPosition>? PositionChanged;
}
