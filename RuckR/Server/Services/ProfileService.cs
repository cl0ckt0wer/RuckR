using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Services;
/// <summary>Defines the server-side class ProfileService.</summary>
public class ProfileService : IProfileService
{
    /// <summary>Default display name used before a player configures their profile.</summary>
    public const string DefaultDisplayName = "RuckR Player";

    private readonly RuckRDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    /// <summary>Initializes a new instance of ProfileService.</summary>
    /// <param name="db">The db.</param>
    /// <param name="userManager">The usermanager.</param>
    public ProfileService(RuckRDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }
    /// <summary>Get a user profile, creating a default if needed.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>The operation result.</returns>
    public async Task<UserProfileModel?> GetProfileAsync(string userId)
    {
        var profile = await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile is not null)
        {
            var existingUser = await _userManager.FindByIdAsync(userId);
            if (IsLoginIdentifier(profile.Name, existingUser?.UserName))
            {
                profile.Name = DefaultDisplayName;
                await _db.SaveChangesAsync();
            }

            return profile;
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return null;

        profile = new UserProfileModel
        {
            UserId = userId,
            Name = DefaultDisplayName,
            JoinedDate = DateTime.UtcNow
        };

        _db.UserProfiles.Add(profile);
        await _db.SaveChangesAsync();

        return profile;
    }
    /// <summary>Create or update the current user's profile.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="profile">The profile.</param>
    /// <returns>The saved profile.</returns>
    public async Task<UserProfileModel> CreateOrUpdateProfileAsync(string userId, UserProfileUpdateRequest profile)
    {
        var displayName = NormalizeText(profile.Name) ?? DefaultDisplayName;
        var biography = NormalizeText(profile.Biography);
        var location = NormalizeText(profile.Location);
        var avatarUrl = NormalizeText(profile.AvatarUrl);
        var existing = await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (existing is null)
        {
            existing = new UserProfileModel
            {
                UserId = userId,
                JoinedDate = DateTime.UtcNow
            };
            _db.UserProfiles.Add(existing);
        }

        existing.Name = displayName;
        existing.Biography = biography;
        existing.Location = location;
        existing.AvatarUrl = avatarUrl;

        await _db.SaveChangesAsync();

        return await _db.UserProfiles.AsNoTracking()
            .FirstAsync(p => p.UserId == userId);
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool IsLoginIdentifier(string? displayName, string? loginName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return true;

        if (!string.IsNullOrWhiteSpace(loginName)
            && string.Equals(displayName.Trim(), loginName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return displayName.Contains('@', StringComparison.Ordinal);
    }
}
