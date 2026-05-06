using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    public interface IBattleResolver
    {
        BattleResult Resolve(PlayerModel challengerPlayer, PlayerModel opponentPlayer, string challengerUsername, string opponentUsername, int? seed = null);
    }
}
