using NUnit.Framework;
using StickmanBeatEmUp.CorePure.Entities.Combat;
using StickmanBeatEmUp.CorePure.Rules.Combat;
using StickmanBeatEmUp.Tests.CoreUnit.TV1.TestDoubles;

namespace StickmanBeatEmUp.Tests.CoreUnit.TV1
{
    [TestFixture]
    public class DamageCalculatorTests
    {
        [Test]
        public void BaseStatSource_DefaultInstance_HasNeutralMultipliersAndZeroBonuses()
        {
            // Arrange & Act
            BaseStatSource stats = BaseStatSource.Default;

            // Assert
            Assert.AreEqual(1f, stats.AttackDamageMultiplier, 0.0001f);
            Assert.AreEqual(1f, stats.MoveSpeedMultiplier, 0.0001f);
            Assert.AreEqual(0f, stats.MaxShieldCapacity, 0.0001f);
            Assert.AreEqual(0f, stats.DamageReflectAmount, 0.0001f);
            Assert.AreEqual(0f, stats.RagePerSecondBonus, 0.0001f);
        }

        [Test]
        public void CalculateIncomingDamage_UnblockedNormalHit_ReducesHpAndTriggersHitStun()
        {
            // Arrange
            CombatDamageInput input = CombatDamageInput.CreateDefault(rawDamage: 25f, currentHealth: 100f, maxHealth: 100f);

            // Act
            CombatDamageResult result = DamageCalculator.CalculateIncomingDamage(input, BaseStatSource.Default);

            // Assert
            Assert.IsFalse(result.WasIgnored);
            Assert.IsFalse(result.WasBlocked);
            Assert.AreEqual(25f, result.FinalHealthDamage, 0.0001f);
            Assert.AreEqual(75f, result.RemainingHealth, 0.0001f);
            Assert.IsFalse(result.IsFatal);
            Assert.IsTrue(result.ShouldTriggerHitStun);
        }

        [Test]
        public void CalculateIncomingDamage_BlockingActive_ReducesDamageBy80PercentAndPreventsHitStun()
        {
            // Arrange
            CombatDamageInput input = CombatDamageInput.CreateDefault(rawDamage: 50f, currentHealth: 100f, maxHealth: 100f);
            input.IsBlocking = true;
            input.BlockDamageMultiplier = 0.2f;

            // Act
            CombatDamageResult result = DamageCalculator.CalculateIncomingDamage(input, BaseStatSource.Default);

            // Assert
            Assert.IsTrue(result.WasBlocked);
            Assert.AreEqual(10f, result.DamageAfterBlock, 0.0001f);
            Assert.AreEqual(10f, result.FinalHealthDamage, 0.0001f);
            Assert.AreEqual(90f, result.RemainingHealth, 0.0001f);
            Assert.IsTrue(result.HasHitStunImmunity);
            Assert.IsFalse(result.ShouldTriggerHitStun);
        }

        [Test]
        public void CalculateIncomingDamage_VirtualShieldFullyAbsorbs_PreservesHealthAndGrantsStunImmunity()
        {
            // Arrange
            CombatDamageInput input = CombatDamageInput.CreateDefault(rawDamage: 25f, currentHealth: 100f, maxHealth: 100f);
            input.CurrentVirtualShield = 40f;

            // Act
            CombatDamageResult result = DamageCalculator.CalculateIncomingDamage(input, BaseStatSource.Default);

            // Assert
            Assert.IsTrue(result.ShieldWasActive);
            Assert.IsFalse(result.ShieldDepleted);
            Assert.AreEqual(25f, result.ShieldAbsorbedDamage, 0.0001f);
            Assert.AreEqual(15f, result.RemainingVirtualShield, 0.0001f);
            Assert.AreEqual(0f, result.FinalHealthDamage, 0.0001f);
            Assert.AreEqual(100f, result.RemainingHealth, 0.0001f);
            Assert.IsFalse(result.ShouldTriggerHitStun);
        }

        [Test]
        public void CalculateIncomingDamage_VirtualShieldPartiallyAbsorbs_SpillsRemainderToHealthAndMarksDepleted()
        {
            // Arrange
            CombatDamageInput input = CombatDamageInput.CreateDefault(rawDamage: 30f, currentHealth: 100f, maxHealth: 100f);
            input.CurrentVirtualShield = 15f;

            // Act
            CombatDamageResult result = DamageCalculator.CalculateIncomingDamage(input, BaseStatSource.Default);

            // Assert
            Assert.IsTrue(result.ShieldWasActive);
            Assert.IsTrue(result.ShieldDepleted);
            Assert.AreEqual(15f, result.ShieldAbsorbedDamage, 0.0001f);
            Assert.AreEqual(0f, result.RemainingVirtualShield, 0.0001f);
            Assert.AreEqual(15f, result.FinalHealthDamage, 0.0001f);
            Assert.AreEqual(85f, result.RemainingHealth, 0.0001f);
            Assert.IsTrue(result.HasHitStunImmunity);
            Assert.IsFalse(result.ShouldTriggerHitStun);
        }

        [Test]
        public void CalculateIncomingDamage_BlockAndVirtualShieldCombined_AppliesBlockFirstThenShield()
        {
            // Arrange: 50 raw * 0.2 block = 10 dmg -> absorbed completely by 40 shield -> 30 shield left
            CombatDamageInput input = CombatDamageInput.CreateDefault(rawDamage: 50f, currentHealth: 100f, maxHealth: 100f);
            input.IsBlocking = true;
            input.BlockDamageMultiplier = 0.2f;
            input.CurrentVirtualShield = 40f;

            // Act
            CombatDamageResult result = DamageCalculator.CalculateIncomingDamage(input, BaseStatSource.Default);

            // Assert
            Assert.AreEqual(10f, result.DamageAfterBlock, 0.0001f);
            Assert.AreEqual(10f, result.ShieldAbsorbedDamage, 0.0001f);
            Assert.AreEqual(30f, result.RemainingVirtualShield, 0.0001f);
            Assert.AreEqual(0f, result.FinalHealthDamage, 0.0001f);
            Assert.AreEqual(100f, result.RemainingHealth, 0.0001f);
        }

        [Test]
        public void CalculateIncomingDamage_BVA_FatalAndOverkillDamage_ClampsHpAtZeroAndDisablesHitStun()
        {
            // Arrange: exact lethal (HP = 20, dmg = 20) and overkill (HP = 20, dmg = 999)
            CombatDamageInput exactLethal = CombatDamageInput.CreateDefault(rawDamage: 20f, currentHealth: 20f, maxHealth: 100f);
            CombatDamageInput overkill = CombatDamageInput.CreateDefault(rawDamage: 999f, currentHealth: 20f, maxHealth: 100f);

            // Act
            CombatDamageResult exactResult = DamageCalculator.CalculateIncomingDamage(exactLethal);
            CombatDamageResult overkillResult = DamageCalculator.CalculateIncomingDamage(overkill);

            // Assert
            Assert.AreEqual(0f, exactResult.RemainingHealth, 0.0001f);
            Assert.IsTrue(exactResult.IsFatal);
            Assert.IsFalse(exactResult.ShouldTriggerHitStun);

            Assert.AreEqual(0f, overkillResult.RemainingHealth, 0.0001f);
            Assert.IsTrue(overkillResult.IsFatal);
            Assert.IsFalse(overkillResult.ShouldTriggerHitStun);
        }

        [Test]
        public void CalculateIncomingDamage_WhenDeadOrInvulnerable_IsCompletelyIgnored()
        {
            // Arrange
            CombatDamageInput deadInput = CombatDamageInput.CreateDefault(rawDamage: 50f, currentHealth: 0f, maxHealth: 100f);
            deadInput.IsDead = true;

            CombatDamageInput invulnInput = CombatDamageInput.CreateDefault(rawDamage: 50f, currentHealth: 60f, maxHealth: 100f);
            invulnInput.IsInvulnerable = true;

            // Act
            CombatDamageResult deadResult = DamageCalculator.CalculateIncomingDamage(deadInput);
            CombatDamageResult invulnResult = DamageCalculator.CalculateIncomingDamage(invulnInput);

            // Assert
            Assert.IsTrue(deadResult.WasIgnored);
            Assert.AreEqual(0f, deadResult.FinalHealthDamage);
            Assert.AreEqual(0f, deadResult.RemainingHealth);

            Assert.IsTrue(invulnResult.WasIgnored);
            Assert.AreEqual(0f, invulnResult.FinalHealthDamage);
            Assert.AreEqual(60f, invulnResult.RemainingHealth);
        }

        [Test]
        public void CalculateIncomingDamage_NegativeAndNaNRawDamage_ClampedToZeroNeverHeals()
        {
            // Arrange
            CombatDamageInput negativeInput = CombatDamageInput.CreateDefault(rawDamage: -35f, currentHealth: 50f, maxHealth: 100f);
            CombatDamageInput nanInput = CombatDamageInput.CreateDefault(rawDamage: float.NaN, currentHealth: 50f, maxHealth: 100f);

            // Act
            CombatDamageResult negResult = DamageCalculator.CalculateIncomingDamage(negativeInput);
            CombatDamageResult nanResult = DamageCalculator.CalculateIncomingDamage(nanInput);

            // Assert
            Assert.AreEqual(0f, negResult.FinalHealthDamage, 0.0001f);
            Assert.AreEqual(50f, negResult.RemainingHealth, 0.0001f);
            Assert.AreEqual(0f, nanResult.FinalHealthDamage, 0.0001f);
            Assert.AreEqual(50f, nanResult.RemainingHealth, 0.0001f);
        }

        [Test]
        public void CalculateIncomingDamage_ReflectBoundaries_ReflectsWithin4_5mOnlyForEnemyAttacks()
        {
            // Arrange: exactly at boundary 4.5f -> reflects; at 4.51f -> does not reflect
            CombatDamageInput inRange = CombatDamageInput.CreateDefault(rawDamage: 20f, currentHealth: 100f, maxHealth: 100f);
            inRange.CurrentVirtualShield = 40f;
            inRange.BonusReflectDamage = 12f;
            inRange.HasAttacker = true;
            inRange.AttackKind = CombatAttackKind.EnemyHeavyAttack;
            inRange.AttackerDistance = 4.5f;

            CombatDamageInput outOfRange = inRange;
            outOfRange.AttackerDistance = 4.51f;

            CombatDamageInput nonEnemyAttack = inRange;
            nonEnemyAttack.AttackKind = CombatAttackKind.TransformBurst;

            // Act
            CombatDamageResult inRangeResult = DamageCalculator.CalculateIncomingDamage(inRange);
            CombatDamageResult outOfRangeResult = DamageCalculator.CalculateIncomingDamage(outOfRange);
            CombatDamageResult nonEnemyResult = DamageCalculator.CalculateIncomingDamage(nonEnemyAttack);

            // Assert
            Assert.IsTrue(inRangeResult.ShouldReflectDamage);
            Assert.AreEqual(12f, inRangeResult.ReflectedDamage, 0.0001f);

            Assert.IsFalse(outOfRangeResult.ShouldReflectDamage);
            Assert.AreEqual(0f, outOfRangeResult.ReflectedDamage);

            Assert.IsFalse(nonEnemyResult.ShouldReflectDamage);
            Assert.AreEqual(0f, nonEnemyResult.ReflectedDamage);
        }

        [Test]
        public void CalculateDamage_WithMockStatSource_AppliesMultiplierAndReflectFromStatSource()
        {
            // Arrange
            MockStatSource attackerStats = new MockStatSource { AttackDamageMultiplier = 1.5f };
            MockStatSource defenderStats = new MockStatSource { DamageReflectAmount = 8f };

            CombatDamageInput input = CombatDamageInput.CreateDefault(rawDamage: 20f, currentHealth: 100f, maxHealth: 100f);
            input.HasAttacker = true;
            input.AttackKind = CombatAttackKind.EnemyAttack;
            input.AttackerDistance = 2.0f;

            // Act
            float outgoing = DamageCalculator.CalculateOutgoingDamage(20f, attackerStats, extraMultiplier: 2f);
            CombatDamageResult incoming = DamageCalculator.CalculateIncomingDamage(input, defenderStats, attackerStats);

            // Assert
            Assert.AreEqual(60f, outgoing, 0.0001f);
            Assert.AreEqual(30f, incoming.ScaledIncomingDamage, 0.0001f);
            Assert.AreEqual(70f, incoming.RemainingHealth, 0.0001f);
            Assert.IsTrue(incoming.ShouldReflectDamage);
            Assert.AreEqual(8f, incoming.ReflectedDamage, 0.0001f);
            Assert.GreaterOrEqual(attackerStats.AttackMultiplierReadCount, 2);
            Assert.GreaterOrEqual(defenderStats.DamageReflectReadCount, 1);
        }
    }
}
