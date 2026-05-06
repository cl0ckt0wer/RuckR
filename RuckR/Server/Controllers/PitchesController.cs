using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PitchesController : ControllerBase
    {
        private const int MaxPitchesPerUserPerDay = 5;
        private const double NameUniquenessRadiusMeters = 100.0;

        private static readonly ConcurrentDictionary<string, List<DateTime>> _rateLimitTracker = new();
        private static readonly GeometryFactory _geometryFactory =
            NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        private readonly RuckRDbContext _db;

        public PitchesController(RuckRDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// GET /pitches — returns a paginated list of all pitches.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<PitchModel>>> GetPitches(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var pitches = await _db.Pitches
                .OrderBy(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(pitches);
        }

        /// <summary>
        /// GET /pitches/nearby — proximity search for pitches within a radius.
        /// </summary>
        [HttpGet("nearby")]
        public async Task<ActionResult<List<PitchModel>>> GetNearbyPitches(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] double radius = 5000)
        {
            if (lat < -90 || lat > 90)
                return BadRequest("Latitude must be between -90 and 90 degrees.");
            if (lng < -180 || lng > 180)
                return BadRequest("Longitude must be between -180 and 180 degrees.");
            if (radius <= 0 || radius > 50_000)
                return BadRequest("Radius must be between 1 and 50000 meters.");

            var searchPoint = _geometryFactory.CreatePoint(new Coordinate(lng, lat));

            var pitches = await _db.Pitches
                .Where(p => p.Location.IsWithinDistance(searchPoint, radius))
                .ToListAsync();

            return Ok(pitches);
        }

        /// <summary>
        /// GET /pitches/{id} — returns a single pitch by id.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<PitchModel>> GetPitch(int id)
        {
            var pitch = await _db.Pitches.FindAsync(id);

            if (pitch is null)
                return NotFound();

            return Ok(pitch);
        }

        /// <summary>
        /// POST /pitches — creates a new pitch. Requires authentication.
        /// Rate-limited to 5 pitches per user per day.
        /// Name uniqueness enforced within 100m radius.
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<PitchModel>> CreatePitch([FromBody] CreatePitchRequest request)
        {
            // Validate lat/lng ranges
            if (request.Latitude < -90 || request.Latitude > 90)
                return BadRequest("Latitude must be between -90 and 90 degrees.");
            if (request.Longitude < -180 || request.Longitude > 180)
                return BadRequest("Longitude must be between -180 and 180 degrees.");

            // Validate PitchType
            if (!Enum.TryParse<PitchType>(request.Type, ignoreCase: true, out var pitchType))
                return BadRequest($"Invalid pitch type '{request.Type}'. Valid types: Standard, Training, Stadium.");

            // Get current user (requires auth)
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            // --- Rate limiting ---
            if (!CheckRateLimit(userId))
                return StatusCode(429, "Rate limit exceeded. You can create up to 5 pitches per day.");

            // --- Name uniqueness check within 100m ---
            var requestPoint = _geometryFactory.CreatePoint(new Coordinate(request.Longitude, request.Latitude));

            var duplicateExists = await _db.Pitches
                .Where(p => p.Name == request.Name)
                .AnyAsync(p => p.Location.IsWithinDistance(requestPoint, NameUniquenessRadiusMeters));

            if (duplicateExists)
                return Conflict($"A pitch named '{request.Name}' already exists within {NameUniquenessRadiusMeters}m of this location.");

            // --- Create and save ---
            var pitch = new PitchModel
            {
                Name = request.Name,
                Location = requestPoint,
                CreatorUserId = userId,
                Type = pitchType,
                CreatedAt = DateTime.UtcNow
            };

            _db.Pitches.Add(pitch);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPitch), new { id = pitch.Id }, pitch);
        }

        /// <summary>
        /// Checks if the user has exceeded their daily rate limit.
        /// Thread-safe via ConcurrentDictionary.
        /// </summary>
        private bool CheckRateLimit(string userId)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddHours(-24);

            var userTimestamps = _rateLimitTracker.GetOrAdd(userId, _ => new List<DateTime>());

            lock (userTimestamps)
            {
                // Remove entries older than 24 hours
                userTimestamps.RemoveAll(ts => ts < cutoff);

                if (userTimestamps.Count >= MaxPitchesPerUserPerDay)
                    return false;

                userTimestamps.Add(now);
                return true;
            }
        }

        /// <summary>
        /// Extracts the current user's ID from the authenticated principal.
        /// </summary>
        private string GetCurrentUserId()
        {
            return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }
    }
}
