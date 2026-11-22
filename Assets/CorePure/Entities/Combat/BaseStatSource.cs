using System;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Entities.Combat
{
    /// <summary>
    /// Nguồn chỉ số nền mặc định (1f cho hệ số nhân, 0f cho phần cộng thêm).
    /// Đóng vai trò ConcreteComponent gốc cho chuỗi GoF Decorator IStatSource của TV2.
    /// </summary>
    public sealed class BaseStatSource : IStatSource
    {
        public static BaseStatSource Default { get; } = new BaseStatSource();

        public float AttackDamageMultiplier { get; }
        public float MoveSpeedMultiplier { get; }
        public float MaxShieldCapacity { get; }
        public float DamageReflectAmount { get; }
        public float RagePerSecondBonus { get; }

        public BaseStatSource()
            : this(1f, 1f, 0f, 0f, 0f)
        {
        }

        public BaseStatSource(
            float attackDamageMultiplier,
            float moveSpeedMultiplier,
            float maxShieldCapacity,
            float damageReflectAmount,
            float ragePerSecondBonus)
        {
            AttackDamageMultiplier = SanitizeMultiplier(attackDamageMultiplier, 1f);
            MoveSpeedMultiplier = SanitizeMultiplier(moveSpeedMultiplier, 1f);
            MaxShieldCapacity = SanitizeNonNegative(maxShieldCapacity);
            DamageReflectAmount = SanitizeNonNegative(damageReflectAmount);
            RagePerSecondBonus = SanitizeNonNegative(ragePerSecondBonus);
        }

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
