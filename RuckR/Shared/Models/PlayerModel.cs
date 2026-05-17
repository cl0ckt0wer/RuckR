using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Represents a collectible rugby-themed player creature.
    /// </summary>
    public class PlayerModel
    {
        /// <summary>Primary key for the player.</summary>
        [Key]
        public int Id { get; set; }

        /// <summary>Display name for the player creature.</summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Primary playing position.</summary>
        public PlayerPosition Position { get; set; }

        /// <summary>Optional team designation.</summary>
        [MaxLength(100)]
        public string? Team { get; set; }

        /// <summary>Speed stat used for encounter and capture calculations.</summary>
        [Range(1, 99)]
        public int Speed { get; set; }

        /// <summary>Strength stat.</summary>
        [Range(1, 99)]
        public int Strength { get; set; }

        /// <summary>Agility stat.</summary>
        [Range(1, 99)]
        public int Agility { get; set; }

        /// <summary>Kicking stat.</summary>
        [Range(1, 99)]
        public int Kicking { get; set; }

        /// <summary>Creature rarity tier.</summary>
        public PlayerRarity Rarity { get; set; }

        /// <summary>Current player level.</summary>
        [Range(1, 100)]
        public int Level { get; set; } = 1;

        /// <summary>
        /// Spawn location as a geography point (SRID 4326).
        /// X = longitude, Y = latitude. Nullable — players may not have a spawn location yet.
        /// Excluded from JSON serialization due to NetTopologySuite geometry cycle.
        /// Use /players/nearby endpoint for spatial queries.
        /// </summary>
        /// <summary>Spawn location as a geography point.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public Point? SpawnLocation { get; set; }

        /// <summary>Optional flavor text.</summary>
        [MaxLength(500)]
        public string? Bio { get; set; }
    }
}
