namespace RuckR.Shared.Models
{
    /// <summary>
    /// Rugby player position options.
    /// </summary>
    public enum PlayerPosition
    {
        Prop,
        Hooker,
        Lock,
        Flanker,
        ScrumHalf,
        FlyHalf,
        Centre,
        Wing,
        Fullback
    }

    /// <summary>
    /// Creature rarity tiers used for balancing.
    /// </summary>
    public enum PlayerRarity
    {
        Common,     // 50% weight
        Uncommon,   // 25% weight
        Rare,       // 15% weight
        Epic,       // 7% weight
        Legendary   // 3% weight
    }

    /// <summary>
    /// Types of rugby pitch definitions supported by the app.
    /// </summary>
    public enum PitchType
    {
        Standard,
        Training,
        Stadium
    }

    /// <summary>
    /// Lifecycle states for battle challenges.
    /// </summary>
    public enum BattleStatus
    {
        Pending,
        Accepted,
        Completed,
        Declined,
        Expired
    }
}
