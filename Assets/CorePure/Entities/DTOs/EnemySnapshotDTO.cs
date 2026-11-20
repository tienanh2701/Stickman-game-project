using System;

namespace StickmanBeatEmUp.CorePure.Entities.DTOs
{
    /// <summary>
    /// Trạng thái lưu tạm của một kẻ địch đang còn sống trên sân đấu để khôi phục giữa trận.
    /// </summary>
    [Serializable]
    public class EnemySnapshotDTO
    {
        public string enemyId;
        public float hp;
        public float maxHp;
        public float posX;
        public float posY;
        public int phase;
        public bool isEnraged;
    }
}
