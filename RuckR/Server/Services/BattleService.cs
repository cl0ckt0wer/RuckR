using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RuckR.Server.Data;
using RuckR.Server.Services;
using RuckR.Shared.Models;

namespace RuckR.Server.Services;

public class BattleService : IBattleService
{
    public int MaxChallengesPerHour => 10;
    public int MaxPendingChallenges => 3;
    public TimeSpan ChallengeExpiryDuration => TimeSpan.FromHours(24);

    // In-memory rate limit for high-frequency checks (supplements DB records)
    private static readonly ConcurrentDictionary<string, List<DateTime>> _recentChallenges = new();

    private readonly RuckRDbContext _db;
    private readonly IRateLimitService _rateLimitService;

    public BattleService(RuckRDbContext db, IRateLimitService rateLimitService)
    {
        _db = db;
        _rateLimitService = rateLimitService;
    }

    public bool CanChallenge(string userId, List<BattleModel> pendingBattles)
    {
        var now = DateTime.UtcNow;
        var expiryCutoff = now - ChallengeExpiryDuration;

        var activePending = pendingBattles
            .Where(b => b.ChallengerId == userId && b.Status == BattleStatus.Pending && b.CreatedAt > expiryCutoff)
            .Count();

        return activePending < MaxPendingChallenges;
    }

    public async Task<BattleResult> ResolveBattleAsync(
        PlayerModel challengerPlayer,
        PlayerModel opponentPlayer,
        string challengerUsername,
        string opponentUsername)
    {
        // Delegate to the pure synchronous resolver — kept for backward compatibility
        return await Task.Run(() => ResolveBattlePure(challengerPlayer, opponentPlayer, challengerUsername, opponentUsername));
    }

    /// <summary>
    /// Pure, synchronous battle resolution — no database access, safe to call
    /// from async contexts without blocking.
    /// </summary>
    public BattleResult ResolveBattlePure(
        PlayerModel challengerPlayer,
        PlayerModel opponentPlayer,
        string challengerUsername,
        string opponentUsername)
    {
        var random = Random.Shared;

        double challengerBase = (challengerPlayer.Speed + challengerPlayer.Strength + challengerPlayer.Agility + challengerPlayer.Kicking) / 4.0;
        double opponentBase = (opponentPlayer.Speed + opponentPlayer.Strength + opponentPlayer.Agility + opponentPlayer.Kicking) / 4.0;

        double challengerPosMult = GetPositionMultiplier(challengerPlayer.Position, opponentPlayer.Position);
        double opponentPosMult = GetPositionMultiplier(opponentPlayer.Position, challengerPlayer.Position);

        double challengerRarityMult = GetRarityMultiplier(challengerPlayer.Rarity);
        double opponentRarityMult = GetRarityMultiplier(opponentPlayer.Rarity);

        double challengerRandom = 0.85 + (random.NextDouble() * 0.3);
        double opponentRandom = 0.85 + (random.NextDouble() * 0.3);

        double challengerScore = challengerBase * challengerPosMult * challengerRarityMult * challengerRandom;
        double opponentScore = opponentBase * opponentPosMult * opponentRarityMult * opponentRandom;

        PlayerModel winner, loser;
        string winnerUsername, loserUsername;

        if (challengerScore > opponentScore)
        {
            winner = challengerPlayer;
            loser = opponentPlayer;
            winnerUsername = challengerUsername;
            loserUsername = opponentUsername;
        }
        else if (opponentScore > challengerScore)
        {
            winner = opponentPlayer;
            loser = challengerPlayer;
            winnerUsername = opponentUsername;
            loserUsername = challengerUsername;
        }
        else
        {
            if (random.Next(2) == 0)
            {
                winner = challengerPlayer;
                loser = opponentPlayer;
                winnerUsername = challengerUsername;
                loserUsername = opponentUsername;
            }
            else
            {
                winner = opponentPlayer;
                loser = challengerPlayer;
                winnerUsername = opponentUsername;
                loserUsername = challengerUsername;
            }
        }

        string method = DetermineMethod(winner, loser);

        return new BattleResult(
            WinnerUsername: winnerUsername,
            LoserUsername: loserUsername,
            WinnerPlayerName: winner.Name,
            LoserPlayerName: loser.Name,
            Method: method,
            CreatedAt: DateTime.UtcNow);
    }

    Task<BattleResult> IBattleService.ResolveBattlePureAsync(
        PlayerModel challengerPlayer,
        PlayerModel opponentPlayer,
        string challengerUsername,
        string opponentUsername)
    {
        return Task.FromResult(ResolveBattlePure(challengerPlayer, opponentPlayer, challengerUsername, opponentUsername));
    }

    private static double GetPositionMultiplier(PlayerPosition attacker, PlayerPosition defender)
    {
        var multipliers = new Dictionary<(PlayerPosition, PlayerPosition), double>
        {
            { (PlayerPosition.Prop, PlayerPosition.Wing), 1.2 },
            { (PlayerPosition.Wing, PlayerPosition.Prop), 0.85 },
            { (PlayerPosition.FlyHalf, PlayerPosition.ScrumHalf), 1.15 },
            { (PlayerPosition.Lock, PlayerPosition.Hooker), 1.1 },
            { (PlayerPosition.Flanker, PlayerPosition.FlyHalf), 1.1 },
        };

        return multipliers.TryGetValue((attacker, defender), out double m) ? m : 1.0;
    }

    private static double GetRarityMultiplier(PlayerRarity rarity) => rarity switch
    {
        PlayerRarity.Common => 1.0,
        PlayerRarity.Uncommon => 1.2,
        PlayerRarity.Rare => 1.5,
        PlayerRarity.Epic => 2.0,
        PlayerRarity.Legendary => 3.0,
        _ => 1.0
    };

    private static string DetermineMethod(PlayerModel winner, PlayerModel loser)
    {
        int speedDiff = winner.Speed - loser.Speed;
        int strengthDiff = winner.Strength - loser.Strength;
        int kickingDiff = winner.Kicking - loser.Kicking;
        int agilityDiff = winner.Agility - loser.Agility;

        if (speedDiff > 15) return "Speed Advantage";
        if (strengthDiff > 15 && (winner.Position == PlayerPosition.Prop || winner.Position == PlayerPosition.Lock))
            return "Power Overwhelming";
        if (strengthDiff > 15) return "Strength Dominance";
        if (kickingDiff > 15) return "Kicking Mastery";
        if (agilityDiff > 15) return "Agility Outplay";
        if ((int)winner.Rarity > (int)loser.Rarity) return "Rarity Advantage";
        return "Close Contest";
    }
}