using System;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.AI
{
    /// <summary>
    /// Multi-phase Boss AI strategy:
    /// - Activates shield + SpecialPhase when boss rage hits threshold (e.g., Broly Green Ki Shield at 100 rage).
    /// - Triggers ParryCounter when player spams light hits in window (threshold tightened on Hard).
    /// - Transitions from Phase 1 to enraged Phase 2 based on difficulty-scaled HP ratio.
    /// - Selects Light/Heavy melee, SpecialPhase mid/long range attacks, or accelerated Phase 2 advance.
    /// </summary>
    public sealed class BossPhaseStrategy : IAIStrategy
    {
        public const string ID = "boss_phase";

        private readonly float defaultMeleeRange;
        private readonly float specialTriggerMinRange;
        private readonly float rageShieldThreshold;

        public string StrategyId => ID;

        public BossPhaseStrategy(
            float defaultMeleeRange = 2.3f,
            float specialTriggerMinRange = 4.2f,
            float rageShieldThreshold = 100f)
        {
            this.defaultMeleeRange = AIDifficultyTuning.SanitizePositive(defaultMeleeRange, 2.3f);
            this.specialTriggerMinRange = AIDifficultyTuning.SanitizePositive(specialTriggerMinRange, 4.2f);
            this.rageShieldThreshold = AIDifficultyTuning.SanitizePositive(rageShieldThreshold, 100f);
        }

        public AIActionDecision ComputeAction(EnemyAIContext context)
        {
            if (AIDifficultyTuning.IsDeadOrInvalid(context))
            {
                return AIActionDecision.IdleDecision;
            }

            int difficulty = AIDifficultyTuning.ClampDifficulty(context.DifficultyLevel);
            bool hasValidMaxHp = !float.IsNaN(context.EnemyMaxHp) &&
                                 !float.IsInfinity(context.EnemyMaxHp) &&
                                 context.EnemyMaxHp > 0f;
            float hpRatio = hasValidMaxHp ? (context.EnemyHp / context.EnemyMaxHp) : 1f;
            bool isPhase2 = hpRatio <= AIDifficultyTuning.GetBossPhase2HpRatio(difficulty);

            // 1. Full Boss Rage -> SpecialPhase + Shield Activation
            if (!float.IsNaN(context.EnemyRage) &&
                !float.IsInfinity(context.EnemyRage) &&
                context.EnemyRage >= rageShieldThreshold)
            {
                return new AIActionDecision(AIActionType.SpecialPhase, 0f, true, true);
            }

            // 2. Anti-spam ParryCounter when player attacks repeatedly inside window
            int parryHitsRequired = AIDifficultyTuning.GetParryTriggerHits(difficulty);
            if (context.IsPlayerAttacking && context.LightHitsReceivedInWindow >= parryHitsRequired)
            {
                return new AIActionDecision(AIActionType.ParryCounter, 0f, true, isPhase2);
            }

            float dx = context.PlayerX - context.EnemyX;
            float distX = Math.Abs(dx);
            float meleeRange = AIDifficultyTuning.SanitizePositive(context.AttackRange, defaultMeleeRange) *
                               AIDifficultyTuning.GetRangeScale(difficulty);
            float maxSpecialRange = Math.Max(
                specialTriggerMinRange + 1.5f,
                AIDifficultyTuning.SanitizePositive(context.PreferredRange, 8.5f));
            bool attackReady = AIDifficultyTuning.IsCooldownReady(context.AttackCooldownRemaining, difficulty);

            // 3. Melee range engagement
            if (distX <= meleeRange)
            {
                if (!attackReady)
                {
                    return new AIActionDecision(AIActionType.Idle, 0f, true, false);
                }

                bool useHeavy = isPhase2 || difficulty == (int)AIDifficultyPreset.Hard;
                return new AIActionDecision(
                    useHeavy ? AIActionType.HeavyAttack : AIActionType.LightAttack,
                    0f,
                    true,
                    false);
            }

            // 4. Mid/long range special or heavy opener
            if (attackReady && distX <= maxSpecialRange)
            {
                if (isPhase2 || distX >= specialTriggerMinRange)
                {
                    return new AIActionDecision(AIActionType.SpecialPhase, 0f, true, false);
                }

                if (difficulty == (int)AIDifficultyPreset.Hard)
                {
                    return new AIActionDecision(AIActionType.HeavyAttack, 0f, true, false);
                }
            }

            // 5. Advance toward player (1.15x faster in Phase 2)
            float sign = AIDifficultyTuning.ComputeHorizontalSign(dx);
            if (sign == 0f)
            {
                return new AIActionDecision(AIActionType.Idle, 0f, true, false);
            }

            float phaseSpeedMult = isPhase2 ? 1.15f : 1.0f;
            float moveDir = sign * AIDifficultyTuning.GetMoveSpeedScale(difficulty) * phaseSpeedMult;
            return new AIActionDecision(AIActionType.MoveTowardPlayer, moveDir, true, false);
        }
    }
}
