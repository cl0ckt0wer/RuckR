using System.Collections.Concurrent;
using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    public class LocationTracker : ILocationTracker
    {
        private static readonly TimeSpan DefaultMaxAge = TimeSpan.FromSeconds(60);

        private readonly ConcurrentDictionary<string, (GeoPosition Position, DateTime Timestamp)> _positions = new();

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

        public void UpdatePosition(string userId, GeoPosition position)
        {
            _positions.AddOrUpdate(userId,
                _ => (position, DateTime.UtcNow),
                (_, _) => (position, DateTime.UtcNow));
        }

        public void RemoveUser(string userId)
        {
            _positions.TryRemove(userId, out _);
        }
    }
}
