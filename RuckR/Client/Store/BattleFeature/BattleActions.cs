using RuckR.Shared.Models;

namespace RuckR.Client.Store.BattleFeature;

public record ChallengeReceivedAction(BattleModel Challenge);
public record ChallengeSentAction(BattleModel Challenge);
public record ChallengeRespondedAction(int BattleId, BattleStatus NewStatus);
public record BattleCompletedAction(BattleModel Battle);
public record FetchBattlesResultAction(IReadOnlyList<BattleModel> Pending, IReadOnlyList<BattleModel> History);
public record BattleErrorAction(string ErrorMessage);
