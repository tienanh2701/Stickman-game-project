using System;

namespace StickmanBeatEmUp.CorePure.Exceptions
{
    /// <summary>
    /// Ném ra khi giao dịch cửa hàng hoặc thao tác trang bị/dùng vật phẩm ở ô Q/E không hợp lệ.
    /// </summary>
    public class InvalidPurchaseException : StickmanGameException
    {
        public string ItemId { get; }

        public InvalidPurchaseException(string message)
            : this(string.Empty, message, null)
        {
        }

        public InvalidPurchaseException(string itemId, string message, Exception innerException = null)
            : base("INVALID_PURCHASE", message, innerException)
        {
            ItemId = itemId ?? string.Empty;
        }
    }
}
