using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Patterns.Items;

namespace StickmanBeatEmUp.CorePure.Rules.Economy
{
    /// <summary>
    /// Động cơ quy tắc Cửa hàng thuần C# kiểm định BVA số dư Xu, giá âm, mã vật phẩm và giới hạn kho 99.
    /// </summary>
    public sealed class ShopRuleEngine
    {
        /// <summary>
        /// Kiểm tra chặt chẽ điều kiện giao dịch, ném InvalidPurchaseException khi dữ liệu đầu vào bất hợp lệ
        /// (itemId == None, giá âm, số dư âm, hoặc kho đã chạm trần 99).
        /// </summary>
        public void ValidatePurchaseOrThrow(
            int itemTypeId,
            int price,
            int currentCoins,
            int currentOwned,
            int maxStack = InventoryRuleEngine.MAX_STACK_PER_ITEM)
        {
            if (itemTypeId <= ConsumableItemIds.NONE)
            {
                throw new InvalidPurchaseException("None", "Vật phẩm không hợp lệ!");
            }

            if (price < 0)
            {
                throw new InvalidPurchaseException(itemTypeId.ToString(), $"Giá vật phẩm không được âm ({price}).");
            }

            if (currentCoins < 0)
            {
                throw new InvalidPurchaseException(itemTypeId.ToString(), $"Số dư Xu không hợp lệ ({currentCoins}).");
            }

            if (currentOwned >= maxStack)
            {
                throw new InvalidPurchaseException(itemTypeId.ToString(), $"Kho đã đầy tối đa ({maxStack})!");
            }

            if (currentCoins < price)
            {
                throw new InvalidPurchaseException(itemTypeId.ToString(), "Không đủ Xu!");
            }
        }

        /// <summary>
        /// Đánh giá giao dịch theo chuẩn UI Shop (giữ nguyên thông điệp "Không đủ Xu!" cho E2E tests).
        /// </summary>
        public ShopPurchaseResult EvaluatePurchase(
            int itemTypeId,
            string itemDisplayName,
            int price,
            int currentCoins,
            int currentOwned,
            int maxStack = InventoryRuleEngine.MAX_STACK_PER_ITEM)
        {
            if (itemTypeId <= ConsumableItemIds.NONE)
            {
                return new ShopPurchaseResult(false, currentCoins, currentOwned, "Vật phẩm không hợp lệ!");
            }

            if (price < 0)
            {
                return new ShopPurchaseResult(false, currentCoins, currentOwned, "Giá vật phẩm không hợp lệ!");
            }

            if (currentOwned >= maxStack)
            {
                return new ShopPurchaseResult(false, currentCoins, currentOwned, $"Kho đã đạt tối đa ({maxStack})!");
            }

            if (currentCoins < price)
            {
                return new ShopPurchaseResult(false, currentCoins, currentOwned, "Không đủ Xu!");
            }

            int remainingCoins = currentCoins - price;
            int newOwned = currentOwned + 1;
            string label = string.IsNullOrWhiteSpace(itemDisplayName) ? $"Item#{itemTypeId}" : itemDisplayName;
            return new ShopPurchaseResult(
                true,
                remainingCoins,
                newOwned,
                $"Đã mua {label}! (Sở hữu: {newOwned})");
        }
    }
}
