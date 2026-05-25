using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Services;

/// <summary>Business service for challenge limits, battle resolution, and battle summaries.</summary>
public class BattleService : IBattleService
{
    /// <summary>Maximum number of challenges a user can send per rolling hour.</summary>
    public int MaxChallengesPerHour => 10;

    /// <summary>Maximum number of pending outgoing challenges allowed per user.</summary>
    public int MaxPendingChallenges => 3;

    /// <summary>Challenge lifetime before it expires.</summary>
    public TimeSpan ChallengeExpiryDuration => TimeSpan.FromHours(24);

    private readonly RuckRDbContext _db;
    private readonly IRateLimitService _rateLimitService;
    private readonly IBattleResolver _battleResolver;
    private readonly UserManager<IdentityUser> _userManager;

    /// <summary>Initializes a new instance of <see cref="BattleService"/>.</summary>
    public BattleService(
        RuckRDbContext db,
        IRateLimitService rateLimitService,
        IBattleResolver battleResolver,
        UserManager<IdentityUser> userManager)
    {
        _db = db;
        _rateLimitService = rateLimitService;
        _battleResolver = battleResolver;
        _userManager = userManager;
    }

    /// <summary>Returns whether the user can create another pending challenge.</summary>
    public bool CanChallenge(string userId, List<BattleModel> pendingBattles)
    {
        var expiryCutoff = DateTime.UtcNow - ChallengeExpiryDuration;
        var activePending = pendingBattles.Count(b =>
            b.ChallengerId == userId &&
            b.Status == BattleStatus.Pending &&
            b.CreatedAt > expiryCutoff);

        return activePending < MaxPendingChallenges;
    }

    /// <summary>Resolves a battle asynchronously.</summary>
    public Task<BattleResult> ResolveBattleAsync(
        PlayerModel challengerPlayer,
        PlayerModel opponentPlayer,
        string challengerUsername,
        string opponentUsername)
    {
        return ResolveBattlePureAsync(challengerPlayer, opponentPlayer, challengerUsername, opponentUsername);
    }

    /// <summary>Resolves a battle using pure resolver logic.</summary>
    public Task<BattleResult> ResolveBattlePureAsync(
        PlayerModel challengerPlayer,
        PlayerModel opponentPlayer,
        string challengerUsername,
        string opponentUsername)
    {
        return Task.FromResult(_battleResolver.Resolve(challengerPlayer, opponentPlayer, challengerUsername, opponentUsername));
    }

    /// <summary>Accepts a pending challenge and resolves it immediately.</summary>
    public async Task<BattleSummaryDto> AcceptAndResolveChallengeAsync(int battleId, string opponentUserId, int selectedPlayerId)
    {
        var battle = await _db.Battles.FirstOrDefaultAsync(b => b.Id == battleId);
        if (battle is null)
            throw new BattleOperationException(HttpStatusCode.NotFound, $"Battle with id {battleId} not found.");

        if (battle.OpponentId != opponentUserId)
            throw new BattleOperationException(HttpStatusCode.Forbidden, "Only the opponent can accept this challenge.");

        if (battle.Status != BattleStatus.Pending)
            throw new BattleOperationException(HttpStatusCode.BadRequest, "Challenge is no longer pending.");

        if (battle.CreatedAt <= DateTime.UtcNow - ChallengeExpiryDuration)
        {
            battle.Status = BattleStatus.Expired;
            battle.ResolvedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            throw new BattleOperationException(HttpStatusCode.Gone, "Challenge expired.");
        }

        var opponentPlayer = await _db.Players.FindAsync(selectedPlayerId);
        if (opponentPlayer is null)
            throw new BattleOperationException(HttpStatusCode.NotFound, $"Recruit with id {selectedPlayerId} not found.");

        var opponentOwnsPlayer = await _db.Collections
            .AnyAsync(c => c.UserId == opponentUserId && c.PlayerId == selectedPlayerId);
        if (!opponentOwnsPlayer)
            throw new BattleOperationException(HttpStatusCode.BadRequest, "Selected recruit is not in your collection.");

        var challengerPlayer = await _db.Players.FindAsync(battle.ChallengerPlayerId);
        if (challengerPlayer is null)
            throw new BattleOperationException(HttpStatusCode.BadRequest, "The challenger's recruit no longer exists.");

        var challengerOwnsPlayer = await _db.Collections
            .AnyAsync(c => c.UserId == battle.ChallengerId && c.PlayerId == battle.ChallengerPlayerId);
        if (!challengerOwnsPlayer)
            throw new BattleOperationException(HttpStatusCode.BadRequest, "The challenger no longer owns the selected recruit.");

        var challengerUser = await _userManager.FindByIdAsync(battle.ChallengerId);
        var opponentUser = await _userManager.FindByIdAsync(opponentUserId);
        var challengerUsername = challengerUser?.UserName ?? battle.ChallengerId;
        var opponentUsername = opponentUser?.UserName ?? opponentUserId;

        var result = _battleResolver.Resolve(challengerPlayer, opponentPlayer, challengerUsername, opponentUsername);
        var winnerId = string.Equals(result.WinnerUsername, challengerUsername, StringComparison.OrdinalIgnoreCase)
            ? battle.ChallengerId
            : opponentUserId;

        battle.OpponentPlayerId = selectedPlayerId;
        battle.Status = BattleStatus.Completed;
        battle.WinnerId = winnerId;
        battle.ResolvedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new BattleOperationException(HttpStatusCode.Conflict, "This challenge was already accepted or modified concurrently.", ex);
        }

        return await ToSummaryAsync(battle, result);
    }

    /// <summary>Builds a user-facing battle summary DTO.</summary>
    public async Task<BattleSummaryDto> ToSummaryAsync(BattleModel battle, BattleResult? result = null)
    {
        var userIds = new[] { battle.ChallengerId, battle.OpponentId }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToArray();
        var users = new Dictionary<string, IdentityUser>();
        foreach (var userId in userIds)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is not null)
                users[userId] = user;
        }

        var challengerPlayer = await _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == battle.ChallengerPlayerId);
        var opponentPlayer = battle.OpponentPlayerId > 0
            ? await _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == battle.OpponentPlayerId)
            : null;

        var challengerUsername = DisplayName(users, battle.ChallengerId);
        var opponentUsername = DisplayName(users, battle.OpponentId);
        var winnerUsername = battle.WinnerId is not null ? DisplayName(users, battle.WinnerId) : null;

        result ??= BuildStoredResult(battle, challengerUsername, opponentUsername, challengerPlayer, opponentPlayer, winnerUsername);

        return new BattleSummaryDto(
            battle.Id,
            battle.Status,
            battle.ChallengerId,
            challengerUsername,
            battle.OpponentId,
            opponentUsername,
            battle.ChallengerPlayerId,
            battle.OpponentPlayerId,
            ToPlayerSummary(challengerPlayer),
            ToPlayerSummary(opponentPlayer),
            battle.WinnerId,
            winnerUsername,
            result,
            battle.CreatedAt,
            battle.ResolvedAt,
            battle.IdempotencyKey);
    }

    /// <summary>Builds user-facing battle summary DTOs.</summary>
    public async Task<IReadOnlyList<BattleSummaryDto>> ToSummariesAsync(IEnumerable<BattleModel> battles)
    {
        var summaries = new List<BattleSummaryDto>();
        foreach (var battle in battles)
        {
            summaries.Add(await ToSummaryAsync(battle));
        }

        return summaries;
    }

    private static string DisplayName(IReadOnlyDictionary<string, IdentityUser> users, string userId)
    {
        return users.TryGetValue(userId, out var user)
            ? user.UserName ?? user.Email ?? userId
            : userId;
    }

    private static BattlePlayerSummaryDto? ToPlayerSummary(PlayerModel? player)
    {
        if (player is null)
            return null;

        return new BattlePlayerSummaryDto(
            player.Id,
            player.Name,
            player.Position.ToString(),
            player.Rarity.ToString(),
            player.Level,
            player.Speed,
            player.Strength,
            player.Agility,
            player.Kicking);
    }

    private static BattleResult? BuildStoredResult(
        BattleModel battle,
        string challengerUsername,
        string opponentUsername,
        PlayerModel? challengerPlayer,
        PlayerModel? opponentPlayer,
        string? winnerUsername)
    {
        if (battle.Status != BattleStatus.Completed ||
            string.IsNullOrWhiteSpace(battle.WinnerId) ||
            challengerPlayer is null ||
            opponentPlayer is null ||
            string.IsNullOrWhiteSpace(winnerUsername))
        {
            return null;
        }

        var challengerWon = battle.WinnerId == battle.ChallengerId;
        var winnerPlayer = challengerWon ? challengerPlayer : opponentPlayer;
        var loserPlayer = challengerWon ? opponentPlayer : challengerPlayer;
        var loserUsername = challengerWon ? opponentUsername : challengerUsername;

        return new BattleResult(
            winnerUsername,
            loserUsername,
            winnerPlayer.Name,
            loserPlayer.Name,
            "Resolved",
            battle.ResolvedAt ?? battle.CreatedAt);
    }
}

/// <summary>Exception carrying an HTTP-friendly battle operation failure.</summary>
public sealed class BattleOperationException : Exception
{
    /// <summary>HTTP status code that best represents the operation failure.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Initializes a new instance of <see cref="BattleOperationException"/>.</summary>
    public BattleOperationException(HttpStatusCode statusCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
