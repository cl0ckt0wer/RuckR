using System;
using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Represents a player creature captured by a user.
    /// </summary>
    public class CollectionModel
    {
        /// <summary>Primary key for the collection record.</summary>
        [Key]
        public int Id { get; set; }

        /// <summary>User identity associated with the captured player.</summary>
        [Required]
        public required string UserId { get; set; }

        /// <summary>Captured player identifier.</summary>
        [Required]
        public int PlayerId { get; set; }

        /// <summary>Navigation to the captured player.</summary>
        public PlayerModel? Player { get; set; }

        /// <summary>UTC timestamp when the player was captured.</summary>
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Indicates whether this captured player is marked as a favorite.</summary>
        public bool IsFavorite { get; set; }

        /// <summary>Optional pitch identifier where capture occurred.</summary>
        public int? CapturedAtPitchId { get; set; }

        // NOTE: A unique index on (UserId, PlayerId) is enforced at the database level
        // (see OnModelCreating in W2-01). This ensures each user can capture a given
        // player creature only once — the CollectionModel is the single source of truth
        // for player capture; PlayerModel has no CapturedByUserId.
    }
}
