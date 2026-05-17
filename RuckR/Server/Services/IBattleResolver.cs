using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side interface IBattleResolver.</summary>
    public interface IBattleResolver
    {
        BattleResult Resolve(PlayerModel challengerPlayer, PlayerModel opponentPlayer, string challengerUsername, string opponentUsername, int? seed = null);
    }
}

