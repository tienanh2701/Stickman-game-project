namespace StickmanBeatEmUp.CorePure.Entities.DTOs
{
    /// <summary>
    /// Hành động chiến thuật được quyết định bởi chiến lược AI thuần C#.
    /// </summary>
    public enum AIActionType
    {
        Idle = 0,
        MoveTowardPlayer = 1,
        MoveAwayFromPlayer = 2,
        LightAttack = 3,
        HeavyAttack = 4,
        RangedVolley = 5,
        ParryCounter = 6,
        TeleportBehind = 7,
        Detonate = 8,
        SpecialPhase = 9
    }
}
