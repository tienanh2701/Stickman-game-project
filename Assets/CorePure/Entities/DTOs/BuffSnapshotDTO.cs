using System;

namespace StickmanBeatEmUp.CorePure.Entities.DTOs
{
    /// <summary>
    /// Trạng thái buff vật phẩm đang kích hoạt để bảo toàn khi Save/Load giữa trận.
    /// </summary>
    [Serializable]
    public class BuffSnapshotDTO
    {
        public string buffId;
        public float remainingSeconds;
        public float remainingShield;
    }
}
