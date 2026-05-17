using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models;

/// <summary>
/// Represents a temporary encounter instance for nearby recruiting.
/// </summary>
public class PlayerEncounterModel
{
    [Key]
    /// <summary>Encounter identifier.</summary>
    public Guid Id { get; set; }

    [Required]
    /// <summary>Encounter owner user identifier.</summary>
    public string UserId { get; set; } = string.Empty;

    [Required]
    /// <summary>Player identifier for this encounter.</summary>
    public int PlayerId { get; set; }

    [Range(-90.0, 90.0)]
    /// <summary>Encounter latitude.</summary>
    public double Latitude { get; set; }

    [Range(-180.0, 180.0)]
    /// <summary>Encounter longitude.</summary>
    public double Longitude { get; set; }

    /// <summary>UTC expiration time for the encounter.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>UTC creation time for the encounter.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Related player reference for this encounter.</summary>
    public PlayerModel? Player { get; set; }
}
