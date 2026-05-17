using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Form model used when submitting a new pitch.
    /// </summary>
    public class PitchFormModel
    {
        [Required(ErrorMessage = "Pitch name is required.")]
        [MaxLength(200, ErrorMessage = "Pitch name must be 200 characters or less.")]
        /// <summary>Pitch name.</summary>
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "Pitch type is required.")]
        /// <summary>Pitch type value.</summary>
        public string Type { get; set; } = nameof(PitchType.Standard);

        [Required(ErrorMessage = "Latitude is required.")]
        [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90 degrees.")]
        /// <summary>Latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        [Required(ErrorMessage = "Longitude is required.")]
        [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180 degrees.")]
        /// <summary>Longitude in decimal degrees.</summary>
        public double Longitude { get; set; }
    }
}
