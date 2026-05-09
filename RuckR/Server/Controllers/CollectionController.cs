using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    [Authorize]
    public class CollectionController : ControllerBase
    {
        private const int MaxCapturesPerHour = 20;
        private const double CaptureProximityMeters = 100.0;

        internal static readonly ConcurrentDictionary<string, List<DateTime>> _rateLimitTracker = new();

        internal static void ResetRateLimits() => _rateLimitTracker.Clear();

        private readonly RuckRDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILocationTracker _locationTracker;

        public CollectionController(
            RuckRDbContext db,
            UserManager<IdentityUser> userManager,
            ILocationTracker locationTracker)
        {
            _db = db;
            _userManager = userManager;
            _locationTracker = locationTracker;
        }

        /// <summary>
        /// GET /collection — returns all players collected by the current user,
        /// with the Player navigation property included.
        /// </summary>
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
            var userPoint = new Point(userPosition.Longitude, userPosition.Latitude) { SRID = 4326 };

            if (!pitch.Location.IsWithinDistance(userPoint, CaptureProximityMeters))
                return BadRequest("You must be within 100m of the pitch.");

            // 5. Duplicate check: user cannot capture the same player twice
            var existingCollection = await _db.Collections
                .AnyAsync(c => c.UserId == userId && c.PlayerId == request.PlayerId);

            if (existingCollection)
                return Conflict("Player already collected.");

            // 6. Rate limiting
            if (!CheckRateLimit(userId))
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
        /// POST /collection/{id}/favorite — toggle the IsFavorite flag on a collection entry.
        /// Only the owning user can toggle their own collection entries.
        /// </summary>
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
        /// Checks if the user has exceeded their hourly capture rate limit.
        /// Thread-safe via ConcurrentDictionary with lock-based list mutation.
        /// </summary>
        private bool CheckRateLimit(string userId)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddHours(-1);

            var userTimestamps = _rateLimitTracker.GetOrAdd(userId, _ => new List<DateTime>());

            lock (userTimestamps)
            {
                // Remove entries older than 1 hour
                userTimestamps.RemoveAll(ts => ts < cutoff);

                if (userTimestamps.Count >= MaxCapturesPerHour)
                    return false;

                userTimestamps.Add(now);
                return true;
            }
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
    }
}
