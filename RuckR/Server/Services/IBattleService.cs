using System.Threading.Tasks;
using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    public interface IBattleService
    {
        int MaxChallengesPerHour { get; }
        int MaxPendingChallenges { get; }
        System.TimeSpan ChallengeExpiryDuration { get; }

        Task<BattleResult> ResolveBattleAsync(
            PlayerModel challengerPlayer,
            PlayerModel opponentPlayer,
            string challengerUsername,
            string opponentUsername);

        Task<BattleResult> ResolveBattlePureAsync(
            PlayerModel challengerPlayer,
            PlayerModel opponentPlayer,
            string challengerUsername,
            string opponentUsername);
    }
}