using System;
using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Records explicit user consent for GPS data collection and processing.
    /// Required for GDPR/CCPA compliance before any location tracking begins.
    /// </summary>
    public class UserConsent
    {
        [Key]
        /// <summary>Primary key.</summary>
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        /// <summary>User identifier associated with this consent.</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// The specific purpose for which consent is given (e.g., "gps_location", "data_storage").
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Purpose { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp in UTC when consent was granted.
        /// </summary>
        public DateTime ConsentGivenAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// IP address of the user when consent was given, for audit purposes.
        /// </summary>
        [MaxLength(45)]
        public string? IpAddress { get; set; }
    }
}
