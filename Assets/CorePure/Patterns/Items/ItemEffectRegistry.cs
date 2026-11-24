using System;
using System.Collections.Generic;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Patterns.Items
{
    /// <summary>
    /// Sổ đăng ký Strategy IItemEffect theo nguyên lý Open/Closed (OCP).
    /// Khi thêm vật phẩm mới, chỉ cần viết lớp implement IItemEffect và gọi Register(...) mà không sửa lớp cũ.
    /// </summary>
    public sealed class ItemEffectRegistry
    {
        private readonly Dictionary<int, IItemEffect> effectsById = new Dictionary<int, IItemEffect>();

        public int RegisteredCount => effectsById.Count;

        public void Register(IItemEffect effect)
        {
            if (effect == null)
            {
                throw new ArgumentNullException(nameof(effect));
            }

            if (effect.ItemTypeId <= ConsumableItemIds.NONE)
            {
                throw new ArgumentOutOfRangeException(nameof(effect), "ItemTypeId must be > 0.");
            }

            effectsById[effect.ItemTypeId] = effect;
        }

        public bool Contains(int itemTypeId)
        {
            return effectsById.ContainsKey(itemTypeId);
        }

        public bool TryGetEffect(int itemTypeId, out IItemEffect effect)
        {
            return effectsById.TryGetValue(itemTypeId, out effect);
        }

        public ItemEffectResult Execute(int itemTypeId, ItemUseContext context)
        {
            if (!effectsById.TryGetValue(itemTypeId, out IItemEffect effect) || effect == null)
            {
                return ItemEffectResult.Rejected($"Chưa đăng ký hiệu ứng cho vật phẩm mã {itemTypeId}.");
            }

            if (!effect.CanApply(context))
            {
                return ItemEffectResult.Rejected($"Không đủ điều kiện sử dụng {effect.ItemName}.");
            }

            return effect.ApplyEffect(context);
        }

        public static ItemEffectRegistry CreateDefault()
        {
            ItemEffectRegistry registry = new ItemEffectRegistry();
            registry.Register(new SenzuBeanItemEffect());
            registry.Register(new SpinalFluidItemEffect());
            registry.Register(new UltraDivineWaterItemEffect());
            registry.Register(new RageElixirItemEffect());
            return registry;
        }
    }
}
