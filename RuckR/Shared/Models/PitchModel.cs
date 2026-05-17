using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Represents a playable rugby pitch discovered or created by users.
    /// </summary>
    public class PitchModel
    {
        /// <summary>Primary key for the pitch.</summary>
        [Key]
        public int Id { get; set; }

        /// <summary>Pitch display name.</summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Geography point (SRID 4326). X = longitude, Y = latitude.
        /// Excluded from JSON serialization due to NetTopologySuite geometry cycle.
        /// Use /pitches/nearby endpoint for spatial queries.
        /// </summary>
        /// <summary>Stored geography point used for spatial queries.</summary>
        [Required]
        [JsonIgnore]
        public Point Location { get; set; } = null!;

        /// <summary>Latitude — populated from Location at query time, serialized for the client.</summary>
        /// <summary>Latitude projected from <see cref="Location"/>.</summary>
        [NotMapped]
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        /// <summary>Longitude — populated from Location at query time, serialized for the client.</summary>
        /// <summary>Longitude projected from <see cref="Location"/>.</summary>
        [NotMapped]
        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        /// <summary>Creator user identifier.</summary>
        [Required]
        public string CreatorUserId { get; set; } = string.Empty;

        /// <summary>Pitch type category.</summary>
        public PitchType Type { get; set; }

        /// <summary>Source system for this pitch (Manual or external).</summary>
        [MaxLength(50)]
        public string Source { get; set; } = "Manual";

        /// <summary>Optional external place identifier.</summary>
        [MaxLength(128)]
        public string? ExternalPlaceId { get; set; }

        /// <summary>Optional source category label.</summary>
        [MaxLength(200)]
        public string? SourceCategory { get; set; }

        /// <summary>Optional source match rationale.</summary>
        [MaxLength(200)]
        public string? SourceMatchReason { get; set; }

        /// <summary>Optional confidence score for candidate source matches.</summary>
        public int? SourceConfidence { get; set; }

        /// <summary>Timestamp when the pitch was created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
