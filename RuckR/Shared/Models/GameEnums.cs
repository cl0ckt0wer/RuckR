namespace RuckR.Shared.Models
{
    /// <summary>
    /// Rugby player position options.
    /// </summary>
    public enum PlayerPosition
    {
        /// <summary>Front-row prop role focused on scrummaging power.</summary>
        Prop,
        /// <summary>Front-row hooker role that strikes for possession in scrums and lineouts.</summary>
        Hooker,
        /// <summary>Second-row lock role providing height and set-piece strength.</summary>
        Lock,
        /// <summary>Loose-forward flanker role focused on tackles, turnovers, and support play.</summary>
        Flanker,
        /// <summary>Scrum-half playmaker role linking forwards and backs.</summary>
        ScrumHalf,
        /// <summary>Fly-half decision-maker role guiding attacking shape and kicking strategy.</summary>
        FlyHalf,
        /// <summary>Midfield centre role balancing ball-carrying, defense, and distribution.</summary>
        Centre,
        /// <summary>Wide wing finisher role emphasizing pace and line breaks.</summary>
        Wing,
        /// <summary>Deep fullback role covering kicks and counterattacking space.</summary>
        Fullback
    }

    /// <summary>
    /// Creature rarity tiers used for balancing.
    /// </summary>
    public enum PlayerRarity
    {
        /// <summary>Most common rarity tier.</summary>
        Common,     // 50% weight
        /// <summary>Second-most common rarity tier.</summary>
        Uncommon,   // 25% weight
        /// <summary>Mid-tier rarity with lower spawn probability.</summary>
        Rare,       // 15% weight
        /// <summary>High-value rarity tier with rare spawn probability.</summary>
        Epic,       // 7% weight
        /// <summary>Top rarity tier with very low spawn probability.</summary>
        Legendary   // 3% weight
    }

    /// <summary>
    /// Types of rugby pitch definitions supported by the app.
    /// </summary>
    public enum PitchType
    {
        /// <summary>Default full-size pitch type.</summary>
        Standard,
        /// <summary>Training-focused pitch type.</summary>
        Training,
        /// <summary>Large stadium pitch type.</summary>
        Stadium
    }

    /// <summary>
    /// Lifecycle states for battle challenges.
    /// </summary>
    public enum BattleStatus
    {
        /// <summary>Challenge has been created and awaits response.</summary>
        Pending,
        /// <summary>Challenge was accepted and is ready for resolution.</summary>
        Accepted,
        /// <summary>Battle has completed with a terminal result.</summary>
        Completed,
        /// <summary>Challenge was declined by the recipient.</summary>
        Declined,
        /// <summary>Challenge expired before completion.</summary>
        Expired
    }
}
