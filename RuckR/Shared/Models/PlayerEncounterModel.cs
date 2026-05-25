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

    /// <summary>User that first caused this shared encounter to be created, when known.</summary>
    public string? UserId { get; set; }

    /// <summary>Player identifier for this encounter.</summary>
    [Required]
    public int PlayerId { get; set; }

    /// <summary>Encounter latitude.</summary>
    [Range(-90.0, 90.0)]
    public double Latitude { get; set; }

    /// <summary>Encounter longitude.</summary>
    [Range(-180.0, 180.0)]
    public double Longitude { get; set; }

    /// <summary>Stable key for the real-world area this shared encounter belongs to.</summary>
    [MaxLength(160)]
    public string? AreaKey { get; set; }

    /// <summary>External park/place identifier for this encounter, when known.</summary>
    [MaxLength(128)]
    public string? ParkPlaceId { get; set; }

    /// <summary>UTC expiration time for the encounter.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>UTC creation time for the encounter.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time when the active recruitment session started.</summary>
    public DateTime? RecruitmentStartedAtUtc { get; set; }

    /// <summary>UTC time when the active recruitment session can complete.</summary>
    public DateTime? RecruitmentCompletesAtUtc { get; set; }

    /// <summary>Base duration required before social and item reductions.</summary>
    public int RecruitmentBaseDurationSeconds { get; set; }

    /// <summary>Actual duration required after social and item reductions.</summary>
    public int RecruitmentRequiredDurationSeconds { get; set; }

    /// <summary>Nearby recruiters counted when the session was started.</summary>
    public int RecruitmentLocalPlayerCount { get; set; }

    /// <summary>Recruitment item used when the session was started.</summary>
    public RecruitmentItemKind RecruitmentItemKind { get; set; } = RecruitmentItemKind.None;

    /// <summary>Related player reference for this encounter.</summary>
    public PlayerModel? Player { get; set; }

    /// <summary>Users explicitly participating in this shared recruitment encounter.</summary>
    public List<RecruitmentParticipantModel> Participants { get; set; } = new();
}
