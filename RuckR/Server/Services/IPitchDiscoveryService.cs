using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side interface IPitchDiscoveryService.</summary>
    public interface IPitchDiscoveryService
    {
        /// <summary>Ensures nearby pitches are discovered and returns the current nearby set.</summary>
        /// <param name="userId">Current user identifier.</param>
        /// <param name="userName">Current username for attribution and diagnostics.</param>
        /// <param name="latitude">Current user latitude in decimal degrees.</param>
        /// <param name="longitude">Current user longitude in decimal degrees.</param>
        /// <returns>Nearby pitch list after discovery and persistence updates.</returns>
        Task<IReadOnlyList<PitchModel>> EnsureNearbyPitchesAsync(
            string userId,
            string userName,
            double latitude,
            double longitude);
    }
}

