using Fluxor;
using RuckR.Shared.Models;

namespace RuckR.Client.Store.BattleFeature;

[FeatureState]
public record BattleState
{
    public bool IsLoading { get; init; }
    public IReadOnlyList<BattleModel> ActiveChallenges { get; init; } = Array.Empty<BattleModel>();
    public IReadOnlyList<BattleModel> BattleHistory { get; init; } = Array.Empty<BattleModel>();
    public string? ErrorMessage { get; init; }

    public BattleState() { }
}
