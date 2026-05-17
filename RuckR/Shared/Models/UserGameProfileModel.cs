using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models;

/// <summary>
/// Progress record storing persisted level and experience for a game user.
/// </summary>
public class UserGameProfileModel
{
    [Key]
    [Required]
    /// <summary>User identifier for the profile owner.</summary>
    public string UserId { get; set; } = string.Empty;

    [Range(1, 100)]
    /// <summary>Current player level.</summary>
    public int Level { get; set; } = 1;

    [Range(0, int.MaxValue)]
    /// <summary>Total accrued experience points.</summary>
    public int Experience { get; set; }

    /// <summary>UTC timestamp when the record was last updated.</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
