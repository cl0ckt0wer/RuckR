using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Editable player-facing profile fields. Authentication usernames are not edited here.
    /// </summary>
    public class UserProfileUpdateRequest
    {
        /// <summary>Player-facing display name shown in game UI.</summary>
        [Required]
        [StringLength(80, MinimumLength = 2)]
        public string? Name { get; set; }

        /// <summary>Short public player biography.</summary>
        [StringLength(1000)]
        public string? Biography { get; set; }

        /// <summary>Optional public location label.</summary>
        [StringLength(100)]
        public string? Location { get; set; }

        /// <summary>Optional public profile image URL.</summary>
        [ProfileAvatarUrl]
        [StringLength(500)]
        public string? AvatarUrl { get; set; }
    }
}
