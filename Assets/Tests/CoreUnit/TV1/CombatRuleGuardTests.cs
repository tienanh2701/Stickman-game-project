using NUnit.Framework;
using StickmanBeatEmUp.CorePure.Entities.Combat;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Rules.Combat;
using StickmanBeatEmUp.Tests.CoreUnit.TV1.TestDoubles;

namespace StickmanBeatEmUp.Tests.CoreUnit.TV1
{
    [TestFixture]
    public class CombatRuleGuardTests
    {
        [Test]
        public void TransitionFromDead_AlwaysForbidden_AndEnsureThrowsInvalidCombatStateException()
        {
            // Arrange & Act
            bool canToIdle = CombatRuleGuard.CanTransition(CombatActorState.Dead, CombatActorState.Idle);
            bool canToAttack = CombatRuleGuard.CanTransition(CombatActorState.Dead, CombatActorState.Attack);
            InvalidCombatStateException ex = Assert.Throws<InvalidCombatStateException>(
                () => CombatRuleGuard.EnsureCanTransition(CombatActorState.Dead, CombatActorState.Idle));

            // Assert
            Assert.IsFalse(canToIdle);
            Assert.IsFalse(canToAttack);
            Assert.AreEqual("INVALID_COMBAT_STATE", ex.ErrorCode);
            Assert.AreEqual("Dead", ex.CurrentStateName);
        }

        [Test]
        public void TransitionFromStunned_BlocksActiveCombatStatesAndAllowsRecoveryOrDeath()
        {
            // Arrange & Act
            bool canToAttack = CombatRuleGuard.CanTransition(CombatActorState.Stunned, CombatActorState.Attack);
            bool canToDash = CombatRuleGuard.CanTransition(CombatActorState.Stunned, CombatActorState.Dash);
            bool canToIdle = CombatRuleGuard.CanTransition(CombatActorState.Stunned, CombatActorState.Idle);
            bool canToDead = CombatRuleGuard.CanTransition(CombatActorState.Stunned, CombatActorState.Dead);

            InvalidCombatStateException ex = Assert.Throws<InvalidCombatStateException>(
                () => CombatRuleGuard.EnsureCanTransition(CombatActorState.Stunned, CombatActorState.Skill));

            // Assert
            Assert.IsFalse(canToAttack);
            Assert.IsFalse(canToDash);
            Assert.IsTrue(canToIdle);
            Assert.IsTrue(canToDead);
            Assert.AreEqual("Stunned", ex.CurrentStateName);
        }

        [Test]
        public void CanPerformAction_WhenDeadOrStunned_BlocksAllCombatActionsAndEnsureThrows()
        {
            // Arrange
            CombatActionType[] actions =
            {
                CombatActionType.Move,
                CombatActionType.Jump,
                CombatActionType.Dash,
                CombatActionType.NormalAttack,
                CombatActionType.DashAttackCombo,
                CombatActionType.Skill1,
                CombatActionType.Skill2,
                CombatActionType.Transform,
                CombatActionType.SpecialCombo,
                CombatActionType.Block,
                CombatActionType.Heal,
                CombatActionType.UseItem
            };

            // Act & Assert
            for (int i = 0; i < actions.Length; i++)
            {
                Assert.IsFalse(CombatRuleGuard.CanPerformAction(CombatActorState.Dead, actions[i]), $"Dead must block {actions[i]}");
                Assert.IsFalse(CombatRuleGuard.CanPerformAction(CombatActorState.Stunned, actions[i]), $"Stunned must block {actions[i]}");
            }

            InvalidCombatStateException deadEx = Assert.Throws<InvalidCombatStateException>(
                () => CombatRuleGuard.EnsureCanPerformAction(CombatActorState.Dead, CombatActionType.NormalAttack));
            InvalidCombatStateException stunnedEx = Assert.Throws<InvalidCombatStateException>(
                () => CombatRuleGuard.EnsureCanPerformAction(CombatActorState.Stunned, CombatActionType.Skill1));

            Assert.AreEqual("Dead", deadEx.CurrentStateName);
            Assert.AreEqual("Stunned", stunnedEx.CurrentStateName);
        }

        [Test]
        public void CanPerformAction_JumpWithStubInputProviderDropModifier_BlocksJumpWhenHeld()
        {
            // Arrange
            StubInputProvider stubInput = new StubInputProvider();
            int jumpTriggeredCount = 0;
            stubInput.OnJumpEvent += () =>
            {
                if (CombatRuleGuard.CanPerformAction(
                    CombatActorState.Idle,
                    CombatActionType.Jump,
                    isDropModifierHeld: stubInput.IsDropModifierHeld))
                {
                    jumpTriggeredCount++;
                }
            };

            // Act: S / Down held -> drop-through modifier suppresses Jump
            stubInput.IsDropModifierHeld = true;
            stubInput.TriggerJump();

            // Act: Released -> Jump allowed
            stubInput.IsDropModifierHeld = false;
            stubInput.TriggerJump();

            // Assert
            Assert.AreEqual(1, jumpTriggeredCount);
        }

        [Test]
        public void StubInputProvider_WhenInputBlocked_SuppressesCombatEventsButAllowsPause()
        {
            // Arrange
            StubInputProvider stubInput = new StubInputProvider { IsInputBlocked = true };
            int attackCount = 0;
            int pauseCount = 0;
            stubInput.OnAttackEvent += () => attackCount++;
            stubInput.OnPauseEvent += () => pauseCount++;

            // Act
            stubInput.TriggerMove(1f);
            stubInput.TriggerAttack();
            stubInput.TriggerPause();

            // Assert
            Assert.AreEqual(0f, stubInput.HorizontalInput);
            Assert.AreEqual(0, attackCount);
            Assert.AreEqual(1, pauseCount);
        }

        [Test]
        public void SkillWithRageGuards_EnforceStateTransformAndRageBoundaries()
        {
            // Arrange
            RageRuleEngine rage = new RageRuleEngine(9f);

            // Act & Assert: 9 Rage -> Skill2 blocked & throws
            Assert.IsFalse(CombatRuleGuard.CanExecuteSkillWithRage(CombatActorState.Idle, CombatActionType.Skill2, rage));
            Assert.Throws<InvalidCombatStateException>(
                () => CombatRuleGuard.EnsureCanExecuteSkillWithRage(CombatActorState.Idle, CombatActionType.Skill2, rage));

            // Act & Assert: 10 Rage -> Skill2 allowed in Idle, blocked in Dash
            rage.AddRage(1);
            Assert.IsTrue(CombatRuleGuard.CanExecuteSkillWithRage(CombatActorState.Idle, CombatActionType.Skill2, rage));
            Assert.IsFalse(CombatRuleGuard.CanExecuteSkillWithRage(CombatActorState.Dash, CombatActionType.Skill2, rage));

            // Act & Assert: 30 Rage -> Transform allowed only if not transformed; SpecialCombo requires Dash + Transformed
            rage.FillMaxRage();
            Assert.IsTrue(CombatRuleGuard.CanExecuteSkillWithRage(CombatActorState.Idle, CombatActionType.Transform, rage, isTransformed: false));
            Assert.IsFalse(CombatRuleGuard.CanExecuteSkillWithRage(CombatActorState.Idle, CombatActionType.Transform, rage, isTransformed: true));
            Assert.IsFalse(CombatRuleGuard.CanExecuteSkillWithRage(CombatActorState.Dash, CombatActionType.SpecialCombo, rage, isTransformed: false));
            Assert.IsTrue(CombatRuleGuard.CanExecuteSkillWithRage(CombatActorState.Dash, CombatActionType.SpecialCombo, rage, isTransformed: true));
            Assert.Throws<InvalidCombatStateException>(
                () => CombatRuleGuard.EnsureCanExecuteSkillWithRage(CombatActorState.Idle, CombatActionType.SpecialCombo, rage, isTransformed: true));
        }

        [Test]
        public void DashAndBlockCooldown_BVA_BelowAndAtExactCooldownBoundary()
        {
            // Arrange & Act
            bool dashBelow = CombatRuleGuard.CanPerformDash(CombatActorState.Idle, elapsedSinceLastDash: 0.49f, dashCooldown: 0.5f);
            bool dashExact = CombatRuleGuard.CanPerformDash(CombatActorState.Idle, elapsedSinceLastDash: 0.50f, dashCooldown: 0.5f);
            bool dashWhileBlocking = CombatRuleGuard.CanPerformDash(CombatActorState.Block, elapsedSinceLastDash: 5f, dashCooldown: 0.5f, isBlocking: true);

            bool blockBelow = CombatRuleGuard.CanPerformBlock(CombatActorState.Idle, elapsedSinceLastBlock: 1.99f, blockCooldown: 2.0f);
            bool blockExact = CombatRuleGuard.CanPerformBlock(CombatActorState.Idle, elapsedSinceLastBlock: 2.0f, blockCooldown: 2.0f);
            bool blockDuringSkill = CombatRuleGuard.CanPerformBlock(CombatActorState.Skill, elapsedSinceLastBlock: 10f, blockCooldown: 0f);

            // Assert
            Assert.IsFalse(dashBelow);
            Assert.IsTrue(dashExact);
            Assert.IsFalse(dashWhileBlocking);

            Assert.IsFalse(blockBelow);
            Assert.IsTrue(blockExact);
            Assert.IsFalse(blockDuringSkill);
        }
    }
}
