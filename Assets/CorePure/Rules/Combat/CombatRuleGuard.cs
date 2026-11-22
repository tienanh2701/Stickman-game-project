using System;
using StickmanBeatEmUp.CorePure.Entities.Combat;
using StickmanBeatEmUp.CorePure.Exceptions;

namespace StickmanBeatEmUp.CorePure.Rules.Combat
{
    /// <summary>
    /// Bộ kiểm tra hợp lệ chuyển trạng thái và hành động chiến đấu thuần C# (FSM Guard).
    /// Ngăn chặn mọi hành động bất hợp lệ khi Dead / Stunned / Hurt và ném InvalidCombatStateException khi cần ràng buộc chặt.
    /// </summary>
    public static class CombatRuleGuard
    {
        public static bool IsDeadOrStunned(CombatActorState state)
        {
            return state == CombatActorState.Dead || state == CombatActorState.Stunned;
        }

        public static bool IsControlLockedState(CombatActorState state)
        {
            return state == CombatActorState.Dead ||
                   state == CombatActorState.Stunned ||
                   state == CombatActorState.Hurt;
        }

        /// <summary>
        /// Kiểm tra xem chuyển đổi trạng thái FSM có hợp lệ hay không.
        /// - Dead là trạng thái kết thúc (không chuyển sang trạng thái khác qua ChangeState thông thường).
        /// - Stunned chỉ được phép chuyển về Idle (hết choáng), Hurt hoặc Dead.
        /// - Tự chuyển sang cùng trạng thái trả về false (no-op).
        /// </summary>
        public static bool CanTransition(CombatActorState currentState, CombatActorState targetState)
        {
            if (currentState == CombatActorState.Dead)
            {
                return false;
            }

            if (currentState == targetState)
            {
                return false;
            }

            if (currentState == CombatActorState.Stunned)
            {
                return targetState == CombatActorState.Idle ||
                       targetState == CombatActorState.Hurt ||
                       targetState == CombatActorState.Dead;
            }

            return true;
        }

        /// <summary>
        /// Ràng buộc chặt chuyển trạng thái FSM; ném InvalidCombatStateException nếu chuyển trạng thái bị cấm.
        /// </summary>
        public static void EnsureCanTransition(CombatActorState currentState, CombatActorState targetState)
        {
            if (currentState == CombatActorState.Dead)
            {
                throw new InvalidCombatStateException(
                    currentState.ToString(),
                    $"Cannot transition from Dead to '{targetState}'.");
            }

            if (currentState == CombatActorState.Stunned &&
                targetState != CombatActorState.Idle &&
                targetState != CombatActorState.Hurt &&
                targetState != CombatActorState.Dead)
            {
                throw new InvalidCombatStateException(
                    currentState.ToString(),
                    $"Cannot transition from Stunned to active combat state '{targetState}'.");
            }
        }

        /// <summary>
        /// Kiểm tra xem hành động chiến đấu có được phép thực hiện ở trạng thái hiện tại hay không.
        /// </summary>
        public static bool CanPerformAction(
            CombatActorState currentState,
            CombatActionType action,
            bool isBlocking = false,
            bool isTransformed = false,
            bool isDropModifierHeld = false)
        {
            if (IsDeadOrStunned(currentState))
            {
                return false;
            }

            switch (action)
            {
                case CombatActionType.Move:
                    return !isBlocking && (currentState == CombatActorState.Idle || currentState == CombatActorState.Run);

                case CombatActionType.Jump:
                    return currentState != CombatActorState.Hurt &&
                           currentState != CombatActorState.Transform &&
                           currentState != CombatActorState.Block &&
                           !isBlocking &&
                           !isDropModifierHeld;

                case CombatActionType.Dash:
                    return currentState != CombatActorState.Hurt &&
                           currentState != CombatActorState.Transform &&
                           currentState != CombatActorState.Dash &&
                           currentState != CombatActorState.Block &&
                           !isBlocking;

                case CombatActionType.NormalAttack:
                case CombatActionType.Skill1:
                case CombatActionType.Skill2:
                    return !isBlocking && (currentState == CombatActorState.Idle || currentState == CombatActorState.Run);

                case CombatActionType.DashAttackCombo:
                case CombatActionType.SpecialCombo:
                    return !isBlocking && currentState == CombatActorState.Dash;

                case CombatActionType.Transform:
                    return currentState != CombatActorState.Hurt &&
                           currentState != CombatActorState.Block &&
                           !isBlocking &&
                           currentState != CombatActorState.Transform &&
                           !isTransformed;

                case CombatActionType.Block:
                    return currentState != CombatActorState.Hurt &&
                           currentState != CombatActorState.Dash &&
                           currentState != CombatActorState.Transform &&
                           currentState != CombatActorState.Skill &&
                           currentState != CombatActorState.Block;

                case CombatActionType.Heal:
                case CombatActionType.UseItem:
                    return true;

                default:
                    return false;
            }
        }

        public static bool CanPerformDash(
            CombatActorState currentState,
            float elapsedSinceLastDash,
            float dashCooldown,
            bool isBlocking = false)
        {
            if (!CanPerformAction(currentState, CombatActionType.Dash, isBlocking))
            {
                return false;
            }

            float safeCooldown = float.IsNaN(dashCooldown) || dashCooldown < 0f ? 0f : dashCooldown;
            return !float.IsNaN(elapsedSinceLastDash) && elapsedSinceLastDash >= safeCooldown;
        }

        public static bool CanPerformBlock(
            CombatActorState currentState,
            float elapsedSinceLastBlock,
            float blockCooldown)
        {
            if (!CanPerformAction(currentState, CombatActionType.Block))
            {
                return false;
            }

            float safeCooldown = float.IsNaN(blockCooldown) || blockCooldown < 0f ? 0f : blockCooldown;
            return !float.IsNaN(elapsedSinceLastBlock) && elapsedSinceLastBlock >= safeCooldown;
        }

        public static bool CanExecuteSkillWithRage(
            CombatActorState currentState,
            CombatActionType action,
            RageRuleEngine rageEngine,
            bool isTransformed = false,
            bool isBlocking = false)
        {
            if (rageEngine == null)
            {
                return false;
            }

            if (!CanPerformAction(currentState, action, isBlocking, isTransformed))
            {
                return false;
            }

            switch (action)
            {
                case CombatActionType.Skill2:
                    return rageEngine.CanUseSkillI();

                case CombatActionType.Transform:
                    return rageEngine.CanTransform();

                case CombatActionType.SpecialCombo:
                    return rageEngine.CanUseSpecialCombo(isTransformed);

                default:
                    return true;
            }
        }

        public static void EnsureCanPerformAction(
            CombatActorState currentState,
            CombatActionType action,
            bool isBlocking = false,
            bool isTransformed = false,
            bool isDropModifierHeld = false)
        {
            if (!CanPerformAction(currentState, action, isBlocking, isTransformed, isDropModifierHeld))
            {
                throw new InvalidCombatStateException(
                    currentState.ToString(),
                    $"Combat action '{action}' is forbidden while in state '{currentState}'.");
            }
        }

        public static void EnsureCanExecuteSkillWithRage(
            CombatActorState currentState,
            CombatActionType action,
            RageRuleEngine rageEngine,
            bool isTransformed = false,
            bool isBlocking = false)
        {
            if (rageEngine == null)
            {
                throw new ArgumentNullException(nameof(rageEngine));
            }

            EnsureCanPerformAction(currentState, action, isBlocking, isTransformed);

            switch (action)
            {
                case CombatActionType.Skill2:
                    if (!rageEngine.CanUseSkillI())
                    {
                        throw new InvalidCombatStateException(
                            currentState.ToString(),
                            $"Skill2 requires {RageRuleEngine.SKILL_I_COST} rage (current: {rageEngine.CurrentRage}/{RageRuleEngine.MAX_RAGE}).");
                    }
                    break;

                case CombatActionType.Transform:
                    if (!rageEngine.CanTransform())
                    {
                        throw new InvalidCombatStateException(
                            currentState.ToString(),
                            $"Transform requires {RageRuleEngine.TRANSFORM_COST} rage (current: {rageEngine.CurrentRage}/{RageRuleEngine.MAX_RAGE}).");
                    }
                    break;

                case CombatActionType.SpecialCombo:
                    if (!rageEngine.CanUseSpecialCombo(isTransformed))
                    {
                        throw new InvalidCombatStateException(
                            currentState.ToString(),
                            $"SpecialCombo requires Transformed={true} and {RageRuleEngine.SPECIAL_COMBO_COST} rage (Transformed={isTransformed}, current: {rageEngine.CurrentRage}/{RageRuleEngine.MAX_RAGE}).");
                    }
                    break;
            }
        }
    }
}
