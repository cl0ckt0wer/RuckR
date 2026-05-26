using RuckR.Client.Store.BattleFeature;
using RuckR.Shared.Models;

namespace RuckR.Tests.ComponentTests;

public class BattleReducersTests
{
    [Fact]
    public void BattleUpdated_ReplacesActiveBattleForLiveSelectionState()
    {
        var pending = Battle(42, BattleStatus.Pending);
        var accepted = Battle(42, BattleStatus.Accepted, challengerSubmitted: true);
        var state = new BattleState { ActiveChallenges = [pending] };

        var next = BattleReducers.ReduceBattleUpdated(state, new BattleUpdatedAction(accepted));

        var active = Assert.Single(next.ActiveChallenges);
        Assert.Equal(BattleStatus.Accepted, active.Status);
        Assert.True(active.ChallengerSubmitted);
        Assert.Empty(next.BattleHistory);
    }

    [Fact]
    public void BattleUpdated_MovesCompletedBattleToHistory()
    {
        var accepted = Battle(42, BattleStatus.Accepted, challengerSubmitted: true, opponentSubmitted: true);
        var completed = Battle(42, BattleStatus.Completed, challengerSubmitted: true, opponentSubmitted: true, withResult: true);
        var state = new BattleState { ActiveChallenges = [accepted] };

        var next = BattleReducers.ReduceBattleUpdated(state, new BattleUpdatedAction(completed));

        Assert.Empty(next.ActiveChallenges);
        var history = Assert.Single(next.BattleHistory);
        Assert.Equal(BattleStatus.Completed, history.Status);
        Assert.NotNull(history.Result);
    }

    private static BattleSummaryDto Battle(
        int id,
        BattleStatus status,
        bool challengerSubmitted = false,
        bool opponentSubmitted = false,
        bool withResult = false)
    {
        var result = withResult
            ? new BattleResult(
                "alice",
                "bob",
                "A recruit",
                "B recruit",
                "Rock crushes Scissors",
                DateTime.UtcNow,
                BattleMove.Rock,
                BattleMove.Scissors,
                90,
                40,
                BattleMove.Rock,
                BattleMove.Scissors,
                90,
                40)
            : null;

        return new BattleSummaryDto(
            id,
            status,
            "user-a",
            "alice",
            "user-b",
            "bob",
            challengerSubmitted ? 1 : null,
            opponentSubmitted ? 2 : null,
            null,
            null,
            withResult ? "user-a" : null,
            withResult ? "alice" : null,
            result,
            DateTime.UtcNow,
            withResult ? DateTime.UtcNow : null,
            null,
            status == BattleStatus.Accepted ? DateTime.UtcNow : null,
            challengerSubmitted,
            opponentSubmitted,
            challengerSubmitted ? BattleMove.Rock : null,
            opponentSubmitted ? BattleMove.Scissors : null,
            challengerSubmitted ? DateTime.UtcNow : null,
            opponentSubmitted ? DateTime.UtcNow : null,
            withResult ? 90 : null,
            withResult ? 40 : null);
    }
}
