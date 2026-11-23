using System;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.AI
{
    /// <summary>
    /// Melee chase AI strategy: approaches player along X, stops inside effective attack range,
    /// and executes light or low-HP Hard heavy strikes once cooldown is ready.
    /// </summary>
    public sealed class MeleeChaseStrategy : IAIStrategy
    {
        public const string ID = "melee_chase";

        private readonly float defaultAttackRange;
        private readonly float verticalTolerance;

        public string StrategyId => ID;

        public MeleeChaseStrategy(float defaultAttackRange = 1.45f, float verticalTolerance = 1.8f)
        {
            this.defaultAttackRange = AIDifficultyTuning.SanitizePositive(defaultAttackRange, 1.45f);
            this.verticalTolerance = AIDifficultyTuning.SanitizePositive(verticalTolerance, 1.8f);
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
            float baseRange = AIDifficultyTuning.SanitizePositive(context.AttackRange, defaultAttackRange);
            float effectiveRange = baseRange * AIDifficultyTuning.GetRangeScale(difficulty);

            if (distX <= effectiveRange && distY <= verticalTolerance)
            {
                if (AIDifficultyTuning.IsCooldownReady(context.AttackCooldownRemaining, difficulty))
                {
                    bool hasValidMaxHp = !float.IsNaN(context.EnemyMaxHp) &&
                                         !float.IsInfinity(context.EnemyMaxHp) &&
                                         context.EnemyMaxHp > 0f;
                    bool lowHpHardFinisher = difficulty == (int)AIDifficultyPreset.Hard &&
                                             hasValidMaxHp &&
                                             (context.EnemyHp / context.EnemyMaxHp) <= 0.50f;
                    AIActionType strike = lowHpHardFinisher ? AIActionType.HeavyAttack : AIActionType.LightAttack;
                    return new AIActionDecision(strike, 0f, true, false);
                }

                return new AIActionDecision(AIActionType.Idle, 0f, true, false);
            }

            float sign = AIDifficultyTuning.ComputeHorizontalSign(dx);
            if (sign == 0f)
            {
                return new AIActionDecision(AIActionType.Idle, 0f, true, false);
            }

            float moveDir = sign * AIDifficultyTuning.GetMoveSpeedScale(difficulty);
            return new AIActionDecision(AIActionType.MoveTowardPlayer, moveDir, true, false);
        }
    }
}
