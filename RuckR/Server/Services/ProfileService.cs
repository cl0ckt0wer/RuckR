using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Services;
/// <summary>Defines the server-side class ProfileService.</summary>
public class ProfileService : IProfileService
{
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
        var profile = await _db.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile is not null)
            return profile;

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return null;

        profile = new UserProfileModel
        {
            UserId = userId,
            Name = user.UserName,
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
    public async Task<UserProfileModel> CreateOrUpdateProfileAsync(string userId, UserProfileModel profile)
    {
        var existing = await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (existing is null)
        {
            profile.UserId = userId;
            _db.UserProfiles.Add(profile);
        }
        else
        {
            existing.Name = profile.Name;
            existing.Biography = profile.Biography;
            existing.Location = profile.Location;
            existing.AvatarUrl = profile.AvatarUrl;
        }

        await _db.SaveChangesAsync();

        return await _db.UserProfiles.AsNoTracking()
            .FirstAsync(p => p.UserId == userId);
    }
}
