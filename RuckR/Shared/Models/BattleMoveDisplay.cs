using System.Collections.Generic;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Player-facing rugby branding for the persisted battle move enum.
    /// </summary>
    public static class BattleMoveDisplay
    {
        private static readonly Dictionary<(BattleMove Winner, BattleMove Loser), string> WinningMoveMethods = new()
        {
            { (BattleMove.Scissors, BattleMove.Paper), "Grubber Kick splits Cut-Out Pass" },
            { (BattleMove.Paper, BattleMove.Rock), "Cut-Out Pass finds space around Crash Ball" },
            { (BattleMove.Rock, BattleMove.Lizard), "Crash Ball flattens Sidestep" },
            { (BattleMove.Lizard, BattleMove.Spock), "Sidestep slips Scrum Drive" },
            { (BattleMove.Spock, BattleMove.Scissors), "Scrum Drive swallows Grubber Kick" },
            { (BattleMove.Scissors, BattleMove.Lizard), "Grubber Kick catches Sidestep" },
            { (BattleMove.Lizard, BattleMove.Paper), "Sidestep breaks Cut-Out Pass" },
            { (BattleMove.Paper, BattleMove.Spock), "Cut-Out Pass stretches Scrum Drive" },
            { (BattleMove.Spock, BattleMove.Rock), "Scrum Drive rolls over Crash Ball" },
            { (BattleMove.Rock, BattleMove.Scissors), "Crash Ball charges down Grubber Kick" }
        };

        /// <summary>
        /// Gets the rugby move label shown to players.
        /// </summary>
        /// <param name="move">Persisted move enum value.</param>
        /// <returns>Rugby move label.</returns>
        public static string Name(BattleMove move) => move switch
        {
            BattleMove.Rock => "Crash Ball",
            BattleMove.Paper => "Cut-Out Pass",
            BattleMove.Scissors => "Grubber Kick",
            BattleMove.Lizard => "Sidestep",
            BattleMove.Spock => "Scrum Drive",
            _ => move.ToString()
        };

        /// <summary>
        /// Determines whether one move beats another.
        /// </summary>
        /// <param name="attacker">Candidate winning move.</param>
        /// <param name="defender">Candidate losing move.</param>
        /// <returns><c>true</c> when the attacker beats the defender.</returns>
        public static bool Beats(BattleMove attacker, BattleMove defender) =>
            WinningMoveMethods.ContainsKey((attacker, defender));

        /// <summary>
        /// Gets the branded resolution text for two submitted moves.
        /// </summary>
        /// <param name="winnerMove">Winning move.</param>
        /// <param name="loserMove">Losing move.</param>
        /// <returns>Player-facing battle method text.</returns>
        public static string ResolutionMethod(BattleMove winnerMove, BattleMove loserMove)
        {
            if (winnerMove == loserMove)
                return "Same move, recruit power wins";

            if (WinningMoveMethods.TryGetValue((winnerMove, loserMove), out var method))
                return method;

            return "Recruit power overcomes move disadvantage";
        }

        /// <summary>
        /// Formats a move and optional score for result surfaces.
        /// </summary>
        /// <param name="move">Submitted move, when available.</param>
        /// <param name="score">Resolved score, when available.</param>
        /// <param name="emptyText">Text to return when neither move nor score is available.</param>
        /// <returns>Player-facing move and score text.</returns>
        public static string Score(BattleMove? move, double? score, string emptyText = "selection")
        {
            if (move is null && score is null)
                return emptyText;

            var moveName = move.HasValue ? Name(move.Value) : emptyText;
            return score is null ? moveName : $"{moveName}: {score:0.##}";
        }
    }
}
