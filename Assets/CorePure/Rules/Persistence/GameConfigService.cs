using System;
using System.Collections.Generic;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Exceptions;

namespace StickmanBeatEmUp.CorePure.Rules.Persistence
{
    /// <summary>
    /// Phân tích và kiểm định tệp cấu hình cân bằng ngoài build (game_balance.json) thuần C#.
    /// Ném InvalidGameConfigException nếu thiếu trường, lỗi JSON hoặc chỉ số vượt biên hợp lệ.
    /// </summary>
    public static class GameConfigService
    {
        public static GameBalanceConfigDTO ParseAndValidate(string json, string configKey = "game_balance.json")
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidGameConfigException(configKey, "Game balance config JSON is empty.");
            }

            try
            {
                Dictionary<string, object> root = SimpleJsonCodec.ParseObject(json);
                GameBalanceConfigDTO cfg = new GameBalanceConfigDTO
                {
                    version = SimpleJsonCodec.ReadInt(root, "version", 0),
                    playerBaseMaxHp = SimpleJsonCodec.ReadFloat(root, "playerBaseMaxHp", 0f),
                    maxRage = SimpleJsonCodec.ReadInt(root, "maxRage", 0),
                    blockDamageMultiplier = SimpleJsonCodec.ReadFloat(root, "blockDamageMultiplier", -1f),
                    endlessWaveHpGrowth = SimpleJsonCodec.ReadFloat(root, "endlessWaveHpGrowth", -1f),
                    endlessMaxDamageMultiplier = SimpleJsonCodec.ReadFloat(root, "endlessMaxDamageMultiplier", 0f),
                    shopItems = ParseShopItems(SimpleJsonCodec.ReadArray(root, "shopItems")),
                    levels = ParseLevels(SimpleJsonCodec.ReadArray(root, "levels"))
                };

                ValidateFull(cfg);
                return cfg;
            }
            catch (InvalidGameConfigException)
            {
                throw;
            }
            catch (FormatException ex)
            {
                throw new InvalidGameConfigException(configKey, $"Malformed config JSON: {ex.Message}", ex);
            }
        }

        public static void ValidateFull(GameBalanceConfigDTO config)
        {
            if (config == null)
            {
                throw new InvalidGameConfigException("root", "GameBalanceConfigDTO cannot be null.");
            }

            if (config.version <= 0)
            {
                throw new InvalidGameConfigException(nameof(config.version), "Config version must be > 0.");
            }

            config.Validate();

            if (config.shopItems != null)
            {
                for (int i = 0; i < config.shopItems.Count; i++)
                {
                    ShopItemBalanceDTO item = config.shopItems[i];
                    if (item == null || item.itemTypeId <= 0)
                    {
                        throw new InvalidGameConfigException($"shopItems[{i}].itemTypeId", "Shop itemTypeId must be > 0.");
                    }
                    if (item.price < 0)
                    {
                        throw new InvalidGameConfigException($"shopItems[{i}].price", "Shop item price cannot be negative.");
                    }
                    if (item.damageMultiplier <= 0f || item.speedMultiplier <= 0f)
                    {
                        throw new InvalidGameConfigException($"shopItems[{i}].multipliers", "Item multipliers must be > 0.");
                    }
                }
            }

            if (config.levels != null)
            {
                for (int i = 0; i < config.levels.Count; i++)
                {
                    LevelBalanceDTO lvl = config.levels[i];
                    if (lvl == null || lvl.worldIndex < 1 || lvl.stageIndex < 1)
                    {
                        throw new InvalidGameConfigException($"levels[{i}]", "Level worldIndex and stageIndex must be >= 1.");
                    }
                    if (lvl.enemyHpMultiplier <= 0f || lvl.enemyDamageMultiplier <= 0f)
                    {
                        throw new InvalidGameConfigException($"levels[{i}].multipliers", "Enemy multipliers must be > 0.");
                    }
                    if (lvl.bonusClearCoins < 0)
                    {
                        throw new InvalidGameConfigException($"levels[{i}].bonusClearCoins", "bonusClearCoins cannot be negative.");
                    }
                }
            }
        }

        private static List<ShopItemBalanceDTO> ParseShopItems(List<object> rawList)
        {
            List<ShopItemBalanceDTO> list = new List<ShopItemBalanceDTO>(rawList.Count);
            for (int i = 0; i < rawList.Count; i++)
            {
                Dictionary<string, object> obj = rawList[i] as Dictionary<string, object>;
                if (obj == null)
                {
                    throw new InvalidGameConfigException($"shopItems[{i}]", "Shop item entry must be a JSON object.");
                }
                list.Add(new ShopItemBalanceDTO
                {
                    itemTypeId = SimpleJsonCodec.ReadInt(obj, "itemTypeId", 0),
                    itemName = SimpleJsonCodec.ReadString(obj, "itemName", string.Empty),
                    price = SimpleJsonCodec.ReadInt(obj, "price", -1),
                    healAmount = SimpleJsonCodec.ReadFloat(obj, "healAmount", 0f),
                    buffDuration = SimpleJsonCodec.ReadFloat(obj, "buffDuration", 0f),
                    shieldAmount = SimpleJsonCodec.ReadFloat(obj, "shieldAmount", 0f),
                    reflectDamage = SimpleJsonCodec.ReadFloat(obj, "reflectDamage", 0f),
                    damageMultiplier = SimpleJsonCodec.ReadFloat(obj, "damageMultiplier", 1f),
                    speedMultiplier = SimpleJsonCodec.ReadFloat(obj, "speedMultiplier", 1f),
                    ragePerSecond = SimpleJsonCodec.ReadFloat(obj, "ragePerSecond", 0f)
                });
            }
            return list;
        }

        private static List<LevelBalanceDTO> ParseLevels(List<object> rawList)
        {
            List<LevelBalanceDTO> list = new List<LevelBalanceDTO>(rawList.Count);
            for (int i = 0; i < rawList.Count; i++)
            {
                Dictionary<string, object> obj = rawList[i] as Dictionary<string, object>;
                if (obj == null)
                {
                    throw new InvalidGameConfigException($"levels[{i}]", "Level entry must be a JSON object.");
                }
                list.Add(new LevelBalanceDTO
                {
                    worldIndex = SimpleJsonCodec.ReadInt(obj, "worldIndex", 0),
                    stageIndex = SimpleJsonCodec.ReadInt(obj, "stageIndex", 0),
                    enemyHpMultiplier = SimpleJsonCodec.ReadFloat(obj, "enemyHpMultiplier", 0f),
                    enemyDamageMultiplier = SimpleJsonCodec.ReadFloat(obj, "enemyDamageMultiplier", 0f),
                    bonusClearCoins = SimpleJsonCodec.ReadInt(obj, "bonusClearCoins", 0)
                });
            }
            return list;
        }
    }
}
