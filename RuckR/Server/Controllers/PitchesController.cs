using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RuckR.Server.Data;
using RuckR.Server.Services;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PitchesController : ControllerBase
    {
        private const int MaxPitchesPerUserPerDay = 5;

        private static readonly GeometryFactory _geometryFactory =
            NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        private readonly RuckRDbContext _db;
        private readonly IRateLimitService _rateLimitService;
        private readonly IRealWorldParkService _parkService;

        public PitchesController(
            RuckRDbContext db,
            IRateLimitService rateLimitService,
            IRealWorldParkService parkService)
        {
            _db = db;
            _rateLimitService = rateLimitService;
            _parkService = parkService;
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
                 .Where(p => !(p.Location.Y == 0 && p.Location.X == 0)
                           && !(p.Location.Y == -1 && p.Location.X == -1))
                 .OrderBy(p => p.CreatedAt)
                 .Skip((page - 1) * pageSize)
                 .Take(pageSize)
                 .ToListAsync();

             foreach (var p in pitches)
             {
                 p.Latitude = p.Location.Y;
                 p.Longitude = p.Location.X;
             }

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
                 .Where(p => !(p.Location.Y == 0 && p.Location.X == 0)
                           && !(p.Location.Y == -1 && p.Location.X == -1))
                 .ToListAsync();

             foreach (var p in pitches)
             {
                 p.Latitude = p.Location.Y;
                 p.Longitude = p.Location.X;
             }

            return Ok(pitches);
        }

        /// <summary>
        /// GET /pitches/place-candidates — ArcGIS Places that look suitable for pitch creation.
        /// </summary>
        [HttpGet("place-candidates")]
        public async Task<ActionResult<IReadOnlyList<PitchCandidatePlaceDto>>> GetPitchCandidatePlaces(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] double radius = 5000)
        {
            if (lat < -90 || lat > 90)
                return BadRequest("Latitude must be between -90 and 90 degrees.");
            if (lng < -180 || lng > 180)
                return BadRequest("Longitude must be between -180 and 180 degrees.");
            if (radius <= 0 || radius > 10_000)
                return BadRequest("Radius must be between 1 and 10000 meters.");

            var candidates = await _parkService.FindNearbyPitchCandidatePlacesAsync(lat, lng, radius, HttpContext.RequestAborted);
            return Ok(candidates);
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
        /// Pitch names must be globally unique.
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
            if (IsNullIsland(request.Latitude, request.Longitude))
                return BadRequest("Latitude and longitude cannot both be zero. Provide a real location.");
            if (IsSentinelLocation(request.Latitude, request.Longitude))
                return BadRequest("Location appears to be a placeholder (-1,-1). Provide a real location.");

            // Validate PitchType
            if (!Enum.TryParse<PitchType>(request.Type, ignoreCase: true, out var pitchType))
                return BadRequest($"Invalid pitch type '{request.Type}'. Valid types: Standard, Training, Stadium.");

            // Get current user (requires auth)
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            // --- Rate limiting ---
            if (!await _rateLimitService.IsAllowedAsync(userId, "create_pitch", MaxPitchesPerUserPerDay, TimeSpan.FromHours(24)))
                return StatusCode(429, "Rate limit exceeded. You can create up to 5 pitches per 24 hours.");

            var requestPoint = _geometryFactory.CreatePoint(new Coordinate(request.Longitude, request.Latitude));

            // --- Global name uniqueness check ---
            var duplicateExists = await _db.Pitches.AnyAsync(p => p.Name == request.Name);

            if (duplicateExists)
                return Conflict($"A pitch named '{request.Name}' already exists.");

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
        /// Extracts the current user's ID from the authenticated principal.
        /// </summary>
        private string GetCurrentUserId()
        {
            return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }

        private static bool IsNullIsland(double lat, double lng) =>
            Math.Abs(lat) < 0.05 && Math.Abs(lng) < 0.05;

        private static bool IsSentinelLocation(double lat, double lng) =>
            Math.Abs(lat - (-1)) < 0.01 && Math.Abs(lng - (-1)) < 0.01;
    }
}
