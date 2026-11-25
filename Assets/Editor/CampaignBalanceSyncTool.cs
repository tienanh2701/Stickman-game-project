using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Rules.Combat;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.EditorTools
{
    /// <summary>
    /// Công cụ Editor đồng bộ hệ số enemyHpMultiplier / enemyDamageMultiplier cho 12 Level_W*_S*.asset
    /// từ Assets/StreamingAssets/Config/game_balance.json (hoặc bảng mặc định của WaveCompositionCalculator).
    /// KHÔNG thay đổi waves, bossPrefab, backgroundSprite để bảo toàn 100% bất biến F11_T2.
    /// </summary>
    public static class CampaignBalanceSyncTool
    {
        private const string BalanceJsonRelativePath = "Config/game_balance.json";
        private const string LevelsFolder = "Assets/Data/Levels";

        [MenuItem("Stickman/Balance/Sync 12 Campaign Level Assets")]
        public static void SyncAllCampaignLevels()
        {
            GameBalanceConfigDTO externalConfig = TryLoadExternalBalanceConfig();
            int updatedCount = 0;

            for (int w = 1; w <= 3; w++)
            {
                for (int s = 1; s <= 4; s++)
                {
                    string assetPath = $"{LevelsFolder}/Level_W{w}_S{s}.asset";
                    LevelDataSO levelAsset = AssetDatabase.LoadAssetAtPath<LevelDataSO>(assetPath);
                    if (levelAsset == null)
                    {
                        Debug.LogError($"[CampaignBalanceSyncTool] Missing level asset: {assetPath}");
                        continue;
                    }

                    LevelBalanceDTO balance = WaveCompositionCalculator.ResolveCampaignLevelBalance(w, s, externalConfig);
                    levelAsset.enemyHpMultiplier = balance.enemyHpMultiplier;
                    levelAsset.enemyDamageMultiplier = balance.enemyDamageMultiplier;
                    EditorUtility.SetDirty(levelAsset);
                    updatedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CampaignBalanceSyncTool] Synced {updatedCount}/12 Campaign Level assets cleanly.");
        }

        private static GameBalanceConfigDTO TryLoadExternalBalanceConfig()
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, BalanceJsonRelativePath);
            if (!File.Exists(fullPath))
            {
                return null;
            }

            try
            {
                string rawJson = File.ReadAllText(fullPath);
                if (string.IsNullOrWhiteSpace(rawJson))
                {
                    throw new InvalidGameConfigException("game_balance.json", "Balance config file is empty.");
                }

                GameBalanceConfigDTO dto;
                try
                {
                    dto = JsonUtility.FromJson<GameBalanceConfigDTO>(rawJson);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidGameConfigException("game_balance.json", $"Malformed JSON syntax: {ex.Message}", ex);
                }

                if (dto == null)
                {
                    throw new InvalidGameConfigException("game_balance.json", "Failed to deserialize GameBalanceConfigDTO.");
                }

                dto.Validate();
                return dto;
            }
            catch (StickmanGameException ex)
            {
                Debug.LogError($"[CampaignBalanceSyncTool] Invalid external config ({ex.ErrorCode}): {ex.Message}");
                return null;
            }
            catch (IOException ex)
            {
                Debug.LogError($"[CampaignBalanceSyncTool] IO error reading balance config: {ex.Message}");
                return null;
            }
        }
    }
}
