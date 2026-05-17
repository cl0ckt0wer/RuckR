using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Represents a collectible rugby-themed player creature.
    /// </summary>
    public class PlayerModel
    {
        [Key]
        /// <summary>Primary key for the player.</summary>
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        /// <summary>Display name for the player creature.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Primary playing position.</summary>
        public PlayerPosition Position { get; set; }

        [MaxLength(100)]
        /// <summary>Optional team designation.</summary>
        public string? Team { get; set; }

        [Range(1, 99)]
        /// <summary>Speed stat used for encounter and capture calculations.</summary>
        public int Speed { get; set; }

        [Range(1, 99)]
        /// <summary>Strength stat.</summary>
        public int Strength { get; set; }

        [Range(1, 99)]
        /// <summary>Agility stat.</summary>
        public int Agility { get; set; }

        [Range(1, 99)]
        /// <summary>Kicking stat.</summary>
        public int Kicking { get; set; }

        /// <summary>Creature rarity tier.</summary>
        public PlayerRarity Rarity { get; set; }

        [Range(1, 100)]
        /// <summary>Current player level.</summary>
        public int Level { get; set; } = 1;

        /// <summary>
        /// Spawn location as a geography point (SRID 4326).
        /// X = longitude, Y = latitude. Nullable — players may not have a spawn location yet.
        /// Excluded from JSON serialization due to NetTopologySuite geometry cycle.
        /// Use /players/nearby endpoint for spatial queries.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        /// <summary>Spawn location as a geography point.</summary>
        public Point? SpawnLocation { get; set; }

        [MaxLength(500)]
        /// <summary>Optional flavor text.</summary>
        public string? Bio { get; set; }
    }
}
