namespace StickmanBeatEmUp.CorePure.Entities.Combat
{
    /// <summary>
    /// Phân loại đòn đánh thuần C# dùng cho luật tích Nộ và luật phản sát thương.
    /// </summary>
    public enum CombatAttackKind
    {
        NormalAttackJ = 0,
        SkillU = 1,
        SkillI = 2,
        ComboDashJ = 3,
        ComboDashI = 4,
        TransformBurst = 5,
        EnemyAttack = 6,
        EnemyHeavyAttack = 7
    }
}
