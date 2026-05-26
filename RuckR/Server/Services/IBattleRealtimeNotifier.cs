using RuckR.Shared.Models;

namespace RuckR.Server.Services;

/// <summary>Publishes battle state changes to connected clients.</summary>
public interface IBattleRealtimeNotifier
{
    /// <summary>Notify both users that a new challenge exists.</summary>
    /// <param name="battleId">Battle identifier.</param>
    Task NotifyChallengeCreatedAsync(int battleId);

    /// <summary>Notify both users that a battle changed.</summary>
    /// <param name="battleId">Battle identifier.</param>
    /// <param name="result">Optional freshly computed battle result.</param>
    Task NotifyBattleChangedAsync(int battleId, BattleResult? result = null);

    /// <summary>Notify both users that a pending challenge was declined.</summary>
    /// <param name="battleId">Battle identifier.</param>
    Task NotifyChallengeDeclinedAsync(int battleId);
}
