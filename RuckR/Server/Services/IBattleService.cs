using System.Threading.Tasks;
using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side interface IBattleService.</summary>
    public interface IBattleService
    {
        /// <summary>Maximum number of challenges a user may send per rolling hour.</summary>
        int MaxChallengesPerHour { get; }
        /// <summary>Maximum number of pending outgoing challenges allowed per user.</summary>
        int MaxPendingChallenges { get; }
        /// <summary>Duration after which an unresolved challenge is treated as expired.</summary>
        System.TimeSpan ChallengeExpiryDuration { get; }

        /// <summary>Resolves a battle and applies runtime business rules.</summary>
        /// <param name="challengerPlayer">Player selected by the challenger.</param>
        /// <param name="opponentPlayer">Player selected by the opponent.</param>
        /// <param name="challengerUsername">Display username of the challenger.</param>
        /// <param name="opponentUsername">Display username of the opponent.</param>
        /// <returns>Resolved battle outcome.</returns>
        Task<BattleResult> ResolveBattleAsync(
            PlayerModel challengerPlayer,
            PlayerModel opponentPlayer,
            string challengerUsername,
            string opponentUsername);

        /// <summary>Resolves a battle using deterministic pure logic without external side effects.</summary>
        /// <param name="challengerPlayer">Player selected by the challenger.</param>
        /// <param name="opponentPlayer">Player selected by the opponent.</param>
        /// <param name="challengerUsername">Display username of the challenger.</param>
        /// <param name="opponentUsername">Display username of the opponent.</param>
        /// <returns>Resolved battle outcome.</returns>
        Task<BattleResult> ResolveBattlePureAsync(
            PlayerModel challengerPlayer,
            PlayerModel opponentPlayer,
            string challengerUsername,
            string opponentUsername);
    }
}
