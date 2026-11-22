namespace StickmanBeatEmUp.CorePure.Entities.Combat
{
    /// <summary>
    /// Các hành động chiến đấu được kiểm soát hợp lệ bởi CombatRuleGuard.
    /// </summary>
    public enum CombatActionType
    {
        Move = 0,
        Jump = 1,
        Dash = 2,
        NormalAttack = 3,
        DashAttackCombo = 4,
        Skill1 = 5,
        Skill2 = 6,
        Transform = 7,
        SpecialCombo = 8,
        Block = 9,
        Heal = 10,
        UseItem = 11
    }
}
