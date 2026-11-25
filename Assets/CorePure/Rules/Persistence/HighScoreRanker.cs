using System;
using System.Collections.Generic;
using StickmanBeatEmUp.CorePure.Entities.DTOs;

namespace StickmanBeatEmUp.CorePure.Rules.Persistence
{
    /// <summary>
    /// Thuật toán xếp hạng Top-K và khử trùng lặp (Deduplication) cho bảng HighScore.
    /// Độ phức tạp thời gian: O(n log n); bộ nhớ phụ: O(n).
    /// </summary>
    public static class HighScoreRanker
    {
        public const int DEFAULT_TOP_K = 10;

        /// <summary>
        /// Khử trùng theo cặp (username, gameMode) giữ kỷ lục cao nhất, sau đó sắp xếp giảm dần theo score,
        /// hòa điểm ưu tiên clearTimeSeconds nhỏ hơn, rồi cắt đúng Top-K phần tử.
        /// </summary>
        public static List<HighScoreEntryDTO> RankTopK(
            IReadOnlyList<HighScoreEntryDTO> rawEntries,
            int topK = DEFAULT_TOP_K,
            int? gameModeFilter = null)
        {
            List<HighScoreEntryDTO> result = new List<HighScoreEntryDTO>();
            if (rawEntries == null || rawEntries.Count == 0 || topK <= 0)
            {
                return result;
            }

            Dictionary<string, HighScoreEntryDTO> bestByUserAndMode =
                new Dictionary<string, HighScoreEntryDTO>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < rawEntries.Count; i++)
            {
                HighScoreEntryDTO candidate = rawEntries[i];
                if (candidate == null ||
                    candidate.score < 0 ||
                    float.IsNaN(candidate.clearTimeSeconds) ||
                    float.IsInfinity(candidate.clearTimeSeconds) ||
                    candidate.clearTimeSeconds < 0f)
                {
                    continue;
                }

                if (gameModeFilter.HasValue && candidate.gameMode != gameModeFilter.Value)
                {
                    continue;
                }

                string normalizedUser = string.IsNullOrWhiteSpace(candidate.username)
                    ? "Guest"
                    : candidate.username.Trim();

                HighScoreEntryDTO sanitized = new HighScoreEntryDTO
                {
                    username = normalizedUser,
                    gameMode = candidate.gameMode,
                    worldOrWaveReached = Math.Max(1, candidate.worldOrWaveReached),
                    score = candidate.score,
                    clearTimeSeconds = candidate.clearTimeSeconds,
                    achievedAtUtc = candidate.achievedAtUtc ?? string.Empty
                };

                string dedupKey = normalizedUser + "|" + sanitized.gameMode;
                if (!bestByUserAndMode.TryGetValue(dedupKey, out HighScoreEntryDTO existing) ||
                    CompareEntries(sanitized, existing) < 0)
                {
                    bestByUserAndMode[dedupKey] = sanitized;
                }
            }

            List<HighScoreEntryDTO> uniqueEntries = new List<HighScoreEntryDTO>(bestByUserAndMode.Values);
            uniqueEntries.Sort(CompareEntries);

            int takeCount = Math.Min(topK, uniqueEntries.Count);
            for (int i = 0; i < takeCount; i++)
            {
                result.Add(uniqueEntries[i]);
            }
            return result;
        }

        public static int CompareEntries(HighScoreEntryDTO a, HighScoreEntryDTO b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int scoreCmp = b.score.CompareTo(a.score); // Điểm cao hơn xếp trước
            if (scoreCmp != 0) return scoreCmp;

            int timeCmp = a.clearTimeSeconds.CompareTo(b.clearTimeSeconds); // Thời gian ngắn hơn xếp trước
            if (timeCmp != 0) return timeCmp;

            int waveCmp = b.worldOrWaveReached.CompareTo(a.worldOrWaveReached);
            if (waveCmp != 0) return waveCmp;

            return string.Compare(a.username, b.username, StringComparison.OrdinalIgnoreCase);
        }
    }
}
