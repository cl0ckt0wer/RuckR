using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side class BattleResolver.</summary>
    public class BattleResolver : IBattleResolver
    {
        private static readonly Dictionary<(PlayerPosition, PlayerPosition), double> PositionMultipliers = new()
        {
            // (attacker, defender) -> attacker's multiplier
            { (PlayerPosition.Prop, PlayerPosition.Wing), 1.2 },
            { (PlayerPosition.Wing, PlayerPosition.Prop), 0.85 },
            { (PlayerPosition.FlyHalf, PlayerPosition.ScrumHalf), 1.15 },
            { (PlayerPosition.Lock, PlayerPosition.Hooker), 1.1 },
            { (PlayerPosition.Flanker, PlayerPosition.FlyHalf), 1.1 },
        };

        private static readonly Dictionary<PlayerRarity, double> RarityMultipliers = new()
        {
            { PlayerRarity.Common, 1.0 },
            { PlayerRarity.Uncommon, 1.2 },
            { PlayerRarity.Rare, 1.5 },
            { PlayerRarity.Epic, 2.0 },
            { PlayerRarity.Legendary, 3.0 },
        };
        /// <summary>R es ol ve.</summary>
        /// <param name="challengerPlayer">The challengerplayer.</param>
        /// <param name="opponentPlayer">The opponentplayer.</param>
        /// <param name="challengerUsername">The challengerusername.</param>
        /// <param name="opponentUsername">The opponentusername.</param>
        /// <param name="seed">The seed.</param>
        /// <returns>The operation result.</returns>
        public BattleResult Resolve(
            PlayerModel challengerPlayer,
            PlayerModel opponentPlayer,
            string challengerUsername,
            string opponentUsername,
            int? seed = null)
        {
            var random = seed.HasValue ? new Random(seed.Value) : Random.Shared;

            // Base stat sums
            double challengerBase = (challengerPlayer.Speed + challengerPlayer.Strength + challengerPlayer.Agility + challengerPlayer.Kicking) / 4.0;
            double opponentBase = (opponentPlayer.Speed + opponentPlayer.Strength + opponentPlayer.Agility + opponentPlayer.Kicking) / 4.0;

            // Position multipliers
            double challengerPosMult = GetPositionMultiplier(challengerPlayer.Position, opponentPlayer.Position);
            double opponentPosMult = GetPositionMultiplier(opponentPlayer.Position, challengerPlayer.Position);

            // Rarity multipliers
            double challengerRarityMult = RarityMultipliers[challengerPlayer.Rarity];
            double opponentRarityMult = RarityMultipliers[opponentPlayer.Rarity];

            // Random factors
            double challengerRandom = 0.85 + (random.NextDouble() * 0.3);
            double opponentRandom = 0.85 + (random.NextDouble() * 0.3);

            // Final scores
            double challengerScore = challengerBase * challengerPosMult * challengerRarityMult * challengerRandom;
            double opponentScore = opponentBase * opponentPosMult * opponentRarityMult * opponentRandom;

            PlayerModel winner;
            PlayerModel loser;
            string winnerUsername;
            string loserUsername;

            if (challengerScore > opponentScore)
            {
                winner = challengerPlayer;
                loser = opponentPlayer;
                winnerUsername = challengerUsername;
                loserUsername = opponentUsername;
            }
            else if (opponentScore > challengerScore)
            {
                winner = opponentPlayer;
                loser = challengerPlayer;
                winnerUsername = opponentUsername;
                loserUsername = challengerUsername;
            }
            else
            {
                // Tiebreaker: random
                if (random.Next(2) == 0)
                {
                    winner = challengerPlayer;
                    loser = opponentPlayer;
                    winnerUsername = challengerUsername;
                    loserUsername = opponentUsername;
                }
                else
                {
                    winner = opponentPlayer;
                    loser = challengerPlayer;
                    winnerUsername = opponentUsername;
                    loserUsername = challengerUsername;
                }
            }

            string method = DetermineMethod(winner, loser);

            return new BattleResult(
                WinnerUsername: winnerUsername,
                LoserUsername: loserUsername,
                WinnerPlayerName: winner.Name,
                LoserPlayerName: loser.Name,
                Method: method,
                CreatedAt: DateTime.UtcNow);
        }

        private static double GetPositionMultiplier(PlayerPosition attacker, PlayerPosition defender)
        {
            if (PositionMultipliers.TryGetValue((attacker, defender), out double multiplier))
            {
                return multiplier;
            }

            return 1.0;
        }

        private static string DetermineMethod(PlayerModel winner, PlayerModel loser)
        {
            int speedDiff = winner.Speed - loser.Speed;
            int strengthDiff = winner.Strength - loser.Strength;
            int kickingDiff = winner.Kicking - loser.Kicking;
            int agilityDiff = winner.Agility - loser.Agility;

            if (speedDiff > 15)
            {
                return "Speed Advantage";
            }

            if (strengthDiff > 15 && (winner.Position == PlayerPosition.Prop || winner.Position == PlayerPosition.Lock))
            {
                return "Power Overwhelming";
            }

            if (strengthDiff > 15)
            {
                return "Strength Dominance";
            }

            if (kickingDiff > 15)
            {
                return "Kicking Mastery";
            }

            if (agilityDiff > 15)
            {
                return "Agility Outplay";
            }

            if ((int)winner.Rarity > (int)loser.Rarity)
            {
                return "Rarity Advantage";
            }

            return "Close Contest";
        }
    }
}

