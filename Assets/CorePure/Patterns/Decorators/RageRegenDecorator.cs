using System;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Patterns.Decorators
{
    /// <summary>
    /// Decorator tăng tốc độ hồi Nộ mỗi giây (dùng cho vật phẩm thứ 4 và làm mẫu mở rộng Live OCP).
    /// </summary>
    public sealed class RageRegenDecorator : StatDecoratorBase
    {
        private readonly float ragePerSecondBonus;

        public RageRegenDecorator(IStatSource inner, float ragePerSecondBonus = 3f)
            : base(inner)
        {
            this.ragePerSecondBonus = SanitizeNonNegative(ragePerSecondBonus);
        }

        public override float RagePerSecondBonus => Inner.RagePerSecondBonus + ragePerSecondBonus;

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
