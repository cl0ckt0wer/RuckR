using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Persistent profile data persisted per authentication user.
    /// </summary>
    public class UserProfileModel
    {
        /// <summary>User identifier for this profile.</summary>
        [Key]
        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>Display name.</summary>
        [MaxLength(200)]
        public string? Name { get; set; }

        /// <summary>Profile biography.</summary>
        [MaxLength(1000)]
        public string? Biography { get; set; }

        /// <summary>User-specified location text.</summary>
        [MaxLength(500)]
        public string? Location { get; set; }

        /// <summary>Avatar image URL.</summary>
        [Url]
        [MaxLength(500)]
        public string? AvatarUrl { get; set; }

        /// <summary>Date when profile was created.</summary>
        public DateTime JoinedDate { get; set; } = DateTime.UtcNow;
    }
}
