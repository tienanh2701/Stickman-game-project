using System;
using System.Collections.Generic;
using System.Text;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Rules.Persistence
{
    /// <summary>
    /// Dịch vụ lưu trữ và truy vấn bảng xếp hạng HighScore thuần C# thông qua ISaveStorage.
    /// </summary>
    public sealed class HighScoreService
    {
        public const string HIGHSCORE_STORAGE_KEY = "highscore_board";

        private readonly ISaveStorage storage;
        private readonly IGameClock clock;
        private readonly int maxEntries;

        public HighScoreService(
            ISaveStorage storage,
            IGameClock clock = null,
            int maxEntries = HighScoreRanker.DEFAULT_TOP_K)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.clock = clock ?? new SystemGameClock();
            this.maxEntries = maxEntries > 0 ? maxEntries : HighScoreRanker.DEFAULT_TOP_K;
        }

        public List<HighScoreEntryDTO> LoadTopScores(int? gameModeFilter = null)
        {
            if (!storage.Exists(HIGHSCORE_STORAGE_KEY))
            {
                return new List<HighScoreEntryDTO>();
            }

            string json = storage.LoadText(HIGHSCORE_STORAGE_KEY);
            List<HighScoreEntryDTO> raw = DeserializeEntriesOrThrow(json);
            return HighScoreRanker.RankTopK(raw, maxEntries, gameModeFilter);
        }

        public List<HighScoreEntryDTO> RecordScore(HighScoreEntryDTO newEntry)
        {
            if (newEntry == null)
            {
                throw new ArgumentNullException(nameof(newEntry));
            }

            if (string.IsNullOrWhiteSpace(newEntry.achievedAtUtc))
            {
                newEntry.achievedAtUtc = clock.UtcNowIso;
            }

            List<HighScoreEntryDTO> all = storage.Exists(HIGHSCORE_STORAGE_KEY)
                ? DeserializeEntriesOrThrow(storage.LoadText(HIGHSCORE_STORAGE_KEY))
                : new List<HighScoreEntryDTO>();

            all.Add(newEntry);
            List<HighScoreEntryDTO> ranked = HighScoreRanker.RankTopK(all, maxEntries);
            storage.SaveText(HIGHSCORE_STORAGE_KEY, SerializeEntries(ranked));
            return ranked;
        }

        public static string SerializeEntries(IReadOnlyList<HighScoreEntryDTO> entries)
        {
            StringBuilder sb = new StringBuilder(512);
            sb.Append("{\n  \"entries\": [");
            if (entries != null)
            {
                int written = 0;
                for (int i = 0; i < entries.Count; i++)
                {
                    HighScoreEntryDTO e = entries[i];
                    if (e == null) continue;
                    if (written > 0) sb.Append(',');
                    sb.Append("\n    {")
                      .Append("\"username\":\"").Append(SimpleJsonCodec.EscapeString(e.username)).Append("\",")
                      .Append("\"gameMode\":").Append(e.gameMode).Append(',')
                      .Append("\"worldOrWaveReached\":").Append(e.worldOrWaveReached).Append(',')
                      .Append("\"score\":").Append(e.score).Append(',')
                      .Append("\"clearTimeSeconds\":").Append(SimpleJsonCodec.FormatFloat(e.clearTimeSeconds)).Append(',')
                      .Append("\"achievedAtUtc\":\"").Append(SimpleJsonCodec.EscapeString(e.achievedAtUtc)).Append("\"")
                      .Append('}');
                    written++;
                }
            }
            sb.Append("\n  ]\n}");
            return sb.ToString();
        }

        public static List<HighScoreEntryDTO> DeserializeEntriesOrThrow(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new SaveCorruptedException(HIGHSCORE_STORAGE_KEY, "HighScore JSON is empty.");
            }

            try
            {
                Dictionary<string, object> root = SimpleJsonCodec.ParseObject(json);
                List<object> rawArray = SimpleJsonCodec.ReadArray(root, "entries");
                List<HighScoreEntryDTO> entries = new List<HighScoreEntryDTO>(rawArray.Count);
                for (int i = 0; i < rawArray.Count; i++)
                {
                    Dictionary<string, object> obj = rawArray[i] as Dictionary<string, object>;
                    if (obj == null)
                    {
                        throw new SaveCorruptedException(HIGHSCORE_STORAGE_KEY, $"HighScore entry #{i} is not an object.");
                    }

                    entries.Add(new HighScoreEntryDTO
                    {
                        username = SimpleJsonCodec.ReadString(obj, "username", "Guest"),
                        gameMode = SimpleJsonCodec.ReadInt(obj, "gameMode", 0),
                        worldOrWaveReached = SimpleJsonCodec.ReadInt(obj, "worldOrWaveReached", 1),
                        score = SimpleJsonCodec.ReadInt(obj, "score", 0),
                        clearTimeSeconds = SimpleJsonCodec.ReadFloat(obj, "clearTimeSeconds", 0f),
                        achievedAtUtc = SimpleJsonCodec.ReadString(obj, "achievedAtUtc", string.Empty)
                    });
                }
                return entries;
            }
            catch (SaveCorruptedException)
            {
                throw;
            }
            catch (FormatException ex)
            {
                throw new SaveCorruptedException(HIGHSCORE_STORAGE_KEY, $"Malformed HighScore JSON: {ex.Message}", ex);
            }
        }
    }
}
