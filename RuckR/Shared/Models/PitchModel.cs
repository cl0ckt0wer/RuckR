using System;
using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace RuckR.Shared.Models
{
    public class PitchModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Geography point (SRID 4326). X = longitude, Y = latitude.
        /// Excluded from JSON serialization due to NetTopologySuite geometry cycle.
        /// Use /pitches/nearby endpoint for spatial queries.
        /// </summary>
        [Required]
        [System.Text.Json.Serialization.JsonIgnore]
        public Point Location { get; set; } = null!;

        [Required]
        public string CreatorUserId { get; set; } = string.Empty;

        public PitchType Type { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
