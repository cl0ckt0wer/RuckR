using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    public interface IPitchDiscoveryService
    {
        Task<IReadOnlyList<PitchModel>> EnsureNearbyPitchesAsync(
            string userId,
            string userName,
            double latitude,
            double longitude);
    }
}
