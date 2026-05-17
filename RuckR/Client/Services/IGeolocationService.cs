using RuckR.Shared.Models;

namespace RuckR.Client.Services;

/// <summary>
/// Abstraction for obtaining and watching geolocation in the client.
/// </summary>
public interface IGeolocationService : IAsyncDisposable
{
    /// <summary>
    /// Gets the current position once via browser geolocation.
    /// </summary>
    /// <returns>The current position, or <c>null</c> when unavailable.</returns>
    Task<GeoPosition?> GetCurrentPositionAsync();

    /// <summary>
    /// Starts continuous watch updates.
    /// </summary>
    Task StartWatchAsync();

    /// <summary>
    /// Stops geolocation watch updates.
    /// </summary>
    void StopWatch();

    /// <summary>
    /// Raised when a position has been accepted and normalized.
    /// </summary>
    event Action<GeoPosition>? PositionChanged;
}
