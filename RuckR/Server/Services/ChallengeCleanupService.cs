using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Services;
/// <summary>Defines the server-side class ChallengeCleanupService.</summary>
public sealed class ChallengeCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChallengeCleanupService> _logger;
    /// <summary>Initializes a new instance of ChallengeCleanupService.</summary>
    /// <param name="scopeFactory">The scopefactory.</param>
    /// <param name="logger">The logger.</param>
    public ChallengeCleanupService(IServiceScopeFactory scopeFactory, ILogger<ChallengeCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }
    /// <summary>E xe cu te As yn c.</summary>
    /// <param name="stoppingToken">The stoppingtoken.</param>
    /// <returns>The operation result.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Challenge cleanup cycle failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuckRDbContext>();

        var now = DateTime.UtcNow;
        var battleExpiryCutoff = now.AddHours(-24);

        var stalePending = await db.Battles
            .Where(b => b.Status == BattleStatus.Pending && b.CreatedAt <= battleExpiryCutoff)
            .ToListAsync(cancellationToken);

        foreach (var battle in stalePending)
        {
            battle.Status = BattleStatus.Expired;
            battle.ResolvedAt = now;
        }

        var expiredEncounters = await db.PlayerEncounters
            .Where(e => e.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        if (expiredEncounters.Count > 0)
        {
            db.PlayerEncounters.RemoveRange(expiredEncounters);
        }

        if (stalePending.Count > 0 || expiredEncounters.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Cleanup: expired {BattleCount} pending battles, removed {EncounterCount} encounters",
                stalePending.Count,
                expiredEncounters.Count);
        }
    }
}

