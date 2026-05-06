using System;
using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    public class CollectionModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public required string UserId { get; set; }

        [Required]
        public int PlayerId { get; set; }

        public PlayerModel? Player { get; set; }

        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

        public bool IsFavorite { get; set; }

        public int? CapturedAtPitchId { get; set; }

        // NOTE: A unique index on (UserId, PlayerId) is enforced at the database level
        // (see OnModelCreating in W2-01). This ensures each user can capture a given
        // player creature only once — the CollectionModel is the single source of truth
        // for player capture; PlayerModel has no CapturedByUserId.
    }
}
