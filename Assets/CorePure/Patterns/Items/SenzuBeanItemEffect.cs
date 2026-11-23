using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Patterns.Items
{
    /// <summary>
    /// Strategy cho Đậu Thần (SenzuBean): hồi HP tức thì và xóa hiệu ứng bất lợi.
    /// </summary>
    public sealed class SenzuBeanItemEffect : IItemEffect
    {
        private readonly float healAmount;

        public int ItemTypeId => ConsumableItemIds.SENZU_BEAN;
        public string ItemName => "Đậu Thần";

        public SenzuBeanItemEffect(float healAmount = 10f)
        {
            this.healAmount = SanitizeNonNegative(healAmount);
        }

        public bool CanApply(ItemUseContext context)
        {
            return !context.IsPlayerDead && context.QuantityOwned > 0 && context.MaxHp > 0f;
        }

        public ItemEffectResult ApplyEffect(ItemUseContext context)
        {
            if (!CanApply(context))
            {
                return ItemEffectResult.Rejected("Không thể dùng Đậu Thần lúc này.");
            }

            float effectiveHeal = healAmount > 0f ? healAmount : 10f;
            return new ItemEffectResult
            {
                Applied = true,
                HealAmount = effectiveHeal,
                RageDelta = 0f,
                ShieldAmount = 0f,
                ReflectDamage = 0f,
                BuffDuration = 0f,
                DamageMultiplier = 1f,
                SpeedMultiplier = 1f,
                RagePerSecond = 0f,
                ClearNegativeEffects = true,
                FeedbackMessage = $"Hồi {effectiveHeal:0} HP và giải trừ debuff!"
            };
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
