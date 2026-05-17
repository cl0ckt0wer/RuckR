using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models;

/// <summary>
/// Progress record storing persisted level and experience for a game user.
/// </summary>
public class UserGameProfileModel
{
    /// <summary>User identifier for the profile owner.</summary>
    [Key]
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Current player level.</summary>
    [Range(1, 100)]
    public int Level { get; set; } = 1;

    /// <summary>Total accrued experience points.</summary>
    [Range(0, int.MaxValue)]
    public int Experience { get; set; }

    /// <summary>UTC timestamp when the record was last updated.</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
