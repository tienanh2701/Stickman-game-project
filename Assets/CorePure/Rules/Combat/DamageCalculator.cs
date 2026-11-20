using System;
using StickmanBeatEmUp.CorePure.Entities.Combat;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Rules.Combat
{
    /// <summary>
    /// Bộ tính toán sát thương thuần C# tách từ BaseCharacter.TakeDamage và BaseCharacter.GetModifiedDamage.
    /// Hỗ trợ giảm sát thương Block (-80%), hấp thụ khiên ảo VirtualShield, phản sát thương và hệ số IStatSource.
    /// </summary>
    public static class DamageCalculator
    {
        public const float DEFAULT_BLOCK_DAMAGE_MULTIPLIER = 0.2f;
        public const float DEFAULT_MAX_REFLECT_RANGE = 4.5f;

        /// <summary>
        /// Tính sát thương đầu ra sau khi áp dụng IStatSource.AttackDamageMultiplier và hệ số bổ trợ.
        /// </summary>
        public static float CalculateOutgoingDamage(
            float rawDamage,
            IStatSource attackerStatSource = null,
            float extraMultiplier = 1f)
        {
            float safeRaw = SanitizeNonNegative(rawDamage);
            float statMult = attackerStatSource != null
                ? SanitizePositiveMultiplier(attackerStatSource.AttackDamageMultiplier, 1f)
                : 1f;
            float safeExtra = SanitizePositiveMultiplier(extraMultiplier, 1f);
            return safeRaw * statMult * safeExtra;
        }

        /// <summary>
        /// Kiểm tra loại đòn đánh có thuộc nhóm đòn địch đủ điều kiện kích hoạt phản sát thương hay không.
        /// </summary>
        public static bool IsReflectableAttack(CombatAttackKind attackKind)
        {
            return attackKind == CombatAttackKind.EnemyAttack ||
                   attackKind == CombatAttackKind.EnemyHeavyAttack;
        }

        /// <summary>
        /// Tính toàn bộ kết quả sát thương nhận vào theo đúng thứ tự:
        /// Guard (Dead/Invulnerable) -> Attacker IStatSource Multiplier -> Block (-80%) -> VirtualShield -> HP Clamp [0, MaxHP] -> Reflect -> Hit-stun.
        /// </summary>
        public static CombatDamageResult CalculateIncomingDamage(
            CombatDamageInput input,
            IStatSource defenderStatSource = null,
            IStatSource attackerStatSource = null)
        {
            float safeMaxHealth = SanitizeNonNegative(input.MaxHealth);
            float safeCurrentHealth = ClampHealth(input.CurrentHealth, safeMaxHealth);
            float safeShield = SanitizeNonNegative(input.CurrentVirtualShield);

            if (input.IsDead || input.IsInvulnerable)
            {
                return CombatDamageResult.Ignored(safeCurrentHealth, safeShield);
            }

            float sanitizedRaw = SanitizeNonNegative(input.RawDamage);
            float attackerMultiplier = attackerStatSource != null
                ? SanitizePositiveMultiplier(attackerStatSource.AttackDamageMultiplier, 1f)
                : 1f;
            float scaledIncoming = sanitizedRaw * attackerMultiplier;

            float blockMultiplier = ClampBlockMultiplier(input.BlockDamageMultiplier);
            float damageAfterBlock = input.IsBlocking
                ? scaledIncoming * blockMultiplier
                : scaledIncoming;

            bool shieldWasActive = safeShield > 0f;
            bool hasHitStunImmunity = input.IsBlocking || shieldWasActive;

            float shieldAbsorbed = 0f;
            float remainingShield = safeShield;
            float finalHealthDamage = damageAfterBlock;

            if (shieldWasActive)
            {
                shieldAbsorbed = Math.Min(safeShield, damageAfterBlock);
                remainingShield = Math.Max(0f, safeShield - shieldAbsorbed);
                finalHealthDamage = Math.Max(0f, damageAfterBlock - shieldAbsorbed);
            }

            bool shieldDepleted = shieldWasActive && remainingShield <= 0f;

            float remainingHealth = finalHealthDamage > 0f
                ? Math.Max(0f, safeCurrentHealth - finalHealthDamage)
                : safeCurrentHealth;

            bool isFatal = remainingHealth <= 0f;

            float maxReflectRange = (float.IsNaN(input.MaxReflectRange) || float.IsInfinity(input.MaxReflectRange) || input.MaxReflectRange <= 0f)
                ? DEFAULT_MAX_REFLECT_RANGE
                : input.MaxReflectRange;

            float statReflect = defenderStatSource != null
                ? SanitizeNonNegative(defenderStatSource.DamageReflectAmount)
                : 0f;
            float bonusReflect = shieldWasActive
                ? SanitizeNonNegative(input.BonusReflectDamage)
                : 0f;
            float candidateReflectDamage = bonusReflect + statReflect;

            bool canReflect = !input.IsAlreadyReflecting &&
                              candidateReflectDamage > 0f &&
                              input.HasAttacker &&
                              !input.IsSelfAttack &&
                              IsReflectableAttack(input.AttackKind) &&
                              !float.IsNaN(input.AttackerDistance) &&
                              input.AttackerDistance <= maxReflectRange;

            float reflectedDamage = canReflect ? candidateReflectDamage : 0f;

            bool shouldTriggerHitStun = !isFatal &&
                                        !hasHitStunImmunity &&
                                        !input.IsStunImmune &&
                                        !input.IsSkillActive &&
                                        !input.IsTransforming;

            return new CombatDamageResult(
                wasIgnored: false,
                wasBlocked: input.IsBlocking,
                shieldWasActive: shieldWasActive,
                shieldDepleted: shieldDepleted,
                hasHitStunImmunity: hasHitStunImmunity,
                shouldTriggerHitStun: shouldTriggerHitStun,
                isFatal: isFatal,
                shouldReflectDamage: canReflect,
                sanitizedRawDamage: sanitizedRaw,
                scaledIncomingDamage: scaledIncoming,
                damageAfterBlock: damageAfterBlock,
                shieldAbsorbedDamage: shieldAbsorbed,
                finalHealthDamage: finalHealthDamage,
                remainingVirtualShield: remainingShield,
                remainingHealth: remainingHealth,
                reflectedDamage: reflectedDamage);
        }

        private static float SanitizeNonNegative(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return 0f;
            }
            return value;
        }

        private static float SanitizePositiveMultiplier(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return fallback;
            }
            return value;
        }

        private static float ClampBlockMultiplier(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return DEFAULT_BLOCK_DAMAGE_MULTIPLIER;
            }
            if (value > 1f)
            {
                return 1f;
            }
            return value;
        }

        private static float ClampHealth(float currentHealth, float maxHealth)
        {
            float safeCurrent = SanitizeNonNegative(currentHealth);
            if (maxHealth > 0f && safeCurrent > maxHealth)
            {
                return maxHealth;
            }
            return safeCurrent;
        }
    }
}
