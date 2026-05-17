using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side interface ILocationTracker.</summary>
    public interface ILocationTracker
    {
        /// <summary>
        /// Returns the user's most recent position and timestamp if available and within the specified maxAge.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="maxAge">Maximum age of the position. Defaults to 60 seconds if null.</param>
        /// <returns>The position and timestamp, or null if no position exists or it is too old.</returns>
        (GeoPosition Position, DateTime Timestamp)? TryGetPosition(string userId, TimeSpan? maxAge = null);

        /// <summary>
        /// Updates the stored position for the given user.
        /// Called by BattleHub when a client sends UpdateLocation.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="position">The new GPS position.</param>
        void UpdatePosition(string userId, GeoPosition position);

        /// <summary>
        /// Removes a user's stored position. Called on SignalR disconnect.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        void RemoveUser(string userId);
    }
}

