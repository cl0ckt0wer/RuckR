using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Stores rate-limit events per user action for throttling enforcement.
    /// </summary>
    public class RateLimitRecord
    {
        [Key]
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        [Required]
        [MaxLength(450)]
        /// <summary>User identifier associated with the request.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        /// <summary>Action name being rate-limited.</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>Request timestamp in UTC.</summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
