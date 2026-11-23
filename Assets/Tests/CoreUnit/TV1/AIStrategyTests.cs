using System;
using NUnit.Framework;
using StickmanBeatEmUp.CorePure.AI;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.Tests.CoreUnit.TV1
{
    [TestFixture]
    public class AIStrategyTests
    {
        private sealed class CustomTestFlankStrategy : IAIStrategy
        {
            public string StrategyId => "custom_flank_test";

            public AIActionDecision ComputeAction(EnemyAIContext context)
            {
                return new AIActionDecision(AIActionType.TeleportBehind, 0f, true, false);
            }
        }

        private sealed class CustomHardBossOverrideStrategy : IAIStrategy
        {
            public string StrategyId => "hard_boss_override";

            public AIActionDecision ComputeAction(EnemyAIContext context)
            {
                return new AIActionDecision(AIActionType.SpecialPhase, 0f, true, true);
            }
        }

        [Test]
        public void MeleeChaseStrategy_OutOfRange_MovesTowardPlayer_AndScalesSpeedByDifficulty()
        {
            // Arrange
            MeleeChaseStrategy strategy = new MeleeChaseStrategy();
            EnemyAIContext easyCtx = new EnemyAIContext
            {
                EnemyX = 0f,
                PlayerX = 6f,
                EnemyHp = 10f,
                EnemyMaxHp = 10f,
                AttackRange = 1.5f,
                DifficultyLevel = (int)AIDifficultyPreset.Easy
            };
            EnemyAIContext normalCtx = easyCtx;
            normalCtx.DifficultyLevel = (int)AIDifficultyPreset.Normal;
            EnemyAIContext hardCtx = easyCtx;
            hardCtx.DifficultyLevel = (int)AIDifficultyPreset.Hard;

            // Act
            AIActionDecision easyDecision = strategy.ComputeAction(easyCtx);
            AIActionDecision normalDecision = strategy.ComputeAction(normalCtx);
            AIActionDecision hardDecision = strategy.ComputeAction(hardCtx);

            // Assert
            Assert.AreEqual(AIActionType.MoveTowardPlayer, easyDecision.ActionType);
            Assert.AreEqual(AIActionType.MoveTowardPlayer, normalDecision.ActionType);
            Assert.AreEqual(AIActionType.MoveTowardPlayer, hardDecision.ActionType);
            Assert.AreEqual(0.85f, easyDecision.MoveDirectionX, 0.001f);
            Assert.AreEqual(1.00f, normalDecision.MoveDirectionX, 0.001f);
            Assert.AreEqual(1.15f, hardDecision.MoveDirectionX, 0.001f);
            Assert.Greater(hardDecision.MoveDirectionX, easyDecision.MoveDirectionX);
        }

        [Test]
        public void MeleeChaseStrategy_InRange_UsesLightOnEasyAndNormal_AndHeavyWhenLowHpHard()
        {
            // Arrange
            MeleeChaseStrategy strategy = new MeleeChaseStrategy();
            EnemyAIContext easyCtx = new EnemyAIContext
            {
                EnemyX = 0f,
                PlayerX = 1.2f,
                EnemyHp = 10f,
                EnemyMaxHp = 10f,
                AttackRange = 1.5f,
                AttackCooldownRemaining = 0f,
                DifficultyLevel = (int)AIDifficultyPreset.Easy
            };
            EnemyAIContext normalCtx = easyCtx;
            normalCtx.DifficultyLevel = (int)AIDifficultyPreset.Normal;

            EnemyAIContext hardLowHpCtx = normalCtx;
            hardLowHpCtx.EnemyHp = 4f;
            hardLowHpCtx.DifficultyLevel = (int)AIDifficultyPreset.Hard;
            hardLowHpCtx.AttackCooldownRemaining = 0.15f; // <= 0.18s Hard ready window

            EnemyAIContext normalWaitingCtx = normalCtx;
            normalWaitingCtx.AttackCooldownRemaining = 0.15f; // > 0s Normal ready window

            // Act
            AIActionDecision easyDecision = strategy.ComputeAction(easyCtx);
            AIActionDecision normalDecision = strategy.ComputeAction(normalCtx);
            AIActionDecision hardDecision = strategy.ComputeAction(hardLowHpCtx);
            AIActionDecision waitingDecision = strategy.ComputeAction(normalWaitingCtx);

            // Assert
            Assert.AreEqual(AIActionType.LightAttack, easyDecision.ActionType);
            Assert.AreEqual(AIActionType.LightAttack, normalDecision.ActionType);
            Assert.AreEqual(AIActionType.HeavyAttack, hardDecision.ActionType);
            Assert.AreEqual(AIActionType.Idle, waitingDecision.ActionType);
            Assert.IsTrue(waitingDecision.ShouldFacePlayer);
        }

        [Test]
        public void MeleeChaseStrategy_BoundaryDeadOrNaNContext_ReturnsIdleSafely()
        {
            // Arrange
            MeleeChaseStrategy strategy = new MeleeChaseStrategy();
            EnemyAIContext playerDead = new EnemyAIContext { EnemyHp = 10f, PlayerX = 1f, IsPlayerDead = true };
            EnemyAIContext enemyDead = new EnemyAIContext { EnemyHp = 0f, PlayerX = 1f, IsPlayerDead = false };
            EnemyAIContext nanPosition = new EnemyAIContext { EnemyHp = 10f, EnemyX = float.NaN, PlayerX = 2f };
            EnemyAIContext verticalTooHigh = new EnemyAIContext
            {
                EnemyX = 0f,
                EnemyY = 0f,
                PlayerX = 0f,
                PlayerY = 5f,
                EnemyHp = 10f,
                AttackRange = 1.5f,
                DifficultyLevel = (int)AIDifficultyPreset.Normal
            };

            // Act & Assert
            Assert.AreEqual(AIActionType.Idle, strategy.ComputeAction(playerDead).ActionType);
            Assert.AreEqual(AIActionType.Idle, strategy.ComputeAction(enemyDead).ActionType);
            Assert.AreEqual(AIActionType.Idle, strategy.ComputeAction(nanPosition).ActionType);
            Assert.AreEqual(AIActionType.Idle, strategy.ComputeAction(verticalTooHigh).ActionType);
        }

        [Test]
        public void RangedKiteStrategy_ThreeDistanceZones_FleesFiresAndAdvancesCorrectly()
        {
            // Arrange
            RangedKiteStrategy strategy = new RangedKiteStrategy();
            EnemyAIContext tooClose = new EnemyAIContext
            {
                EnemyX = 5f,
                PlayerX = 6.5f, // dist = 1.5 < 2.6 flee
                EnemyHp = 10f,
                PreferredRange = 6.5f,
                FleeRange = 2.6f,
                AttackCooldownRemaining = 0f,
                DifficultyLevel = (int)AIDifficultyPreset.Normal
            };
            EnemyAIContext sweetSpot = tooClose;
            sweetSpot.PlayerX = 10f; // dist = 5.0 in [2.6, 7.475]

            EnemyAIContext sweetSpotOnCooldown = sweetSpot;
            sweetSpotOnCooldown.AttackCooldownRemaining = 0.8f;

            EnemyAIContext tooFar = tooClose;
            tooFar.PlayerX = 15f; // dist = 10.0 > 7.475

            // Act
            AIActionDecision fleeDecision = strategy.ComputeAction(tooClose);
            AIActionDecision fireDecision = strategy.ComputeAction(sweetSpot);
            AIActionDecision waitDecision = strategy.ComputeAction(sweetSpotOnCooldown);
            AIActionDecision chaseDecision = strategy.ComputeAction(tooFar);

            // Assert
            Assert.AreEqual(AIActionType.MoveAwayFromPlayer, fleeDecision.ActionType);
            Assert.Less(fleeDecision.MoveDirectionX, 0f);
            Assert.IsFalse(fleeDecision.ShouldFacePlayer);

            Assert.AreEqual(AIActionType.RangedVolley, fireDecision.ActionType);
            Assert.IsTrue(fireDecision.ShouldFacePlayer);

            Assert.AreEqual(AIActionType.Idle, waitDecision.ActionType);

            Assert.AreEqual(AIActionType.MoveTowardPlayer, chaseDecision.ActionType);
            Assert.Greater(chaseDecision.MoveDirectionX, 0f);
        }

        [Test]
        public void RangedKiteStrategy_EasyVsHardFleeThreshold_HardRetreatsEarlier()
        {
            // Arrange: FleeRange = 2.5 -> Easy = 2.125, Hard = 2.875. At dist = 2.5: Easy fires, Hard flees.
            RangedKiteStrategy strategy = new RangedKiteStrategy();
            EnemyAIContext easyCtx = new EnemyAIContext
            {
                EnemyX = 0f,
                PlayerX = 2.5f,
                EnemyHp = 10f,
                PreferredRange = 6.5f,
                FleeRange = 2.5f,
                AttackCooldownRemaining = 0f,
                DifficultyLevel = (int)AIDifficultyPreset.Easy
            };
            EnemyAIContext hardCtx = easyCtx;
            hardCtx.DifficultyLevel = (int)AIDifficultyPreset.Hard;

            // Act
            AIActionDecision easyDecision = strategy.ComputeAction(easyCtx);
            AIActionDecision hardDecision = strategy.ComputeAction(hardCtx);

            // Assert
            Assert.AreEqual(AIActionType.RangedVolley, easyDecision.ActionType);
            Assert.AreEqual(AIActionType.MoveAwayFromPlayer, hardDecision.ActionType);
        }

        [Test]
        public void KamikazeRushStrategy_RushesAt1_5xOutsideRange_AndDetonatesInsideRange()
        {
            // Arrange
            KamikazeRushStrategy strategy = new KamikazeRushStrategy();
            EnemyAIContext farCtx = new EnemyAIContext
            {
                EnemyX = 0f,
                PlayerX = 5f,
                EnemyHp = 5f,
                AttackRange = 1.45f,
                DifficultyLevel = (int)AIDifficultyPreset.Normal
            };
            EnemyAIContext farHardCtx = farCtx;
            farHardCtx.DifficultyLevel = (int)AIDifficultyPreset.Hard;

            EnemyAIContext closeCtx = farCtx;
            closeCtx.PlayerX = 1.2f;
            closeCtx.AttackCooldownRemaining = 0f;

            // Act
            AIActionDecision rushNormal = strategy.ComputeAction(farCtx);
            AIActionDecision rushHard = strategy.ComputeAction(farHardCtx);
            AIActionDecision detonateDecision = strategy.ComputeAction(closeCtx);

            // Assert
            Assert.AreEqual(AIActionType.MoveTowardPlayer, rushNormal.ActionType);
            Assert.AreEqual(1.5f, rushNormal.MoveDirectionX, 0.001f);
            Assert.AreEqual(1.5f * 1.15f, rushHard.MoveDirectionX, 0.001f);
            Assert.AreEqual(AIActionType.Detonate, detonateDecision.ActionType);
        }

        [Test]
        public void BossPhaseStrategy_FullRage_TriggersSpecialPhaseWithShieldActivation()
        {
            // Arrange
            BossPhaseStrategy strategy = new BossPhaseStrategy();
            EnemyAIContext ctx = new EnemyAIContext
            {
                EnemyX = 10f,
                PlayerX = 6f,
                EnemyHp = 120f,
                EnemyMaxHp = 150f,
                EnemyRage = 100f,
                DifficultyLevel = (int)AIDifficultyPreset.Normal
            };

            // Act
            AIActionDecision decision = strategy.ComputeAction(ctx);

            // Assert
            Assert.AreEqual(AIActionType.SpecialPhase, decision.ActionType);
            Assert.IsTrue(decision.ShouldActivateShield);
            Assert.IsTrue(decision.ShouldFacePlayer);
        }

        [Test]
        public void BossPhaseStrategy_Phase1VsPhase2_TransitionsMeleeAttackAndIncreasesChaseSpeed()
        {
            // Arrange
            BossPhaseStrategy strategy = new BossPhaseStrategy();
            EnemyAIContext phase1Melee = new EnemyAIContext
            {
                EnemyX = 5f,
                PlayerX = 6.5f,
                EnemyHp = 120f,
                EnemyMaxHp = 150f,
                AttackRange = 2.3f,
                AttackCooldownRemaining = 0f,
                DifficultyLevel = (int)AIDifficultyPreset.Normal
            };
            EnemyAIContext phase2Melee = phase1Melee;
            phase2Melee.EnemyHp = 60f; // 40% <= 50% Normal Phase 2 threshold

            EnemyAIContext phase1Chase = phase1Melee;
            phase1Chase.PlayerX = 18f; // far outside special range
            EnemyAIContext phase2Chase = phase1Chase;
            phase2Chase.EnemyHp = 60f;

            // Act
            AIActionDecision melee1 = strategy.ComputeAction(phase1Melee);
            AIActionDecision melee2 = strategy.ComputeAction(phase2Melee);
            AIActionDecision chase1 = strategy.ComputeAction(phase1Chase);
            AIActionDecision chase2 = strategy.ComputeAction(phase2Chase);

            // Assert
            Assert.AreEqual(AIActionType.LightAttack, melee1.ActionType);
            Assert.AreEqual(AIActionType.HeavyAttack, melee2.ActionType);
            Assert.AreEqual(AIActionType.MoveTowardPlayer, chase1.ActionType);
            Assert.AreEqual(AIActionType.MoveTowardPlayer, chase2.ActionType);
            Assert.AreEqual(1.0f, chase1.MoveDirectionX, 0.001f);
            Assert.AreEqual(1.15f, chase2.MoveDirectionX, 0.001f);
        }

        [Test]
        public void BossPhaseStrategy_SpamLightHits_TriggersParryCounterAtDifficultyThresholds()
        {
            // Arrange: Easy=4, Normal=3, Hard=2
            BossPhaseStrategy strategy = new BossPhaseStrategy();
            EnemyAIContext ctx = new EnemyAIContext
            {
                EnemyX = 5f,
                PlayerX = 6.5f,
                EnemyHp = 100f,
                EnemyMaxHp = 150f,
                IsPlayerAttacking = true,
                LightHitsReceivedInWindow = 2,
                AttackRange = 2.3f,
                AttackCooldownRemaining = 1f,
                DifficultyLevel = (int)AIDifficultyPreset.Normal
            };

            // Act & Assert: 2 hits -> Normal does not parry, Hard parries
            Assert.AreNotEqual(AIActionType.ParryCounter, strategy.ComputeAction(ctx).ActionType);

            ctx.DifficultyLevel = (int)AIDifficultyPreset.Hard;
            Assert.AreEqual(AIActionType.ParryCounter, strategy.ComputeAction(ctx).ActionType);

            // 3 hits -> Easy still does not parry, Normal parries
            ctx.LightHitsReceivedInWindow = 3;
            ctx.DifficultyLevel = (int)AIDifficultyPreset.Easy;
            Assert.AreNotEqual(AIActionType.ParryCounter, strategy.ComputeAction(ctx).ActionType);

            ctx.DifficultyLevel = (int)AIDifficultyPreset.Normal;
            Assert.AreEqual(AIActionType.ParryCounter, strategy.ComputeAction(ctx).ActionType);
        }

        [Test]
        public void AIDifficultyTuning_ClampsOutOfRangeDifficultyLevels()
        {
            // Arrange & Act & Assert
            Assert.AreEqual((int)AIDifficultyPreset.Easy, AIDifficultyTuning.ClampDifficulty(-99));
            Assert.AreEqual((int)AIDifficultyPreset.Hard, AIDifficultyTuning.ClampDifficulty(42));
            Assert.AreEqual(AIDifficultyPreset.Normal, AIDifficultyTuning.ToPreset(1));
            Assert.AreEqual(1.25f, AIDifficultyTuning.GetAttackCooldownScale(-5), 0.0001f);
            Assert.AreEqual(0.85f, AIDifficultyTuning.GetAttackCooldownScale(10), 0.0001f);
        }

        [Test]
        public void AIStrategyRegistry_OCP_RegistersCustomStrategyAndDifficultyOverrideWithoutModifyingExistingClasses()
        {
            // Arrange
            AIStrategyRegistry registry = AIStrategyRegistry.CreateDefault();
            CustomTestFlankStrategy custom = new CustomTestFlankStrategy();
            CustomHardBossOverrideStrategy hardBoss = new CustomHardBossOverrideStrategy();

            // Act
            int defaultCount = registry.Count;
            registry.Register(custom);
            registry.RegisterForDifficulty(BossPhaseStrategy.ID, AIDifficultyPreset.Hard, hardBoss);

            IAIStrategy resolvedCustom = registry.Resolve("custom_flank_test");
            IAIStrategy resolvedNormalBoss = registry.Resolve(BossPhaseStrategy.ID, AIDifficultyPreset.Normal);
            IAIStrategy resolvedHardBoss = registry.Resolve(BossPhaseStrategy.ID, AIDifficultyPreset.Hard);
            IAIStrategy resolvedFallback = registry.Resolve("non_existent_strategy");

            // Assert
            Assert.AreEqual(4, defaultCount);
            Assert.IsTrue(registry.Contains("custom_flank_test"));
            Assert.AreSame(custom, resolvedCustom);
            Assert.AreEqual(AIActionType.TeleportBehind, resolvedCustom.ComputeAction(new EnemyAIContext { EnemyHp = 10f }).ActionType);

            Assert.AreEqual(BossPhaseStrategy.ID, resolvedNormalBoss.StrategyId);
            Assert.AreSame(hardBoss, resolvedHardBoss);
            Assert.AreEqual(MeleeChaseStrategy.ID, resolvedFallback.StrategyId);

            Assert.Throws<ArgumentNullException>(() => registry.Register(null));
            Assert.Throws<ArgumentException>(() => registry.Register("  ", custom));
        }
    }
}
