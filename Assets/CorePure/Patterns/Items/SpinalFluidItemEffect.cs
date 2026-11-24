using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Patterns.Items
{
    /// <summary>
    /// Strategy cho Dịch Tủy Titan (SpinalFluid): cấp giáp ảo và phản sát thương theo thời lượng.
    /// </summary>
    public sealed class SpinalFluidItemEffect : IItemEffect
    {
        private readonly float durationSeconds;
        private readonly float shieldAmount;
        private readonly float reflectDamage;

        public int ItemTypeId => ConsumableItemIds.SPINAL_FLUID;
        public string ItemName => "Dịch Tủy Titan";

        public SpinalFluidItemEffect(
            float durationSeconds = 30f,
            float shieldAmount = 40f,
            float reflectDamage = 5f)
        {
            this.durationSeconds = SanitizePositive(durationSeconds, 30f);
            this.shieldAmount = SanitizeNonNegative(shieldAmount);
            this.reflectDamage = SanitizeNonNegative(reflectDamage);
        }

        public bool CanApply(ItemUseContext context)
        {
            return !context.IsPlayerDead && context.QuantityOwned > 0;
        }

        public ItemEffectResult ApplyEffect(ItemUseContext context)
        {
            if (!CanApply(context))
            {
                return ItemEffectResult.Rejected("Không thể kích hoạt Dịch Tủy Titan.");
            }

            return new ItemEffectResult
            {
                Applied = true,
                HealAmount = 0f,
                RageDelta = 0f,
                ShieldAmount = shieldAmount,
                ReflectDamage = reflectDamage,
                BuffDuration = durationSeconds,
                DamageMultiplier = 1f,
                SpeedMultiplier = 1f,
                RagePerSecond = 0f,
                ClearNegativeEffects = false,
                FeedbackMessage = $"Kích hoạt giáp ảo +{shieldAmount:0} HP và phản {reflectDamage:0} sát thương!"
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
