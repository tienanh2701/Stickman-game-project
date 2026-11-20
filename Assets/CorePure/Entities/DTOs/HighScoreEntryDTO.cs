using System;

namespace StickmanBeatEmUp.CorePure.Entities.DTOs
{
    /// <summary>
    /// Một bản ghi thành tích trong bảng xếp hạng / thống kê trận đấu (HighScore JSON).
    /// </summary>
    [Serializable]
    public class HighScoreEntryDTO
    {
        public string username = string.Empty;
        public int gameMode;
        public int worldOrWaveReached;
        public int score;
        public float clearTimeSeconds;
        public string achievedAtUtc = string.Empty;
    }
}
