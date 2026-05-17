using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
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
        [JsonIgnore]
        public Point Location { get; set; } = null!;

        /// <summary>Latitude — populated from Location at query time, serialized for the client.</summary>
        [NotMapped]
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        /// <summary>Longitude — populated from Location at query time, serialized for the client.</summary>
        [NotMapped]
        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [Required]
        public string CreatorUserId { get; set; } = string.Empty;

        public PitchType Type { get; set; }

        [MaxLength(50)]
        public string Source { get; set; } = "Manual";

        [MaxLength(128)]
        public string? ExternalPlaceId { get; set; }

        [MaxLength(200)]
        public string? SourceCategory { get; set; }

        [MaxLength(200)]
        public string? SourceMatchReason { get; set; }

        public int? SourceConfidence { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
