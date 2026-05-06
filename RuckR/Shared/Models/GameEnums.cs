namespace RuckR.Shared.Models
{
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

    public enum PlayerRarity
    {
        Common,     // 50% weight
        Uncommon,   // 25% weight
        Rare,       // 15% weight
        Epic,       // 7% weight
        Legendary   // 3% weight
    }

    public enum PitchType
    {
        Standard,
        Training,
        Stadium
    }

    public enum BattleStatus
    {
        Pending,
        Accepted,
        Completed,
        Declined,
        Expired
    }
}
