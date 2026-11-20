using System.Collections.Generic;
using NUnit.Framework;
using StickmanBeatEmUp.CorePure.Entities.Combat;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Rules.Combat;

namespace StickmanBeatEmUp.Tests.CoreUnit.TV1
{
    [TestFixture]
    public class RageRuleEngineTests
    {
        [Test]
        public void InitialState_DefaultConstructor_StartsAtZeroBoundary()
        {
            // Arrange & Act
            RageRuleEngine engine = new RageRuleEngine();

            // Assert
            Assert.AreEqual(0, engine.CurrentRage);
            Assert.AreEqual(0f, engine.CurrentRageFloat, 0.0001f);
            Assert.AreEqual(0, engine.FilledSegments);
            Assert.IsFalse(engine.IsMaxRage);
            Assert.IsFalse(engine.CanUseSkillI());
            Assert.IsFalse(engine.CanTransform());
            Assert.IsFalse(engine.CanUseSpecialCombo(true));
        }

        [Test]
        public void AddRageFloat_BVA_0_10_20_30_FiresSegmentsAndUnlocksSkillsAtExactThresholds()
        {
            // Arrange
            RageRuleEngine engine = new RageRuleEngine();
            List<int> filledSegments = new List<int>();
            int lastNotifiedRage = -1;
            engine.OnSegmentFilled += seg => filledSegments.Add(seg);
            engine.OnRageChanged += (cur, max) => lastNotifiedRage = cur;

            // Act & Assert: Boundary 9.9 -> still 0 segments, Skill I locked
            engine.AddRageFloat(9.9f);
            Assert.AreEqual(9, engine.CurrentRage);
            Assert.AreEqual(0, engine.FilledSegments);
            Assert.IsFalse(engine.CanUseSkillI());
            Assert.AreEqual(0, filledSegments.Count);

            // Act & Assert: Boundary 10 -> segment 1 filled, Skill I unlocked
            engine.AddRageFloat(0.1f);
            Assert.AreEqual(10, engine.CurrentRage);
            Assert.AreEqual(1, engine.FilledSegments);
            Assert.IsTrue(engine.CanUseSkillI());
            Assert.IsFalse(engine.CanTransform());
            CollectionAssert.AreEqual(new[] { 1 }, filledSegments);

            // Act & Assert: Boundary 20 -> segment 2 filled, Transform still locked
            engine.AddRageFloat(10f);
            Assert.AreEqual(20, engine.CurrentRage);
            Assert.AreEqual(2, engine.FilledSegments);
            Assert.IsTrue(engine.CanUseSkillI());
            Assert.IsFalse(engine.CanTransform());
            CollectionAssert.AreEqual(new[] { 1, 2 }, filledSegments);

            // Act & Assert: Boundary 30 -> segment 3 filled, Transform & SpecialCombo unlocked
            engine.AddRageFloat(10f);
            Assert.AreEqual(30, engine.CurrentRage);
            Assert.AreEqual(3, engine.FilledSegments);
            Assert.IsTrue(engine.IsMaxRage);
            Assert.IsTrue(engine.CanTransform());
            Assert.IsFalse(engine.CanUseSpecialCombo(isTransformed: false));
            Assert.IsTrue(engine.CanUseSpecialCombo(isTransformed: true));
            Assert.AreEqual(30, lastNotifiedRage);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, filledSegments);
        }

        [Test]
        public void AddRageFloat_OverflowBeyondMax_PreservesUpperBoundInvariant30()
        {
            // Arrange
            RageRuleEngine engine = new RageRuleEngine();
            List<int> filledSegments = new List<int>();
            engine.OnSegmentFilled += seg => filledSegments.Add(seg);

            // Act
            float gained = engine.AddRageFloat(150f);
            float extraAfterMax = engine.AddRageFloat(5f);

            // Assert
            Assert.AreEqual(30f, gained, 0.0001f);
            Assert.AreEqual(0f, extraAfterMax, 0.0001f);
            Assert.AreEqual(RageRuleEngine.MAX_RAGE, engine.CurrentRage);
            Assert.AreEqual(30f, engine.CurrentRageFloat, 0.0001f);
            Assert.AreEqual(3, engine.FilledSegments);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, filledSegments);
        }

        [Test]
        public void AddRageFloat_ZeroNegativeAndNaN_IgnoredWithoutEvents()
        {
            // Arrange
            RageRuleEngine engine = new RageRuleEngine(5f);
            int eventCount = 0;
            engine.OnRageChanged += (_, __) => eventCount++;

            // Act
            float deltaZero = engine.AddRageFloat(0f);
            float deltaNegative = engine.AddRageFloat(-10f);
            float deltaNaN = engine.AddRageFloat(float.NaN);
            float deltaInf = engine.AddRageFloat(float.PositiveInfinity);

            // Assert
            Assert.AreEqual(0f, deltaZero);
            Assert.AreEqual(0f, deltaNegative);
            Assert.AreEqual(0f, deltaNaN);
            Assert.AreEqual(0f, deltaInf);
            Assert.AreEqual(5f, engine.CurrentRageFloat, 0.0001f);
            Assert.AreEqual(0, eventCount);
        }

        [Test]
        public void AddRageFromHit_EquivalencePartitions_OnlyEligiblePlayerAttacksGainRage()
        {
            // Arrange
            RageRuleEngine engine = new RageRuleEngine();

            // Act: Eligible hits (J, U, Dash+J)
            float fromJ = engine.AddRageFromHit(CombatAttackKind.NormalAttackJ);
            float fromU = engine.AddRageFromHit(CombatAttackKind.SkillU);
            float fromDashJ = engine.AddRageFromHit(CombatAttackKind.ComboDashJ, 0.75f);

            // Act: Ineligible hits (Skill I, Dash+I, TransformBurst, Enemy attacks)
            float fromSkillI = engine.AddRageFromHit(CombatAttackKind.SkillI);
            float fromDashI = engine.AddRageFromHit(CombatAttackKind.ComboDashI);
            float fromBurst = engine.AddRageFromHit(CombatAttackKind.TransformBurst);
            float fromEnemy = engine.AddRageFromHit(CombatAttackKind.EnemyAttack);

            // Assert
            Assert.AreEqual(RageRuleEngine.RAGE_PER_HIT, fromJ, 0.0001f);
            Assert.AreEqual(RageRuleEngine.RAGE_PER_HIT, fromU, 0.0001f);
            Assert.AreEqual(0.75f, fromDashJ, 0.0001f);
            Assert.AreEqual(0f, fromSkillI);
            Assert.AreEqual(0f, fromDashI);
            Assert.AreEqual(0f, fromBurst);
            Assert.AreEqual(0f, fromEnemy);
            Assert.AreEqual(3.75f, engine.CurrentRageFloat, 0.0001f);
        }

        [Test]
        public void AddRageFromKill_AddsExactKillReward()
        {
            // Arrange
            RageRuleEngine engine = new RageRuleEngine();

            // Act
            float gained = engine.AddRageFromKill();

            // Assert
            Assert.AreEqual(RageRuleEngine.RAGE_PER_KILL, gained, 0.0001f);
            Assert.AreEqual(2, engine.CurrentRage);
        }

        [Test]
        public void TryConsumeRage_BVA_ExactInsufficientAndNegative_PreservesLowerBoundInvariant0()
        {
            // Arrange
            RageRuleEngine engine = new RageRuleEngine(10f);

            // Act
            bool negativeResult = engine.TryConsumeRage(-5);
            bool exactResult = engine.TryConsumeRage(RageRuleEngine.SKILL_I_COST);
            bool insufficientResult = engine.TryConsumeRage(1);

            // Assert
            Assert.IsFalse(negativeResult);
            Assert.IsTrue(exactResult);
            Assert.IsFalse(insufficientResult);
            Assert.AreEqual(0, engine.CurrentRage);
            Assert.AreEqual(0f, engine.CurrentRageFloat, 0.0001f);
        }

        [Test]
        public void ConsumeRageOrThrow_InsufficientRage_ThrowsInvalidCombatStateException()
        {
            // Arrange
            RageRuleEngine engine = new RageRuleEngine(9f);

            // Act & Assert
            InvalidCombatStateException ex = Assert.Throws<InvalidCombatStateException>(
                () => engine.ConsumeRageOrThrow(RageRuleEngine.SKILL_I_COST, "Idle"));

            Assert.AreEqual("INVALID_COMBAT_STATE", ex.ErrorCode);
            Assert.AreEqual("Idle", ex.CurrentStateName);
            Assert.AreEqual(9, engine.CurrentRage);
        }

        [Test]
        public void FillMaxRageAndResetRage_UpdatesBoundsAndFiresRemainingSegmentsOnly()
        {
            // Arrange
            RageRuleEngine engine = new RageRuleEngine(10f);
            List<int> segments = new List<int>();
            engine.OnSegmentFilled += seg => segments.Add(seg);

            // Act
            engine.FillMaxRage();
            int maxRageAfterFill = engine.CurrentRage;
            engine.ResetRage();

            // Assert
            Assert.AreEqual(30, maxRageAfterFill);
            CollectionAssert.AreEqual(new[] { 2, 3 }, segments);
            Assert.AreEqual(0, engine.CurrentRage);
            Assert.AreEqual(0, engine.FilledSegments);
        }
    }
}
