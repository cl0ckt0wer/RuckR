using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Server.Services;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers
{
    /// <summary>API endpoints for user discovery and battle targeting.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private static readonly TimeSpan NearbyUserPositionMaxAge = TimeSpan.FromSeconds(60);
        private const double PitchInteractionMeters = 5000.0;
        private const double MaxPitchInteractionAccuracyMeters = 100.0;

        private readonly RuckRDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILocationTracker _locationTracker;
        private readonly IRateLimitService _rateLimitService;

        /// <summary>Initializes a new instance of <see cref="UsersController"/>.</summary>
        public UsersController(
            RuckRDbContext db,
            UserManager<IdentityUser> userManager,
            ILocationTracker locationTracker,
            IRateLimitService rateLimitService)
        {
            _db = db;
            _userManager = userManager;
            _locationTracker = locationTracker;
            _rateLimitService = rateLimitService;
        }

        /// <summary>Return nearby challengeable users from recent live GPS state.</summary>
        [HttpGet("nearby")]
        public async Task<ActionResult<List<NearbyUserDto>>> GetNearbyUsers(
            [FromQuery] double? lat,
            [FromQuery] double? lng,
            [FromQuery] double radius = 10000,
            [FromQuery] int? pitchId = null,
            [FromQuery] double? accuracy = null)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            var allowed = await _rateLimitService.IsAllowedAsync(userId, "users_nearby", 60, TimeSpan.FromMinutes(1));
            if (!allowed)
                return StatusCode(429, "Rate limit exceeded for nearby user scans.");

            if (lat is < -90 or > 90)
                return BadRequest("Latitude must be between -90 and 90 degrees.");
            if (lng is < -180 or > 180)
                return BadRequest("Longitude must be between -180 and 180 degrees.");
            if (lat.HasValue != lng.HasValue)
                return BadRequest("Latitude and longitude must be provided together.");

            var effectiveRadius = Math.Min(Math.Max(radius, 0), 50000);
            var origin = ResolveOrigin(userId, lat, lng, accuracy);
            if (origin is null)
                return BadRequest("GPS_REQUIRED");

            var pitchOrigin = origin;
            if (pitchId.HasValue)
            {
                var pitch = await _db.Pitches.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pitchId.Value);
                if (pitch is null)
                    return NotFound($"Pitch with id {pitchId.Value} not found.");

                if (origin.Accuracy.HasValue && origin.Accuracy.Value > MaxPitchInteractionAccuracyMeters)
                    return BadRequest("GPS_INACCURATE");

                var pitchPosition = new GeoPosition
                {
                    Latitude = pitch.Location.Y,
                    Longitude = pitch.Location.X
                };
                if (GeoPosition.HaversineDistance(origin, pitchPosition) > PitchInteractionMeters)
                    return BadRequest("TOO_FAR");

                pitchOrigin = pitchPosition;
                effectiveRadius = Math.Min(effectiveRadius, PitchInteractionMeters);
            }

            var now = DateTime.UtcNow;
            var nearbyPositions = _locationTracker
                .GetRecentPositions(NearbyUserPositionMaxAge)
                .Where(entry => entry.Key != userId)
                .Select(entry => new
                {
                    UserId = entry.Key,
                    DistanceMeters = GeoPosition.HaversineDistance(origin, entry.Value.Position),
                    PitchDistanceMeters = GeoPosition.HaversineDistance(pitchOrigin, entry.Value.Position),
                    LastSeenSecondsAgo = Math.Max(0, (int)(now - entry.Value.Timestamp).TotalSeconds)
                })
                .Where(entry => (pitchId.HasValue ? entry.PitchDistanceMeters : entry.DistanceMeters) <= effectiveRadius)
                .ToList();

            if (nearbyPositions.Count == 0)
                return new List<NearbyUserDto>();

            var nearbyUserIds = nearbyPositions.Select(entry => entry.UserId).ToList();
            var users = await _db.Users
                .Where(user => nearbyUserIds.Contains(user.Id))
                .Select(user => new { user.Id, user.UserName })
                .ToDictionaryAsync(user => user.Id, user => user.UserName ?? user.Id);

            var recruitCounts = await _db.Collections
                .Where(collection => nearbyUserIds.Contains(collection.UserId))
                .GroupBy(collection => collection.UserId)
                .Select(group => new { UserId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(group => group.UserId, group => group.Count);

            return nearbyPositions
                .Where(entry => users.ContainsKey(entry.UserId)
                    && recruitCounts.TryGetValue(entry.UserId, out var count)
                    && count > 0)
                .OrderBy(entry => entry.DistanceMeters)
                .Select(entry => new NearbyUserDto(
                    entry.UserId,
                    users[entry.UserId],
                    GeoPosition.GetDistanceBucket(entry.DistanceMeters),
                    entry.LastSeenSecondsAgo,
                    recruitCounts[entry.UserId]))
                .ToList();
        }

        private GeoPosition? ResolveOrigin(
            string userId,
            double? lat,
            double? lng,
            double? accuracy)
        {
            if (lat.HasValue && lng.HasValue)
            {
                var origin = new GeoPosition
                {
                    Latitude = lat.Value,
                    Longitude = lng.Value,
                    Accuracy = accuracy,
                    Timestamp = DateTime.UtcNow
                };
                _locationTracker.UpdatePosition(userId, origin);
                return origin;
            }

            return _locationTracker.TryGetPosition(userId, NearbyUserPositionMaxAge)?.Position;
        }
    }
}
