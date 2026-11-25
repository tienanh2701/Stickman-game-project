using System;
using System.Collections.Generic;
using System.Text;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Exceptions;

namespace StickmanBeatEmUp.CorePure.Rules.Persistence
{
    /// <summary>
    /// Tuần tự hóa và giải mã GameStateDTO thuần C# với kiểm định schema chặt chẽ (ném SaveCorruptedException).
    /// </summary>
    public static class GameStateJsonCodec
    {
        public static string Serialize(GameStateDTO state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            StringBuilder sb = new StringBuilder(1024);
            sb.Append("{\n");
            sb.Append("  \"schemaVersion\": ").Append(state.schemaVersion).Append(",\n");
            sb.Append("  \"accountUsername\": \"").Append(SimpleJsonCodec.EscapeString(state.accountUsername)).Append("\",\n");
            sb.Append("  \"gameMode\": ").Append(state.gameMode).Append(",\n");
            sb.Append("  \"worldIndex\": ").Append(state.worldIndex).Append(",\n");
            sb.Append("  \"stageIndex\": ").Append(state.stageIndex).Append(",\n");
            sb.Append("  \"currentWaveIndex\": ").Append(state.currentWaveIndex).Append(",\n");
            sb.Append("  \"playerCharacterIndex\": ").Append(state.playerCharacterIndex).Append(",\n");
            sb.Append("  \"playerHp\": ").Append(SimpleJsonCodec.FormatFloat(state.playerHp)).Append(",\n");
            sb.Append("  \"playerMaxHp\": ").Append(SimpleJsonCodec.FormatFloat(state.playerMaxHp)).Append(",\n");
            sb.Append("  \"playerPosX\": ").Append(SimpleJsonCodec.FormatFloat(state.playerPosX)).Append(",\n");
            sb.Append("  \"playerPosY\": ").Append(SimpleJsonCodec.FormatFloat(state.playerPosY)).Append(",\n");
            sb.Append("  \"playerRage\": ").Append(SimpleJsonCodec.FormatFloat(state.playerRage)).Append(",\n");
            sb.Append("  \"isTransformed\": ").Append(state.isTransformed ? "true" : "false").Append(",\n");
            sb.Append("  \"virtualShieldHp\": ").Append(SimpleJsonCodec.FormatFloat(state.virtualShieldHp)).Append(",\n");
            sb.Append("  \"coins\": ").Append(state.coins).Append(",\n");
            sb.Append("  \"senzuCount\": ").Append(state.senzuCount).Append(",\n");
            sb.Append("  \"spinalFluidCount\": ").Append(state.spinalFluidCount).Append(",\n");
            sb.Append("  \"ultraWaterCount\": ").Append(state.ultraWaterCount).Append(",\n");
            sb.Append("  \"equippedSlotQ\": ").Append(state.equippedSlotQ).Append(",\n");
            sb.Append("  \"equippedSlotE\": ").Append(state.equippedSlotE).Append(",\n");
            sb.Append("  \"score\": ").Append(state.score).Append(",\n");
            sb.Append("  \"elapsedSeconds\": ").Append(SimpleJsonCodec.FormatFloat(state.elapsedSeconds)).Append(",\n");
            sb.Append("  \"savedAtUtc\": \"").Append(SimpleJsonCodec.EscapeString(state.savedAtUtc)).Append("\",\n");

            sb.Append("  \"extraItemCounts\": [");
            List<ItemCountEntryDTO> extras = state.extraItemCounts ?? new List<ItemCountEntryDTO>();
            int extraWritten = 0;
            for (int i = 0; i < extras.Count; i++)
            {
                ItemCountEntryDTO e = extras[i];
                if (e == null) continue;
                if (extraWritten > 0) sb.Append(',');
                sb.Append("{\"itemTypeId\":").Append(e.itemTypeId).Append(",\"count\":").Append(e.count).Append('}');
                extraWritten++;
            }
            sb.Append("],\n");

            sb.Append("  \"activeBuffs\": [");
            List<BuffSnapshotDTO> buffs = state.activeBuffs ?? new List<BuffSnapshotDTO>();
            int buffWritten = 0;
            for (int i = 0; i < buffs.Count; i++)
            {
                BuffSnapshotDTO b = buffs[i];
                if (b == null) continue;
                if (buffWritten > 0) sb.Append(',');
                sb.Append("{\"buffId\":\"").Append(SimpleJsonCodec.EscapeString(b.buffId))
                  .Append("\",\"remainingSeconds\":").Append(SimpleJsonCodec.FormatFloat(b.remainingSeconds))
                  .Append(",\"remainingShield\":").Append(SimpleJsonCodec.FormatFloat(b.remainingShield)).Append('}');
                buffWritten++;
            }
            sb.Append("],\n");

            sb.Append("  \"activeEnemies\": [");
            List<EnemySnapshotDTO> enemies = state.activeEnemies ?? new List<EnemySnapshotDTO>();
            int enemyWritten = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemySnapshotDTO en = enemies[i];
                if (en == null) continue;
                if (enemyWritten > 0) sb.Append(',');
                sb.Append("{\"enemyId\":\"").Append(SimpleJsonCodec.EscapeString(en.enemyId))
                  .Append("\",\"hp\":").Append(SimpleJsonCodec.FormatFloat(en.hp))
                  .Append(",\"maxHp\":").Append(SimpleJsonCodec.FormatFloat(en.maxHp))
                  .Append(",\"posX\":").Append(SimpleJsonCodec.FormatFloat(en.posX))
                  .Append(",\"posY\":").Append(SimpleJsonCodec.FormatFloat(en.posY))
                  .Append(",\"phase\":").Append(en.phase)
                  .Append(",\"isEnraged\":").Append(en.isEnraged ? "true" : "false").Append('}');
                enemyWritten++;
            }
            sb.Append("]\n}");
            return sb.ToString();
        }

        public static GameStateDTO DeserializeOrThrow(string json, string storageKey = "save_slot")
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new SaveCorruptedException(storageKey, "Save file content is empty.");
            }

            try
            {
                Dictionary<string, object> root = SimpleJsonCodec.ParseObject(json);
                GameStateDTO dto = new GameStateDTO
                {
                    schemaVersion = SimpleJsonCodec.ReadInt(root, "schemaVersion", 0),
                    accountUsername = SimpleJsonCodec.ReadString(root, "accountUsername", string.Empty),
                    gameMode = SimpleJsonCodec.ReadInt(root, "gameMode", 0),
                    worldIndex = SimpleJsonCodec.ReadInt(root, "worldIndex", 0),
                    stageIndex = SimpleJsonCodec.ReadInt(root, "stageIndex", 0),
                    currentWaveIndex = SimpleJsonCodec.ReadInt(root, "currentWaveIndex", 0),
                    playerCharacterIndex = SimpleJsonCodec.ReadInt(root, "playerCharacterIndex", 0),
                    playerHp = SimpleJsonCodec.ReadFloat(root, "playerHp", -1f),
                    playerMaxHp = SimpleJsonCodec.ReadFloat(root, "playerMaxHp", 0f),
                    playerPosX = SimpleJsonCodec.ReadFloat(root, "playerPosX", 0f),
                    playerPosY = SimpleJsonCodec.ReadFloat(root, "playerPosY", -2.5f),
                    playerRage = SimpleJsonCodec.ReadFloat(root, "playerRage", 0f),
                    isTransformed = SimpleJsonCodec.ReadBool(root, "isTransformed", false),
                    virtualShieldHp = SimpleJsonCodec.ReadFloat(root, "virtualShieldHp", 0f),
                    coins = SimpleJsonCodec.ReadInt(root, "coins", 0),
                    senzuCount = SimpleJsonCodec.ReadInt(root, "senzuCount", 0),
                    spinalFluidCount = SimpleJsonCodec.ReadInt(root, "spinalFluidCount", 0),
                    ultraWaterCount = SimpleJsonCodec.ReadInt(root, "ultraWaterCount", 0),
                    equippedSlotQ = SimpleJsonCodec.ReadInt(root, "equippedSlotQ", 1),
                    equippedSlotE = SimpleJsonCodec.ReadInt(root, "equippedSlotE", 2),
                    score = SimpleJsonCodec.ReadInt(root, "score", 0),
                    elapsedSeconds = SimpleJsonCodec.ReadFloat(root, "elapsedSeconds", 0f),
                    savedAtUtc = SimpleJsonCodec.ReadString(root, "savedAtUtc", string.Empty),
                    extraItemCounts = ParseExtraItems(SimpleJsonCodec.ReadArray(root, "extraItemCounts")),
                    activeBuffs = ParseBuffs(SimpleJsonCodec.ReadArray(root, "activeBuffs")),
                    activeEnemies = ParseEnemies(SimpleJsonCodec.ReadArray(root, "activeEnemies"))
                };

                dto.Validate(storageKey);
                return dto;
            }
            catch (SaveCorruptedException)
            {
                throw;
            }
            catch (FormatException ex)
            {
                throw new SaveCorruptedException(storageKey, $"Malformed JSON in save data: {ex.Message}", ex);
            }
        }

        private static List<ItemCountEntryDTO> ParseExtraItems(List<object> rawList)
        {
            List<ItemCountEntryDTO> list = new List<ItemCountEntryDTO>(rawList.Count);
            for (int i = 0; i < rawList.Count; i++)
            {
                Dictionary<string, object> obj = rawList[i] as Dictionary<string, object>;
                if (obj == null) continue;
                list.Add(new ItemCountEntryDTO
                {
                    itemTypeId = SimpleJsonCodec.ReadInt(obj, "itemTypeId", 0),
                    count = Math.Max(0, SimpleJsonCodec.ReadInt(obj, "count", 0))
                });
            }
            return list;
        }

        private static List<BuffSnapshotDTO> ParseBuffs(List<object> rawList)
        {
            List<BuffSnapshotDTO> list = new List<BuffSnapshotDTO>(rawList.Count);
            for (int i = 0; i < rawList.Count; i++)
            {
                Dictionary<string, object> obj = rawList[i] as Dictionary<string, object>;
                if (obj == null) continue;
                list.Add(new BuffSnapshotDTO
                {
                    buffId = SimpleJsonCodec.ReadString(obj, "buffId", string.Empty),
                    remainingSeconds = SimpleJsonCodec.ReadFloat(obj, "remainingSeconds", 0f),
                    remainingShield = SimpleJsonCodec.ReadFloat(obj, "remainingShield", 0f)
                });
            }
            return list;
        }

        private static List<EnemySnapshotDTO> ParseEnemies(List<object> rawList)
        {
            List<EnemySnapshotDTO> list = new List<EnemySnapshotDTO>(rawList.Count);
            for (int i = 0; i < rawList.Count; i++)
            {
                Dictionary<string, object> obj = rawList[i] as Dictionary<string, object>;
                if (obj == null) continue;
                list.Add(new EnemySnapshotDTO
                {
                    enemyId = SimpleJsonCodec.ReadString(obj, "enemyId", string.Empty),
                    hp = SimpleJsonCodec.ReadFloat(obj, "hp", 0f),
                    maxHp = SimpleJsonCodec.ReadFloat(obj, "maxHp", 0f),
                    posX = SimpleJsonCodec.ReadFloat(obj, "posX", 0f),
                    posY = SimpleJsonCodec.ReadFloat(obj, "posY", -2.5f),
                    phase = SimpleJsonCodec.ReadInt(obj, "phase", 1),
                    isEnraged = SimpleJsonCodec.ReadBool(obj, "isEnraged", false)
                });
            }
            return list;
        }
    }
}
