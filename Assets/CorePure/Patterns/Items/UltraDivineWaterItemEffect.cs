using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Patterns.Items
{
    /// <summary>
    /// Strategy cho Nước Siêu Thần (UltraDivineWater): tăng sát thương, tốc độ và hồi Nộ mỗi giây.
    /// </summary>
    public sealed class UltraDivineWaterItemEffect : IItemEffect
    {
        private readonly float durationSeconds;
        private readonly float damageMultiplier;
        private readonly float speedMultiplier;
        private readonly float ragePerSecond;

        public int ItemTypeId => ConsumableItemIds.ULTRA_DIVINE_WATER;
        public string ItemName => "Nước Siêu Thần";

        public UltraDivineWaterItemEffect(
            float durationSeconds = 60f,
            float damageMultiplier = 1.5f,
            float speedMultiplier = 1.3f,
            float ragePerSecond = 2f)
        {
            this.durationSeconds = SanitizePositive(durationSeconds, 60f);
            this.damageMultiplier = SanitizePositive(damageMultiplier, 1.5f);
            this.speedMultiplier = SanitizePositive(speedMultiplier, 1.3f);
            this.ragePerSecond = SanitizeNonNegative(ragePerSecond);
        }

        public bool CanApply(ItemUseContext context)
        {
            return !context.IsPlayerDead && context.QuantityOwned > 0;
        }

        public ItemEffectResult ApplyEffect(ItemUseContext context)
        {
            if (!CanApply(context))
            {
                return ItemEffectResult.Rejected("Không thể dùng Nước Siêu Thần.");
            }

            return new ItemEffectResult
            {
                Applied = true,
                HealAmount = 0f,
                RageDelta = 0f,
                ShieldAmount = 0f,
                ReflectDamage = 0f,
                BuffDuration = durationSeconds,
                DamageMultiplier = damageMultiplier,
                SpeedMultiplier = speedMultiplier,
                RagePerSecond = ragePerSecond,
                ClearNegativeEffects = false,
                FeedbackMessage = $"Tăng {damageMultiplier:0.0}x sát thương, {speedMultiplier:0.0}x tốc độ và +{ragePerSecond:0.0} Nộ/s!"
            };
        }

        private static float SanitizePositive(float value, float fallback)
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
