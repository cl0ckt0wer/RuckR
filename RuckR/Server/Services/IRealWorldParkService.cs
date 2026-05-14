namespace RuckR.Server.Services;

public sealed record RealWorldPark(
    string PlaceId,
    string Name,
    double Latitude,
    double Longitude,
    double DistanceMeters);

public interface IRealWorldParkService
{
    Task<IReadOnlyList<RealWorldPark>> FindNearbyParksAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken = default);
}
