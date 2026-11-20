using StickmanBeatEmUp.CorePure.Entities.DTOs;

namespace StickmanBeatEmUp.CorePure.Interfaces
{
    /// <summary>
    /// Hợp đồng Strategy cho hiệu ứng vật phẩm tiêu hao, thay thế switch-case cứng để đạt Live OCP.
    /// </summary>
    public interface IItemEffect
    {
        int ItemTypeId { get; }
        string ItemName { get; }

        bool CanApply(ItemUseContext context);

        ItemEffectResult ApplyEffect(ItemUseContext context);
    }
}
