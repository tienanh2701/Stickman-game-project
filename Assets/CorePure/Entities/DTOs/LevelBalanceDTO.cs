using System;

namespace StickmanBeatEmUp.CorePure.Entities.DTOs
{
    /// <summary>
    /// Hệ số cân bằng độ khó theo từng màn Campaign đọc từ cấu hình JSON ngoài build.
    /// </summary>
    [Serializable]
    public class LevelBalanceDTO
    {
        public int worldIndex = 1;
        public int stageIndex = 1;
        public float enemyHpMultiplier = 1f;
        public float enemyDamageMultiplier = 1f;
        public int bonusClearCoins = 20;
    }
}
