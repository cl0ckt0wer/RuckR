using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Services;
/// <summary>Defines the server-side class RateLimitService.</summary>
public class RateLimitService : IRateLimitService
{
    private readonly RuckRDbContext _db;
    /// <summary>Initializes a new instance of RateLimitService.</summary>
    /// <param name="db">The db.</param>
    public RateLimitService(RuckRDbContext db)
    {
        _db = db;
    }
    /// <summary>I sA ll ow ed As yn c.</summary>
    /// <param name="userId">The userid.</param>
    /// <param name="action">The action.</param>
    /// <param name="maxCount">The maxcount.</param>
    /// <param name="window">The window.</param>
    /// <returns>The operation result.</returns>
    public async Task<bool> IsAllowedAsync(string userId, string action, int maxCount, TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;

        var count = await _db.RateLimitRecords
            .Where(r => r.UserId == userId && r.Action == action && r.TimestampUtc > cutoff)
            .CountAsync();

        if (count >= maxCount)
            return false;

        _db.RateLimitRecords.Add(new RateLimitRecord
        {
            UserId = userId,
            Action = action,
            TimestampUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return true;
    }
}
