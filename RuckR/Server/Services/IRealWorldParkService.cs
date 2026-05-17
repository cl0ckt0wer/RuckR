using RuckR.Shared.Models;

namespace RuckR.Server.Services;
/// <summary>Defines the server-side record RealWorldPark.</summary>
public sealed record RealWorldPark(
    string PlaceId,
    string Name,
    double Latitude,
    double Longitude,
    double DistanceMeters);
/// <summary>Defines the server-side interface IRealWorldParkService.</summary>
public interface IRealWorldParkService
{
    /// <summary>Finds nearby parks around a geographic point.</summary>
    /// <param name="latitude">Search center latitude in decimal degrees.</param>
    /// <param name="longitude">Search center longitude in decimal degrees.</param>
    /// <param name="radiusMeters">Search radius in meters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Nearby park candidates.</returns>
    Task<IReadOnlyList<RealWorldPark>> FindNearbyParksAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken = default);

    /// <summary>Finds nearby pitch candidate places around a geographic point.</summary>
    /// <param name="latitude">Search center latitude in decimal degrees.</param>
    /// <param name="longitude">Search center longitude in decimal degrees.</param>
    /// <param name="radiusMeters">Search radius in meters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Nearby place candidates suitable for pitch creation.</returns>
    Task<IReadOnlyList<PitchCandidatePlaceDto>> FindNearbyPitchCandidatePlacesAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken = default);
}

