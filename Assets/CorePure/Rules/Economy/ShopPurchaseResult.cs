namespace StickmanBeatEmUp.CorePure.Rules.Economy
{
    /// <summary>
    /// Kết quả thuần C# của một giao dịch mua vật phẩm tại Cửa hàng.
    /// </summary>
    public readonly struct ShopPurchaseResult
    {
        public bool Succeeded { get; }
        public int RemainingCoins { get; }
        public int NewOwnedCount { get; }
        public string FeedbackMessage { get; }

        public ShopPurchaseResult(bool succeeded, int remainingCoins, int newOwnedCount, string feedbackMessage)
        {
            Succeeded = succeeded;
            RemainingCoins = remainingCoins;
            NewOwnedCount = newOwnedCount;
            FeedbackMessage = feedbackMessage ?? string.Empty;
        }
    }
}
