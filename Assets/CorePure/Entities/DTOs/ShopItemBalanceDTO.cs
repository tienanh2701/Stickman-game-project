using System;

namespace StickmanBeatEmUp.CorePure.Entities.DTOs
{
    /// <summary>
    /// Thông số cân bằng vật phẩm tiêu hao đọc từ cấu hình JSON ngoài build.
    /// </summary>
    [Serializable]
    public class ShopItemBalanceDTO
    {
        public int itemTypeId;
        public string itemName = string.Empty;
        public int price = 30;
        public float healAmount;
        public float buffDuration;
        public float shieldAmount;
        public float reflectDamage;
        public float damageMultiplier = 1f;
        public float speedMultiplier = 1f;
        public float ragePerSecond;
    }
}
