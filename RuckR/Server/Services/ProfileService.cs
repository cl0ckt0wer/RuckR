using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Services;

public class ProfileService : IProfileService
{
    private readonly RuckRDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public ProfileService(RuckRDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

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