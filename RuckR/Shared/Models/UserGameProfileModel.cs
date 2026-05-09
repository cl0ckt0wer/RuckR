using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models;

public class UserGameProfileModel
{
    [Key]
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Range(1, 100)]
    public int Level { get; set; } = 1;

    [Range(0, int.MaxValue)]
    public int Experience { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
