using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace RuckR.Shared.Models
{
    public class PlayerModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public PlayerPosition Position { get; set; }

        [MaxLength(100)]
        public string? Team { get; set; }

        [Range(1, 99)]
        public int Speed { get; set; }

        [Range(1, 99)]
        public int Strength { get; set; }

        [Range(1, 99)]
        public int Agility { get; set; }

        [Range(1, 99)]
        public int Kicking { get; set; }

        public PlayerRarity Rarity { get; set; }

        [Range(1, 100)]
        public int Level { get; set; } = 1;

        /// <summary>
        /// Spawn location as a geography point (SRID 4326).
        /// X = longitude, Y = latitude. Nullable — players may not have a spawn location yet.
        /// Excluded from JSON serialization due to NetTopologySuite geometry cycle.
        /// Use /players/nearby endpoint for spatial queries.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public Point? SpawnLocation { get; set; }

        [MaxLength(500)]
        public string? Bio { get; set; }
    }
}
