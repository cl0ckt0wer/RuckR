using RuckR.Server.Services;
using RuckR.Shared.Models;

namespace RuckR.Tests.Services;

public class BattleResolverTests
{
    [Theory]
    [InlineData(BattleMove.Rock, "Crash Ball")]
    [InlineData(BattleMove.Paper, "Cut-Out Pass")]
    [InlineData(BattleMove.Scissors, "Grubber Kick")]
    [InlineData(BattleMove.Lizard, "Sidestep")]
    [InlineData(BattleMove.Spock, "Scrum Drive")]
    public void BattleMoveDisplay_UsesRugbyMoveNames(BattleMove move, string expected)
    {
        Assert.Equal(expected, BattleMoveDisplay.Name(move));
    }

    [Theory]
    [InlineData(BattleMove.Scissors, BattleMove.Paper, "Grubber Kick splits Cut-Out Pass")]
    [InlineData(BattleMove.Paper, BattleMove.Rock, "Cut-Out Pass finds space around Crash Ball")]
    [InlineData(BattleMove.Rock, BattleMove.Lizard, "Crash Ball flattens Sidestep")]
    [InlineData(BattleMove.Lizard, BattleMove.Spock, "Sidestep slips Scrum Drive")]
    [InlineData(BattleMove.Spock, BattleMove.Scissors, "Scrum Drive swallows Grubber Kick")]
    [InlineData(BattleMove.Scissors, BattleMove.Lizard, "Grubber Kick catches Sidestep")]
    [InlineData(BattleMove.Lizard, BattleMove.Paper, "Sidestep breaks Cut-Out Pass")]
    [InlineData(BattleMove.Paper, BattleMove.Spock, "Cut-Out Pass stretches Scrum Drive")]
    [InlineData(BattleMove.Spock, BattleMove.Rock, "Scrum Drive rolls over Crash Ball")]
    [InlineData(BattleMove.Rock, BattleMove.Scissors, "Crash Ball charges down Grubber Kick")]
    public void Resolve_CanonicalMoveWinnerReceivesBonus(BattleMove winnerMove, BattleMove loserMove, string method)
    {
        var resolver = new BattleResolver();
        var challenger = CreatePlayer("Challenger", PlayerRarity.Common, 1, 50);
        var opponent = CreatePlayer("Opponent", PlayerRarity.Common, 1, 50);

        var result = resolver.Resolve(
            challenger,
            opponent,
            "user-a",
            "user-b",
            "alice",
            "bob",
            winnerMove,
            loserMove,
            123);

        Assert.Equal("alice", result.WinnerUsername);
        Assert.Equal(winnerMove, result.WinnerMove);
        Assert.Equal(loserMove, result.LoserMove);
        Assert.Equal(method, result.Method);
        Assert.Equal(77, result.ChallengerScore);
        Assert.Equal(27, result.OpponentScore);
    }

    [Fact]
    public void Resolve_UsesRarityThenLevelThenStatsThenStableUserIdTiebreak()
    {
        var resolver = new BattleResolver();
        var challenger = CreatePlayer("Challenger", PlayerRarity.Rare, 5, 60);
        var opponent = CreatePlayer("Opponent", PlayerRarity.Common, 5, 60);

        var rarityResult = resolver.Resolve(
            challenger,
            opponent,
            "user-z",
            "user-a",
            "alice",
            "bob",
            BattleMove.Rock,
            BattleMove.Rock,
            123);

        Assert.Equal("alice", rarityResult.WinnerUsername);
        Assert.Equal("Same move, recruit power wins", rarityResult.Method);

        challenger.Rarity = PlayerRarity.Common;
        opponent.Rarity = PlayerRarity.Common;
        challenger.Level = 1;
        opponent.Level = 1;
        challenger.Speed = opponent.Speed = 50;
        challenger.Strength = opponent.Strength = 50;
        challenger.Agility = opponent.Agility = 50;
        challenger.Kicking = opponent.Kicking = 50;

        var tiebreakResult = resolver.Resolve(
            challenger,
            opponent,
            "user-z",
            "user-a",
            "alice",
            "bob",
            BattleMove.Spock,
            BattleMove.Spock,
            123);

        Assert.Equal("bob", tiebreakResult.WinnerUsername);
    }

    private static PlayerModel CreatePlayer(string name, PlayerRarity rarity, int level, int stat)
    {
        return new PlayerModel
        {
            Id = Random.Shared.Next(1, int.MaxValue),
            Name = name,
            Position = PlayerPosition.FlyHalf,
            Rarity = rarity,
            Level = level,
            Speed = stat,
            Strength = stat,
            Agility = stat,
            Kicking = stat
        };
    }
}
