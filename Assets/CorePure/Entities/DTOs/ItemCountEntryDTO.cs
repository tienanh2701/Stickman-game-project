using System;

namespace StickmanBeatEmUp.CorePure.Entities.DTOs
{
    /// <summary>
    /// Cặp mã vật phẩm và số lượng tương thích tuần tự hóa JSON (thay thế Dictionary để tương thích JsonUtility).
    /// </summary>
    [Serializable]
    public class ItemCountEntryDTO
    {
        public int itemTypeId;
        public int count;
    }
}
