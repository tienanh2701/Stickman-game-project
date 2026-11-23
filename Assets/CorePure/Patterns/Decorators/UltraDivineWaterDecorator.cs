using System;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Patterns.Decorators
{
    /// <summary>
    /// Decorator cho Nước Siêu Thần: nhân hệ số sát thương, tốc độ di chuyển và cộng hồi Nộ mỗi giây.
    /// </summary>
    public sealed class UltraDivineWaterDecorator : StatDecoratorBase
    {
        private readonly float damageMultiplier;
        private readonly float speedMultiplier;
        private readonly float ragePerSecondBonus;

        public UltraDivineWaterDecorator(
            IStatSource inner,
            float damageMultiplier = 1.5f,
            float speedMultiplier = 1.3f,
            float ragePerSecondBonus = 2f)
            : base(inner)
        {
            this.damageMultiplier = SanitizeMultiplier(damageMultiplier, 1f);
            this.speedMultiplier = SanitizeMultiplier(speedMultiplier, 1f);
            this.ragePerSecondBonus = SanitizeNonNegative(ragePerSecondBonus);
        }

        public override float AttackDamageMultiplier => Inner.AttackDamageMultiplier * damageMultiplier;
        public override float MoveSpeedMultiplier => Inner.MoveSpeedMultiplier * speedMultiplier;
        public override float RagePerSecondBonus => Inner.RagePerSecondBonus + ragePerSecondBonus;

        private static float SanitizeMultiplier(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return fallback;
            }
            return value;
        }

        private static float SanitizeNonNegative(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return 0f;
            }
            return value;
        }
    }
}
