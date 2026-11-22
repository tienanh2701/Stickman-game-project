namespace StickmanBeatEmUp.CorePure.Entities.Combat
{
    /// <summary>
    /// Kết quả tính toán sát thương nhận vào bất biến (immutable value object) trả về từ DamageCalculator.
    /// </summary>
    public readonly struct CombatDamageResult
    {
        public bool WasIgnored { get; }
        public bool WasBlocked { get; }
        public bool ShieldWasActive { get; }
        public bool ShieldDepleted { get; }
        public bool HasHitStunImmunity { get; }
        public bool ShouldTriggerHitStun { get; }
        public bool IsFatal { get; }
        public bool ShouldReflectDamage { get; }

        public float SanitizedRawDamage { get; }
        public float ScaledIncomingDamage { get; }
        public float DamageAfterBlock { get; }
        public float ShieldAbsorbedDamage { get; }
        public float FinalHealthDamage { get; }
        public float RemainingVirtualShield { get; }
        public float RemainingHealth { get; }
        public float ReflectedDamage { get; }

        public CombatDamageResult(
            bool wasIgnored,
            bool wasBlocked,
            bool shieldWasActive,
            bool shieldDepleted,
            bool hasHitStunImmunity,
            bool shouldTriggerHitStun,
            bool isFatal,
            bool shouldReflectDamage,
            float sanitizedRawDamage,
            float scaledIncomingDamage,
            float damageAfterBlock,
            float shieldAbsorbedDamage,
            float finalHealthDamage,
            float remainingVirtualShield,
            float remainingHealth,
            float reflectedDamage)
        {
            WasIgnored = wasIgnored;
            WasBlocked = wasBlocked;
            ShieldWasActive = shieldWasActive;
            ShieldDepleted = shieldDepleted;
            HasHitStunImmunity = hasHitStunImmunity;
            ShouldTriggerHitStun = shouldTriggerHitStun;
            IsFatal = isFatal;
            ShouldReflectDamage = shouldReflectDamage;
            SanitizedRawDamage = sanitizedRawDamage;
            ScaledIncomingDamage = scaledIncomingDamage;
            DamageAfterBlock = damageAfterBlock;
            ShieldAbsorbedDamage = shieldAbsorbedDamage;
            FinalHealthDamage = finalHealthDamage;
            RemainingVirtualShield = remainingVirtualShield;
            RemainingHealth = remainingHealth;
            ReflectedDamage = reflectedDamage;
        }

        public static CombatDamageResult Ignored(float currentHealth, float currentVirtualShield)
        {
            float safeHp = currentHealth > 0f ? currentHealth : 0f;
            float safeShield = currentVirtualShield > 0f ? currentVirtualShield : 0f;
            return new CombatDamageResult(
                wasIgnored: true,
                wasBlocked: false,
                shieldWasActive: safeShield > 0f,
                shieldDepleted: false,
                hasHitStunImmunity: true,
                shouldTriggerHitStun: false,
                isFatal: false,
                shouldReflectDamage: false,
                sanitizedRawDamage: 0f,
                scaledIncomingDamage: 0f,
                damageAfterBlock: 0f,
                shieldAbsorbedDamage: 0f,
                finalHealthDamage: 0f,
                remainingVirtualShield: safeShield,
                remainingHealth: safeHp,
                reflectedDamage: 0f);
        }
    }
}
