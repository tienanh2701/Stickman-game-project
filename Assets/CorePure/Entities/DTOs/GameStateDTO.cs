using System;
using System.Collections.Generic;
using StickmanBeatEmUp.CorePure.Exceptions;

namespace StickmanBeatEmUp.CorePure.Entities.DTOs
{
    /// <summary>
    /// DTO lưu trữ thống nhất toàn bộ trạng thái trận đấu giữa 2 phân hệ để Save/Load khôi phục 100%.
    /// </summary>
    [Serializable]
    public class GameStateDTO
    {
        public int schemaVersion = 1;
        public string accountUsername = string.Empty;

        // Phân hệ Chiến đấu / Màn chơi (TV1)
        public int gameMode;
        public int worldIndex = 1;
        public int stageIndex = 1;
        public int currentWaveIndex = 1;
        public int playerCharacterIndex;
        public float playerHp = 100f;
        public float playerMaxHp = 100f;
        public float playerPosX;
        public float playerPosY = -2.5f;
        public float playerRage;
        public bool isTransformed;
        public float virtualShieldHp;
        public List<EnemySnapshotDTO> activeEnemies = new List<EnemySnapshotDTO>();

        // Phân hệ Kinh tế / Vật phẩm / Buff / Điểm (TV2)
        public int coins;
        public int senzuCount;
        public int spinalFluidCount;
        public int ultraWaterCount;
        public List<ItemCountEntryDTO> extraItemCounts = new List<ItemCountEntryDTO>();
        public int equippedSlotQ = 1;
        public int equippedSlotE = 2;
        public List<BuffSnapshotDTO> activeBuffs = new List<BuffSnapshotDTO>();
        public int score;
        public float elapsedSeconds;
        public string savedAtUtc = string.Empty;

        /// <summary>
        /// Kiểm tra tính hợp lệ của dữ liệu Save trước khi nạp vào trận đấu.
        /// </summary>
        public void Validate(string storageKey = "save_slot")
        {
            if (schemaVersion <= 0)
            {
                throw new SaveCorruptedException(storageKey, "Invalid save schemaVersion.");
            }

            if (playerMaxHp <= 0f || playerHp < 0f || playerHp > playerMaxHp)
            {
                throw new SaveCorruptedException(storageKey, "Corrupted player health values in save data.");
            }

            if (playerRage < 0f || virtualShieldHp < 0f)
            {
                throw new SaveCorruptedException(storageKey, "Negative rage or shield value in save data.");
            }

            if (worldIndex < 1 || stageIndex < 1 || currentWaveIndex < 1)
            {
                throw new SaveCorruptedException(storageKey, "Invalid world, stage, or wave index in save data.");
            }

            if (coins < 0 || senzuCount < 0 || spinalFluidCount < 0 || ultraWaterCount < 0 || score < 0)
            {
                throw new SaveCorruptedException(storageKey, "Negative economy, inventory, or score value in save data.");
            }

            if (string.IsNullOrWhiteSpace(savedAtUtc))
            {
                throw new SaveCorruptedException(storageKey, "Missing savedAtUtc timestamp in save data.");
            }
        }
    }
}
