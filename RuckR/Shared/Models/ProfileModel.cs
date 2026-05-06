using System;
using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    public class ProfileModel
    {
        public string? Name { get; set; } = null;
        public string? Biography { get; set; } = null;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string? Email { get; set; }

        [StringLength(100)]
        public string? Location { get; set; }

        public DateTime JoinedDate { get; set; } = DateTime.Today;

        [Url]
        [StringLength(500)]
        public string? AvatarUrl { get; set; }
    }
}
