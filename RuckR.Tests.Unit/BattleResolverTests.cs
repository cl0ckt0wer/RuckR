using System;
using System.Collections.Generic;
using FluentAssertions;
using RuckR.Server.Services;
using RuckR.Shared.Models;
using Xunit;

namespace RuckR.Tests.Unit;

    /// <summary>
    /// Provides access to class.
    /// </summary>
public class BattleResolverTests
{
    private readonly IBattleResolver _resolver = new BattleResolver();

    // ── Edge-case stat combinations ──

    [Fact]
    /// <summary>
    /// Verifies resolve All Stats At Min Lower Stat Loses.
    /// </summary>
    public void Resolve_AllStatsAtMin_LowerStatLoses()
    {
        var low = CreatePlayer("Low", 1, 1, 1, 1);
        var high = CreatePlayer("High", 99, 99, 99, 99);

        var result = _resolver.Resolve(low, high, "low_user", "high_user", seed: 42);

        result.WinnerUsername.Should().Be("high_user");
        result.LoserUsername.Should().Be("low_user");
    }

    [Fact]
    /// <summary>
    /// Verifies resolve All Stats At Max All99 Beats All1.
    /// </summary>
    public void Resolve_AllStatsAtMax_All99BeatsAll1()
    {
        var all99 = CreatePlayer("Max", 99, 99, 99, 99);
        var all1 = CreatePlayer("Min", 1, 1, 1, 1);

        var result = _resolver.Resolve(all99, all1, "max_user", "min_user", seed: 42);

        result.WinnerUsername.Should().Be("max_user");
    }

    [Fact]
    /// <summary>
    /// Verifies resolve Identical Stats And Position Winner Is Non Deterministic.
    /// </summary>
    public void Resolve_IdenticalStatsAndPosition_WinnerIsNonDeterministic()
    {
        var a = CreatePlayer("A", 50, 50, 50, 50);
        var b = CreatePlayer("B", 50, 50, 50, 50);

        // With same seed, result should be deterministic
        var result1 = _resolver.Resolve(a, b, "user_a", "user_b", seed: 123);
        var result2 = _resolver.Resolve(a, b, "user_a", "user_b", seed: 123);

        result1.WinnerUsername.Should().Be(result2.WinnerUsername);
    }

    [Fact]
    /// <summary>
    /// Verifies resolve Identical Stats Different Seeds Can Yield Different Winners.
    /// </summary>
    public void Resolve_IdenticalStatsDifferentSeeds_CanYieldDifferentWinners()
    {
        var a = CreatePlayer("A", 50, 50, 50, 50);
        var b = CreatePlayer("B", 50, 50, 50, 50);

        var results = new HashSet<string>();
        for (int i = 0; i < 20; i++)
        {
            var result = _resolver.Resolve(a, b, "user_a", "user_b", seed: i);
            results.Add(result.WinnerUsername);
        }

        // With enough seeds, both should win at least once
        results.Should().Contain("user_a");
        results.Should().Contain("user_b");
    }

    // ── Position multiplier tests ──

    [Theory]
    [InlineData(PlayerPosition.Prop, PlayerPosition.Wing, 1.2)]
    [InlineData(PlayerPosition.FlyHalf, PlayerPosition.ScrumHalf, 1.15)]
    /// <summary>
    /// Verifies resolve Advantageous Position Multiplier Gives Advantage.
    /// </summary>
    public void Resolve_AdvantageousPositionMultiplier_GivesAdvantage(
        PlayerPosition attacker, PlayerPosition defender, double expectedMult)
    {
        var attackerPlayer = CreatePlayer("A", 100, 100, 1, 1, position: attacker);
        var defenderPlayer = CreatePlayer("B", 1, 1, 100, 100, position: defender);

        int wins = 0;
        for (int i = 0; i < 200; i++)
        {
            var result = _resolver.Resolve(attackerPlayer, defenderPlayer, "a", "b", seed: i);
            if (result.WinnerUsername == "a") wins++;
        }

        wins.Should().BeGreaterThan(150, $"attacker should win most with {expectedMult}x advantage multiplier");
    }

    [Theory]
    [InlineData(PlayerPosition.Wing, PlayerPosition.Prop, 0.85)]
    /// <summary>
    /// Verifies resolve Disadvantageous Position Multiplier Loses More.
    /// </summary>
    public void Resolve_DisadvantageousPositionMultiplier_LosesMore(
        PlayerPosition attacker, PlayerPosition defender, double expectedMult)
    {
        var attackerPlayer = CreatePlayer("A", 100, 100, 1, 1, position: attacker);
        var defenderPlayer = CreatePlayer("B", 1, 1, 100, 100, position: defender);

        int wins = 0;
        for (int i = 0; i < 200; i++)
        {
            var result = _resolver.Resolve(attackerPlayer, defenderPlayer, "a", "b", seed: i);
            if (result.WinnerUsername == "a") wins++;
        }

        wins.Should().BeLessThan(50, $"attacker should lose most with {expectedMult}x disadvantage multiplier");
    }

    [Theory]
    [InlineData(PlayerPosition.Prop, PlayerPosition.Prop)]
    [InlineData(PlayerPosition.Wing, PlayerPosition.Lock)]
    /// <summary>
    /// Verifies resolve Neutral Position Multiplier Is Random.
    /// </summary>
    public void Resolve_NeutralPositionMultiplier_IsRandom(
        PlayerPosition attacker, PlayerPosition defender)
    {
        var a = CreatePlayer("A", 50, 50, 50, 50, position: attacker);
        var b = CreatePlayer("B", 50, 50, 50, 50, position: defender);

        int aWins = 0;
        for (int i = 0; i < 200; i++)
        {
            var result = _resolver.Resolve(a, b, "a", "b", seed: i);
            if (result.WinnerUsername == "a") aWins++;
        }

        aWins.Should().BeInRange(70, 130);
    }

    // ── Rarity multiplier tests ──

    [Theory]
    [InlineData(PlayerRarity.Common, PlayerRarity.Rare)]
    [InlineData(PlayerRarity.Common, PlayerRarity.Legendary)]
    [InlineData(PlayerRarity.Uncommon, PlayerRarity.Legendary)]
    [InlineData(PlayerRarity.Rare, PlayerRarity.Legendary)]
    /// <summary>
    /// Verifies resolve Higher Rarity Wins Over Lower.
    /// </summary>
    /// <param name="lower">The lower to use.</param>
    /// <param name="higher">The higher to use.</param>
    public void Resolve_HigherRarity_WinsOverLower(PlayerRarity lower, PlayerRarity higher)
    {
        // Non-adjacent rarity tiers ensure multiplier gap eliminates random overlap
        var low = CreatePlayer("Low", 50, 50, 50, 50, rarity: lower);
        var high = CreatePlayer("High", 50, 50, 50, 50, rarity: higher);

        int highWins = 0;
        for (int i = 0; i < 50; i++)
        {
            var result = _resolver.Resolve(high, low, "high", "low", seed: i);
            if (result.WinnerUsername == "high") highWins++;
        }

        highWins.Should().Be(50, "higher rarity (non-adjacent) should always win against equal stats");
    }

    [Fact]
    /// <summary>
    /// Verifies resolve Adjacent Rarity Higher Wins More Often.
    /// </summary>
    public void Resolve_AdjacentRarity_HigherWinsMoreOften()
    {
        // Adjacent rarities (Common vs Uncommon) overlap due to random factor
        // but the higher rarity should win significantly more than 50%
        var common = CreatePlayer("Common", 50, 50, 50, 50, rarity: PlayerRarity.Common);
        var uncommon = CreatePlayer("Uncommon", 50, 50, 50, 50, rarity: PlayerRarity.Uncommon);

        int uncommonWins = 0;
        for (int i = 0; i < 200; i++)
        {
            var result = _resolver.Resolve(uncommon, common, "u", "c", seed: i);
            if (result.WinnerUsername == "u") uncommonWins++;
        }

        // Uncommon (1.2x) should win notably more than Common (1.0x)
        uncommonWins.Should().BeGreaterThan(120, "Uncommon should win significantly more than Common at equal stats");
    }

    [Fact]
    /// <summary>
    /// Verifies resolve Same Rarity Same Stats Is Random.
    /// </summary>
    public void Resolve_SameRarity_SameStats_IsRandom()
    {
        var a = CreatePlayer("A", 50, 50, 50, 50, rarity: PlayerRarity.Common);
        var b = CreatePlayer("B", 50, 50, 50, 50, rarity: PlayerRarity.Common);

        int aWins = 0;
        for (int i = 0; i < 100; i++)
        {
            var result = _resolver.Resolve(a, b, "a", "b", seed: i);
            if (result.WinnerUsername == "a") aWins++;
        }

        // Should be roughly 50/50, not 0 or 100
        aWins.Should().BeInRange(25, 75);
    }

    [Theory]
    [InlineData(PlayerRarity.Common, PlayerRarity.Rare)]
    [InlineData(PlayerRarity.Common, PlayerRarity.Epic)]
    [InlineData(PlayerRarity.Common, PlayerRarity.Legendary)]
    [InlineData(PlayerRarity.Uncommon, PlayerRarity.Legendary)]
    [InlineData(PlayerRarity.Rare, PlayerRarity.Legendary)]
    /// <summary>
    /// Verifies resolve Non Adjacent Higher Rarity Always Wins.
    /// </summary>
    /// <param name="lower">The lower to use.</param>
    /// <param name="higher">The higher to use.</param>
    public void Resolve_NonAdjacentHigherRarity_AlwaysWins(PlayerRarity lower, PlayerRarity higher)
    {
        var low = CreatePlayer("Low", 50, 50, 50, 50, rarity: lower);
        var high = CreatePlayer("High", 50, 50, 50, 50, rarity: higher);

        int highWins = 0;
        for (int i = 0; i < 50; i++)
        {
            var result = _resolver.Resolve(high, low, "high", "low", seed: i);
            if (result.WinnerUsername == "high") highWins++;
        }

        highWins.Should().Be(50, $"{higher} vs {lower} (non-adjacent) should always win at equal stats");
    }

    [Fact]
    /// <summary>
    /// Verifies resolve Adjacent Rarity Higher Wins Significantly More.
    /// </summary>
    public void Resolve_AdjacentRarity_HigherWinsSignificantlyMore()
    {
        // Adjacent rarities (Common=1.0 vs Uncommon=1.2) with identical stats still produce
        // occasional random upsets, but the higher rarity should win the majority.
        var common = CreatePlayer("Common", 50, 50, 50, 50, rarity: PlayerRarity.Common);
        var uncommon = CreatePlayer("Uncommon", 50, 50, 50, 50, rarity: PlayerRarity.Uncommon);

        int uncommonWins = 0;
        for (int i = 0; i < 200; i++)
        {
            var result = _resolver.Resolve(uncommon, common, "u", "c", seed: i);
            if (result.WinnerUsername == "u") uncommonWins++;
        }

        // Theoretical win rate ~91.5% for 1.2x vs 1.0x with identical stats
        uncommonWins.Should().BeGreaterThan(120, "Uncommon should win significantly more than Common at equal stats");
    }

    // ── DetermineMethod tests ──

[Fact]
    /// <summary>
    /// Verifies resolve High Speed Difference Returns Speed Advantage.
    /// </summary>
    public void Resolve_HighSpeedDifference_ReturnsSpeedAdvantage()
    {
        // Ensure strength/agility/kicking are close so speed is the deciding diff
        var fast = CreatePlayer("Fast", speed: 99, strength: 50, agility: 50, kicking: 50);
        var slow = CreatePlayer("Slow", speed: 1, strength: 50, agility: 50, kicking: 50);

        var result = _resolver.Resolve(fast, slow, "fast", "slow", seed: 42);

        result.Method.Should().Be("Speed Advantage");
    }

    [Fact]
    /// <summary>
    /// Verifies resolve High Strength Prop Returns Power Overwhelming.
    /// </summary>
    public void Resolve_HighStrengthProp_ReturnsPowerOverwhelming()
    {
        // Strength diff >15, winner is Prop, keep speed/agility/kicking diffs ≤15
        var prop = CreatePlayer("Prop", speed: 50, strength: 90, agility: 50, kicking: 50, position: PlayerPosition.Prop);
        var wing = CreatePlayer("Wing", speed: 55, strength: 1, agility: 55, kicking: 55, position: PlayerPosition.Wing);

        var result = _resolver.Resolve(prop, wing, "prop", "wing", seed: 42);

        result.Method.Should().Be("Power Overwhelming");
    }

    [Fact]
    /// <summary>
    /// Verifies resolve High Strength Non Pack Returns Strength Dominance.
    /// </summary>
    public void Resolve_HighStrengthNonPack_ReturnsStrengthDominance()
    {
        // Strength diff >15, winner is Wing (not pack), keep speed/agility/kicking diffs ≤15
        var strongWing = CreatePlayer("StrongWing", speed: 50, strength: 90, agility: 50, kicking: 50, position: PlayerPosition.Wing);
        var weakProp = CreatePlayer("WeakProp", speed: 55, strength: 1, agility: 55, kicking: 55, position: PlayerPosition.Prop);

        var result = _resolver.Resolve(strongWing, weakProp, "wing", "prop", seed: 42);

        result.Method.Should().Be("Strength Dominance");
    }

    [Fact]
    /// <summary>
    /// Verifies resolve High Kicking Returns Kicking Mastery.
    /// </summary>
    public void Resolve_HighKicking_ReturnsKickingMastery()
    {
        // Keep other stats close so kicking diff is the deciding factor
        var kicker = CreatePlayer("Kicker", speed: 50, strength: 50, agility: 50, kicking: 99);
        var other = CreatePlayer("Other", speed: 50, strength: 50, agility: 50, kicking: 1);

        var result = _resolver.Resolve(kicker, other, "kicker", "other", seed: 42);

        result.Method.Should().Be("Kicking Mastery");
    }

    [Fact]
    /// <summary>
    /// Verifies resolve High Agility Returns Agility Outplay.
    /// </summary>
    public void Resolve_HighAgility_ReturnsAgilityOutplay()
    {
        // Keep other stats close so agility diff is the deciding factor
        var agile = CreatePlayer("Agile", speed: 50, strength: 50, agility: 99, kicking: 50);
        var slow = CreatePlayer("Slow", speed: 50, strength: 50, agility: 1, kicking: 50);

        var result = _resolver.Resolve(agile, slow, "agile", "slow", seed: 42);

        result.Method.Should().Be("Agility Outplay");
    }

    [Fact]
    /// <summary>
    /// Verifies resolve Higher Rarity Returns Rarity Advantage When Stats Equal.
    /// </summary>
    public void Resolve_HigherRarity_ReturnsRarityAdvantage_WhenStatsEqual()
    {
        var epic = CreatePlayer("Epic", 50, 50, 50, 50, rarity: PlayerRarity.Epic);
        var common = CreatePlayer("Common", 50, 50, 50, 50, rarity: PlayerRarity.Common);

        var result = _resolver.Resolve(epic, common, "epic", "common", seed: 42);

        result.Method.Should().Be("Rarity Advantage");
    }

    // ── BattleResult content validation ──

    [Fact]
    /// <summary>
    /// Verifies resolve Result Contains Both Usernames And Names.
    /// </summary>
    public void Resolve_ResultContainsBothUsernamesAndNames()
    {
        var a = CreatePlayer("Alpha", 99, 99, 99, 99);
        var b = CreatePlayer("Beta", 1, 1, 1, 1);

        var result = _resolver.Resolve(a, b, "user_a", "user_b", seed: 42);

        result.WinnerUsername.Should().Be("user_a");
        result.LoserUsername.Should().Be("user_b");
        result.WinnerPlayerName.Should().Be("Alpha");
        result.LoserPlayerName.Should().Be("Beta");
        result.Method.Should().NotBeNullOrEmpty();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    /// <summary>
    /// Verifies resolve Result Method Is Never Null Or Empty.
    /// </summary>
    public void Resolve_ResultMethodIsNeverNullOrEmpty()
    {
        var a = CreatePlayer("A", 50, 50, 50, 50);
        var b = CreatePlayer("B", 50, 50, 50, 50);

        for (int i = 0; i < 50; i++)
        {
            var result = _resolver.Resolve(a, b, "a", "b", seed: i);
            result.Method.Should().NotBeNullOrWhiteSpace();
        }
    }

    // ── Typos / regression guards ──

    [Fact]
    /// <summary>
    /// Verifies resolve Fly Half Vs Scrum Half Fly Half Advantaged.
    /// </summary>
    public void Resolve_FlyHalfVsScrumHalf_FlyHalfAdvantaged()
    {
        // Regression test: ensure the typo "(FlyHalf, ScrumHalf): 1.15" is correctly ordered
        var flyHalf = CreatePlayer("FH", 50, 50, 50, 50, position: PlayerPosition.FlyHalf);
        var scrumHalf = CreatePlayer("SH", 50, 50, 50, 50, position: PlayerPosition.ScrumHalf);

        int flyHalfWins = 0;
        for (int i = 0; i < 100; i++)
        {
            var result = _resolver.Resolve(flyHalf, scrumHalf, "fh", "sh", seed: i);
            if (result.WinnerUsername == "fh") flyHalfWins++;
        }

        flyHalfWins.Should().BeGreaterThan(60, "FlyHalf should have advantage over ScrumHalf");
    }

    [Fact]
    /// <summary>
    /// Verifies resolve Tiebreaker Selects One Winner Does Not Crash.
    /// </summary>
    public void Resolve_TiebreakerSelectsOneWinner_DoesNotCrash()
    {
        // Same stats, same rarity, same position — pure tiebreaker
        var a = CreatePlayer("A", 50, 50, 50, 50);
        var b = CreatePlayer("B", 50, 50, 50, 50);

        for (int i = 0; i < 100; i++)
        {
            var result = _resolver.Resolve(a, b, "a", "b", seed: i);
            result.WinnerUsername.Should().BeOneOf("a", "b");
            result.LoserUsername.Should().BeOneOf("a", "b");
            result.WinnerUsername.Should().NotBe(result.LoserUsername);
        }
    }

    // ── Helpers ──

    private static PlayerModel CreatePlayer(
        string name,
        int speed = 50, int strength = 50, int agility = 50, int kicking = 50,
        PlayerPosition position = PlayerPosition.Wing,
        PlayerRarity rarity = PlayerRarity.Common)
    {
        return new PlayerModel
        {
            Id = 0,
            Name = name,
            Position = position,
            Speed = speed,
            Strength = strength,
            Agility = agility,
            Kicking = kicking,
            Rarity = rarity,
            Level = 1,
            Bio = string.Empty,
            Team = string.Empty
        };
    }
}

