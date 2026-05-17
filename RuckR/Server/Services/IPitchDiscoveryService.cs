using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side interface IPitchDiscoveryService.</summary>
    public interface IPitchDiscoveryService
    {
        Task<IReadOnlyList<PitchModel>> EnsureNearbyPitchesAsync(
            string userId,
            string userName,
            double latitude,
            double longitude);
    }
}

