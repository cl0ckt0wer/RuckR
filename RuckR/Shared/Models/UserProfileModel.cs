using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    public class UserProfileModel
    {
        [Key]
        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(1000)]
        public string? Biography { get; set; }

        [MaxLength(500)]
        public string? Location { get; set; }

        [Url]
        [MaxLength(500)]
        public string? AvatarUrl { get; set; }

        public DateTime JoinedDate { get; set; } = DateTime.UtcNow;
    }
}