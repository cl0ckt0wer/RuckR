using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    public interface IProfileService
    {
        Task<UserProfileModel?> GetProfileAsync(string userId);
        Task<UserProfileModel> CreateOrUpdateProfileAsync(string userId, UserProfileModel profile);
    }
}