using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models;

/// <summary>
/// A user explicitly participating in a shared recruitment encounter.
/// </summary>
public class RecruitmentParticipantModel
{
    /// <summary>Shared encounter identifier.</summary>
    public Guid EncounterId { get; set; }

    /// <summary>Participating user identifier.</summary>
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>UTC time when the user joined the shared recruitment.</summary>
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Latitude reported when the user joined.</summary>
    [Range(-90.0, 90.0)]
    public double Latitude { get; set; }

    /// <summary>Longitude reported when the user joined.</summary>
    [Range(-180.0, 180.0)]
    public double Longitude { get; set; }

    /// <summary>GPS accuracy in meters reported when the user joined.</summary>
    public double? AccuracyMeters { get; set; }

    /// <summary>UTC time when the participant received the recruited player.</summary>
    public DateTime? CollectionAwardedAtUtc { get; set; }

    /// <summary>Shared encounter reference.</summary>
    public PlayerEncounterModel? Encounter { get; set; }
}
