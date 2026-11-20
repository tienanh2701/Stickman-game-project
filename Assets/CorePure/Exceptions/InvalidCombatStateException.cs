using System;

namespace StickmanBeatEmUp.CorePure.Exceptions
{
    /// <summary>
    /// Ném ra khi thực hiện hành động chiến đấu không hợp lệ theo State Machine (ví dụ đánh khi Dead hoặc thiếu Nộ).
    /// </summary>
    public class InvalidCombatStateException : StickmanGameException
    {
        public string CurrentStateName { get; }

        public InvalidCombatStateException(string message)
            : this(string.Empty, message, null)
        {
        }

        public InvalidCombatStateException(string currentStateName, string message, Exception innerException = null)
            : base("INVALID_COMBAT_STATE", message, innerException)
        {
            CurrentStateName = currentStateName ?? string.Empty;
        }
    }
}
