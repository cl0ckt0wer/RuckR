using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Server.Services;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers
{
    /// <summary>API endpoints for creating and managing user battles.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BattlesController : ControllerBase
    {
        private readonly RuckRDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IBattleService _battleService;
        private readonly IBattleRealtimeNotifier _battleRealtimeNotifier;

        /// <summary>Initializes a new instance of <see cref="BattlesController"/>.</summary>
        public BattlesController(
            RuckRDbContext db,
            UserManager<IdentityUser> userManager,
            IBattleService battleService,
            IBattleRealtimeNotifier battleRealtimeNotifier)
        {
            _db = db;
            _userManager = userManager;
            _battleService = battleService;
            _battleRealtimeNotifier = battleRealtimeNotifier;
        }

        /// <summary>Send a battle challenge to another user.</summary>
        [HttpPost("challenge")]
        public async Task<ActionResult<BattleSummaryDto>> Challenge(
            [FromBody] ChallengeRequest request,
            [FromQuery] string? idempotencyKey = null)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            try
            {
                var key = idempotencyKey ?? request.IdempotencyKey;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    var existing = await _db.Battles
                        .FirstOrDefaultAsync(b => b.IdempotencyKey == key && b.ChallengerId == userId);
                    if (existing is not null)
                        return Ok(await _battleService.ToSummaryAsync(existing, userId));
                }

                var summary = await _battleService.CreateChallengeAsync(
                    userId,
                    request.OpponentUsername,
                    key);

                await _battleRealtimeNotifier.NotifyChallengeCreatedAsync(summary.Id);

                return CreatedAtAction(nameof(GetPending), new { id = summary.Id }, summary);
            }
            catch (BattleOperationException ex)
            {
                return MapBattleOperationException(ex);
            }
        }

        /// <summary>Accept a pending battle challenge without resolving it.</summary>
        [HttpPost("{id}/accept")]
        public async Task<ActionResult<BattleSummaryDto>> Accept(int id, [FromBody] AcceptChallengeRequest? request = null)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            try
            {
                var summary = await _battleService.AcceptChallengeAsync(id, userId);
                await _battleRealtimeNotifier.NotifyBattleChangedAsync(summary.Id);
                return Ok(summary);
            }
            catch (BattleOperationException ex)
            {
                return MapBattleOperationException(ex);
            }
        }

        /// <summary>Submit the current user's hidden recruit and rugby play for an accepted battle.</summary>
        [HttpPost("{id}/selection")]
        public async Task<ActionResult<BattleSummaryDto>> SubmitSelection(int id, [FromBody] BattleSelectionRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            try
            {
                var summary = await _battleService.SubmitSelectionAsync(id, userId, request.PlayerId, request.Move);
                await _battleRealtimeNotifier.NotifyBattleChangedAsync(summary.Id, summary.Result);
                return Ok(summary);
            }
            catch (BattleOperationException ex)
            {
                return MapBattleOperationException(ex);
            }
        }

        /// <summary>Decline a pending battle challenge.</summary>
        [HttpPost("{id}/decline")]
        public async Task<ActionResult> Decline(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            try
            {
                await _battleService.DeclineChallengeAsync(id, userId);
                await _battleRealtimeNotifier.NotifyChallengeDeclinedAsync(id);
                return Ok();
            }
            catch (BattleOperationException ex)
            {
                return MapBattleOperationException(ex);
            }
        }

        /// <summary>Get active pending and accepted challenges for the current user.</summary>
        [HttpGet("pending")]
        public async Task<ActionResult<IReadOnlyList<BattleSummaryDto>>> GetPending()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            var expiryCutoff = DateTime.UtcNow - _battleService.ChallengeExpiryDuration;
            var activeBattles = await _db.Battles
                .Where(b => (b.ChallengerId == userId || b.OpponentId == userId)
                    && (b.Status == BattleStatus.Pending || b.Status == BattleStatus.Accepted))
                .ToListAsync();

            var expired = activeBattles.Where(b => b.CreatedAt <= expiryCutoff).ToList();
            foreach (var battle in expired)
            {
                battle.Status = BattleStatus.Expired;
                battle.ResolvedAt = DateTime.UtcNow;
            }

            if (expired.Count > 0)
                await _db.SaveChangesAsync();

            var remaining = activeBattles
                .Where(b => b.CreatedAt > expiryCutoff)
                .OrderByDescending(b => b.CreatedAt)
                .ToList();

            return Ok(await _battleService.ToSummariesAsync(remaining, userId));
        }

        /// <summary>Get completed, declined, and expired battle history for the current user.</summary>
        [HttpGet("history")]
        public async Task<ActionResult<IReadOnlyList<BattleSummaryDto>>> GetHistory()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            var history = await _db.Battles
                .Where(b => (b.ChallengerId == userId || b.OpponentId == userId)
                    && b.Status != BattleStatus.Pending
                    && b.Status != BattleStatus.Accepted)
                .OrderByDescending(b => b.ResolvedAt ?? b.CreatedAt)
                .ToListAsync();

            return Ok(await _battleService.ToSummariesAsync(history, userId));
        }

        private string GetCurrentUserId()
        {
            return _userManager.GetUserId(User) ?? string.Empty;
        }

        private ActionResult MapBattleOperationException(BattleOperationException ex)
        {
            return ex.StatusCode switch
            {
                HttpStatusCode.Unauthorized => Unauthorized(ex.Message),
                HttpStatusCode.NotFound => NotFound(ex.Message),
                HttpStatusCode.Forbidden => Forbid(),
                HttpStatusCode.Gone => StatusCode(410, ex.Message),
                HttpStatusCode.Conflict => Conflict(ex.Message),
                HttpStatusCode.TooManyRequests => StatusCode(429, ex.Message),
                _ => BadRequest(ex.Message)
            };
        }
    }
}
