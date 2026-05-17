using Fluxor;
using RuckR.Shared.Models;

namespace RuckR.Client.Store.BattleFeature;

/// <summary>
/// Battle feature state stored in Fluxor.
/// </summary>
[FeatureState]
public record BattleState
{
    /// <summary>
    /// Indicates whether battles are currently loading.
    /// </summary>
    public bool IsLoading { get; init; }

    /// <summary>
    /// Active challenge list.
    /// </summary>
    public IReadOnlyList<BattleModel> ActiveChallenges { get; init; } = Array.Empty<BattleModel>();

    /// <summary>
    /// Completed battle history.
    /// </summary>
    public IReadOnlyList<BattleModel> BattleHistory { get; init; } = Array.Empty<BattleModel>();

    /// <summary>
    /// Last battle-related error message.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates an empty battle state.
    /// </summary>
    public BattleState() { }
}
