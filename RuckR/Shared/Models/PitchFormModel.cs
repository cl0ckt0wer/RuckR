using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    public class PitchFormModel
    {
        [Required(ErrorMessage = "Pitch name is required.")]
        [MaxLength(200, ErrorMessage = "Pitch name must be 200 characters or less.")]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "Pitch type is required.")]
        public string Type { get; set; } = nameof(PitchType.Standard);

        [Required(ErrorMessage = "Latitude is required.")]
        [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90 degrees.")]
        public double Latitude { get; set; }

        [Required(ErrorMessage = "Longitude is required.")]
        [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180 degrees.")]
        public double Longitude { get; set; }
    }
}
