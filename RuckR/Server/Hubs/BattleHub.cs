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
        private readonly IBattleService _battleService;
        private readonly IPitchDiscoveryService _pitchDiscoveryService;
    /// <summary>Initializes a new instance of <see cref="BattleHub"/>.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="userManager">The identity user manager.</param>
    /// <param name="locationTracker">The location tracker service.</param>
    /// <param name="battleService">The battle service.</param>
    /// <param name="pitchDiscoveryService">The pitch discovery service.</param>
        public BattleHub(
            RuckRDbContext db,
            UserManager<IdentityUser> userManager,
            ILocationTracker locationTracker,
            IBattleService battleService,
            IPitchDiscoveryService pitchDiscoveryService)
        {
            _db = db;
            _userManager = userManager;
            _locationTracker = locationTracker;
            _battleService = battleService;
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
        /// <summary>Update the current user's location and discover nearby pitches.</summary>
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
        /// <param name="idempotencyKey">Optional idempotency key.</param>
        /// <returns>The operation result.</returns>
        public async Task SendChallenge(string opponentUsername, string? idempotencyKey = null)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("User identity not found.");

            try
            {
                var summary = await _battleService.CreateChallengeAsync(userId, opponentUsername, idempotencyKey);

                await Clients.User(summary.OpponentId).SendAsync(
                    "ReceiveChallenge",
                    new ChallengeNotification(summary.ChallengerUsername, summary.Id));
                await Clients.User(summary.OpponentId).SendAsync("BattleUpdated", summary);
                await Clients.Caller.SendAsync("ChallengeSent", summary.Id);
                await Clients.Caller.SendAsync("BattleUpdated", summary);
            }
            catch (BattleOperationException ex)
            {
                throw new HubException(ex.Message);
            }
        }
        /// <summary>Accept a challenge without resolving it.</summary>
        /// <param name="battleId">The battle identifier.</param>
        /// <returns>The operation result.</returns>
        public async Task AcceptChallenge(int battleId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("User identity not found.");

            try
            {
                var summary = await _battleService.AcceptChallengeAsync(battleId, userId);
                await Clients.User(summary.ChallengerId).SendAsync("BattleUpdated", summary);
                await Clients.Caller.SendAsync("BattleUpdated", summary);
            }
            catch (BattleOperationException ex)
            {
                throw new HubException(ex.Message);
            }
        }
        /// <summary>Submit a recruit and hidden RPSLS move for an accepted battle.</summary>
        /// <param name="battleId">The battle identifier.</param>
        /// <param name="playerId">The selected recruit/player-card identifier.</param>
        /// <param name="move">The hidden RPSLS move.</param>
        /// <returns>The operation result.</returns>
        public async Task SubmitBattleSelection(int battleId, int playerId, BattleMove move)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("User identity not found.");

            try
            {
                var summary = await _battleService.SubmitSelectionAsync(battleId, userId, playerId, move);
                var battle = await _db.Battles.AsNoTracking().FirstAsync(b => b.Id == battleId);
                var challengerSummary = await _battleService.ToSummaryAsync(battle, summary.ChallengerId, summary.Result);
                var opponentSummary = await _battleService.ToSummaryAsync(battle, summary.OpponentId, summary.Result);
                await Clients.User(summary.ChallengerId).SendAsync("BattleUpdated", challengerSummary);
                await Clients.User(summary.OpponentId).SendAsync("BattleUpdated", opponentSummary);
                if (summary.Result is not null)
                {
                    await Clients.User(summary.ChallengerId).SendAsync("BattleResolved", summary.Result);
                    await Clients.User(summary.OpponentId).SendAsync("BattleResolved", summary.Result);
                }
            }
            catch (BattleOperationException ex)
            {
                throw new HubException(ex.Message);
            }
        }
        /// <summary>Decline a pending challenge.</summary>
        /// <param name="battleId">The battle identifier.</param>
        /// <returns>The operation result.</returns>
        public async Task DeclineChallenge(int battleId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("User identity not found.");

            try
            {
                var battle = await _db.Battles.AsNoTracking().FirstOrDefaultAsync(b => b.Id == battleId);
                await _battleService.DeclineChallengeAsync(battleId, userId);
                if (battle is not null)
                    await Clients.User(battle.ChallengerId).SendAsync("ChallengeDeclined", battle.Id);
            }
            catch (BattleOperationException ex)
            {
                throw new HubException(ex.Message);
            }
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

