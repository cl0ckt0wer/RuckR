using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Resolves one-round hidden rugby move battles.</summary>
    public class BattleResolver : IBattleResolver
    {
        private const double MoveWinBonus = 25.0;
        private const double MoveLossPenalty = -25.0;

        private static readonly Dictionary<PlayerRarity, double> RarityBonuses = new()
        {
            { PlayerRarity.Common, 0.0 },
            { PlayerRarity.Uncommon, 5.0 },
            { PlayerRarity.Rare, 10.0 },
            { PlayerRarity.Epic, 15.0 },
            { PlayerRarity.Legendary, 20.0 }
        };

        /// <inheritdoc />
        public BattleResult Resolve(
            PlayerModel challengerPlayer,
            PlayerModel opponentPlayer,
            string challengerUserId,
            string opponentUserId,
            string challengerUsername,
            string opponentUsername,
            BattleMove challengerMove,
            BattleMove opponentMove,
            int battleId)
        {
            var challengerMoveBonus = GetMoveBonus(challengerMove, opponentMove);
            var opponentMoveBonus = GetMoveBonus(opponentMove, challengerMove);
            var challengerScore = GetRecruitPower(challengerPlayer) + challengerMoveBonus;
            var opponentScore = GetRecruitPower(opponentPlayer) + opponentMoveBonus;

            var challengerWins = DetermineChallengerWins(
                challengerPlayer,
                opponentPlayer,
                challengerScore,
                opponentScore,
                challengerUserId,
                opponentUserId,
                battleId);

            var winner = challengerWins ? challengerPlayer : opponentPlayer;
            var loser = challengerWins ? opponentPlayer : challengerPlayer;
            var winnerUsername = challengerWins ? challengerUsername : opponentUsername;
            var loserUsername = challengerWins ? opponentUsername : challengerUsername;
            var winnerMove = challengerWins ? challengerMove : opponentMove;
            var loserMove = challengerWins ? opponentMove : challengerMove;
            var winnerScore = challengerWins ? challengerScore : opponentScore;
            var loserScore = challengerWins ? opponentScore : challengerScore;

            return new BattleResult(
                WinnerUsername: winnerUsername,
                LoserUsername: loserUsername,
                WinnerPlayerName: winner.Name,
                LoserPlayerName: loser.Name,
                Method: DetermineMethod(winnerMove, loserMove),
                CreatedAt: DateTime.UtcNow,
                WinnerMove: winnerMove,
                LoserMove: loserMove,
                WinnerScore: Math.Round(winnerScore, 2),
                LoserScore: Math.Round(loserScore, 2),
                ChallengerMove: challengerMove,
                OpponentMove: opponentMove,
                ChallengerScore: Math.Round(challengerScore, 2),
                OpponentScore: Math.Round(opponentScore, 2));
        }

        private static double GetRecruitPower(PlayerModel player)
        {
            var statAverage = (player.Speed + player.Strength + player.Agility + player.Kicking) / 4.0;
            return statAverage + (player.Level * 2.0) + RarityBonuses[player.Rarity];
        }

        private static double GetMoveBonus(BattleMove attacker, BattleMove defender)
        {
            if (attacker == defender)
                return 0.0;

            return BattleMoveDisplay.Beats(attacker, defender)
                ? MoveWinBonus
                : MoveLossPenalty;
        }

        private static bool DetermineChallengerWins(
            PlayerModel challengerPlayer,
            PlayerModel opponentPlayer,
            double challengerScore,
            double opponentScore,
            string challengerUserId,
            string opponentUserId,
            int battleId)
        {
            if (challengerScore > opponentScore)
                return true;
            if (opponentScore > challengerScore)
                return false;

            if (challengerPlayer.Rarity != opponentPlayer.Rarity)
                return challengerPlayer.Rarity > opponentPlayer.Rarity;
            if (challengerPlayer.Level != opponentPlayer.Level)
                return challengerPlayer.Level > opponentPlayer.Level;

            var challengerStatTotal = challengerPlayer.Speed + challengerPlayer.Strength + challengerPlayer.Agility + challengerPlayer.Kicking;
            var opponentStatTotal = opponentPlayer.Speed + opponentPlayer.Strength + opponentPlayer.Agility + opponentPlayer.Kicking;
            if (challengerStatTotal != opponentStatTotal)
                return challengerStatTotal > opponentStatTotal;

            var challengerTieKey = $"{battleId}:{challengerUserId}";
            var opponentTieKey = $"{battleId}:{opponentUserId}";
            return StringComparer.Ordinal.Compare(challengerTieKey, opponentTieKey) <= 0;
        }

        private static string DetermineMethod(BattleMove winnerMove, BattleMove loserMove)
        {
            return BattleMoveDisplay.ResolutionMethod(winnerMove, loserMove);
        }
    }
}
