using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side interface IBattleResolver.</summary>
    public interface IBattleResolver
    {
        /// <summary>Resolves a battle between two players and returns the outcome.</summary>
        /// <param name="challengerPlayer">Player selected by the challenger.</param>
        /// <param name="opponentPlayer">Player selected by the opponent.</param>
        /// <param name="challengerUsername">Display username of the challenger.</param>
        /// <param name="opponentUsername">Display username of the opponent.</param>
        /// <param name="seed">Optional deterministic seed for reproducible outcomes.</param>
        /// <returns>Computed battle result payload.</returns>
        BattleResult Resolve(PlayerModel challengerPlayer, PlayerModel opponentPlayer, string challengerUsername, string opponentUsername, int? seed = null);
    }
}

