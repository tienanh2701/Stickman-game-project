using System;
using StickmanBeatEmUp.CorePure.Entities.DTOs;

namespace StickmanBeatEmUp.CorePure.AI
{
    /// <summary>
    /// Standard difficulty preset for enemy and boss AI (maps directly to EnemyAIContext.DifficultyLevel).
    /// </summary>
    public enum AIDifficultyPreset
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    /// <summary>
    /// Pure C# AI tuning parameters for Easy / Normal / Hard presets plus defensive context validation helpers.
    /// </summary>
    public static class AIDifficultyTuning
    {
        public static int ClampDifficulty(int difficultyLevel)
        {
            if (difficultyLevel <= (int)AIDifficultyPreset.Easy)
            {
                return (int)AIDifficultyPreset.Easy;
            }

            if (difficultyLevel >= (int)AIDifficultyPreset.Hard)
            {
                return (int)AIDifficultyPreset.Hard;
            }

            return (int)AIDifficultyPreset.Normal;
        }

        public static AIDifficultyPreset ToPreset(int difficultyLevel)
        {
            return (AIDifficultyPreset)ClampDifficulty(difficultyLevel);
        }

        public static float GetRangeScale(int difficultyLevel)
        {
            int clamped = ClampDifficulty(difficultyLevel);
            if (clamped == (int)AIDifficultyPreset.Easy)
            {
                return 0.90f;
            }

            if (clamped == (int)AIDifficultyPreset.Hard)
            {
                return 1.12f;
            }

            return 1.0f;
        }

        public static float GetMoveSpeedScale(int difficultyLevel)
        {
            int clamped = ClampDifficulty(difficultyLevel);
            if (clamped == (int)AIDifficultyPreset.Easy)
            {
                return 0.85f;
            }

            if (clamped == (int)AIDifficultyPreset.Hard)
            {
                return 1.15f;
            }

            return 1.0f;
        }

        /// <summary>
        /// Multiplier applied to base attack cooldown duration (higher = slower attack cadence on Easy).
        /// </summary>
        public static float GetAttackCooldownScale(int difficultyLevel)
        {
            int clamped = ClampDifficulty(difficultyLevel);
            if (clamped == (int)AIDifficultyPreset.Easy)
            {
                return 1.25f;
            }

            if (clamped == (int)AIDifficultyPreset.Hard)
            {
                return 0.85f;
            }

            return 1.0f;
        }

        /// <summary>
        /// Remaining-cooldown threshold at or below which an AI considers its attack ready.
        /// Easy and Normal fire at &lt;= 0f (safe with clamped non-negative cooldowns); Hard attacks up to 0.18s early.
        /// </summary>
        public static float GetCooldownReadyThreshold(int difficultyLevel)
        {
            int clamped = ClampDifficulty(difficultyLevel);
            if (clamped == (int)AIDifficultyPreset.Hard)
            {
                return 0.18f;
            }

            return 0f;
        }

        public static bool IsCooldownReady(float attackCooldownRemaining, int difficultyLevel)
        {
            if (float.IsNaN(attackCooldownRemaining) || float.IsInfinity(attackCooldownRemaining))
            {
                return false;
            }

            return attackCooldownRemaining <= GetCooldownReadyThreshold(difficultyLevel);
        }

        public static float GetFleeRangeScale(int difficultyLevel)
        {
            int clamped = ClampDifficulty(difficultyLevel);
            if (clamped == (int)AIDifficultyPreset.Easy)
            {
                return 0.85f;
            }

            if (clamped == (int)AIDifficultyPreset.Hard)
            {
                return 1.15f;
            }

            return 1.0f;
        }

        public static int GetParryTriggerHits(int difficultyLevel)
        {
            int clamped = ClampDifficulty(difficultyLevel);
            if (clamped == (int)AIDifficultyPreset.Easy)
            {
                return 4;
            }

            if (clamped == (int)AIDifficultyPreset.Hard)
            {
                return 2;
            }

            return 3;
        }

        public static float GetBossPhase2HpRatio(int difficultyLevel)
        {
            int clamped = ClampDifficulty(difficultyLevel);
            if (clamped == (int)AIDifficultyPreset.Easy)
            {
                return 0.40f;
            }

            if (clamped == (int)AIDifficultyPreset.Hard)
            {
                return 0.60f;
            }

            return 0.50f;
        }

        public static bool IsDeadOrInvalid(EnemyAIContext context)
        {
            if (context.IsPlayerDead)
            {
                return true;
            }

            if (float.IsNaN(context.EnemyHp) || float.IsInfinity(context.EnemyHp) || context.EnemyHp <= 0f)
            {
                return true;
            }

            return float.IsNaN(context.EnemyX) ||
                   float.IsInfinity(context.EnemyX) ||
                   float.IsNaN(context.EnemyY) ||
                   float.IsInfinity(context.EnemyY) ||
                   float.IsNaN(context.PlayerX) ||
                   float.IsInfinity(context.PlayerX) ||
                   float.IsNaN(context.PlayerY) ||
                   float.IsInfinity(context.PlayerY);
        }

        public static float ComputeHorizontalSign(float deltaX, float deadZone = 0.05f)
        {
            if (float.IsNaN(deltaX) || float.IsInfinity(deltaX))
            {
                return 0f;
            }

            float safeDeadZone = (float.IsNaN(deadZone) || float.IsInfinity(deadZone) || deadZone < 0f) ? 0f : deadZone;
            if (Math.Abs(deltaX) <= safeDeadZone)
            {
                return 0f;
            }

            return deltaX > 0f ? 1f : -1f;
        }

        public static float SanitizePositive(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return fallback;
            }

            return value;
        }
    }
}
