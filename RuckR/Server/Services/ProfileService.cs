using System.Security.Cryptography;
using System.Text;
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
    private const long MaxAvatarBytes = 2 * 1024 * 1024;

    private readonly RuckRDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly string _profileImagesRoot;
    /// <summary>Initializes a new instance of ProfileService.</summary>
    /// <param name="db">The db.</param>
    /// <param name="userManager">The usermanager.</param>
    /// <param name="configuration">App configuration.</param>
    /// <param name="environment">Host environment.</param>
    public ProfileService(
        RuckRDbContext db,
        UserManager<IdentityUser> userManager,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _db = db;
        _userManager = userManager;
        _profileImagesRoot = UploadStoragePaths.ResolveProfileImagesRoot(configuration, environment);
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

    /// <inheritdoc />
    public async Task<UserProfileModel> UploadAvatarAsync(string userId, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file is null)
            throw new ProfileAvatarValidationException("Profile picture file is required.");

        if (file.Length <= 0)
            throw new ProfileAvatarValidationException("Profile picture file is empty.");

        if (file.Length > MaxAvatarBytes)
            throw new ProfileAvatarValidationException("Profile picture must be 2 MB or smaller.");

        var fileBytes = await ReadFileBytesAsync(file, cancellationToken);
        var detected = DetectImage(file.FileName, file.ContentType, fileBytes);
        var hash = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();
        var safeUserId = SafePathSegment(userId);
        var userDirectory = Path.Combine(_profileImagesRoot, safeUserId);
        Directory.CreateDirectory(userDirectory);

        var fileName = $"{hash}{detected.Extension}";
        var physicalPath = Path.Combine(userDirectory, fileName);
        await File.WriteAllBytesAsync(physicalPath, fileBytes, cancellationToken);

        var profile = await GetOrCreateProfileForUpdateAsync(userId);
        var previousAvatarUrl = profile.AvatarUrl;
        profile.AvatarUrl = $"{UploadStoragePaths.ProfileImagesRequestPath}/{safeUserId}/{fileName}";
        await _db.SaveChangesAsync(cancellationToken);

        DeletePreviousLocalAvatar(previousAvatarUrl, profile.AvatarUrl, safeUserId);

        return await _db.UserProfiles.AsNoTracking()
            .FirstAsync(p => p.UserId == userId, cancellationToken);
    }

    private async Task<UserProfileModel> GetOrCreateProfileForUpdateAsync(string userId)
    {
        var existing = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (existing is not null)
            return existing;

        var profile = new UserProfileModel
        {
            UserId = userId,
            Name = DefaultDisplayName,
            JoinedDate = DateTime.UtcNow
        };
        _db.UserProfiles.Add(profile);
        return profile;
    }

    private static async Task<byte[]> ReadFileBytesAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream((int)Math.Min(file.Length, MaxAvatarBytes));
        await stream.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }

    private static DetectedImage DetectImage(string fileName, string? contentType, byte[] bytes)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var normalizedContentType = contentType?.Trim().ToLowerInvariant();

        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            throw new ProfileAvatarValidationException("Profile picture must be a JPEG, PNG, or WebP image.");

        if (normalizedContentType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new ProfileAvatarValidationException("Profile picture must be a JPEG, PNG, or WebP image.");

        var detected = DetectMagicBytes(bytes);
        if (detected is null)
            throw new ProfileAvatarValidationException("Profile picture file content is not a supported image.");

        var extensionMatches = detected.Extension switch
        {
            ".jpg" => extension is ".jpg" or ".jpeg",
            _ => extension == detected.Extension
        };
        if (!extensionMatches || normalizedContentType != detected.ContentType)
            throw new ProfileAvatarValidationException("Profile picture extension, type, and content do not match.");

        return detected;
    }

    private static DetectedImage? DetectMagicBytes(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return new DetectedImage(".jpg", "image/jpeg");

        if (bytes.Length >= 8
            && bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4E
            && bytes[3] == 0x47
            && bytes[4] == 0x0D
            && bytes[5] == 0x0A
            && bytes[6] == 0x1A
            && bytes[7] == 0x0A)
        {
            return new DetectedImage(".png", "image/png");
        }

        if (bytes.Length >= 12
            && bytes[0] == 'R'
            && bytes[1] == 'I'
            && bytes[2] == 'F'
            && bytes[3] == 'F'
            && bytes[8] == 'W'
            && bytes[9] == 'E'
            && bytes[10] == 'B'
            && bytes[11] == 'P')
        {
            return new DetectedImage(".webp", "image/webp");
        }

        return null;
    }

    private static string SafePathSegment(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_');
        }

        return builder.Length == 0 ? "user" : builder.ToString();
    }

    private void DeletePreviousLocalAvatar(string? previousUrl, string currentUrl, string safeUserId)
    {
        if (string.IsNullOrWhiteSpace(previousUrl)
            || !previousUrl.StartsWith($"{UploadStoragePaths.ProfileImagesRequestPath}/{safeUserId}/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previousUrl, currentUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fileName = Path.GetFileName(previousUrl);
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        var previousPath = Path.Combine(_profileImagesRoot, safeUserId, fileName);
        try
        {
            if (File.Exists(previousPath))
                File.Delete(previousPath);
        }
        catch
        {
            // Losing cleanup should not break a successful profile update.
        }
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

    private sealed record DetectedImage(string Extension, string ContentType);
}
