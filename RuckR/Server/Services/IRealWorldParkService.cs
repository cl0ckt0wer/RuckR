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
    Task<IReadOnlyList<RealWorldPark>> FindNearbyParksAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PitchCandidatePlaceDto>> FindNearbyPitchCandidatePlacesAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken = default);
}

