using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side battle workflow service.</summary>
    public interface IBattleService
    {
        /// <summary>Maximum number of challenges a user may send per rolling hour.</summary>
        int MaxChallengesPerHour { get; }

        /// <summary>Maximum number of pending outgoing challenges allowed per user.</summary>
        int MaxPendingChallenges { get; }

        /// <summary>Duration after which an unresolved challenge is treated as expired.</summary>
        TimeSpan ChallengeExpiryDuration { get; }

        /// <summary>Creates a user-first pending challenge.</summary>
        Task<BattleSummaryDto> CreateChallengeAsync(string challengerUserId, string opponentUsername, string? idempotencyKey = null);

        /// <summary>Accepts a pending challenge without resolving it.</summary>
        Task<BattleSummaryDto> AcceptChallengeAsync(int battleId, string opponentUserId);

        /// <summary>Submits the current user's recruit and hidden RPSLS move.</summary>
        Task<BattleSummaryDto> SubmitSelectionAsync(int battleId, string userId, int playerId, BattleMove move);

        /// <summary>Declines a pending challenge.</summary>
        Task DeclineChallengeAsync(int battleId, string opponentUserId);

        /// <summary>Builds a user-facing battle summary DTO.</summary>
        Task<BattleSummaryDto> ToSummaryAsync(BattleModel battle, string? viewerUserId = null, BattleResult? result = null);

        /// <summary>Builds user-facing battle summary DTOs.</summary>
        Task<IReadOnlyList<BattleSummaryDto>> ToSummariesAsync(IEnumerable<BattleModel> battles, string? viewerUserId = null);
    }
}
