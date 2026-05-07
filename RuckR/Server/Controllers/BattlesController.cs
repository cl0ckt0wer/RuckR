using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class BattlesController : ControllerBase
    {
        private const int MaxChallengesPerHour = 10;
        private const int MaxPendingChallenges = 3;
        private static readonly TimeSpan ChallengeExpiryDuration = TimeSpan.FromHours(24);

        internal static readonly ConcurrentDictionary<string, List<DateTime>> _rateLimitTracker = new();

        internal static void ResetRateLimits() => _rateLimitTracker.Clear();

        private readonly RuckRDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public BattlesController(RuckRDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        /// <summary>
        /// POST /battles/challenge — send a challenge to another user.
        /// Validates: not self, opponent exists, player owned, ≤3 pending, rate limit.
        /// </summary>
        [HttpPost("challenge")]
        public async Task<ActionResult<BattleModel>> Challenge([FromBody] ChallengeRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            // 1. Cannot challenge self
            var opponent = await _userManager.FindByNameAsync(request.OpponentUsername);
            if (opponent is null)
                return NotFound($"User '{request.OpponentUsername}' not found.");

            if (opponent.Id == userId)
                return BadRequest("Cannot challenge yourself.");

            // 2. Selected player exists and is in current user's collection
            var player = await _db.Players.FindAsync(request.SelectedPlayerId);
            if (player is null)
                return NotFound($"Player with id {request.SelectedPlayerId} not found.");

            var playerInCollection = await _db.Collections
                .AnyAsync(c => c.UserId == userId && c.PlayerId == request.SelectedPlayerId);
            if (!playerInCollection)
                return BadRequest("Selected player is not in your collection.");

            // 3. Current user has ≤3 pending challenges (Status=Pending and not expired)
            var now = DateTime.UtcNow;
            var expiryCutoff = now - ChallengeExpiryDuration;

            var pendingCount = await _db.Battles
                .CountAsync(b => b.ChallengerId == userId
                    && b.Status == BattleStatus.Pending
                    && b.CreatedAt > expiryCutoff);

            if (pendingCount >= MaxPendingChallenges)
                return BadRequest($"You already have {MaxPendingChallenges} or more pending challenges. Wait for them to expire or be resolved.");

            // 4. Rate limit: max 10 challenges per hour
            if (!CheckRateLimit(userId))
                return StatusCode(429, $"Rate limit exceeded. You can send up to {MaxChallengesPerHour} challenges per hour.");

            var battle = new BattleModel
            {
                ChallengerId = userId,
                OpponentId = opponent.Id,
                ChallengerPlayerId = request.SelectedPlayerId,
                Status = BattleStatus.Pending,
                CreatedAt = now
            };

            _db.Battles.Add(battle);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPending), new { id = battle.Id }, battle);
        }

        /// <summary>
        /// POST /battles/{id}/accept — accept a pending challenge.
        /// Validates: battle exists, pending, current user is opponent, player owned.
        /// Lazy-expiry: challenges older than 24h are expired on access.
        /// Optimistic concurrency: DbUpdateConcurrencyException → 409 Conflict.
        /// </summary>
        [HttpPost("{id}/accept")]
        public async Task<ActionResult<BattleModel>> Accept(int id, [FromBody] AcceptChallengeRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            var battle = await _db.Battles.FindAsync(id);
            if (battle is null)
                return NotFound($"Battle with id {id} not found.");

            // Only the opponent can accept
            if (battle.OpponentId != userId)
                return Forbid();

            // Must be pending
            if (battle.Status != BattleStatus.Pending)
                return BadRequest("Challenge is no longer pending.");

            // Lazy-expiry: expire challenges older than 24h
            if (IsExpired(battle))
            {
                battle.Status = BattleStatus.Expired;
                battle.ResolvedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return StatusCode(410, "Challenge expired.");
            }

            // Selected player exists and is in current user's collection
            var player = await _db.Players.FindAsync(request.SelectedPlayerId);
            if (player is null)
                return NotFound($"Player with id {request.SelectedPlayerId} not found.");

            var playerInCollection = await _db.Collections
                .AnyAsync(c => c.UserId == userId && c.PlayerId == request.SelectedPlayerId);
            if (!playerInCollection)
                return BadRequest("Selected player is not in your collection.");

            battle.OpponentPlayerId = request.SelectedPlayerId;
            battle.Status = BattleStatus.Accepted;
            battle.ResolvedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("This challenge was already accepted or modified concurrently.");
            }

            return Ok(battle);
        }

        /// <summary>
        /// POST /battles/{id}/decline — decline a pending challenge.
        /// Validates: battle exists, pending, current user is opponent.
        /// Lazy-expiry: challenges older than 24h are expired on access.
        /// </summary>
        [HttpPost("{id}/decline")]
        public async Task<ActionResult> Decline(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            var battle = await _db.Battles.FindAsync(id);
            if (battle is null)
                return NotFound($"Battle with id {id} not found.");

            // Only the opponent can decline
            if (battle.OpponentId != userId)
                return Forbid();

            // Must be pending
            if (battle.Status != BattleStatus.Pending)
                return BadRequest("Challenge is no longer pending.");

            // Lazy-expiry: expire challenges older than 24h
            if (IsExpired(battle))
            {
                battle.Status = BattleStatus.Expired;
                battle.ResolvedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return StatusCode(410, "Challenge expired.");
            }

            battle.Status = BattleStatus.Declined;
            battle.ResolvedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok();
        }

        /// <summary>
        /// GET /battles/pending — pending challenges for the current user (incoming + outgoing).
        /// Lazy-expiry: challenges older than 24h are expired on access and persisted.
        /// </summary>
        [HttpGet("pending")]
        public async Task<ActionResult<List<BattleModel>>> GetPending()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            var now = DateTime.UtcNow;
            var expiryCutoff = now - ChallengeExpiryDuration;

            var pendingBattles = await _db.Battles
                .Where(b => (b.ChallengerId == userId || b.OpponentId == userId)
                    && b.Status == BattleStatus.Pending)
                .ToListAsync();

            // Lazy-expiry: expire challenges older than 24h and persist
            var expired = pendingBattles.Where(b => b.CreatedAt <= expiryCutoff).ToList();
            foreach (var battle in expired)
            {
                battle.Status = BattleStatus.Expired;
                battle.ResolvedAt = now;
            }

            if (expired.Count > 0)
                await _db.SaveChangesAsync();

            // Return only non-expired pending challenges
            var remaining = pendingBattles.Where(b => b.CreatedAt > expiryCutoff).ToList();
            return Ok(remaining);
        }

        /// <summary>
        /// GET /battles/history — completed, declined, and expired battles for the current user.
        /// Sorted by ResolvedAt descending (falls back to CreatedAt if not resolved).
        /// </summary>
        [HttpGet("history")]
        public async Task<ActionResult<List<BattleModel>>> GetHistory()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            var history = await _db.Battles
                .Where(b => (b.ChallengerId == userId || b.OpponentId == userId)
                    && b.Status != BattleStatus.Pending)
                .OrderByDescending(b => b.ResolvedAt ?? b.CreatedAt)
                .ToListAsync();

            return Ok(history);
        }

        /// <summary>
        /// Thread-safe rate limit check using ConcurrentDictionary with lock-based list mutation.
        /// Cleans entries older than 1 hour. Returns true if the user is within the limit.
        /// </summary>
        private bool CheckRateLimit(string userId)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddHours(-1);

            var userTimestamps = _rateLimitTracker.GetOrAdd(userId, _ => new List<DateTime>());

            lock (userTimestamps)
            {
                userTimestamps.RemoveAll(ts => ts < cutoff);

                if (userTimestamps.Count >= MaxChallengesPerHour)
                    return false;

                userTimestamps.Add(now);
                return true;
            }
        }

        /// <summary>
        /// Returns true if the battle's CreatedAt is older than 24 hours.
        /// </summary>
        private static bool IsExpired(BattleModel battle)
        {
            return battle.CreatedAt <= DateTime.UtcNow - ChallengeExpiryDuration;
        }

        /// <summary>
        /// Extracts the current user's ID using UserManager.
        /// </summary>
        private string GetCurrentUserId()
        {
            return _userManager.GetUserId(User) ?? string.Empty;
        }
    }
}
