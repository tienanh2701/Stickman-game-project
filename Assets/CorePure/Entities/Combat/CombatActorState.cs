namespace StickmanBeatEmUp.CorePure.Entities.Combat
{
    /// <summary>
    /// Trạng thái chiến đấu thuần C# của thực thể (tách rời khỏi UnityEngine).
    /// Giá trị 0..8 ánh xạ 1-1 với CharacterState; Stunned = 9 dùng cho trạng thái bị khống chế/choáng.
    /// </summary>
    public enum CombatActorState
    {
        Idle = 0,
        Run = 1,
        Dash = 2,
        Attack = 3,
        Skill = 4,
        Transform = 5,
        Hurt = 6,
        Dead = 7,
        Block = 8,
        Stunned = 9
    }
}
