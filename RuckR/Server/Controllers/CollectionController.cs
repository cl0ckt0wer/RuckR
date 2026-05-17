using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Server.Services;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers
{
    /// <summary>API endpoints for player collections and capture actions.</summary>
    [ApiController]
    [Route("api/[controller]")]
    /// <summary>Defines the server-side class CollectionController.</summary>
    [Authorize]
    public class CollectionController : ControllerBase
    {
        private const double CaptureProximityMeters = 100.0;
        private const double MaxCaptureAccuracyMeters = 50.0;

        private readonly RuckRDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILocationTracker _locationTracker;
        private readonly IRateLimitService _rateLimitService;
    /// <summary>Initializes a new instance of <see cref="CollectionController"/>.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="userManager">The identity user manager.</param>
    /// <param name="locationTracker">The location tracker service.</param>
    /// <param name="rateLimitService">The rate limit service.</param>
    public CollectionController(
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

        /// <summary>
        /// GET /collection — returns all players collected by the current user,
        /// with the Player navigation property included.
        /// </summary>
        /// <summary>Get the current user's captured player collection.</summary>
        /// <returns>The operation result.</returns>
        [HttpGet]
        public async Task<ActionResult<List<CollectionModel>>> GetCollections()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            var collections = await _db.Collections
                .Where(c => c.UserId == userId)
                .Include(c => c.Player)
                .AsNoTracking()
                .ToListAsync();

            // Strip SpawnLocation from Player to prevent JSON serialization
            // errors with NetTopologySuite Point NaN Z/M coordinate values.
            foreach (var c in collections)
            {
                if (c.Player != null)
                    c.Player.SpawnLocation = null;
            }

            return Ok(collections);
        }

        /// <summary>
        /// POST /collection/capture — capture a player at a pitch.
        /// Requires server-side GPS validation and proximity check.
        /// Rate-limited to 20 captures per hour per user.
        /// </summary>
        /// <summary>Capture a player at a pitch after proximity and GPS checks.</summary>
        /// <param name="request">The request.</param>
        /// <returns>The operation result.</returns>
        [HttpPost("capture")]
        public async Task<ActionResult<CollectionModel>> CapturePlayer([FromBody] CapturePlayerRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            // 1. Validate player exists
            var player = await _db.Players.FindAsync(request.PlayerId);
            if (player is null)
                return NotFound($"Player with id {request.PlayerId} not found.");

            // 2. Validate pitch exists
            var pitch = await _db.Pitches.FindAsync(request.PitchId);
            if (pitch is null)
                return NotFound($"Pitch with id {request.PitchId} not found.");

            // 3. Server-side GPS validation
            var positionResult = _locationTracker.TryGetPosition(userId, TimeSpan.FromSeconds(60));
            if (positionResult is null)
                return BadRequest("GPS position required. Please enable location services.");

            var userPosition = positionResult.Value.Position;

            // 4. Proximity check: user must be within 100m of the pitch
            var pitchPosition = new GeoPosition
            {
                Latitude = pitch.Location.Y,
                Longitude = pitch.Location.X
            };

            var captureDistanceMeters = GeoPosition.HaversineDistance(userPosition, pitchPosition);
            if (captureDistanceMeters > CaptureProximityMeters)
                return BadRequest("You must be within 100m of the pitch.");

            // 5. Duplicate check: user cannot capture the same player twice
            var existingCollection = await _db.Collections
                .AnyAsync(c => c.UserId == userId && c.PlayerId == request.PlayerId);

            if (existingCollection)
                return Conflict("Player already collected.");

            // 6. Rate limiting
            const int MaxCapturesPerHour = 20;
            if (!await _rateLimitService.IsAllowedAsync(userId, "capture", MaxCapturesPerHour, TimeSpan.FromHours(1)))
                return StatusCode(429, $"Rate limit exceeded. You can capture up to {MaxCapturesPerHour} players per hour.");

            // 7. Create and save collection entry
            var collection = new CollectionModel
            {
                UserId = userId,
                PlayerId = request.PlayerId,
                CapturedAt = DateTime.UtcNow,
                CapturedAtPitchId = request.PitchId,
                IsFavorite = false
            };

            _db.Collections.Add(collection);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                return Conflict("Player already collected.");
            }

            // Detach navigation property to prevent JSON serialization errors
            // (Player.SpawnLocation Point may contain NaN Z/M values).
            collection.Player = null;

            return CreatedAtAction(nameof(GetCollections), new { id = collection.Id }, collection);
        }

        /// <summary>
        /// GET /collection/capture-eligibility/{pitchId} — checks whether the current user
        /// can capture players from a pitch based on recent GPS, accuracy, proximity, and
        /// available uncaptured players.
        /// </summary>
        /// <summary>Get whether capture is currently allowed for the specified pitch.</summary>
        /// <param name="pitchId">The pitchid.</param>
        /// <returns>The operation result.</returns>
        [HttpGet("capture-eligibility/{pitchId:int}")]
        public async Task<ActionResult<CaptureEligibilityDto>> GetCaptureEligibility(int pitchId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            var pitch = await _db.Pitches.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pitchId);
            if (pitch is null)
                return NotFound($"Pitch with id {pitchId} not found.");

            var availablePlayerCount = await CountAvailablePlayersAtPitchAsync(userId, pitch);

            var positionResult = _locationTracker.TryGetPosition(userId, TimeSpan.FromSeconds(60));
            if (positionResult is null)
            {
                return Ok(new CaptureEligibilityDto(
                    CanCapture: false,
                    Reason: "GPS_REQUIRED",
                    DistanceBucket: nameof(DistanceBucket.Beyond),
                    AccuracyMeters: null,
                    AvailablePlayerCount: availablePlayerCount));
            }

            var userPosition = positionResult.Value.Position;
            var pitchPosition = new GeoPosition
            {
                Latitude = pitch.Location.Y,
                Longitude = pitch.Location.X
            };

            var distanceMeters = GeoPosition.HaversineDistance(userPosition, pitchPosition);
            var distanceBucket = GeoPosition.GetDistanceBucket(distanceMeters).ToString();

            if (userPosition.Accuracy.HasValue && userPosition.Accuracy.Value > MaxCaptureAccuracyMeters)
            {
                return Ok(new CaptureEligibilityDto(
                    CanCapture: false,
                    Reason: "GPS_INACCURATE",
                    DistanceBucket: distanceBucket,
                    AccuracyMeters: userPosition.Accuracy,
                    AvailablePlayerCount: availablePlayerCount));
            }

            if (distanceMeters > CaptureProximityMeters)
            {
                return Ok(new CaptureEligibilityDto(
                    CanCapture: false,
                    Reason: "TOO_FAR",
                    DistanceBucket: distanceBucket,
                    AccuracyMeters: userPosition.Accuracy,
                    AvailablePlayerCount: availablePlayerCount));
            }

            if (availablePlayerCount <= 0)
            {
                return Ok(new CaptureEligibilityDto(
                    CanCapture: false,
                    Reason: "NO_PLAYERS",
                    DistanceBucket: distanceBucket,
                    AccuracyMeters: userPosition.Accuracy,
                    AvailablePlayerCount: 0));
            }

            return Ok(new CaptureEligibilityDto(
                CanCapture: true,
                Reason: "ELIGIBLE",
                DistanceBucket: distanceBucket,
                AccuracyMeters: userPosition.Accuracy,
                AvailablePlayerCount: availablePlayerCount));
        }

        /// <summary>
        /// POST /collection/{id}/favorite — toggle the IsFavorite flag on a collection entry.
        /// Only the owning user can toggle their own collection entries.
        /// </summary>
        /// <summary>Toggle the favorite state of a collection entry.</summary>
        /// <param name="id">The id.</param>
        /// <returns>The operation result.</returns>
        [HttpPost("{id}/favorite")]
        public async Task<ActionResult> ToggleFavorite(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            var collection = await _db.Collections.FindAsync(id);
            if (collection is null)
                return NotFound();

            if (collection.UserId != userId)
                return Forbid();

            collection.IsFavorite = !collection.IsFavorite;
            await _db.SaveChangesAsync();

return Ok();
        }

        /// <summary>
        /// Extracts the current user's ID using UserManager.
        /// </summary>
        private string GetCurrentUserId()
        {
            return _userManager.GetUserId(User) ?? string.Empty;
        }

        /// <summary>
        /// Detects unique constraint violations in DbUpdateException by inspecting
        /// the inner exception for SQL Server error code 2601 or 2627.
        /// </summary>
        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            return ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx
                && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
        }

        private async Task<int> CountAvailablePlayersAtPitchAsync(string userId, PitchModel pitch)
        {
            return await _db.Players
                .Where(p => p.SpawnLocation != null)
                .Where(p => p.SpawnLocation!.IsWithinDistance(pitch.Location, CaptureProximityMeters))
                .Where(p => !_db.Collections.Any(c => c.UserId == userId && c.PlayerId == p.Id))
                .CountAsync();
        }
    }
}

