using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side interface IProfileService.</summary>
    public interface IProfileService
    {
        /// <summary>Gets the profile for a specific user, if one exists.</summary>
        /// <param name="userId">User identifier.</param>
        /// <returns>Profile model or <see langword="null"/> when not found.</returns>
        Task<UserProfileModel?> GetProfileAsync(string userId);
        /// <summary>Creates a profile or updates an existing profile for a user.</summary>
        /// <param name="userId">User identifier.</param>
        /// <param name="profile">Profile payload to persist.</param>
        /// <returns>Saved profile model.</returns>
        Task<UserProfileModel> CreateOrUpdateProfileAsync(string userId, UserProfileModel profile);
    }
}
