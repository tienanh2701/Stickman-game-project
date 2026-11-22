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
            this.damageMultiplier = damageMultiplier > 0f ? damageMultiplier : 1f;
            this.speedMultiplier = speedMultiplier > 0f ? speedMultiplier : 1f;
            this.ragePerSecondBonus = Math.Max(0f, ragePerSecondBonus);
        }

        public override float AttackDamageMultiplier => Inner.AttackDamageMultiplier * damageMultiplier;
        public override float MoveSpeedMultiplier => Inner.MoveSpeedMultiplier * speedMultiplier;
        public override float RagePerSecondBonus => Inner.RagePerSecondBonus + ragePerSecondBonus;
    }
}
