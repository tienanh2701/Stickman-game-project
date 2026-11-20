using System;

namespace StickmanBeatEmUp.CorePure.Exceptions
{
    /// <summary>
    /// Lớp ngoại lệ miền gốc cho toàn bộ hệ thống MULTI-DIMENSIONS STICKMAN.
    /// </summary>
    public class StickmanGameException : Exception
    {
        public string ErrorCode { get; }

        public StickmanGameException(string message)
            : this("GAME_ERROR", message)
        {
        }

        public StickmanGameException(string errorCode, string message)
            : base(message)
        {
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "GAME_ERROR" : errorCode;
        }

        public StickmanGameException(string errorCode, string message, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "GAME_ERROR" : errorCode;
        }
    }
}
