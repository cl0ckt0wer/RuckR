using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models;

/// <summary>
/// Tracks a user's available temporary recruitment items.
/// </summary>
public class UserRecruitmentItemModel
{
    /// <summary>Item inventory row identifier.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>User that owns the item stack.</summary>
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Recruitment item kind.</summary>
    public RecruitmentItemKind ItemKind { get; set; }

    /// <summary>Available count for this item kind.</summary>
    public int Quantity { get; set; }

    /// <summary>Last update timestamp.</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
