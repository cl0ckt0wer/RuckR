using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side class PitchDiscoveryService.</summary>
    public class PitchDiscoveryService : IPitchDiscoveryService
    {
        private const int AutoPromoteConfidenceThreshold = 74;
        private const double CandidateDuplicateDistanceMeters = 100.0;

        private readonly RuckRDbContext _db;
        private readonly IRealWorldParkService _parkService;
        private readonly ILogger<PitchDiscoveryService> _logger;
        private readonly GeometryFactory _geometryFactory =
            NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        /// <summary>Initializes a new instance of PitchDiscoveryService.</summary>
        /// <param name="db">The db.</param>
        /// <param name="parkService">The ArcGIS Places-backed pitch candidate source.</param>
        /// <param name="logger">Logger used for diagnostics.</param>
        public PitchDiscoveryService(
            RuckRDbContext db,
            IRealWorldParkService parkService,
            ILogger<PitchDiscoveryService> logger)
        {
            _db = db;
            _parkService = parkService;
            _logger = logger;
        }
        /// <summary>Ensures nearby Places-backed pitches are persisted and returns active pitches in range.</summary>
        /// <param name="userId">The userid.</param>
        /// <param name="userName">The username.</param>
        /// <param name="latitude">The latitude.</param>
        /// <param name="longitude">The longitude.</param>
        /// <param name="radiusMeters">The search radius.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The operation result.</returns>
        public async Task<IReadOnlyList<PitchModel>> EnsureNearbyPitchesAsync(
            string? userId,
            string? userName,
            double latitude,
            double longitude,
            double radiusMeters = 5000,
            CancellationToken cancellationToken = default)
        {
            var userPoint = _geometryFactory.CreatePoint(new Coordinate(longitude, latitude));
            var effectiveRadius = Math.Clamp(radiusMeters, 1.0, 50_000.0);
            var candidateRadius = Math.Min(effectiveRadius, 10_000.0);

            var candidates = await _parkService.FindNearbyPitchCandidatePlacesAsync(
                latitude,
                longitude,
                candidateRadius,
                cancellationToken);

            foreach (var candidate in candidates.Where(IsAutoPromotableCandidate))
            {
                await UpsertCandidatePitchAsync(candidate, cancellationToken);
            }

            await _db.SaveChangesAsync(cancellationToken);

            var nearbyPitches = await _db.Pitches
                .Where(p => p.Location.IsWithinDistance(userPoint, effectiveRadius))
                .Where(p => !(p.Location.Y == 0 && p.Location.X == 0)
                          && !(p.Location.Y == -1 && p.Location.X == -1))
                .ToListAsync(cancellationToken);

            foreach (var pitch in nearbyPitches)
            {
                pitch.Latitude = pitch.Location.Y;
                pitch.Longitude = pitch.Location.X;
            }

            return nearbyPitches
                .OrderBy(p => p.Location.Distance(userPoint))
                .ThenBy(p => p.Name)
                .ToList();
        }

        private async Task UpsertCandidatePitchAsync(
            PitchCandidatePlaceDto candidate,
            CancellationToken cancellationToken)
        {
            var placeId = candidate.PlaceId.Trim();
            if (string.IsNullOrWhiteSpace(placeId))
                return;

            if (await _db.Pitches.AnyAsync(p => p.ExternalPlaceId == placeId, cancellationToken))
                return;

            var pitchName = candidate.Name.Trim();
            if (string.IsNullOrWhiteSpace(pitchName))
                return;

            var location = _geometryFactory.CreatePoint(new Coordinate(candidate.Longitude, candidate.Latitude));
            if (HasPendingDuplicate(placeId, pitchName, location))
                return;

            if (await _db.Pitches.AnyAsync(p => p.Name == pitchName, cancellationToken))
                return;

            var nearbyDuplicateExists = await _db.Pitches.AnyAsync(
                p => p.Location.IsWithinDistance(location, CandidateDuplicateDistanceMeters),
                cancellationToken);
            if (nearbyDuplicateExists)
                return;

            if (!Enum.TryParse<PitchType>(candidate.RecommendedPitchType, ignoreCase: true, out var pitchType))
            {
                _logger.LogDebug(
                    "Skipping Places candidate {PlaceId}; invalid pitch type {PitchType}.",
                    placeId,
                    candidate.RecommendedPitchType);
                return;
            }

            _db.Pitches.Add(new PitchModel
            {
                Name = pitchName,
                Location = location,
                CreatorUserId = null,
                Type = pitchType,
                Source = "ArcGISPlaces",
                ExternalPlaceId = placeId,
                SourceCategory = candidate.CategoryLabel.Trim(),
                SourceMatchReason = candidate.MatchReason.Trim(),
                SourceConfidence = candidate.Confidence,
                CreatedAt = DateTime.UtcNow
            });
        }

        private bool HasPendingDuplicate(string placeId, string pitchName, Point location)
        {
            var candidatePosition = new GeoPosition
            {
                Latitude = location.Y,
                Longitude = location.X
            };

            return _db.ChangeTracker
                .Entries<PitchModel>()
                .Where(entry => entry.State == EntityState.Added)
                .Select(entry => entry.Entity)
                .Any(pitch =>
                    string.Equals(pitch.ExternalPlaceId, placeId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pitch.Name, pitchName, StringComparison.OrdinalIgnoreCase)
                    || GeoPosition.HaversineDistance(
                        candidatePosition,
                        new GeoPosition
                        {
                            Latitude = pitch.Location.Y,
                            Longitude = pitch.Location.X
                        }) <= CandidateDuplicateDistanceMeters);
        }

        private static bool IsAutoPromotableCandidate(PitchCandidatePlaceDto candidate) =>
            candidate.Confidence >= AutoPromoteConfidenceThreshold
            && !string.IsNullOrWhiteSpace(candidate.PlaceId)
            && !string.IsNullOrWhiteSpace(candidate.Name);
    }
}

