using System;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.AI
{
    /// <summary>
    /// Ranged kiting AI strategy: retreats when closer than scaled FleeRange, advances when beyond
    /// PreferredRange * 1.15, and fires RangedVolley inside the sweet spot once cooldown is ready.
    /// </summary>
    public sealed class RangedKiteStrategy : IAIStrategy
    {
        public const string ID = "ranged_kite";

        private readonly float defaultPreferredRange;
        private readonly float defaultFleeRange;
        private readonly float retreatSpeedFactor;

        public string StrategyId => ID;

        public RangedKiteStrategy(
            float defaultPreferredRange = 6.5f,
            float defaultFleeRange = 2.5f,
            float retreatSpeedFactor = 0.9f)
        {
            this.defaultPreferredRange = AIDifficultyTuning.SanitizePositive(defaultPreferredRange, 6.5f);
            this.defaultFleeRange = AIDifficultyTuning.SanitizePositive(defaultFleeRange, 2.5f);
            this.retreatSpeedFactor = AIDifficultyTuning.SanitizePositive(retreatSpeedFactor, 0.9f);
        }

        public AIActionDecision ComputeAction(EnemyAIContext context)
        {
            if (AIDifficultyTuning.IsDeadOrInvalid(context))
            {
                return AIActionDecision.IdleDecision;
            }

            float dx = context.PlayerX - context.EnemyX;
            float distX = Math.Abs(dx);

            int difficulty = AIDifficultyTuning.ClampDifficulty(context.DifficultyLevel);
            float pref = AIDifficultyTuning.SanitizePositive(context.PreferredRange, defaultPreferredRange);
            float flee = AIDifficultyTuning.SanitizePositive(context.FleeRange, defaultFleeRange) *
                         AIDifficultyTuning.GetFleeRangeScale(difficulty);
            float moveScale = AIDifficultyTuning.GetMoveSpeedScale(difficulty);

            float signToPlayer = AIDifficultyTuning.ComputeHorizontalSign(dx, 0.001f);
            if (signToPlayer == 0f)
            {
                signToPlayer = 1f;
            }

            if (distX < flee)
            {
                float awayDir = -signToPlayer * retreatSpeedFactor * moveScale;
                return new AIActionDecision(AIActionType.MoveAwayFromPlayer, awayDir, false, false);
            }

            if (distX > pref * 1.15f)
            {
                float towardDir = signToPlayer * moveScale;
                return new AIActionDecision(AIActionType.MoveTowardPlayer, towardDir, true, false);
            }

            if (AIDifficultyTuning.IsCooldownReady(context.AttackCooldownRemaining, difficulty))
            {
                return new AIActionDecision(AIActionType.RangedVolley, 0f, true, false);
            }

            return new AIActionDecision(AIActionType.Idle, 0f, true, false);
        }
    }
}
