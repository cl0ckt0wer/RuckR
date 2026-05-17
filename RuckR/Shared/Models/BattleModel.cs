using System;
using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Represents a head-to-head challenge between two players for a battle.
    /// </summary>
    public class BattleModel
    {
        [Key]
        /// <summary>Primary key for the battle record.</summary>
        public int Id { get; set; }

        [Required]
        /// <summary>Unique identifier of the user issuing the challenge.</summary>
        public required string ChallengerId { get; set; }

        [Required]
        /// <summary>Unique identifier of the challenged user.</summary>
        public required string OpponentId { get; set; }

        [Required]
        /// <summary>Player id selected by the challenger.</summary>
        public int ChallengerPlayerId { get; set; }

        [Required]
        /// <summary>Player id selected by the opponent.</summary>
        public int OpponentPlayerId { get; set; }

        /// <summary>Current lifecycle state of the battle.</summary>
        public BattleStatus Status { get; set; } = BattleStatus.Pending;

        [MaxLength(450)]
        /// <summary>Winner user identifier when the battle has been resolved.</summary>
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
