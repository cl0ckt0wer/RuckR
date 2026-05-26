using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Server.Hubs;
using RuckR.Shared.Models;

namespace RuckR.Server.Services;

/// <summary>SignalR publisher for battle state changes.</summary>
public sealed class BattleRealtimeNotifier : IBattleRealtimeNotifier
{
    private readonly RuckRDbContext _db;
    private readonly IBattleService _battleService;
    private readonly IHubContext<BattleHub> _hubContext;

    /// <summary>Initializes a new instance of <see cref="BattleRealtimeNotifier"/>.</summary>
    public BattleRealtimeNotifier(
        RuckRDbContext db,
        IBattleService battleService,
        IHubContext<BattleHub> hubContext)
    {
        _db = db;
        _battleService = battleService;
        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public async Task NotifyChallengeCreatedAsync(int battleId)
    {
        var battle = await LoadBattleAsync(battleId);
        if (battle is null)
            return;

        var challengerSummary = await _battleService.ToSummaryAsync(battle, battle.ChallengerId);
        await _hubContext.Clients.User(battle.OpponentId).SendAsync(
            "ReceiveChallenge",
            new ChallengeNotification(challengerSummary.ChallengerUsername, battle.Id));

        await NotifyBattleChangedAsync(battle, null);
    }

    /// <inheritdoc />
    public async Task NotifyBattleChangedAsync(int battleId, BattleResult? result = null)
    {
        var battle = await LoadBattleAsync(battleId);
        if (battle is null)
            return;

        await NotifyBattleChangedAsync(battle, result);
    }

    /// <inheritdoc />
    public async Task NotifyChallengeDeclinedAsync(int battleId)
    {
        var battle = await LoadBattleAsync(battleId);
        if (battle is null)
            return;

        await NotifyBattleChangedAsync(battle, null);
        await _hubContext.Clients.User(battle.ChallengerId).SendAsync("ChallengeDeclined", battle.Id);
    }

    private async Task NotifyBattleChangedAsync(BattleModel battle, BattleResult? result)
    {
        var challengerSummary = await _battleService.ToSummaryAsync(battle, battle.ChallengerId, result);
        var opponentSummary = await _battleService.ToSummaryAsync(battle, battle.OpponentId, result);

        await _hubContext.Clients.User(battle.ChallengerId).SendAsync("BattleUpdated", challengerSummary);
        await _hubContext.Clients.User(battle.OpponentId).SendAsync("BattleUpdated", opponentSummary);

        if (result is not null)
        {
            await _hubContext.Clients.User(battle.ChallengerId).SendAsync("BattleResolved", result);
            await _hubContext.Clients.User(battle.OpponentId).SendAsync("BattleResolved", result);
        }
    }

    private async Task<BattleModel?> LoadBattleAsync(int battleId)
    {
        return await _db.Battles
            .AsNoTracking()
            .FirstOrDefaultAsync(battle => battle.Id == battleId);
    }
}
