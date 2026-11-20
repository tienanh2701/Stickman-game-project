using System;

namespace StickmanBeatEmUp.CorePure.Exceptions
{
    /// <summary>
    /// Ném ra khi tệp cấu hình ngoài (JSON) bị thiếu, sai định dạng hoặc có thông số vượt miền hợp lệ.
    /// </summary>
    public class InvalidGameConfigException : StickmanGameException
    {
        public string ConfigKey { get; }

        public InvalidGameConfigException(string message)
            : this(string.Empty, message, null)
        {
        }

        public InvalidGameConfigException(string configKey, string message, Exception innerException = null)
            : base("INVALID_CONFIG", message, innerException)
        {
            ConfigKey = configKey ?? string.Empty;
        }
    }
}
