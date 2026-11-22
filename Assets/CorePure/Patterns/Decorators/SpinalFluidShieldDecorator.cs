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
            this.shieldCapacityBonus = Math.Max(0f, shieldCapacityBonus);
            this.damageReflectBonus = Math.Max(0f, damageReflectBonus);
        }

        public override float MaxShieldCapacity => Inner.MaxShieldCapacity + shieldCapacityBonus;
        public override float DamageReflectAmount => Inner.DamageReflectAmount + damageReflectBonus;
    }
}
