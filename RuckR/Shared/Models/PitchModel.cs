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
        [Key]
        /// <summary>Primary key for the pitch.</summary>
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        /// <summary>Pitch display name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Geography point (SRID 4326). X = longitude, Y = latitude.
        /// Excluded from JSON serialization due to NetTopologySuite geometry cycle.
        /// Use /pitches/nearby endpoint for spatial queries.
        /// </summary>
        [Required]
        [JsonIgnore]
        /// <summary>Stored geography point used for spatial queries.</summary>
        public Point Location { get; set; } = null!;

        /// <summary>Latitude — populated from Location at query time, serialized for the client.</summary>
        [NotMapped]
        [JsonPropertyName("latitude")]
        /// <summary>Latitude projected from <see cref="Location"/>.</summary>
        public double Latitude { get; set; }

        /// <summary>Longitude — populated from Location at query time, serialized for the client.</summary>
        [NotMapped]
        [JsonPropertyName("longitude")]
        /// <summary>Longitude projected from <see cref="Location"/>.</summary>
        public double Longitude { get; set; }

        [Required]
        /// <summary>Creator user identifier.</summary>
        public string CreatorUserId { get; set; } = string.Empty;

        /// <summary>Pitch type category.</summary>
        public PitchType Type { get; set; }

        [MaxLength(50)]
        /// <summary>Source system for this pitch (Manual or external).</summary>
        public string Source { get; set; } = "Manual";

        [MaxLength(128)]
        /// <summary>Optional external place identifier.</summary>
        public string? ExternalPlaceId { get; set; }

        [MaxLength(200)]
        /// <summary>Optional source category label.</summary>
        public string? SourceCategory { get; set; }

        [MaxLength(200)]
        /// <summary>Optional source match rationale.</summary>
        public string? SourceMatchReason { get; set; }

        /// <summary>Optional confidence score for candidate source matches.</summary>
        public int? SourceConfidence { get; set; }

        /// <summary>Timestamp when the pitch was created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
