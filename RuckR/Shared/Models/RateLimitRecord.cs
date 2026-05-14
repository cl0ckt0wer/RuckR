using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    public class RateLimitRecord
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}