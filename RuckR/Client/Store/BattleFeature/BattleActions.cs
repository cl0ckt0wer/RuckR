using RuckR.Shared.Models;

namespace RuckR.Client.Store.BattleFeature;

/// <summary>
/// Signals that a battle challenge has been received by the current user.
/// </summary>
/// <param name="Challenge">Challenge payload.</param>
public record ChallengeReceivedAction(BattleModel Challenge);

/// <summary>
/// Signals that a battle challenge has been sent by the current user.
/// </summary>
/// <param name="Challenge">Challenge payload.</param>
public record ChallengeSentAction(BattleModel Challenge);

/// <summary>
/// Signals a battle status update from the server or UI.
/// </summary>
/// <param name="BattleId">Identifier of the affected battle.</param>
/// <param name="NewStatus">New battle status value.</param>
public record ChallengeRespondedAction(int BattleId, BattleStatus NewStatus);

/// <summary>
/// Signals completion of a battle and final battle payload.
/// </summary>
/// <param name="Battle">Completed battle.</param>
public record BattleCompletedAction(BattleModel Battle);

/// <summary>
/// Replaces battle collections after a fetch operation.
/// </summary>
/// <param name="Pending">Pending battles.</param>
/// <param name="History">Completed battle history.</param>
public record FetchBattlesResultAction(IReadOnlyList<BattleModel> Pending, IReadOnlyList<BattleModel> History);

/// <summary>
/// Signals an error while loading or updating battle data.
/// </summary>
/// <param name="ErrorMessage">Error detail text.</param>
public record BattleErrorAction(string ErrorMessage);
