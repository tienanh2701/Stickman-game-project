using System;
using System.Collections.Generic;
using StickmanBeatEmUp.CorePure.Exceptions;

namespace StickmanBeatEmUp.CorePure.Entities.DTOs
{
    /// <summary>
    /// Schema cấu hình cân bằng toàn cục đọc từ tệp JSON ngoài build (StreamingAssets/Config/game_balance.json).
    /// </summary>
    [Serializable]
    public class GameBalanceConfigDTO
    {
        public int version = 1;
        public float playerBaseMaxHp = 100f;
        public int maxRage = 30;
        public float blockDamageMultiplier = 0.2f;
        public float endlessWaveHpGrowth = 0.03f;
        public float endlessMaxDamageMultiplier = 1.75f;
        public List<ShopItemBalanceDTO> shopItems = new List<ShopItemBalanceDTO>();
        public List<LevelBalanceDTO> levels = new List<LevelBalanceDTO>();

        /// <summary>
        /// Kiểm tra các bất biến miền của cấu hình cân bằng trước khi áp dụng.
        /// </summary>
        public void Validate()
        {
            if (playerBaseMaxHp <= 0f)
            {
                throw new InvalidGameConfigException(nameof(playerBaseMaxHp), "playerBaseMaxHp must be > 0.");
            }

            if (maxRage <= 0)
            {
                throw new InvalidGameConfigException(nameof(maxRage), "maxRage must be > 0.");
            }

            if (blockDamageMultiplier < 0f || blockDamageMultiplier > 1f)
            {
                throw new InvalidGameConfigException(nameof(blockDamageMultiplier), "blockDamageMultiplier must be in [0, 1].");
            }

            if (endlessWaveHpGrowth < 0f || endlessMaxDamageMultiplier < 1f)
            {
                throw new InvalidGameConfigException("endlessScaling", "Endless scaling parameters are out of valid bounds.");
            }
        }
    }
}
