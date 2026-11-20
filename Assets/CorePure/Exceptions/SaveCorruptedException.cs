using System;

namespace StickmanBeatEmUp.CorePure.Exceptions
{
    /// <summary>
    /// Ném ra khi dữ liệu lưu trữ (tài khoản, trạng thái giữa trận, bảng điểm cao) bị hỏng hoặc thiếu trường bắt buộc.
    /// </summary>
    public class SaveCorruptedException : StickmanGameException
    {
        public string StorageKey { get; }

        public SaveCorruptedException(string message)
            : this(string.Empty, message, null)
        {
        }

        public SaveCorruptedException(string storageKey, string message, Exception innerException = null)
            : base("SAVE_CORRUPTED", message, innerException)
        {
            StorageKey = storageKey ?? string.Empty;
        }
    }
}
