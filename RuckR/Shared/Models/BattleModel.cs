using System;
using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    public class BattleModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public required string ChallengerId { get; set; }

        [Required]
        public required string OpponentId { get; set; }

        [Required]
        public int ChallengerPlayerId { get; set; }

        [Required]
        public int OpponentPlayerId { get; set; }

        public BattleStatus Status { get; set; } = BattleStatus.Pending;

        [MaxLength(450)]
        public string? WinnerId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ResolvedAt { get; set; }

        /// <summary>
        /// Concurrency token for optimistic concurrency — prevents race conditions
        /// when two users attempt to accept the same challenge simultaneously.
        /// </summary>
        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
