using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side class PitchDiscoveryService.</summary>
    public class PitchDiscoveryService : IPitchDiscoveryService
    {
        private const double MetersPerMile = 1609.344;
        private const double StadiumRadiusMeters = 30 * MetersPerMile;
        private const double StandardRadiusMeters = 10 * MetersPerMile;
        private const double TrainingRadiusMeters = 2 * MetersPerMile;

        private readonly RuckRDbContext _db;
        private readonly GeometryFactory _geometryFactory =
            NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        /// <summary>Initializes a new instance of PitchDiscoveryService.</summary>
        /// <param name="db">The db.</param>
        public PitchDiscoveryService(RuckRDbContext db)
        {
            _db = db;
        }
        /// <summary>E ns ur eN ea rb yP it ch es As yn c.</summary>
        /// <param name="userId">The userid.</param>
        /// <param name="userName">The username.</param>
        /// <param name="latitude">The latitude.</param>
        /// <param name="longitude">The longitude.</param>
        /// <returns>The operation result.</returns>
        public async Task<IReadOnlyList<PitchModel>> EnsureNearbyPitchesAsync(
            string userId,
            string userName,
            double latitude,
            double longitude)
        {
            var userPoint = _geometryFactory.CreatePoint(new Coordinate(longitude, latitude));
            var nearbyPitches = await _db.Pitches
                .Where(p => p.Location.IsWithinDistance(userPoint, StadiumRadiusMeters))
                .ToListAsync();

            PitchModel? createdPitch = null;

            if (!nearbyPitches.Any(p => p.Type == PitchType.Stadium))
            {
                createdPitch = CreatePitch(userId, userName, PitchType.Stadium, userPoint);
            }
            else if (!await HasAnyPitchTypeWithinDistanceAsync(
                userPoint,
                StandardRadiusMeters,
                PitchType.Stadium,
                PitchType.Standard))
            {
                createdPitch = CreatePitch(userId, userName, PitchType.Standard, userPoint);
            }
            else if (!await HasAnyPitchTypeWithinDistanceAsync(
                userPoint,
                TrainingRadiusMeters,
                PitchType.Stadium,
                PitchType.Standard,
                PitchType.Training))
            {
                createdPitch = CreatePitch(userId, userName, PitchType.Training, userPoint);
            }

            if (createdPitch is not null)
            {
                _db.Pitches.Add(createdPitch);
                await _db.SaveChangesAsync();
                nearbyPitches.Add(createdPitch);
            }

            return nearbyPitches
                .GroupBy(p => p.Type)
                .Select(group => group.OrderBy(p => p.Location.Distance(userPoint)).First())
                .OrderBy(p => p.Type)
                .ToList();
        }

        private static PitchModel CreatePitch(
            string userId,
            string userName,
            PitchType type,
            Point location)
        {
            var ownerName = string.IsNullOrWhiteSpace(userName) ? "Player" : userName;

            return new PitchModel
            {
                Name = $"{ownerName}'s {FormatPitchType(type)}",
                Location = location,
                CreatorUserId = userId,
                Type = type,
                CreatedAt = DateTime.UtcNow
            };
        }

        private static string FormatPitchType(PitchType type) => type switch
        {
            PitchType.Training => "Practice Pitch",
            PitchType.Standard => "Standard Pitch",
            PitchType.Stadium => "Stadium",
            _ => "Pitch"
        };

        private Task<bool> HasAnyPitchTypeWithinDistanceAsync(
            Point userPoint,
            double distanceMeters,
            params PitchType[] pitchTypes)
        {
            return _db.Pitches.AnyAsync(p =>
                pitchTypes.Contains(p.Type)
                && p.Location.IsWithinDistance(userPoint, distanceMeters));
        }
    }
}

