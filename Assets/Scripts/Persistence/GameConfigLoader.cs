using System;
using System.IO;
using System.Text;
using UnityEngine;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Rules.Persistence;

namespace StickmanBeatEmUp.Persistence
{
    /// <summary>
    /// Nạp tệp cấu hình cân bằng ngoài build (StreamingAssets/Config/game_balance.json).
    /// Cho phép sửa chỉ số bằng Notepad ngoài bản build .exe và kiểm định qua InvalidGameConfigException.
    /// </summary>
    public static class GameConfigLoader
    {
        public const string CONFIG_FILE_NAME = "game_balance.json";

        private static GameBalanceConfigDTO cachedConfig;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cachedConfig = null;
        }

        public static string GetConfigFilePath()
        {
            return Path.Combine(Application.streamingAssetsPath, "Config", CONFIG_FILE_NAME);
        }

        /// <summary>
        /// Đọc và kiểm định nghiêm ngặt game_balance.json — ném InvalidGameConfigException khi hỏng (dùng cho Live Fault-Injection).
        /// </summary>
        public static GameBalanceConfigDTO LoadOrThrow(string overridePath = null)
        {
            string path = string.IsNullOrWhiteSpace(overridePath) ? GetConfigFilePath() : overridePath;
            if (!File.Exists(path))
            {
                throw new InvalidGameConfigException(CONFIG_FILE_NAME, $"Config file missing at '{path}'.");
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                GameBalanceConfigDTO parsed = GameConfigService.ParseAndValidate(json, CONFIG_FILE_NAME);
                cachedConfig = parsed;
                return parsed;
            }
            catch (InvalidGameConfigException)
            {
                throw;
            }
            catch (IOException ex)
            {
                throw new InvalidGameConfigException(CONFIG_FILE_NAME, $"I/O failure loading config: {ex.Message}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidGameConfigException(CONFIG_FILE_NAME, $"Access denied loading config: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Đọc an toàn cho runtime: nếu tệp bị người dùng sửa hỏng, ghi log có cấu trúc và quay về cấu hình mặc định.
        /// </summary>
        public static GameBalanceConfigDTO LoadOrDefault(bool forceReload = false)
        {
            if (!forceReload && cachedConfig != null)
            {
                return cachedConfig;
            }

            try
            {
                return LoadOrThrow();
            }
            catch (InvalidGameConfigException ex)
            {
                Debug.Log($"[GameConfigLoader][{ex.ErrorCode}] Key='{ex.ConfigKey}': {ex.Message}. Falling back to defaults.");
                cachedConfig = new GameBalanceConfigDTO();
                return cachedConfig;
            }
        }

        public static LevelBalanceDTO GetLevelBalance(int worldIndex, int stageIndex)
        {
            GameBalanceConfigDTO cfg = LoadOrDefault();
            if (cfg != null && cfg.levels != null)
            {
                for (int i = 0; i < cfg.levels.Count; i++)
                {
                    LevelBalanceDTO lvl = cfg.levels[i];
                    if (lvl != null && lvl.worldIndex == worldIndex && lvl.stageIndex == stageIndex)
                    {
                        return lvl;
                    }
                }
            }
            return new LevelBalanceDTO { worldIndex = worldIndex, stageIndex = stageIndex };
        }
    }
}
