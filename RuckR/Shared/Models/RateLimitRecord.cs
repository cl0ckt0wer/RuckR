using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Stores rate-limit events per user action for throttling enforcement.
    /// </summary>
    public class RateLimitRecord
    {
        /// <summary>Primary key.</summary>
        [Key]
        public long Id { get; set; }

        /// <summary>User identifier associated with the request.
        /// </summary>
        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>Action name being rate-limited.</summary>
        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        /// <summary>Request timestamp in UTC.</summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
