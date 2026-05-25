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
    /// <summary>API endpoints for player lookup and nearby player queries.</summary>
    [ApiController]
    [Route("api/[controller]")]
    /// <summary>Defines the server-side class PlayersController.</summary>
    [Authorize]
    public class PlayersController : ControllerBase
    {
        private static readonly TimeSpan NearbyOwnerPositionMaxAge = TimeSpan.FromSeconds(60);

        private readonly RuckRDbContext _db;
        private readonly ILogger<PlayersController> _logger;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IRateLimitService _rateLimitService;
        private readonly ILocationTracker _locationTracker;
    /// <summary>Initializes a new instance of <see cref="PlayersController"/>.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="userManager">The identity user manager.</param>
    /// <param name="rateLimitService">The rate limit service.</param>
    /// <param name="locationTracker">The live user location tracker.</param>
    public PlayersController(
            RuckRDbContext db,
            ILogger<PlayersController> logger,
            UserManager<IdentityUser> userManager,
            IRateLimitService rateLimitService,
            ILocationTracker locationTracker)
        {
            _db = db;
            _logger = logger;
            _userManager = userManager;
            _rateLimitService = rateLimitService;
            _locationTracker = locationTracker;
        }

        /// <summary>Get players with optional filtering by position, rarity, or name.</summary>
        /// <param name="position">The position.</param>
        /// <param name="rarity">The rarity.</param>
        /// <param name="name">The name.</param>
        /// <returns>The operation result.</returns>
        [HttpGet]
        public async Task<ActionResult<List<PlayerModel>>> GetPlayers(
            [FromQuery] string? position = null,
            [FromQuery] string? rarity = null,
            [FromQuery] string? name = null)
        {
            IQueryable<PlayerModel> query = _db.Players;

            if (!string.IsNullOrWhiteSpace(position)
                && Enum.TryParse<PlayerPosition>(position, ignoreCase: true, out var playerPosition))
            {
                query = query.Where(p => p.Position == playerPosition);
            }

            if (!string.IsNullOrWhiteSpace(rarity)
                && Enum.TryParse<PlayerRarity>(rarity, ignoreCase: true, out var playerRarity))
            {
                query = query.Where(p => p.Rarity == playerRarity);
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(p => p.Name.Contains(name));
            }

            return await query.ToListAsync();
        }

        /// <summary>Get a player by identifier.</summary>
        /// <param name="id">The id.</param>
        /// <returns>The operation result.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<PlayerModel>> GetPlayer(int id)
        {
            var player = await _db.Players.FindAsync(id);

            if (player is null)
            {
                return NotFound();
            }

            return player;
        }

        /// <summary>
        /// GET /api/players/nearby — returns players within radius, using DistanceBucket instead of exact distance.
        /// Wild players are found by spawn point; owned players are found by recent owner GPS position.
        /// </summary>
        /// <summary>Get nearby players by GPS coordinate and radius.</summary>
        /// <param name="lat">The lat.</param>
        /// <param name="lng">The lng.</param>
        /// <param name="radius">The radius.</param>
        /// <returns>The operation result.</returns>
        [HttpGet("nearby")]
        public async Task<ActionResult<List<NearbyPlayerDto>>> GetNearbyPlayers(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] double radius = 10000)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            var allowed = await _rateLimitService.IsAllowedAsync(userId, "players_nearby", 60, TimeSpan.FromMinutes(1));
            if (!allowed)
                return StatusCode(429, "Rate limit exceeded for nearby player scans.");

            // Validate lat/lng ranges
            if (lat < -90 || lat > 90)
            {
                return BadRequest("Latitude must be between -90 and 90 degrees.");
            }

            if (lng < -180 || lng > 180)
            {
                return BadRequest("Longitude must be between -180 and 180 degrees.");
            }

            // Cap radius at 50000m
            var effectiveRadius = Math.Min(radius, 50000);

            var point = new Point(lng, lat) { SRID = 4326 };

            var spawnNearbyPlayers = await (
                from p in _db.Players
                where p.SpawnLocation != null && p.SpawnLocation!.Distance(point) <= effectiveRadius
                orderby p.SpawnLocation!.Distance(point)
                join c in _db.Collections on p.Id equals c.PlayerId into pcj
                from pc in pcj.DefaultIfEmpty()
                join u in _db.Users on (pc != null ? pc.UserId : null) equals u.Id into uj
                from u in uj.DefaultIfEmpty()
                select new NearbyPlayerDto(
                    p.Id,
                    p.Name,
                    p.Position.ToString(),
                    p.Rarity.ToString(),
                    GeoPosition.GetDistanceBucket(p.SpawnLocation!.Distance(point)),
                    u != null ? u.UserName! : null!
                )).ToListAsync();

            var liveOwnedPlayers = await GetLiveOwnedPlayersNearAsync(lat, lng, effectiveRadius);

            var keyedResults = new Dictionary<(int PlayerId, string OwnerUsername), NearbyPlayerDto>();
            foreach (var player in spawnNearbyPlayers)
            {
                keyedResults[(player.PlayerId, player.OwnerUsername ?? string.Empty)] = player;
            }

            foreach (var player in liveOwnedPlayers)
            {
                keyedResults[(player.PlayerId, player.OwnerUsername ?? string.Empty)] = player;
            }

            return keyedResults.Values
                .OrderBy(p => (int)p.DistanceBucket)
                .ThenBy(p => p.Name)
                .ThenBy(p => p.OwnerUsername)
                .ToList();
        }

        private async Task<IReadOnlyList<NearbyPlayerDto>> GetLiveOwnedPlayersNearAsync(
            double lat,
            double lng,
            double radiusMeters)
        {
            var origin = new GeoPosition { Latitude = lat, Longitude = lng };
            var nearbyOwnerDistances = _locationTracker
                .GetRecentPositions(NearbyOwnerPositionMaxAge)
                .Select(entry => new
                {
                    UserId = entry.Key,
                    DistanceMeters = GeoPosition.HaversineDistance(origin, entry.Value.Position)
                })
                .Where(entry => entry.DistanceMeters <= radiusMeters)
                .ToDictionary(entry => entry.UserId, entry => entry.DistanceMeters);

            if (nearbyOwnerDistances.Count == 0)
            {
                return Array.Empty<NearbyPlayerDto>();
            }

            var nearbyUserIds = nearbyOwnerDistances.Keys.ToList();
            var usernames = await _db.Users
                .Where(u => nearbyUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName })
                .ToDictionaryAsync(u => u.Id, u => u.UserName ?? string.Empty);

            var ownedPlayers = await _db.Collections
                .Where(c => nearbyUserIds.Contains(c.UserId))
                .Join(_db.Players,
                    collection => collection.PlayerId,
                    player => player.Id,
                    (collection, player) => new
                    {
                        collection.UserId,
                        Player = player
                    })
                .ToListAsync();

            return ownedPlayers
                .Where(entry => usernames.ContainsKey(entry.UserId))
                .Select(entry => new NearbyPlayerDto(
                    entry.Player.Id,
                    entry.Player.Name,
                    entry.Player.Position.ToString(),
                    entry.Player.Rarity.ToString(),
                    GeoPosition.GetDistanceBucket(nearbyOwnerDistances[entry.UserId]),
                    usernames[entry.UserId]))
                .ToList();
        }
    }
}

