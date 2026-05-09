using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models;

public class PlayerEncounterModel
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public int PlayerId { get; set; }

    [Range(-90.0, 90.0)]
    public double Latitude { get; set; }

    [Range(-180.0, 180.0)]
    public double Longitude { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public PlayerModel? Player { get; set; }
}
