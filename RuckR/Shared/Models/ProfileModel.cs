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

        [Required]
        [EmailAddress]
        [StringLength(256)]
        /// <summary>Contact email address.</summary>
        public string? Email { get; set; }

        [StringLength(100)]
        /// <summary>Location label shown on profile.</summary>
        public string? Location { get; set; }

        /// <summary>Date when the profile was created.</summary>
        public DateTime JoinedDate { get; set; } = DateTime.Today;

        [Url]
        [StringLength(500)]
        /// <summary>Avatar URL used in profile display.</summary>
        public string? AvatarUrl { get; set; }
    }
}
