using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side interface IBattleResolver.</summary>
    public interface IBattleResolver
    {
        /// <summary>Resolves a battle between two selected recruits and returns the outcome.</summary>
        /// <param name="challengerPlayer">Recruit selected by the challenger.</param>
        /// <param name="opponentPlayer">Recruit selected by the opponent.</param>
        /// <param name="challengerUserId">Stable user id of the challenger.</param>
        /// <param name="opponentUserId">Stable user id of the opponent.</param>
        /// <param name="challengerUsername">Display username of the challenger.</param>
        /// <param name="opponentUsername">Display username of the opponent.</param>
        /// <param name="challengerMove">Hidden rugby play selected by the challenger.</param>
        /// <param name="opponentMove">Hidden rugby play selected by the opponent.</param>
        /// <param name="battleId">Battle identifier used for deterministic final tiebreaks.</param>
        /// <returns>Computed battle result payload.</returns>
        BattleResult Resolve(
            PlayerModel challengerPlayer,
            PlayerModel opponentPlayer,
            string challengerUserId,
            string opponentUserId,
            string challengerUsername,
            string opponentUsername,
            BattleMove challengerMove,
            BattleMove opponentMove,
            int battleId);
    }
}

