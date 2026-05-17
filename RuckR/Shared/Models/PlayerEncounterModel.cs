using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models;

/// <summary>
/// Represents a temporary encounter instance for nearby recruiting.
/// </summary>
public class PlayerEncounterModel
{
    /// <summary>Encounter identifier.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Encounter owner user identifier.</summary>
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Player identifier for this encounter.</summary>
    [Required]
    public int PlayerId { get; set; }

    /// <summary>Encounter latitude.</summary>
    [Range(-90.0, 90.0)]
    public double Latitude { get; set; }

    /// <summary>Encounter longitude.</summary>
    [Range(-180.0, 180.0)]
    public double Longitude { get; set; }

    /// <summary>UTC expiration time for the encounter.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>UTC creation time for the encounter.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Related player reference for this encounter.</summary>
    public PlayerModel? Player { get; set; }
}
