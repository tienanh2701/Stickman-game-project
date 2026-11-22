using System;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Patterns.Decorators
{
    /// <summary>
    /// Decorator cho Dịch Tủy Titan: cộng thêm dung lượng giáp ảo và sát thương phản lại khi bị đánh.
    /// </summary>
    public sealed class SpinalFluidShieldDecorator : StatDecoratorBase
    {
        private readonly float shieldCapacityBonus;
        private readonly float damageReflectBonus;

        public SpinalFluidShieldDecorator(
            IStatSource inner,
            float shieldCapacityBonus = 40f,
            float damageReflectBonus = 5f)
            : base(inner)
        {
            this.shieldCapacityBonus = SanitizeNonNegative(shieldCapacityBonus);
            this.damageReflectBonus = SanitizeNonNegative(damageReflectBonus);
        }

        public override float MaxShieldCapacity => Inner.MaxShieldCapacity + shieldCapacityBonus;
        public override float DamageReflectAmount => Inner.DamageReflectAmount + damageReflectBonus;

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
