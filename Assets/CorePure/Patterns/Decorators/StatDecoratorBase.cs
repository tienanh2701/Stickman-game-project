using System;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Patterns.Decorators
{
    /// <summary>
    /// Lớp gốc theo mẫu GoF Decorator bọc một IStatSource để cộng dồn buff chỉ số linh hoạt tại runtime.
    /// </summary>
    public abstract class StatDecoratorBase : IStatSource
    {
        protected readonly IStatSource Inner;

        protected StatDecoratorBase(IStatSource inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public virtual float AttackDamageMultiplier => Inner.AttackDamageMultiplier;
        public virtual float MoveSpeedMultiplier => Inner.MoveSpeedMultiplier;
        public virtual float MaxShieldCapacity => Inner.MaxShieldCapacity;
        public virtual float DamageReflectAmount => Inner.DamageReflectAmount;
        public virtual float RagePerSecondBonus => Inner.RagePerSecondBonus;
    }
}
