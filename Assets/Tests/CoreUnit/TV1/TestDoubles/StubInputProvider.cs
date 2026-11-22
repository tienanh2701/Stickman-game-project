using System;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.Tests.CoreUnit.TV1.TestDoubles
{
    /// <summary>
    /// Stub nguồn điều khiển thuần C# thay thế InputHandler/Keyboard trong Pure Logic Unit Tests.
    /// </summary>
    public sealed class StubInputProvider : IInputProvider
    {
        public float HorizontalInput { get; set; }
        public bool IsInputBlocked { get; set; }
        public bool IsDropModifierHeld { get; set; }

        public event Action<float> OnMoveEvent;
        public event Action OnJumpEvent;
        public event Action<float> OnDashEvent;
        public event Action OnAttackEvent;
        public event Action OnSkill1Event;
        public event Action OnSkill2Event;
        public event Action OnTransformEvent;
        public event Action OnBlockEvent;
        public event Action OnItemQEvent;
        public event Action OnItemEEvent;
        public event Action OnPauseEvent;

        public void TriggerMove(float horizontal)
        {
            if (IsInputBlocked)
            {
                HorizontalInput = 0f;
                return;
            }

            HorizontalInput = horizontal;
            OnMoveEvent?.Invoke(horizontal);
        }

        public void TriggerJump()
        {
            if (IsInputBlocked) return;
            OnJumpEvent?.Invoke();
        }

        public void TriggerDash(float direction)
        {
            if (IsInputBlocked) return;
            OnDashEvent?.Invoke(direction);
        }

        public void TriggerAttack()
        {
            if (IsInputBlocked) return;
            OnAttackEvent?.Invoke();
        }

        public void TriggerSkill1()
        {
            if (IsInputBlocked) return;
            OnSkill1Event?.Invoke();
        }

        public void TriggerSkill2()
        {
            if (IsInputBlocked) return;
            OnSkill2Event?.Invoke();
        }

        public void TriggerTransform()
        {
            if (IsInputBlocked) return;
            OnTransformEvent?.Invoke();
        }

        public void TriggerBlock()
        {
            if (IsInputBlocked) return;
            OnBlockEvent?.Invoke();
        }

        public void TriggerItemQ()
        {
            if (IsInputBlocked) return;
            OnItemQEvent?.Invoke();
        }

        public void TriggerItemE()
        {
            if (IsInputBlocked) return;
            OnItemEEvent?.Invoke();
        }

        public void TriggerPause()
        {
            OnPauseEvent?.Invoke();
        }
    }
}
