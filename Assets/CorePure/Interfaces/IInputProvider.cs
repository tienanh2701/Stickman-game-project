using System;

namespace StickmanBeatEmUp.CorePure.Interfaces
{
    /// <summary>
    /// Hợp đồng trừu tượng nguồn điều khiển (DIP), giúp tầng Core/Player không phụ thuộc trực tiếp InputSystem hay bàn phím.
    /// </summary>
    public interface IInputProvider
    {
        float HorizontalInput { get; }
        bool IsInputBlocked { get; set; }
        bool IsDropModifierHeld { get; }

        event Action<float> OnMoveEvent;
        event Action OnJumpEvent;
        event Action<float> OnDashEvent;
        event Action OnAttackEvent;
        event Action OnSkill1Event;
        event Action OnSkill2Event;
        event Action OnTransformEvent;
        event Action OnBlockEvent;
        event Action OnItemQEvent;
        event Action OnItemEEvent;
        event Action OnPauseEvent;
    }
}
