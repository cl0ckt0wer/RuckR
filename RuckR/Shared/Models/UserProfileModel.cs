using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Persistent profile data persisted per authentication user.
    /// </summary>
    public class UserProfileModel
    {
        [Key]
        [Required]
        [MaxLength(450)]
        /// <summary>User identifier for this profile.</summary>
        public string UserId { get; set; } = string.Empty;

        [MaxLength(200)]
        /// <summary>Display name.</summary>
        public string? Name { get; set; }

        [MaxLength(1000)]
        /// <summary>Profile biography.</summary>
        public string? Biography { get; set; }

        [MaxLength(500)]
        /// <summary>User-specified location text.</summary>
        public string? Location { get; set; }

        [Url]
        [MaxLength(500)]
        /// <summary>Avatar image URL.</summary>
        public string? AvatarUrl { get; set; }

        /// <summary>Date when profile was created.</summary>
        public DateTime JoinedDate { get; set; } = DateTime.UtcNow;
    }
}
