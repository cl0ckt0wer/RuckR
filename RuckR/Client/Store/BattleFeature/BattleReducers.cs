using Fluxor;
using RuckR.Shared.Models;

namespace RuckR.Client.Store.BattleFeature;

/// <summary>
/// Reducers for battle-related state transitions.
/// </summary>
public static class BattleReducers
{
    [ReducerMethod]
    /// <summary>
    /// Adds a newly received challenge to active challenges.
    /// </summary>
    /// <param name="state">Current battle state.</param>
    /// <param name="action">Action containing the new challenge.</param>
    /// <returns>Updated battle state.</returns>
    public static BattleState ReduceChallengeReceived(BattleState state, ChallengeReceivedAction action) =>
        state with { ActiveChallenges = state.ActiveChallenges.Append(action.Challenge).ToList() };

    [ReducerMethod]
    /// <summary>
    /// Adds a sent challenge to active challenges.
    /// </summary>
    /// <param name="state">Current battle state.</param>
    /// <param name="action">Action containing the sent challenge.</param>
    /// <returns>Updated battle state.</returns>
    public static BattleState ReduceChallengeSent(BattleState state, ChallengeSentAction action) =>
        state with { ActiveChallenges = state.ActiveChallenges.Append(action.Challenge).ToList() };

    [ReducerMethod]
    /// <summary>
    /// Updates challenge status and moves completed/closed battles into history.
    /// </summary>
    /// <param name="state">Current battle state.</param>
    /// <param name="action">Action carrying the challenge status change.</param>
    /// <returns>Updated battle state.</returns>
    public static BattleState ReduceChallengeResponded(BattleState state, ChallengeRespondedAction action)
    {
        var list = state.ActiveChallenges.ToList();
        var idx = list.FindIndex(b => b.Id == action.BattleId);
        if (idx >= 0)
            list[idx] = CloneWithStatus(list[idx], action.NewStatus);

        // If completed/declined/expired, move from active to history
        if (action.NewStatus == BattleStatus.Completed
            || action.NewStatus == BattleStatus.Declined
            || action.NewStatus == BattleStatus.Expired)
        {
            var completed = list[idx];
            list.RemoveAt(idx);
            return state with
            {
                ActiveChallenges = list,
                BattleHistory = state.BattleHistory.Prepend(completed).ToList()
            };
        }

        return state with { ActiveChallenges = list };
    }

    [ReducerMethod]
    /// <summary>
    /// Moves a resolved battle from active list to battle history.
    /// </summary>
    /// <param name="state">Current battle state.</param>
    /// <param name="action">Action carrying resolved battle details.</param>
    /// <returns>Updated battle state.</returns>
    public static BattleState ReduceBattleCompleted(BattleState state, BattleCompletedAction action) =>
        state with
        {
            ActiveChallenges = state.ActiveChallenges.Where(b => b.Id != action.Battle.Id).ToList(),
            BattleHistory = state.BattleHistory.Prepend(action.Battle).ToList()
        };

    [ReducerMethod]
    /// <summary>
    /// Replaces battle lists and clears pending loading state.
    /// </summary>
    /// <param name="state">Current battle state.</param>
    /// <param name="action">Action containing fetched battles.</param>
    /// <returns>Updated battle state.</returns>
    public static BattleState ReduceFetchBattlesResult(BattleState state, FetchBattlesResultAction action) =>
        state with
        {
            IsLoading = false,
            ActiveChallenges = action.Pending,
            BattleHistory = action.History
        };

    [ReducerMethod]
    /// <summary>
    /// Marks battle flow as errored.
    /// </summary>
    /// <param name="state">Current battle state.</param>
    /// <param name="action">Action carrying the error message.</param>
    /// <returns>Updated battle state.</returns>
    public static BattleState ReduceBattleError(BattleState state, BattleErrorAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    private static BattleModel CloneWithStatus(BattleModel source, BattleStatus newStatus) =>
        new BattleModel
        {
            Id = source.Id,
            ChallengerId = source.ChallengerId,
            OpponentId = source.OpponentId,
            ChallengerPlayerId = source.ChallengerPlayerId,
            OpponentPlayerId = source.OpponentPlayerId,
            Status = newStatus,
            WinnerId = source.WinnerId,
            CreatedAt = source.CreatedAt,
            ResolvedAt = source.ResolvedAt,
            RowVersion = source.RowVersion
        };
}
