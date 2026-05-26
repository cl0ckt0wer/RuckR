using RuckR.Server.Services;
using RuckR.Shared.Models;

namespace RuckR.Tests.Services;

public class BattleResolverTests
{
    [Theory]
    [InlineData(BattleMove.Scissors, BattleMove.Paper, "Scissors cuts Paper")]
    [InlineData(BattleMove.Paper, BattleMove.Rock, "Paper covers Rock")]
    [InlineData(BattleMove.Rock, BattleMove.Lizard, "Rock crushes Lizard")]
    [InlineData(BattleMove.Lizard, BattleMove.Spock, "Lizard poisons Spock")]
    [InlineData(BattleMove.Spock, BattleMove.Scissors, "Spock smashes Scissors")]
    [InlineData(BattleMove.Scissors, BattleMove.Lizard, "Scissors decapitates Lizard")]
    [InlineData(BattleMove.Lizard, BattleMove.Paper, "Lizard eats Paper")]
    [InlineData(BattleMove.Paper, BattleMove.Spock, "Paper disproves Spock")]
    [InlineData(BattleMove.Spock, BattleMove.Rock, "Spock vaporizes Rock")]
    [InlineData(BattleMove.Rock, BattleMove.Scissors, "Rock crushes Scissors")]
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
