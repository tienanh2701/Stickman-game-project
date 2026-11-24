using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Patterns.Items
{
    /// <summary>
    /// Strategy cho Vật phẩm thứ 4 — Tinh Chất Cuồng Nộ (RageElixir):
    /// cộng tức thì +5 Nộ và kích hoạt RageRegenDecorator (+3 Nộ/giây trong 20 giây).
    /// </summary>
    public sealed class RageElixirItemEffect : IItemEffect
    {
        private readonly float instantRageBonus;
        private readonly float durationSeconds;
        private readonly float ragePerSecond;

        public int ItemTypeId => ConsumableItemIds.RAGE_ELIXIR;
        public string ItemName => "Tinh Chất Cuồng Nộ";

        public RageElixirItemEffect(
            float instantRageBonus = 5f,
            float durationSeconds = 20f,
            float ragePerSecond = 3f)
        {
            this.instantRageBonus = SanitizeNonNegative(instantRageBonus);
            this.durationSeconds = SanitizePositive(durationSeconds, 20f);
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
                return ItemEffectResult.Rejected("Không thể dùng Tinh Chất Cuồng Nộ.");
            }

            return new ItemEffectResult
            {
                Applied = true,
                HealAmount = 0f,
                RageDelta = instantRageBonus,
                ShieldAmount = 0f,
                ReflectDamage = 0f,
                BuffDuration = durationSeconds,
                DamageMultiplier = 1f,
                SpeedMultiplier = 1f,
                RagePerSecond = ragePerSecond,
                ClearNegativeEffects = false,
                FeedbackMessage = $"+{instantRageBonus:0} Nộ tức thì và +{ragePerSecond:0.0} Nộ/s trong {durationSeconds:0}s!"
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
