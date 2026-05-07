using System.Collections.Concurrent;
using RuckR.Server.Services;
using RuckR.Shared.Models;

namespace RuckR.Tests.Fixtures;

/// <summary>
/// Test implementation of ILocationTracker that allows tests to inject GPS positions
/// for server-side validation in capture flow tests.
/// </summary>
public class TestLocationTracker : ILocationTracker
{
    private readonly ConcurrentDictionary<string, (GeoPosition Position, DateTime Timestamp)> _positions = new();

    /// <summary>
    /// Sets a GPS position for a user that will be returned by TryGetPosition.
    /// </summary>
    public void SetPosition(string userId, double latitude, double longitude)
    {
        var position = new GeoPosition
        {
            Latitude = latitude,
            Longitude = longitude,
            Timestamp = DateTime.UtcNow
        };
        _positions[userId] = (position, DateTime.UtcNow);
    }

    /// <summary>
    /// Sets a GPS position with a specific timestamp (for testing stale-position scenarios).
    /// </summary>
    public void SetPosition(string userId, double latitude, double longitude, DateTime timestamp)
    {
        var position = new GeoPosition
        {
            Latitude = latitude,
            Longitude = longitude,
            Timestamp = timestamp
        };
        _positions[userId] = (position, timestamp);
    }

    /// <summary>
    /// Removes the stored position for a user, simulating no GPS data.
    /// </summary>
    public void ClearPosition(string userId)
    {
        _positions.TryRemove(userId, out _);
    }

    /// <summary>
    /// Clears all stored positions.
    /// </summary>
    public void ClearAll()
    {
        _positions.Clear();
    }

    public (GeoPosition Position, DateTime Timestamp)? TryGetPosition(string userId, TimeSpan? maxAge = null)
    {
        var effectiveMaxAge = maxAge ?? TimeSpan.FromSeconds(60);

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
