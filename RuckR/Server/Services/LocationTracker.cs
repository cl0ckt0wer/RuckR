using System.Collections.Concurrent;
using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side class LocationTracker.</summary>
    public class LocationTracker : ILocationTracker
    {
        private static readonly TimeSpan DefaultMaxAge = TimeSpan.FromSeconds(60);

        private readonly ConcurrentDictionary<string, (GeoPosition Position, DateTime Timestamp)> _positions = new();
        /// <summary>
        /// Gets the latest stored position for a user when it has not aged out.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="maxAge">
        /// Optional maximum age used to filter stale positions.
        /// </param>
        /// <returns>
        /// The user position and timestamp when a valid recent value exists; otherwise <see langword="null"/>.
        /// </returns>
        public (GeoPosition Position, DateTime Timestamp)? TryGetPosition(string userId, TimeSpan? maxAge = null)
        {
            TimeSpan effectiveMaxAge = maxAge ?? DefaultMaxAge;

            if (_positions.TryGetValue(userId, out var entry))
            {
                if (DateTime.UtcNow - entry.Timestamp <= effectiveMaxAge)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// Updates the tracked position for a user to the current timestamp.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="position">The position.</param>
        public void UpdatePosition(string userId, GeoPosition position)
        {
            _positions.AddOrUpdate(userId,
                _ => (position, DateTime.UtcNow),
                (_, _) => (position, DateTime.UtcNow));
        }

        /// <summary>
        /// Removes a user from the in-memory position tracker.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        public void RemoveUser(string userId)
        {
            _positions.TryRemove(userId, out _);
        }
    }
}

