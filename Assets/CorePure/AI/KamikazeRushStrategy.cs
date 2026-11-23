using System;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.AI
{
    /// <summary>
    /// Kamikaze rush AI strategy (e.g., Saibaman Kamikaze): rushes toward player at 1.5x speed
    /// and triggers Detonate immediately when within scaled blast trigger range.
    /// </summary>
    public sealed class KamikazeRushStrategy : IAIStrategy
    {
        public const string ID = "kamikaze_rush";

        private readonly float defaultDetonateRange;
        private readonly float verticalTolerance;
        private readonly float rushSpeedFactor;

        public string StrategyId => ID;

        public KamikazeRushStrategy(
            float defaultDetonateRange = 1.45f,
            float verticalTolerance = 1.8f,
            float rushSpeedFactor = 1.5f)
        {
            this.defaultDetonateRange = AIDifficultyTuning.SanitizePositive(defaultDetonateRange, 1.45f);
            this.verticalTolerance = AIDifficultyTuning.SanitizePositive(verticalTolerance, 1.8f);
            this.rushSpeedFactor = AIDifficultyTuning.SanitizePositive(rushSpeedFactor, 1.5f);
        }

        public AIActionDecision ComputeAction(EnemyAIContext context)
        {
            if (AIDifficultyTuning.IsDeadOrInvalid(context))
            {
                return AIActionDecision.IdleDecision;
            }

            float dx = context.PlayerX - context.EnemyX;
            float distX = Math.Abs(dx);
            float distY = Math.Abs(context.PlayerY - context.EnemyY);

            int difficulty = AIDifficultyTuning.ClampDifficulty(context.DifficultyLevel);
            float range = AIDifficultyTuning.SanitizePositive(context.AttackRange, defaultDetonateRange) *
                          AIDifficultyTuning.GetRangeScale(difficulty);

            if (distX <= range && distY <= verticalTolerance)
            {
                if (AIDifficultyTuning.IsCooldownReady(context.AttackCooldownRemaining, difficulty))
                {
                    return new AIActionDecision(AIActionType.Detonate, 0f, true, false);
                }

                return new AIActionDecision(AIActionType.Idle, 0f, true, false);
            }

            float sign = AIDifficultyTuning.ComputeHorizontalSign(dx);
            if (sign == 0f)
            {
                return new AIActionDecision(AIActionType.Idle, 0f, true, false);
            }

            float moveDir = sign * rushSpeedFactor * AIDifficultyTuning.GetMoveSpeedScale(difficulty);
            return new AIActionDecision(AIActionType.MoveTowardPlayer, moveDir, true, false);
        }
    }
}
