using System;
using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Public profile payload used by the profile endpoint and UI.
    /// </summary>
    public class ProfileModel
    {
        /// <summary>Display name.</summary>
        public string? Name { get; set; } = null;

        /// <summary>Optional biography text.</summary>
        public string? Biography { get; set; } = null;

        /// <summary>Contact email address.</summary>
        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string? Email { get; set; }

        /// <summary>Location label shown on profile.</summary>
        [StringLength(100)]
        public string? Location { get; set; }

        /// <summary>Date when the profile was created.</summary>
        public DateTime JoinedDate { get; set; } = DateTime.Today;

        /// <summary>Avatar URL used in profile display.</summary>
        [Url]
        [StringLength(500)]
        public string? AvatarUrl { get; set; }
    }
}
