namespace StickmanBeatEmUp.CorePure.Entities.Combat
{
    /// <summary>
    /// Dữ liệu đầu vào thuần C# cho công thức tính sát thương nhận vào trong DamageCalculator.
    /// </summary>
    public struct CombatDamageInput
    {
        public float RawDamage;
        public float CurrentHealth;
        public float MaxHealth;
        public float CurrentVirtualShield;
        public float BlockDamageMultiplier;
        public float BonusReflectDamage;
        public bool IsDead;
        public bool IsInvulnerable;
        public bool IsBlocking;
        public bool IsStunImmune;
        public bool IsSkillActive;
        public bool IsTransforming;
        public CombatAttackKind AttackKind;
        public bool HasAttacker;
        public bool IsSelfAttack;
        public bool IsAlreadyReflecting;
        public float AttackerDistance;
        public float MaxReflectRange;

        public static CombatDamageInput CreateDefault(
            float rawDamage,
            float currentHealth,
            float maxHealth = 100f)
        {
            return new CombatDamageInput
            {
                RawDamage = rawDamage,
                CurrentHealth = currentHealth,
                MaxHealth = maxHealth,
                CurrentVirtualShield = 0f,
                BlockDamageMultiplier = 0.2f,
                BonusReflectDamage = 0f,
                IsDead = false,
                IsInvulnerable = false,
                IsBlocking = false,
                IsStunImmune = false,
                IsSkillActive = false,
                IsTransforming = false,
                AttackKind = CombatAttackKind.EnemyAttack,
                HasAttacker = false,
                IsSelfAttack = false,
                IsAlreadyReflecting = false,
                AttackerDistance = float.PositiveInfinity,
                MaxReflectRange = 4.5f
            };
        }
    }
}
