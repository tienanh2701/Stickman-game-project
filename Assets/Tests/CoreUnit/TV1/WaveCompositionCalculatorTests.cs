using System.Collections.Generic;
using NUnit.Framework;
using StickmanBeatEmUp.CorePure.Entities.Combat;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Rules.Combat;
using StickmanBeatEmUp.Tests.CoreUnit.TV1.TestDoubles;

namespace StickmanBeatEmUp.Tests.CoreUnit.TV1
{
    [TestFixture]
    public class WaveCompositionCalculatorTests
    {
        [Test]
        public void CalculateComposition_Wave1AndWave3_MatchesCanonicalCreepAndEliteCounts()
        {
            WaveCompositionResult w1 = WaveCompositionCalculator.CalculateComposition(1);
            WaveCompositionResult w3 = WaveCompositionCalculator.CalculateComposition(3);

            // Wave 1: 3 + floor(1.2) = 4 creeps, 0 elites, 0 boss
            Assert.AreEqual(4, w1.CreepCount);
            Assert.AreEqual(0, w1.EliteCount);
            Assert.AreEqual(0, w1.BossCount);
            Assert.IsFalse(w1.IsBossWave);

            // Wave 3: 3 + floor(3.6) = 6 creeps, 1 elite, 0 boss
            Assert.AreEqual(6, w3.CreepCount);
            Assert.AreEqual(1, w3.EliteCount);
            Assert.AreEqual(0, w3.BossCount);
            Assert.AreEqual(7, w3.TotalEnemies);
        }

        [Test]
        public void CalculateComposition_BossMilestoneWave5AndWave25_SuppressesElitesAndClampsCreepsTo2To3()
        {
            WaveCompositionResult w5 = WaveCompositionCalculator.CalculateComposition(5, hasBossPool: true);
            WaveCompositionResult w25 = WaveCompositionCalculator.CalculateComposition(25, hasBossPool: true);

            Assert.IsTrue(w5.IsBossWave);
            Assert.AreEqual(1, w5.BossCount);
            Assert.AreEqual(0, w5.EliteCount);
            Assert.GreaterOrEqual(w5.CreepCount, 2);
            Assert.LessOrEqual(w5.CreepCount, 3);

            Assert.IsTrue(w25.IsBossWave);
            Assert.AreEqual(1, w25.BossCount);
            Assert.AreEqual(0, w25.EliteCount);
            Assert.AreEqual(3, w25.CreepCount);
        }

        [Test]
        public void CalculateComposition_NonPositiveWave_ClampsToWave1()
        {
            WaveCompositionResult w0 = WaveCompositionCalculator.CalculateComposition(0);
            WaveCompositionResult wNeg = WaveCompositionCalculator.CalculateComposition(-10);

            Assert.AreEqual(1, w0.WaveNumber);
            Assert.AreEqual(4, w0.CreepCount);
            Assert.AreEqual(1, wNeg.WaveNumber);
            Assert.AreEqual(4, wNeg.CreepCount);
        }

        [Test]
        public void CalculateEnemyScaling_MilestoneBoundaries_1_10_15_20_40_RespectCapsAndThresholds()
        {
            WaveScalingResult w1 = WaveCompositionCalculator.CalculateEnemyScaling(1);
            WaveScalingResult w10 = WaveCompositionCalculator.CalculateEnemyScaling(10);
            WaveScalingResult w15 = WaveCompositionCalculator.CalculateEnemyScaling(15);
            WaveScalingResult w20 = WaveCompositionCalculator.CalculateEnemyScaling(20);
            WaveScalingResult w40 = WaveCompositionCalculator.CalculateEnemyScaling(40);

            Assert.AreEqual(1.0f, w1.HealthMultiplier, 0.0001f);
            Assert.AreEqual(1.0f, w1.StaggerMultiplier, 0.0001f);
            Assert.AreEqual(0.0f, w1.EnragedChance, 0.0001f);

            Assert.AreEqual(0.80f, w10.StaggerMultiplier, 0.0001f);
            Assert.AreEqual(0.0f, w10.EnragedChance, 0.0001f);

            Assert.AreEqual(0.15f, w15.EnragedChance, 0.0001f);
            Assert.AreEqual(0.0f, w15.DamageReduction, 0.0001f);

            Assert.AreEqual(0.10f, w20.DamageReduction, 0.0001f);
            Assert.AreEqual(1.12f, w20.AttackSpeedMultiplier, 0.0001f);

            Assert.AreEqual(0.25f, w40.DamageReduction, 0.0001f);
            Assert.LessOrEqual(w40.MoveSpeedMultiplier, 1.25f + 0.0001f);
            Assert.LessOrEqual(w40.DamageMultiplier, 1.75f + 0.0001f);
        }

        [Test]
        public void CalculateEnemyScaling_WithCustomConfig_AppliesOverridesAndValidatesInvalidConfig()
        {
            GameBalanceConfigDTO customCfg = new GameBalanceConfigDTO
            {
                endlessWaveHpGrowth = 0.05f,
                endlessMaxDamageMultiplier = 2.0f
            };
            WaveScalingResult w11 = WaveCompositionCalculator.CalculateEnemyScaling(11, customCfg);
            Assert.AreEqual(1.50f, w11.HealthMultiplier, 0.0001f);

            GameBalanceConfigDTO invalidCfg = new GameBalanceConfigDTO { endlessWaveHpGrowth = -0.1f };
            InvalidGameConfigException ex = Assert.Throws<InvalidGameConfigException>(
                () => WaveCompositionCalculator.CalculateEnemyScaling(5, invalidCfg));
            Assert.AreEqual("INVALID_CONFIG", ex.ErrorCode);
        }

        [Test]
        public void ResolveCampaignLevelBalance_Progressive12Stages_StartsAt1_00AndScalesUpToW3S4()
        {
            LevelBalanceDTO w1s1 = WaveCompositionCalculator.ResolveCampaignLevelBalance(1, 1);
            LevelBalanceDTO w2s4 = WaveCompositionCalculator.ResolveCampaignLevelBalance(2, 4);
            LevelBalanceDTO w3s4 = WaveCompositionCalculator.ResolveCampaignLevelBalance(3, 4);

            Assert.AreEqual(1.00f, w1s1.enemyHpMultiplier, 0.0001f);
            Assert.AreEqual(1.00f, w1s1.enemyDamageMultiplier, 0.0001f);
            Assert.AreEqual(20, w1s1.bonusClearCoins);
            Assert.AreEqual(1.55f, w2s4.enemyHpMultiplier, 0.0001f);
            Assert.AreEqual(1.35f, w2s4.enemyDamageMultiplier, 0.0001f);
            Assert.AreEqual(75, w2s4.bonusClearCoins);
            Assert.AreEqual(1.85f, w3s4.enemyHpMultiplier, 0.0001f);
            Assert.AreEqual(1.55f, w3s4.enemyDamageMultiplier, 0.0001f);
            Assert.AreEqual(100, w3s4.bonusClearCoins);

            GameBalanceConfigDTO overrideCfg = new GameBalanceConfigDTO
            {
                levels = new List<LevelBalanceDTO>
                {
                    new LevelBalanceDTO { worldIndex = 1, stageIndex = 2, enemyHpMultiplier = 1.12f, enemyDamageMultiplier = 1.07f, bonusClearCoins = 35 }
                }
            };
            LevelBalanceDTO overridden = WaveCompositionCalculator.ResolveCampaignLevelBalance(1, 2, overrideCfg);
            Assert.AreEqual(1.12f, overridden.enemyHpMultiplier, 0.0001f);
            Assert.AreEqual(1.07f, overridden.enemyDamageMultiplier, 0.0001f);
            Assert.AreEqual(35, overridden.bonusClearCoins);
        }

        [Test]
        public void SpyAudioService_CapturesSegmentFilledAndBossWaveAudioCues()
        {
            SpyAudioService spyAudio = new SpyAudioService();
            RageRuleEngine rage = new RageRuleEngine();
            rage.OnSegmentFilled += seg => spyAudio.PlaySfx($"sfx_rage_segment_{seg}");

            WaveCompositionResult bossWave = WaveCompositionCalculator.CalculateComposition(5, hasBossPool: true);
            if (bossWave.IsBossWave)
            {
                spyAudio.PlayBgm("bgm_boss_alert");
            }

            rage.AddRageFromHit(CombatAttackKind.NormalAttackJ, 10f);
            spyAudio.SetMasterVolume(0.75f);

            Assert.AreEqual("bgm_boss_alert", spyAudio.LastBgmTrack);
            Assert.AreEqual(1, spyAudio.PlayedSfxIds.Count);
            Assert.AreEqual("sfx_rage_segment_1", spyAudio.LastSfxId);
            Assert.AreEqual(0.75f, spyAudio.MasterVolume, 0.0001f);
            Assert.IsFalse(spyAudio.IsMuted);
        }
    }
}
