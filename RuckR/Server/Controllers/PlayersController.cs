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
    public class PlayersController : ControllerBase
    {
        private readonly RuckRDbContext _db;
        private readonly ILogger<PlayersController> _logger;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IRateLimitService _rateLimitService;

        public PlayersController(
            RuckRDbContext db,
            ILogger<PlayersController> logger,
            UserManager<IdentityUser> userManager,
            IRateLimitService rateLimitService)
        {
            _db = db;
            _logger = logger;
            _userManager = userManager;
            _rateLimitService = rateLimitService;
        }

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
        /// Does NOT expose OwnerUsername to prevent location triangulation.
        /// </summary>
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

            var nearbyQuery =
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
                );

            return await nearbyQuery.ToListAsync();
        }
    }
}
