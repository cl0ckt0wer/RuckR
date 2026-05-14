using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Services;

public class RateLimitService : IRateLimitService
{
    private readonly RuckRDbContext _db;

    public RateLimitService(RuckRDbContext db)
    {
        _db = db;
    }

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