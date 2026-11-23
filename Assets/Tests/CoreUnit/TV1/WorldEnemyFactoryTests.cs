using System.Collections.Generic;
using NUnit.Framework;
using StickmanBeatEmUp.CorePure.AI;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Interfaces;
using StickmanBeatEmUp.CorePure.Patterns.Factories;

namespace StickmanBeatEmUp.Tests.CoreUnit.TV1
{
    [TestFixture]
    public class WorldEnemyFactoryTests
    {
        [Test]
        public void DefaultCatalog_CanCreateAll13CanonicalEnemiesAcrossWorlds1To3()
        {
            // Arrange
            WorldEnemyFactory factory = new WorldEnemyFactory();
            string[] canonicalIds =
            {
                "w1_pure_titan", "w1_beast_titan", "w1_female_titan", "w1_warhammer_boss",
                "w2_saibamen", "w2_frieza_soldier", "w2_saibaman_kamikaze", "w2_nappa", "w2_broly_boss",
                "w3_shocker", "w3_kasshin", "w3_shadow_moon", "w3_mecha_boss"
            };

            // Act & Assert
            Assert.AreEqual(13, factory.RegisteredTemplateCount);
            Assert.AreEqual(4, factory.GetEnemyIdsForWorld(1).Count);
            Assert.AreEqual(5, factory.GetEnemyIdsForWorld(2).Count);
            Assert.AreEqual(4, factory.GetEnemyIdsForWorld(3).Count);

            for (int i = 0; i < canonicalIds.Length; i++)
            {
                Assert.IsTrue(factory.CanCreate(canonicalIds[i]), $"Missing template '{canonicalIds[i]}'.");
            }

            Assert.IsFalse(factory.CanCreate("unknown_enemy"));
            Assert.IsFalse(factory.CanCreate(string.Empty));
            Assert.IsFalse(factory.CanCreate(null));
        }

        [Test]
        public void CreateEnemyBlueprint_ArchetypeStrategiesMatchCanonicalRolesAcrossWorlds()
        {
            // Arrange
            IEnemyFactory factory = new WorldEnemyFactory();

            // Act
            EnemyBlueprint melee = factory.CreateEnemyBlueprint("w1_pure_titan", 1, 1, false);
            EnemyBlueprint ranged = factory.CreateEnemyBlueprint("w2_frieza_soldier", 2, 1, false);
            EnemyBlueprint kamikaze = factory.CreateEnemyBlueprint("w2_saibaman_kamikaze", 2, 1, false);
            EnemyBlueprint boss = factory.CreateEnemyBlueprint("w3_mecha_boss", 3, 4, false);

            // Assert
            Assert.AreEqual(MeleeChaseStrategy.ID, melee.strategyId);
            Assert.IsFalse(melee.isBoss);
            Assert.AreEqual(1.0f, melee.healthMultiplier, 0.0001f);
            Assert.AreEqual(10f, melee.maxHealth, 0.001f);

            Assert.AreEqual(RangedKiteStrategy.ID, ranged.strategyId);
            Assert.AreEqual(KamikazeRushStrategy.ID, kamikaze.strategyId);
            Assert.AreEqual(BossPhaseStrategy.ID, boss.strategyId);
            Assert.IsTrue(boss.isBoss);
            Assert.IsTrue(boss.hasSuperArmor);
        }

        [Test]
        public void CreateEnemyBlueprint_EndlessWave11Creep_AppliesExactHpAndDamageScaling()
        {
            // Arrange
            WorldEnemyFactory factory = new WorldEnemyFactory();

            // Act
            EnemyBlueprint bp = factory.CreateEnemyBlueprint("w1_pure_titan", worldIndex: 1, waveIndex: 11, isEndlessMode: true);

            // Assert — Wave 11: hpMult = 1 + 0.03 * 10 = 1.30; dmgMult = 1 + 0.015 * 10 = 1.15
            Assert.AreEqual(MeleeChaseStrategy.ID, bp.strategyId);
            Assert.AreEqual(1.30f, bp.healthMultiplier, 0.0001f);
            Assert.AreEqual(1.15f, bp.damageMultiplier, 0.0001f);
            Assert.AreEqual(13.0f, bp.maxHealth, 0.001f);
            Assert.AreEqual(5.75f, bp.attackDamage, 0.001f);
        }

        [Test]
        public void CreateEnemyBlueprint_EndlessBossWave15AndDamageCapWave100_AppliesBossTierAndCapsDamage()
        {
            // Arrange
            WorldEnemyFactory factory = new WorldEnemyFactory();

            // Act
            EnemyBlueprint bossWave15 = factory.CreateEnemyBlueprint("w2_broly_boss", worldIndex: 2, waveIndex: 15, isEndlessMode: true);
            EnemyBlueprint creepWave100 = factory.CreateEnemyBlueprint("w3_shocker", worldIndex: 3, waveIndex: 100, isEndlessMode: true);

            // Assert — Wave 15: bossTier = (15 / 5) - 1 = 2 -> hpMult = 1.20; dmgMult = 1 + 0.015 * 10 = 1.15
            Assert.IsTrue(bossWave15.isBoss);
            Assert.IsTrue(bossWave15.hasSuperArmor);
            Assert.AreEqual(BossPhaseStrategy.ID, bossWave15.strategyId);
            Assert.AreEqual(1.20f, bossWave15.healthMultiplier, 0.0001f);
            Assert.AreEqual(1.15f, bossWave15.damageMultiplier, 0.0001f);
            Assert.AreEqual(180f, bossWave15.maxHealth, 0.01f);

            // Wave 100 damage multiplier is capped at 1.75x
            Assert.AreEqual(1.75f, creepWave100.damageMultiplier, 0.0001f);
            Assert.AreEqual(8.75f, creepWave100.attackDamage, 0.001f);
        }

        [Test]
        public void CreateEnemyBlueprint_WithDifficultyPreset_TunesMoveSpeedAndAttackCooldown()
        {
            // Arrange
            WorldEnemyFactory factory = new WorldEnemyFactory();

            // Act
            EnemyBlueprint easy = factory.CreateEnemyBlueprint("w1_pure_titan", 1, 1, false, AIDifficultyPreset.Easy);
            EnemyBlueprint normal = factory.CreateEnemyBlueprint("w1_pure_titan", 1, 1, false, AIDifficultyPreset.Normal);
            EnemyBlueprint hard = factory.CreateEnemyBlueprint("w1_pure_titan", 1, 1, false, AIDifficultyPreset.Hard);

            // Assert
            Assert.AreEqual(3.5f * 0.85f, easy.moveSpeed, 0.001f);
            Assert.AreEqual(3.5f, normal.moveSpeed, 0.001f);
            Assert.AreEqual(3.5f * 1.15f, hard.moveSpeed, 0.001f);

            Assert.AreEqual(1.5f * 1.25f, easy.attackCooldown, 0.001f);
            Assert.AreEqual(1.5f, normal.attackCooldown, 0.001f);
            Assert.AreEqual(1.5f * 0.85f, hard.attackCooldown, 0.001f);
        }

        [Test]
        public void CreateEnemyBlueprint_CampaignDefaultProgression_MatchesUnified12LevelTV1TV2Formula()
        {
            // Arrange
            WorldEnemyFactory factory = new WorldEnemyFactory();

            // Act
            EnemyBlueprint w1s1 = factory.CreateEnemyBlueprint("w1_pure_titan", 1, 1, false);
            EnemyBlueprint w1s4 = factory.CreateEnemyBlueprint("w1_warhammer_boss", 1, 4, false);
            EnemyBlueprint w2s1 = factory.CreateEnemyBlueprint("w2_saibamen", 2, 1, false);
            EnemyBlueprint w2s4 = factory.CreateEnemyBlueprint("w2_broly_boss", 2, 4, false);
            EnemyBlueprint w3s1 = factory.CreateEnemyBlueprint("w3_shocker", 3, 1, false);
            EnemyBlueprint w3s4 = factory.CreateEnemyBlueprint("w3_mecha_boss", 3, 4, false);

            // Assert
            Assert.AreEqual(1.00f, w1s1.healthMultiplier, 0.0001f);
            Assert.AreEqual(1.00f, w1s1.damageMultiplier, 0.0001f);

            Assert.AreEqual(1.25f, w1s4.healthMultiplier, 0.0001f);
            Assert.AreEqual(1.15f, w1s4.damageMultiplier, 0.0001f);

            Assert.AreEqual(1.30f, w2s1.healthMultiplier, 0.0001f);
            Assert.AreEqual(1.20f, w2s1.damageMultiplier, 0.0001f);

            Assert.AreEqual(1.55f, w2s4.healthMultiplier, 0.0001f);
            Assert.AreEqual(1.35f, w2s4.damageMultiplier, 0.0001f);

            Assert.AreEqual(1.60f, w3s1.healthMultiplier, 0.0001f);
            Assert.AreEqual(1.40f, w3s1.damageMultiplier, 0.0001f);

            Assert.AreEqual(1.85f, w3s4.healthMultiplier, 0.0001f);
            Assert.AreEqual(1.55f, w3s4.damageMultiplier, 0.0001f);
            Assert.AreEqual(277.5f, w3s4.maxHealth, 0.01f);
            Assert.AreEqual(31.0f, w3s4.heavyAttackDamage, 0.01f);
        }

        [Test]
        public void CreateEnemyBlueprint_CampaignWithExternalBalanceConfig_AppliesConfiguredLevelMultipliers()
        {
            // Arrange
            GameBalanceConfigDTO cfg = new GameBalanceConfigDTO
            {
                levels = new List<LevelBalanceDTO>
                {
                    new LevelBalanceDTO
                    {
                        worldIndex = 3,
                        stageIndex = 4,
                        enemyHpMultiplier = 2.00f,
                        enemyDamageMultiplier = 1.60f
                    }
                }
            };
            WorldEnemyFactory factory = new WorldEnemyFactory(cfg);

            // Act
            EnemyBlueprint mecha = factory.CreateEnemyBlueprint("w3_mecha_boss", worldIndex: 3, waveIndex: 4, isEndlessMode: false);

            // Assert
            Assert.AreEqual(2.00f, mecha.healthMultiplier, 0.0001f);
            Assert.AreEqual(1.60f, mecha.damageMultiplier, 0.0001f);
            Assert.AreEqual(300.0f, mecha.maxHealth, 0.01f);
            Assert.AreEqual(32.0f, mecha.heavyAttackDamage, 0.01f);
        }

        [Test]
        public void ResolveCanonicalEnemyId_MapsPrefabNamesAndDisplayNamesAccurately()
        {
            // Arrange
            WorldEnemyFactory factory = new WorldEnemyFactory();

            // Act & Assert
            Assert.AreEqual("w1_pure_titan", factory.ResolveCanonicalEnemyId("W1_Creep_PureTitan"));
            Assert.AreEqual("w1_warhammer_boss", factory.ResolveCanonicalEnemyId("W1_Boss_WarHammer"));
            Assert.AreEqual("w2_saibaman_kamikaze", factory.ResolveCanonicalEnemyId("W2_Creep_SaibamanKamikaze"));
            Assert.AreEqual("w2_saibamen", factory.ResolveCanonicalEnemyId("W2_Creep_Saibamen"));
            Assert.AreEqual("w2_broly_boss", factory.ResolveCanonicalEnemyId("Berserker Broly"));
            Assert.AreEqual("w3_shocker", factory.ResolveCanonicalEnemyId("W3_Creep_Shocker"));
            Assert.AreEqual("w3_mecha_boss", factory.ResolveCanonicalEnemyId("Dai-Shocker Mecha"));
            Assert.IsNull(factory.ResolveCanonicalEnemyId("UnknownRandomPrefab"));
            Assert.IsNull(factory.ResolveCanonicalEnemyId("   "));
        }

        [Test]
        public void CreateEnemyBlueprint_InvalidAndBoundaryInputs_ThrowsInvalidGameConfigException()
        {
            // Arrange
            WorldEnemyFactory factory = new WorldEnemyFactory();

            // Act & Assert
            InvalidGameConfigException emptyIdEx = Assert.Throws<InvalidGameConfigException>(
                () => factory.CreateEnemyBlueprint("", 1, 1, false));
            InvalidGameConfigException unknownEx = Assert.Throws<InvalidGameConfigException>(
                () => factory.CreateEnemyBlueprint("ghost_titan", 1, 1, false));
            InvalidGameConfigException lowWorldEx = Assert.Throws<InvalidGameConfigException>(
                () => factory.CreateEnemyBlueprint("w1_pure_titan", 0, 1, false));
            InvalidGameConfigException highWorldEx = Assert.Throws<InvalidGameConfigException>(
                () => factory.CreateEnemyBlueprint("w1_pure_titan", 4, 1, false));
            InvalidGameConfigException badWaveEx = Assert.Throws<InvalidGameConfigException>(
                () => factory.CreateEnemyBlueprint("w1_pure_titan", 1, 0, true));
            InvalidGameConfigException badConfigEx = Assert.Throws<InvalidGameConfigException>(
                () => new WorldEnemyFactory(new GameBalanceConfigDTO { endlessWaveHpGrowth = -0.1f }));

            Assert.AreEqual("enemyId", emptyIdEx.ConfigKey);
            Assert.AreEqual("INVALID_CONFIG", unknownEx.ErrorCode);
            Assert.AreEqual("worldIndex", lowWorldEx.ConfigKey);
            Assert.AreEqual("worldIndex", highWorldEx.ConfigKey);
            Assert.AreEqual("waveIndex", badWaveEx.ConfigKey);
            Assert.AreEqual("INVALID_CONFIG", badConfigEx.ErrorCode);
        }

        [Test]
        public void RegisterTemplate_OCP_AddsCustomEnemyBlueprintAndRejectsInvalidTemplates()
        {
            // Arrange
            WorldEnemyFactory factory = new WorldEnemyFactory();

            // Act
            factory.RegisterTemplate(new EnemyBlueprint
            {
                enemyId = "w3_cyber_assassin",
                worldIndex = 3,
                strategyId = "ambush_flank",
                maxHealth = 40f,
                attackDamage = 14f,
                heavyAttackDamage = 22f,
                moveSpeed = 4.8f,
                attackCooldown = 1.1f,
                hasSuperArmor = false,
                isBoss = false
            });
            EnemyBlueprint created = factory.CreateEnemyBlueprint("w3_cyber_assassin", 3, 1, isEndlessMode: true);

            // Assert
            Assert.IsTrue(factory.CanCreate("w3_cyber_assassin"));
            Assert.AreEqual("ambush_flank", created.strategyId);
            Assert.AreEqual(40f, created.maxHealth, 0.001f);

            Assert.Throws<InvalidGameConfigException>(() => factory.RegisterTemplate(null));
            Assert.Throws<InvalidGameConfigException>(() => factory.RegisterTemplate(new EnemyBlueprint
            {
                enemyId = "broken_zero_hp",
                worldIndex = 1,
                maxHealth = 0f
            }));
        }
    }
}
