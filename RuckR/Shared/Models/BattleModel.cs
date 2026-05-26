using System;
using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Represents a head-to-head challenge between two users for a battle.
    /// </summary>
    public class BattleModel
    {
        /// <summary>Primary key for the battle record.</summary>
        [Key]
        public int Id { get; set; }

        /// <summary>Unique identifier of the user issuing the challenge.</summary>
        [Required]
        public required string ChallengerId { get; set; }

        /// <summary>Unique identifier of the challenged user.</summary>
        [Required]
        public required string OpponentId { get; set; }

        /// <summary>Recruit/player-card id selected by the challenger after the challenge is accepted.</summary>
        public int? ChallengerPlayerId { get; set; }

        /// <summary>Recruit/player-card id selected by the opponent after the challenge is accepted.</summary>
        public int? OpponentPlayerId { get; set; }

        /// <summary>Current lifecycle state of the battle.</summary>
        public BattleStatus Status { get; set; } = BattleStatus.Pending;

        /// <summary>Hidden rugby play selected by the challenger.</summary>
        public BattleMove? ChallengerMove { get; set; }

        /// <summary>Hidden rugby play selected by the opponent.</summary>
        public BattleMove? OpponentMove { get; set; }

        /// <summary>Timestamp when the opponent accepted the challenge.</summary>
        public DateTime? AcceptedAt { get; set; }

        /// <summary>Timestamp when the challenger submitted recruit and move.</summary>
        public DateTime? ChallengerSubmittedAt { get; set; }

        /// <summary>Timestamp when the opponent submitted recruit and move.</summary>
        public DateTime? OpponentSubmittedAt { get; set; }

        /// <summary>Final challenger score when the battle has resolved.</summary>
        public double? ChallengerScore { get; set; }

        /// <summary>Final opponent score when the battle has resolved.</summary>
        public double? OpponentScore { get; set; }

        /// <summary>Human-readable resolution method once completed.</summary>
        [MaxLength(200)]
        public string? ResolutionMethod { get; set; }

        /// <summary>Winner user identifier when the battle has been resolved.</summary>
        [MaxLength(450)]
        public string? WinnerId { get; set; }

        /// <summary>Timestamp when the battle challenge was created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Timestamp when the battle reached a terminal state.</summary>
        public DateTime? ResolvedAt { get; set; }

        /// <summary>
        /// Concurrency token for optimistic concurrency — prevents race conditions
        /// when two users attempt to accept the same challenge simultaneously.
        /// </summary>
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        /// <summary>
        /// Client-generated idempotency key (GUID) to prevent duplicate challenges
        /// from network retries.
        /// </summary>
        [MaxLength(36)]
        public string? IdempotencyKey { get; set; }
    }
}
