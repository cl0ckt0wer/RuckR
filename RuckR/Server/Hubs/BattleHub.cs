using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RuckR.Server.Data;
using RuckR.Server.Services;
using RuckR.Shared.Models;

namespace RuckR.Server.Hubs
{
    /// <summary>SignalR hub for real-time battle and location updates.</summary>
    /// <summary>Defines the server-side class BattleHub.</summary>
    [Authorize]
    public class BattleHub : Hub
    {
        private const int PitchProximityMeters = 100;

        private readonly RuckRDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILocationTracker _locationTracker;
        private readonly IBattleResolver _battleResolver;
        private readonly IBattleService _battleService;
        private readonly IRateLimitService _rateLimitService;
        private readonly IPitchDiscoveryService _pitchDiscoveryService;
    /// <summary>Initializes a new instance of <see cref="BattleHub"/>.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="userManager">The identity user manager.</param>
    /// <param name="locationTracker">The location tracker service.</param>
    /// <param name="battleResolver">The battle resolver service.</param>
    /// <param name="battleService">The battle service.</param>
    /// <param name="rateLimitService">The rate limit service.</param>
    /// <param name="pitchDiscoveryService">The pitch discovery service.</param>
        public BattleHub(
            RuckRDbContext db,
            UserManager<IdentityUser> userManager,
            ILocationTracker locationTracker,
            IBattleResolver battleResolver,
            IBattleService battleService,
            IRateLimitService rateLimitService,
            IPitchDiscoveryService pitchDiscoveryService)
        {
            _db = db;
            _userManager = userManager;
            _locationTracker = locationTracker;
            _battleResolver = battleResolver;
            _battleService = battleService;
            _rateLimitService = rateLimitService;
            _pitchDiscoveryService = pitchDiscoveryService;
        }
        /// <summary>Track a new SignalR connection.</summary>
        /// <returns>The operation result.</returns>
        public override async Task OnConnectedAsync()
        {
            // Client should immediately send their position via UpdateLocation
            await base.OnConnectedAsync();
        }
        /// <summary>Track a disconnected SignalR connection.</summary>
        /// <param name="exception">The exception.</param>
        /// <returns>The operation result.</returns>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                _locationTracker.RemoveUser(userId);
            }
            await base.OnDisconnectedAsync(exception);
        }
        /// <summary>Update the player's location and discover nearby pitches.</summary>
        /// <param name="latitude">The latitude.</param>
        /// <param name="longitude">The longitude.</param>
        /// <returns>The operation result.</returns>
        public async Task UpdateLocation(double latitude, double longitude)
        {
            var userId = Context.UserIdentifier!;
            var position = new GeoPosition
            {
                Latitude = latitude,
                Longitude = longitude,
                Accuracy = null,
                Timestamp = DateTime.UtcNow
            };
            _locationTracker.UpdatePosition(userId, position);

            var nearestPitches = await _pitchDiscoveryService.EnsureNearbyPitchesAsync(
                userId,
                Context.User?.Identity?.Name ?? string.Empty,
                latitude,
                longitude);

            foreach (var pitch in nearestPitches)
            {
                await Clients.Caller.SendAsync("PitchDiscovered", pitch);
            }

            // Notify for any exact nearby pitch too, so existing 100m discovery behavior remains intact.
            var userPoint = new Point(longitude, latitude) { SRID = 4326 };
            var nearbyPitches = await _db.Pitches
                .Where(p => p.Location.IsWithinDistance(userPoint, PitchProximityMeters))
                .ToListAsync();

            foreach (var pitch in nearbyPitches)
            {
                if (!nearestPitches.Any(p => p.Id == pitch.Id))
                {
                    await Clients.Caller.SendAsync("PitchDiscovered", pitch);
                }
            }
        }
        /// <summary>Send a challenge to an opponent.</summary>
        /// <param name="opponentUsername">The opponent's username.</param>
        /// <param name="playerId">The selected player identifier.</param>
        /// <param name="idempotencyKey">Optional idempotency key.</param>
        /// <returns>The operation result.</returns>
        public async Task SendChallenge(string opponentUsername, int playerId, string? idempotencyKey = null)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("User identity not found.");

            // 1. Cannot challenge self
            var opponent = await _userManager.FindByNameAsync(opponentUsername);
            if (opponent is null)
                throw new HubException($"User '{opponentUsername}' not found.");

            if (opponent.Id == userId)
                throw new HubException("Cannot challenge yourself.");

            // 2. Selected player exists and is in current user's collection
            var player = await _db.Players.FindAsync(playerId);
            if (player is null)
                throw new HubException($"Player with id {playerId} not found.");

            var playerInCollection = await _db.Collections
                .AnyAsync(c => c.UserId == userId && c.PlayerId == playerId);
            if (!playerInCollection)
                throw new HubException("Selected player is not in your collection.");

            // 3. Check pending challenge count (shared limit from IBattleService)
            var expiryCutoff = DateTime.UtcNow - _battleService.ChallengeExpiryDuration;

            // 0. Idempotency check: prevent duplicate challenges from retries
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var existing = await _db.Battles
                    .AnyAsync(b => b.IdempotencyKey == idempotencyKey
                        && b.ChallengerId == userId
                        && b.CreatedAt > expiryCutoff);
                if (existing)
                {
                    // Return the existing battle instead of creating a duplicate
                    var existingBattle = await _db.Battles
                        .FirstAsync(b => b.IdempotencyKey == idempotencyKey
                            && b.ChallengerId == userId
                            && b.CreatedAt > expiryCutoff);
                    await Clients.Caller.SendAsync("ChallengeSent", existingBattle.Id);
                    return;
                }
            }

            var pendingCount = await _db.Battles
                .CountAsync(b => b.ChallengerId == userId
                    && b.Status == BattleStatus.Pending
                    && b.CreatedAt > expiryCutoff);

            if (pendingCount >= _battleService.MaxPendingChallenges)
                throw new HubException("You have too many pending challenges. Wait for them to expire or be resolved.");

            // 4. Rate limit (shared with REST API via IRateLimitService)
            var allowed = await _rateLimitService.IsAllowedAsync(userId, "challenge", _battleService.MaxChallengesPerHour, TimeSpan.FromHours(1));
            if (!allowed)
                throw new HubException($"Rate limit exceeded. You can send up to {_battleService.MaxChallengesPerHour} challenges per hour.");

            var battle = new BattleModel
            {
                ChallengerId = userId,
                OpponentId = opponent.Id,
                ChallengerPlayerId = playerId,
                Status = BattleStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                IdempotencyKey = idempotencyKey
            };

            _db.Battles.Add(battle);
            await _db.SaveChangesAsync();

            // 5. Notify the opponent with a ChallengeNotification DTO
            var challengerUser = await _userManager.FindByIdAsync(userId);
            var challengerUsername = challengerUser?.UserName ?? "Unknown";

            var notification = new ChallengeNotification(
                ChallengerUsername: challengerUsername,
                PlayerName: player.Name,
                PlayerPosition: player.Position.ToString(),
                PlayerRarity: player.Rarity.ToString(),
                ChallengeId: battle.Id);

            await Clients.User(opponent.Id).SendAsync("ReceiveChallenge", notification);

            // 6. Confirm success to the caller
            await Clients.Caller.SendAsync("ChallengeSent", battle.Id);
        }
        /// <summary>Accept a challenge and resolve it with the selected player.</summary>
        /// <param name="battleId">The battle identifier.</param>
        /// <param name="playerId">The selected player identifier.</param>
        /// <returns>The operation result.</returns>
        public async Task AcceptChallenge(int battleId, int playerId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("User identity not found.");

            var battle = await _db.Battles.FindAsync(battleId);
            if (battle is null)
                throw new HubException($"Battle with id {battleId} not found.");

            if (battle.OpponentId != userId)
                throw new HubException("Only the opponent can accept this challenge.");

            if (battle.Status != BattleStatus.Pending)
                throw new HubException("Challenge is no longer pending.");

            // Lazy-expiry check: if challenge is older than 24h, expire it
            if (battle.CreatedAt <= DateTime.UtcNow - _battleService.ChallengeExpiryDuration)
            {
                battle.Status = BattleStatus.Expired;
                battle.ResolvedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                throw new HubException("Challenge has expired.");
            }

            // Validate selected player exists and is owned by the accepting user
            var opponentPlayer = await _db.Players.FindAsync(playerId);
            if (opponentPlayer is null)
                throw new HubException($"Player with id {playerId} not found.");

            var opponentPlayerInCollection = await _db.Collections
                .AnyAsync(c => c.UserId == userId && c.PlayerId == playerId);
            if (!opponentPlayerInCollection)
                throw new HubException("Selected player is not in your collection.");

            // Validate challenger player still exists and is owned by challenger
            var challengerPlayer = await _db.Players.FindAsync(battle.ChallengerPlayerId);
            if (challengerPlayer is null)
                throw new HubException("The challenger's player no longer exists.");

            var challengerPlayerInCollection = await _db.Collections
                .AnyAsync(c => c.UserId == battle.ChallengerId && c.PlayerId == battle.ChallengerPlayerId);
            if (!challengerPlayerInCollection)
                throw new HubException("The challenger no longer owns the selected player.");

            // Get usernames for the BattleResolver and result notifications
            var challengerUser = await _userManager.FindByIdAsync(battle.ChallengerId);
            var opponentUser = await _userManager.FindByIdAsync(userId);
            var challengerUsername = challengerUser?.UserName ?? "Unknown";
            var opponentUsername = opponentUser?.UserName ?? "Unknown";

            // Resolve the battle via shared IBattleService
            var result = await _battleService.ResolveBattlePureAsync(
                challengerPlayer,
                opponentPlayer,
                challengerUsername,
                opponentUsername);

            // Update battle record
            battle.OpponentPlayerId = playerId;
            battle.Status = BattleStatus.Completed;
            battle.WinnerId = result.WinnerUsername;
            battle.ResolvedAt = DateTime.UtcNow;

            // Atomic DB update — handle race conditions via concurrency token
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Someone else already accepted or modified this battle
                await Clients.Caller.SendAsync("Error", "Battle was already accepted or modified by another request.");
                return;
            }

            // Notify both users of the result
            await Clients.User(battle.ChallengerId).SendAsync("BattleResolved", result);
            await Clients.Caller.SendAsync("BattleResolved", result);
        }
        /// <summary>Decline a pending challenge.</summary>
        /// <param name="battleId">The battle identifier.</param>
        /// <returns>The operation result.</returns>
        public async Task DeclineChallenge(int battleId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("User identity not found.");

            var battle = await _db.Battles.FindAsync(battleId);
            if (battle is null)
                throw new HubException($"Battle with id {battleId} not found.");

            if (battle.OpponentId != userId)
                throw new HubException("Only the opponent can decline this challenge.");

            if (battle.Status != BattleStatus.Pending)
                throw new HubException("Challenge is no longer pending.");

            // Lazy-expiry check before declining
            if (battle.CreatedAt <= DateTime.UtcNow - _battleService.ChallengeExpiryDuration)
            {
                battle.Status = BattleStatus.Expired;
                battle.ResolvedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                throw new HubException("Challenge has expired.");
            }

            battle.Status = BattleStatus.Declined;
            battle.ResolvedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Notify the challenger
            await Clients.User(battle.ChallengerId).SendAsync("ChallengeDeclined", battle.Id);
        }
        /// <summary>Join the live group for a battle.</summary>
        /// <param name="battleId">The battle identifier.</param>
        /// <returns>The operation result.</returns>
        public async Task JoinBattleGroup(int battleId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetBattleGroupName(battleId));
        }
        /// <summary>Leave the live group for a battle.</summary>
        /// <param name="battleId">The battle identifier.</param>
        /// <returns>The operation result.</returns>
        public async Task LeaveBattleGroup(int battleId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetBattleGroupName(battleId));
        }
        /// <summary>Ping the hub and return the current Unix timestamp in milliseconds.</summary>
        /// <returns>The operation result.</returns>
        public Task<long> Ping()
        {
            return Task.FromResult(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        private string GetCurrentUserId()
        {
            return _userManager.GetUserId(Context.User!) ?? string.Empty;
        }

        private static string GetBattleGroupName(int battleId)
        {
            return $"battle_{battleId}";
        }
    }
}

