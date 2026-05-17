using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side interface IProfileService.</summary>
    public interface IProfileService
    {
        Task<UserProfileModel?> GetProfileAsync(string userId);
        Task<UserProfileModel> CreateOrUpdateProfileAsync(string userId, UserProfileModel profile);
    }
}
