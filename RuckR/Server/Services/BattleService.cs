using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Services;

/// <summary>Business service for challenge limits, battle state transitions, and hidden rugby play resolution.</summary>
public class BattleService : IBattleService
{
    /// <inheritdoc />
    public int MaxChallengesPerHour => 10;

    /// <inheritdoc />
    public int MaxPendingChallenges => 3;

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<BattleSummaryDto> CreateChallengeAsync(string challengerUserId, string opponentUsername, string? idempotencyKey = null)
    {
        if (string.IsNullOrWhiteSpace(challengerUserId))
            throw new BattleOperationException(HttpStatusCode.Unauthorized, "User identity not found.");

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.Battles
                .FirstOrDefaultAsync(b => b.IdempotencyKey == idempotencyKey && b.ChallengerId == challengerUserId);
            if (existing is not null)
                return await ToSummaryAsync(existing, challengerUserId);
        }

        var opponent = await _userManager.FindByNameAsync(opponentUsername);
        if (opponent is null)
            throw new BattleOperationException(HttpStatusCode.NotFound, $"User '{opponentUsername}' not found.");

        if (opponent.Id == challengerUserId)
            throw new BattleOperationException(HttpStatusCode.BadRequest, "Cannot challenge yourself.");

        var challengerRecruitCount = await _db.Collections.CountAsync(c => c.UserId == challengerUserId);
        if (challengerRecruitCount == 0)
            throw new BattleOperationException(HttpStatusCode.BadRequest, "You need at least one recruit before sending a challenge.");

        var opponentRecruitCount = await _db.Collections.CountAsync(c => c.UserId == opponent.Id);
        if (opponentRecruitCount == 0)
            throw new BattleOperationException(HttpStatusCode.BadRequest, "Opponent has no recruits to battle with.");

        var expiryCutoff = DateTime.UtcNow - ChallengeExpiryDuration;
        var pendingCount = await _db.Battles
            .CountAsync(b => b.ChallengerId == challengerUserId
                && b.Status == BattleStatus.Pending
                && b.CreatedAt > expiryCutoff);

        if (pendingCount >= MaxPendingChallenges)
            throw new BattleOperationException(HttpStatusCode.BadRequest, $"You already have {MaxPendingChallenges} or more pending challenges. Wait for them to expire or be resolved.");

        var allowed = await _rateLimitService.IsAllowedAsync(challengerUserId, "challenge", MaxChallengesPerHour, TimeSpan.FromHours(1));
        if (!allowed)
            throw new BattleOperationException(HttpStatusCode.TooManyRequests, $"Rate limit exceeded. You can send up to {MaxChallengesPerHour} challenges per hour.");

        var battle = new BattleModel
        {
            ChallengerId = challengerUserId,
            OpponentId = opponent.Id,
            Status = BattleStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            IdempotencyKey = idempotencyKey
        };

        _db.Battles.Add(battle);
        await _db.SaveChangesAsync();

        return await ToSummaryAsync(battle, challengerUserId);
    }

    /// <inheritdoc />
    public async Task<BattleSummaryDto> AcceptChallengeAsync(int battleId, string opponentUserId)
    {
        var battle = await _db.Battles.FirstOrDefaultAsync(b => b.Id == battleId);
        if (battle is null)
            throw new BattleOperationException(HttpStatusCode.NotFound, $"Battle with id {battleId} not found.");

        if (battle.OpponentId != opponentUserId)
            throw new BattleOperationException(HttpStatusCode.Forbidden, "Only the opponent can accept this challenge.");

        if (await ExpireIfNeededAsync(battle))
            throw new BattleOperationException(HttpStatusCode.Gone, "Challenge expired.");

        if (battle.Status != BattleStatus.Pending)
            throw new BattleOperationException(HttpStatusCode.BadRequest, "Challenge is no longer pending.");

        battle.Status = BattleStatus.Accepted;
        battle.AcceptedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new BattleOperationException(HttpStatusCode.Conflict, "This challenge was already accepted or modified concurrently.", ex);
        }

        return await ToSummaryAsync(battle, opponentUserId);
    }

    /// <inheritdoc />
    public async Task<BattleSummaryDto> SubmitSelectionAsync(int battleId, string userId, int playerId, BattleMove move)
    {
        var battle = await _db.Battles.FirstOrDefaultAsync(b => b.Id == battleId);
        if (battle is null)
            throw new BattleOperationException(HttpStatusCode.NotFound, $"Battle with id {battleId} not found.");

        if (battle.ChallengerId != userId && battle.OpponentId != userId)
            throw new BattleOperationException(HttpStatusCode.Forbidden, "Only battle participants can submit selections.");

        var isChallenger = battle.ChallengerId == userId;
        if (battle.Status == BattleStatus.Completed && HasSameSelection(battle, isChallenger, playerId, move))
            return await ToSummaryAsync(battle, userId);

        if (await ExpireIfNeededAsync(battle))
            throw new BattleOperationException(HttpStatusCode.Gone, "Challenge expired.");

        if (battle.Status != BattleStatus.Accepted)
            throw new BattleOperationException(HttpStatusCode.BadRequest, "Challenge must be accepted before selections can be submitted.");

        var selectedPlayer = await _db.Players.FirstOrDefaultAsync(p => p.Id == playerId);
        if (selectedPlayer is null)
            throw new BattleOperationException(HttpStatusCode.NotFound, $"Recruit with id {playerId} not found.");

        var ownsPlayer = await _db.Collections.AnyAsync(c => c.UserId == userId && c.PlayerId == playerId);
        if (!ownsPlayer)
            throw new BattleOperationException(HttpStatusCode.BadRequest, "Selected recruit is not in your collection.");

        if (isChallenger)
        {
            if (battle.ChallengerSubmittedAt.HasValue)
            {
                if (battle.ChallengerPlayerId == playerId && battle.ChallengerMove == move)
                    return await ToSummaryAsync(battle, userId);
                throw new BattleOperationException(HttpStatusCode.BadRequest, "Your battle selection has already been submitted.");
            }

            battle.ChallengerPlayerId = playerId;
            battle.ChallengerMove = move;
            battle.ChallengerSubmittedAt = DateTime.UtcNow;
        }
        else
        {
            if (battle.OpponentSubmittedAt.HasValue)
            {
                if (battle.OpponentPlayerId == playerId && battle.OpponentMove == move)
                    return await ToSummaryAsync(battle, userId);
                throw new BattleOperationException(HttpStatusCode.BadRequest, "Your battle selection has already been submitted.");
            }

            battle.OpponentPlayerId = playerId;
            battle.OpponentMove = move;
            battle.OpponentSubmittedAt = DateTime.UtcNow;
        }

        BattleResult? result = null;
        if (battle.ChallengerPlayerId.HasValue
            && battle.OpponentPlayerId.HasValue
            && battle.ChallengerMove.HasValue
            && battle.OpponentMove.HasValue)
        {
            result = await ResolveAcceptedBattleAsync(battle);
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new BattleOperationException(HttpStatusCode.Conflict, "This challenge was already modified concurrently.", ex);
        }

        return await ToSummaryAsync(battle, userId, result);
    }

    /// <inheritdoc />
    public async Task DeclineChallengeAsync(int battleId, string opponentUserId)
    {
        var battle = await _db.Battles.FirstOrDefaultAsync(b => b.Id == battleId);
        if (battle is null)
            throw new BattleOperationException(HttpStatusCode.NotFound, $"Battle with id {battleId} not found.");

        if (battle.OpponentId != opponentUserId)
            throw new BattleOperationException(HttpStatusCode.Forbidden, "Only the opponent can decline this challenge.");

        if (await ExpireIfNeededAsync(battle))
            throw new BattleOperationException(HttpStatusCode.Gone, "Challenge expired.");

        if (battle.Status != BattleStatus.Pending)
            throw new BattleOperationException(HttpStatusCode.BadRequest, "Challenge is no longer pending.");

        battle.Status = BattleStatus.Declined;
        battle.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<BattleSummaryDto> ToSummaryAsync(BattleModel battle, string? viewerUserId = null, BattleResult? result = null)
    {
        var userIds = new[] { battle.ChallengerId, battle.OpponentId, battle.WinnerId }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .Cast<string>()
            .ToArray();

        var users = new Dictionary<string, IdentityUser>();
        foreach (var userId in userIds)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is not null)
                users[userId] = user;
        }

        var playerIds = new[] { battle.ChallengerPlayerId, battle.OpponentPlayerId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        var players = await _db.Players
            .AsNoTracking()
            .Where(p => playerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var challengerPlayer = battle.ChallengerPlayerId.HasValue && players.TryGetValue(battle.ChallengerPlayerId.Value, out var cp)
            ? cp
            : null;
        var opponentPlayer = battle.OpponentPlayerId.HasValue && players.TryGetValue(battle.OpponentPlayerId.Value, out var op)
            ? op
            : null;

        var challengerUsername = DisplayName(users, battle.ChallengerId);
        var opponentUsername = DisplayName(users, battle.OpponentId);
        var winnerUsername = battle.WinnerId is not null ? DisplayName(users, battle.WinnerId) : null;

        var isCompleted = battle.Status == BattleStatus.Completed;
        var viewerIsChallenger = viewerUserId == battle.ChallengerId;
        var viewerIsOpponent = viewerUserId == battle.OpponentId;
        var showChallengerSelection = isCompleted || viewerIsChallenger;
        var showOpponentSelection = isCompleted || viewerIsOpponent;

        result ??= BuildStoredResult(battle, challengerUsername, opponentUsername, challengerPlayer, opponentPlayer, winnerUsername);

        return new BattleSummaryDto(
            battle.Id,
            battle.Status,
            battle.ChallengerId,
            challengerUsername,
            battle.OpponentId,
            opponentUsername,
            showChallengerSelection ? battle.ChallengerPlayerId : null,
            showOpponentSelection ? battle.OpponentPlayerId : null,
            showChallengerSelection ? ToPlayerSummary(challengerPlayer) : null,
            showOpponentSelection ? ToPlayerSummary(opponentPlayer) : null,
            battle.WinnerId,
            winnerUsername,
            isCompleted ? result : null,
            battle.CreatedAt,
            battle.ResolvedAt,
            battle.IdempotencyKey,
            battle.AcceptedAt,
            battle.ChallengerSubmittedAt.HasValue,
            battle.OpponentSubmittedAt.HasValue,
            showChallengerSelection ? battle.ChallengerMove : null,
            showOpponentSelection ? battle.OpponentMove : null,
            showChallengerSelection ? battle.ChallengerSubmittedAt : null,
            showOpponentSelection ? battle.OpponentSubmittedAt : null,
            isCompleted ? battle.ChallengerScore : null,
            isCompleted ? battle.OpponentScore : null);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BattleSummaryDto>> ToSummariesAsync(IEnumerable<BattleModel> battles, string? viewerUserId = null)
    {
        var summaries = new List<BattleSummaryDto>();
        foreach (var battle in battles)
        {
            summaries.Add(await ToSummaryAsync(battle, viewerUserId));
        }

        return summaries;
    }

    private async Task<bool> ExpireIfNeededAsync(BattleModel battle)
    {
        if (battle.Status is not (BattleStatus.Pending or BattleStatus.Accepted))
            return false;

        if (battle.CreatedAt > DateTime.UtcNow - ChallengeExpiryDuration)
            return false;

        battle.Status = BattleStatus.Expired;
        battle.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    private static bool HasSameSelection(BattleModel battle, bool isChallenger, int playerId, BattleMove move)
    {
        return isChallenger
            ? battle.ChallengerPlayerId == playerId && battle.ChallengerMove == move
            : battle.OpponentPlayerId == playerId && battle.OpponentMove == move;
    }

    private async Task<BattleResult> ResolveAcceptedBattleAsync(BattleModel battle)
    {
        var challengerPlayer = await _db.Players.FirstAsync(p => p.Id == battle.ChallengerPlayerId!.Value);
        var opponentPlayer = await _db.Players.FirstAsync(p => p.Id == battle.OpponentPlayerId!.Value);
        var challengerUser = await _userManager.FindByIdAsync(battle.ChallengerId);
        var opponentUser = await _userManager.FindByIdAsync(battle.OpponentId);
        var challengerUsername = challengerUser?.UserName ?? battle.ChallengerId;
        var opponentUsername = opponentUser?.UserName ?? battle.OpponentId;

        var result = _battleResolver.Resolve(
            challengerPlayer,
            opponentPlayer,
            battle.ChallengerId,
            battle.OpponentId,
            challengerUsername,
            opponentUsername,
            battle.ChallengerMove!.Value,
            battle.OpponentMove!.Value,
            battle.Id);

        battle.Status = BattleStatus.Completed;
        battle.WinnerId = string.Equals(result.WinnerUsername, challengerUsername, StringComparison.OrdinalIgnoreCase)
            ? battle.ChallengerId
            : battle.OpponentId;
        battle.ResolvedAt = DateTime.UtcNow;
        battle.ChallengerScore = result.ChallengerScore;
        battle.OpponentScore = result.OpponentScore;
        battle.ResolutionMethod = result.Method;

        return result;
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
        if (battle.Status != BattleStatus.Completed
            || string.IsNullOrWhiteSpace(battle.WinnerId)
            || challengerPlayer is null
            || opponentPlayer is null
            || string.IsNullOrWhiteSpace(winnerUsername)
            || battle.ChallengerMove is null
            || battle.OpponentMove is null)
        {
            return null;
        }

        var challengerWon = battle.WinnerId == battle.ChallengerId;
        var winnerPlayer = challengerWon ? challengerPlayer : opponentPlayer;
        var loserPlayer = challengerWon ? opponentPlayer : challengerPlayer;
        var loserUsername = challengerWon ? opponentUsername : challengerUsername;
        var winnerMove = challengerWon ? battle.ChallengerMove : battle.OpponentMove;
        var loserMove = challengerWon ? battle.OpponentMove : battle.ChallengerMove;
        var winnerScore = challengerWon ? battle.ChallengerScore : battle.OpponentScore;
        var loserScore = challengerWon ? battle.OpponentScore : battle.ChallengerScore;

        return new BattleResult(
            winnerUsername,
            loserUsername,
            winnerPlayer.Name,
            loserPlayer.Name,
            battle.ResolutionMethod ?? "Resolved",
            battle.ResolvedAt ?? battle.CreatedAt,
            winnerMove,
            loserMove,
            winnerScore,
            loserScore,
            battle.ChallengerMove,
            battle.OpponentMove,
            battle.ChallengerScore,
            battle.OpponentScore);
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
