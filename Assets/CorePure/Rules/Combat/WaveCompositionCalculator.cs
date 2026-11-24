using System;
using StickmanBeatEmUp.CorePure.Entities.DTOs;

namespace StickmanBeatEmUp.CorePure.Rules.Combat
{
    /// <summary>
    /// Kết quả bất biến mô tả số lượng Creep / Elite / Boss cần sinh ở một Wave Endless.
    /// </summary>
    public readonly struct WaveCompositionResult
    {
        public int WaveNumber { get; }
        public int CreepCount { get; }
        public int EliteCount { get; }
        public int BossCount { get; }
        public bool IsBossWave { get; }
        public int TotalEnemies => CreepCount + EliteCount + BossCount;

        public WaveCompositionResult(int waveNumber, int creepCount, int eliteCount, int bossCount, bool isBossWave)
        {
            WaveNumber = waveNumber;
            CreepCount = creepCount;
            EliteCount = eliteCount;
            BossCount = bossCount;
            IsBossWave = isBossWave;
        }
    }

    /// <summary>
    /// Kết quả bất biến mô tả toàn bộ hệ số tăng tiến độ khó của quái/Boss tại một Wave Endless.
    /// </summary>
    public readonly struct WaveScalingResult
    {
        public int WaveNumber { get; }
        public float HealthMultiplier { get; }
        public float DamageMultiplier { get; }
        public float MoveSpeedMultiplier { get; }
        public float StaggerMultiplier { get; }
        public float DamageReduction { get; }
        public float AttackSpeedMultiplier { get; }
        public float EnragedChance { get; }

        public WaveScalingResult(
            int waveNumber,
            float healthMultiplier,
            float damageMultiplier,
            float moveSpeedMultiplier,
            float staggerMultiplier,
            float damageReduction,
            float attackSpeedMultiplier,
            float enragedChance)
        {
            WaveNumber = waveNumber;
            HealthMultiplier = healthMultiplier;
            DamageMultiplier = damageMultiplier;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            StaggerMultiplier = staggerMultiplier;
            DamageReduction = damageReduction;
            AttackSpeedMultiplier = attackSpeedMultiplier;
            EnragedChance = enragedChance;
        }
    }

    /// <summary>
    /// Động cơ tính toán cấu trúc Wave, độ khó Endless và hệ số 12 màn Campaign thuần C# (tách từ EndlessModeManager.cs).
    /// Hỗ trợ đồng bộ tham số động từ GameBalanceConfigDTO (StreamingAssets/Config/game_balance.json).
    /// </summary>
    public static class WaveCompositionCalculator
    {
        public const float DEFAULT_ENDLESS_HP_GROWTH = 0.03f;
        public const float DEFAULT_ENDLESS_DMG_GROWTH = 0.015f;
        public const float DEFAULT_ENDLESS_MAX_DMG_MULT = 1.75f;
        public const float DEFAULT_ENDLESS_SPEED_GROWTH = 0.008f;
        public const float DEFAULT_ENDLESS_MAX_SPEED_MULT = 1.25f;

        /// <summary>
        /// Tính số lượng Creep, Elite và Boss cho một Wave Endless theo công thức chuẩn của EndlessModeManager.
        /// - Creep = 3 + floor(w * 1.2)
        /// - Elite xuất hiện từ Wave 3, cách 2 wave (3, 5*, 7, 9...): 1 + floor((w - 3) / 3)
        /// - Boss xuất hiện mỗi bội số 5 (5, 10, 15...): khi là Wave Boss, Elite = 0 và Creep bị kẹp trong [2, 3].
        /// </summary>
        public static WaveCompositionResult CalculateComposition(
            int wave,
            bool hasBossPool = true,
            bool hasElitePool = true,
            bool hasCreepPool = true)
        {
            int w = Math.Max(1, wave);
            int creepCount = 3 + (int)Math.Floor(w * 1.2f);
            int eliteCount = (w >= 3 && (w - 3) % 2 == 0)
                ? 1 + (int)Math.Floor((w - 3) / 3f)
                : 0;
            bool isBossMilestone = w >= 5 && (w % 5 == 0);
            int bossCount = (isBossMilestone && hasBossPool) ? 1 : 0;

            if (isBossMilestone || bossCount > 0)
            {
                int halved = Math.Max(2, creepCount / 2);
                creepCount = Math.Min(3, Math.Max(2, halved));
                eliteCount = 0;
            }

            if (!hasCreepPool)
            {
                creepCount = 0;
            }

            if (!hasElitePool)
            {
                eliteCount = 0;
            }

            return new WaveCompositionResult(w, creepCount, eliteCount, bossCount, isBossMilestone);
        }

        /// <summary>
        /// Tính hệ số mở rộng chỉ số cho Creep/Elite theo Wave Endless, ưu tiên tham số từ GameBalanceConfigDTO nếu có.
        /// </summary>
        public static WaveScalingResult CalculateEnemyScaling(int wave, GameBalanceConfigDTO balanceConfig = null)
        {
            float hpGrowth = DEFAULT_ENDLESS_HP_GROWTH;
            float maxDmgMult = DEFAULT_ENDLESS_MAX_DMG_MULT;

            if (balanceConfig != null)
            {
                balanceConfig.Validate();
                hpGrowth = balanceConfig.endlessWaveHpGrowth;
                maxDmgMult = balanceConfig.endlessMaxDamageMultiplier;
            }

            int w = Math.Max(1, wave);
            float hpMult = 1f + hpGrowth * (w - 1);
            float dmgMult = Math.Min(maxDmgMult, 1f + DEFAULT_ENDLESS_DMG_GROWTH * (w - 1));
            float speedMult = Math.Min(DEFAULT_ENDLESS_MAX_SPEED_MULT, 1f + DEFAULT_ENDLESS_SPEED_GROWTH * (w - 1));
            float staggerMult = w >= 10 ? 0.80f : 1f;
            float damageReduction = w >= 20 ? Math.Min(0.25f, 0.10f + 0.05f * ((w - 20) / 5)) : 0f;
            float attackSpeedMult = w >= 20 ? 1.12f : 1f;
            float enragedChance = w >= 15 ? 0.15f : 0f;

            return new WaveScalingResult(
                w,
                hpMult,
                dmgMult,
                speedMult,
                staggerMult,
                damageReduction,
                attackSpeedMult,
                enragedChance);
        }

        /// <summary>
        /// Tính hệ số mở rộng chỉ số cho Boss tại các mốc Wave bội số 5 (5, 10, 15...).
        /// </summary>
        public static WaveScalingResult CalculateBossScaling(int wave, GameBalanceConfigDTO balanceConfig = null)
        {
            float maxDmgMult = DEFAULT_ENDLESS_MAX_DMG_MULT;

            if (balanceConfig != null)
            {
                balanceConfig.Validate();
                maxDmgMult = balanceConfig.endlessMaxDamageMultiplier;
            }

            int w = Math.Max(1, wave);
            int bossTier = Math.Max(0, (w / 5) - 1);
            float bossHpMult = 1f + 0.10f * bossTier;
            float bossDmgMult = w <= 5 ? 1f : Math.Min(maxDmgMult, 1f + DEFAULT_ENDLESS_DMG_GROWTH * (w - 5));
            float bossSpeedMult = w <= 5 ? 1f : Math.Min(DEFAULT_ENDLESS_MAX_SPEED_MULT, 1f + DEFAULT_ENDLESS_SPEED_GROWTH * (w - 5));
            float bossStaggerMult = w >= 10 ? 0.80f : 1f;
            float bossDrPct = w >= 20 ? Math.Min(0.25f, 0.10f + 0.05f * ((w - 20) / 5)) : 0f;
            float bossAtkSpd = w >= 20 ? 1.10f : 1f;

            return new WaveScalingResult(
                w,
                bossHpMult,
                bossDmgMult,
                bossSpeedMult,
                bossStaggerMult,
                bossDrPct,
                bossAtkSpd,
                0f);
        }

        /// <summary>
        /// Tra cứu hoặc tính hệ số cân bằng Campaign cho 12 màn (World 1..3 x Stage 1..4).
        /// W1-S1 luôn giữ chính xác (1.00x HP, 1.00x DMG) để tương thích 100% bài test hiện có.
        /// </summary>
        public static LevelBalanceDTO ResolveCampaignLevelBalance(
            int worldIndex,
            int stageIndex,
            GameBalanceConfigDTO balanceConfig = null)
        {
            int w = Math.Max(1, Math.Min(3, worldIndex));
            int s = Math.Max(1, Math.Min(4, stageIndex));

            if (balanceConfig != null)
            {
                balanceConfig.Validate();
                if (balanceConfig.levels != null)
                {
                    for (int i = 0; i < balanceConfig.levels.Count; i++)
                    {
                        LevelBalanceDTO entry = balanceConfig.levels[i];
                        if (entry != null && entry.worldIndex == w && entry.stageIndex == s)
                        {
                            return new LevelBalanceDTO
                            {
                                worldIndex = w,
                                stageIndex = s,
                                enemyHpMultiplier = entry.enemyHpMultiplier > 0f ? entry.enemyHpMultiplier : 1f,
                                enemyDamageMultiplier = entry.enemyDamageMultiplier > 0f ? entry.enemyDamageMultiplier : 1f,
                                bonusClearCoins = entry.bonusClearCoins >= 0 ? entry.bonusClearCoins : 20
                            };
                        }
                    }
                }
            }

            float defaultHpMult = (float)Math.Round(1.00f + 0.30f * (w - 1) + (s == 4 ? 0.25f : 0.08f * (s - 1)), 2);
            float defaultDmgMult = (float)Math.Round(1.00f + 0.20f * (w - 1) + 0.05f * (s - 1), 2);
            int defaultBonusCoins = GetDefaultBonusClearCoins(w, s);

            return new LevelBalanceDTO
            {
                worldIndex = w,
                stageIndex = s,
                enemyHpMultiplier = defaultHpMult,
                enemyDamageMultiplier = defaultDmgMult,
                bonusClearCoins = defaultBonusCoins
            };
        }

        private static int GetDefaultBonusClearCoins(int worldIndex, int stageIndex)
        {
            if (worldIndex == 1)
            {
                return stageIndex == 4 ? 50 : 20 + (stageIndex - 1) * 5;
            }

            if (worldIndex == 2)
            {
                return stageIndex == 4 ? 75 : 35 + (stageIndex - 1) * 5;
            }

            return stageIndex == 4 ? 100 : 50 + (stageIndex - 1) * 10;
        }
    }
}
