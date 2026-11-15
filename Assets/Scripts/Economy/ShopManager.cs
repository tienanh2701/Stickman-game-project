using System;
using UnityEngine;
using UnityEngine.UI;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Items;

namespace StickmanBeatEmUp.Economy
{
    /// <summary>
    /// Dữ liệu UI cho một dòng/thẻ vật phẩm trong Shop Panel.
    /// </summary>
    [Serializable]
    public class ShopItemRow
    {
        public ConsumableItemType itemType;
        public Button buyButton;
        public Button equipSlotQButton;
        public Button equipSlotEButton;
        public Image iconImage;
        public Text nameText;
        public Text priceText;
        public Text ownedCountText;
        public Text slotQButtonText;
        public Text slotEButtonText;
        public GameObject equippedQHighlight;
        public GameObject equippedEHighlight;
    }

    /// <summary>
    /// Quản lý logic mua vật phẩm trong Cửa hàng, trừ Xu qua CurrencyManager,
    /// cộng kho dùng chung qua InventoryManager và cập nhật trang bị 2 ô Q / E.
    /// </summary>
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }

        [Header("Optional Shop UI References")]
        [SerializeField] private Text coinsBalanceText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Text slotQSummaryText;
        [SerializeField] private Text slotESummaryText;
        [SerializeField] private Image slotQSummaryIcon;
        [SerializeField] private Image slotESummaryIcon;
        [SerializeField] private ShopItemRow[] shopRows;
        [SerializeField] private float feedbackDisplayDuration = 2.5f;

        private float feedbackTimer;
        private bool rowsBound;

        /// <summary>
        /// Thông báo kết quả giao dịch mua vật phẩm (itemType, success, feedbackMessage).
        /// </summary>
        public event Action<ConsumableItemType, bool, string> OnPurchaseResult;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
                CurrencyManager.Instance.OnCoinsChanged += HandleCoinsChanged;
            }

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged -= HandleInventoryChanged;
                InventoryManager.Instance.OnInventoryChanged += HandleInventoryChanged;
            }

            BindRowButtons();
            RefreshShopUI();
        }

        private void OnDisable()
        {
            if (CurrencyManager.HasInstance)
            {
                CurrencyManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
            }

            if (InventoryManager.HasInstance)
            {
                InventoryManager.Instance.OnInventoryChanged -= HandleInventoryChanged;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (feedbackTimer > 0f)
            {
                feedbackTimer -= Time.unscaledDeltaTime;
                if (feedbackTimer <= 0f && feedbackText != null)
                {
                    feedbackText.text = string.Empty;
                }
            }
        }

        /// <summary>
        /// Cho phép MainMenuSceneBuilder / UIManager gán tham chiếu UI Shop từ code Editor.
        /// </summary>
        public void ConfigureBuilderReferences(
            Text balanceTxt,
            Text feedbackTxt,
            Text slotQTxt,
            Text slotETxt,
            Image slotQIcon,
            Image slotEIcon,
            ShopItemRow[] rows)
        {
            coinsBalanceText = balanceTxt;
            feedbackText = feedbackTxt;
            slotQSummaryText = slotQTxt;
            slotESummaryText = slotETxt;
            slotQSummaryIcon = slotQIcon;
            slotESummaryIcon = slotEIcon;
            shopRows = rows;
            rowsBound = false;

            BindRowButtons();
            RefreshShopUI();
        }

        /// <summary>
        /// Lấy toàn bộ danh sách ConsumableItemSO từ InventoryManager.
        /// </summary>
        public ConsumableItemSO[] GetCatalogItems()
        {
            return InventoryManager.Instance != null
                ? InventoryManager.Instance.GetAllItems()
                : Array.Empty<ConsumableItemSO>();
        }

        /// <summary>
        /// Thử mua 1 vật phẩm bằng Xu. Trả về true nếu thành công kèm thông báo tiếng Việt.
        /// </summary>
        public bool TryPurchaseItem(ConsumableItemType type, out string feedbackMessage)
        {
            if (type == ConsumableItemType.None)
            {
                feedbackMessage = "Vật phẩm không hợp lệ!";
                ShowFeedback(feedbackMessage, false);
                OnPurchaseResult?.Invoke(type, false, feedbackMessage);
                return false;
            }

            InventoryManager inventory = InventoryManager.Instance;
            CurrencyManager currency = CurrencyManager.Instance;
            ConsumableItemSO itemData = inventory != null ? inventory.GetItemData(type) : null;

            if (inventory == null || itemData == null)
            {
                feedbackMessage = "Không tìm thấy thông tin vật phẩm!";
                ShowFeedback(feedbackMessage, false);
                OnPurchaseResult?.Invoke(type, false, feedbackMessage);
                return false;
            }

            int price = Mathf.Max(0, itemData.price);
            if (currency == null || !currency.TrySpendCoins(price))
            {
                feedbackMessage = "Không đủ Xu!";
                ShowFeedback(feedbackMessage, false);
                OnPurchaseResult?.Invoke(type, false, feedbackMessage);
                return false;
            }

            inventory.AddOwnedItem(type, 1);
            AutoEquipIfSlotAvailable(inventory, type);
            int newCount = inventory.GetOwnedCount(type);
            string displayName = string.IsNullOrEmpty(itemData.itemName) ? type.ToString() : itemData.itemName;
            feedbackMessage = $"Đã mua {displayName}! (Sở hữu: {newCount})";

            ShowFeedback(feedbackMessage, true);
            RefreshShopUI();
            OnPurchaseResult?.Invoke(type, true, feedbackMessage);
            return true;
        }

        public void PurchaseItem(ConsumableItemType type)
        {
            TryPurchaseItem(type, out _);
        }

        public void PurchaseSenzu()
        {
            PurchaseItem(ConsumableItemType.SenzuBean);
        }

        public void PurchaseSpinalFluid()
        {
            PurchaseItem(ConsumableItemType.SpinalFluid);
        }

        public void PurchaseUltraWater()
        {
            PurchaseItem(ConsumableItemType.UltraDivineWater);
        }

        /// <summary>
        /// Trang bị vật phẩm vào ô Q (slotIndex = 0) hoặc E (slotIndex = 1).
        /// </summary>
        public void EquipToSlot(int slotIndex, ConsumableItemType type)
        {
            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null) return;

            inventory.SetEquippedItem(slotIndex, type);
            ConsumableItemSO data = inventory.GetItemData(type);
            string itemLabel = data != null && !string.IsNullOrEmpty(data.itemName) ? data.itemName : type.ToString();
            string slotLabel = slotIndex == InventoryManager.SLOT_Q ? "Q" : "E";
            ShowFeedback($"Đã trang bị {itemLabel} vào ô {slotLabel}!", true);
            RefreshShopUI();
        }

        public void EquipSlotQ(ConsumableItemType type)
        {
            EquipToSlot(InventoryManager.SLOT_Q, type);
        }

        public void EquipSlotE(ConsumableItemType type)
        {
            EquipToSlot(InventoryManager.SLOT_E, type);
        }

        /// <summary>
        /// Làm mới toàn bộ giao diện Shop dựa trên CurrencyManager và InventoryManager.
        /// </summary>
        public void RefreshShopUI()
        {
            CurrencyManager currency = CurrencyManager.Instance;
            InventoryManager inventory = InventoryManager.Instance;

            int coins = currency != null ? currency.CurrentCoins : 0;
            if (coinsBalanceText != null)
            {
                coinsBalanceText.text = $"{coins} Xu";
            }

            if (inventory == null) return;

            ConsumableItemType equippedQ = inventory.GetEquippedItem(InventoryManager.SLOT_Q);
            ConsumableItemType equippedE = inventory.GetEquippedItem(InventoryManager.SLOT_E);

            UpdateSlotSummary(slotQSummaryText, slotQSummaryIcon, "Q", equippedQ, inventory);
            UpdateSlotSummary(slotESummaryText, slotESummaryIcon, "E", equippedE, inventory);

            if (shopRows == null) return;

            for (int i = 0; i < shopRows.Length; i++)
            {
                ShopItemRow row = shopRows[i];
                if (row == null || row.itemType == ConsumableItemType.None) continue;

                ConsumableItemSO data = inventory.GetItemData(row.itemType);
                int owned = inventory.GetOwnedCount(row.itemType);
                int price = data != null ? data.price : 0;
                bool isEquippedQ = equippedQ == row.itemType;
                bool isEquippedE = equippedE == row.itemType;

                if (row.iconImage != null && data != null && data.icon != null)
                {
                    row.iconImage.sprite = data.icon;
                    row.iconImage.preserveAspect = true;
                }

                if (row.nameText != null && data != null)
                {
                    row.nameText.text = data.itemName;
                }

                if (row.priceText != null)
                {
                    row.priceText.text = $"{price} Xu";
                    row.priceText.color = coins >= price
                        ? new Color(1f, 0.86f, 0.28f, 1f)
                        : new Color(0.95f, 0.35f, 0.35f, 1f);
                }

                if (row.ownedCountText != null)
                {
                    row.ownedCountText.text = $"Sở hữu: {owned}";
                }

                if (row.buyButton != null)
                {
                    row.buyButton.interactable = coins >= price;
                }

                if (row.slotQButtonText != null)
                {
                    row.slotQButtonText.text = isEquippedQ ? "Đang gắn [Q]" : "Gắn vào [Q]";
                }

                if (row.slotEButtonText != null)
                {
                    row.slotEButtonText.text = isEquippedE ? "Đang gắn [E]" : "Gắn vào [E]";
                }

                if (row.equippedQHighlight != null)
                {
                    row.equippedQHighlight.SetActive(isEquippedQ);
                }

                if (row.equippedEHighlight != null)
                {
                    row.equippedEHighlight.SetActive(isEquippedE);
                }
            }
        }

        private static void AutoEquipIfSlotAvailable(InventoryManager inventory, ConsumableItemType type)
        {
            if (inventory == null || type == ConsumableItemType.None) return;

            ConsumableItemType equippedQ = inventory.GetEquippedItem(InventoryManager.SLOT_Q);
            ConsumableItemType equippedE = inventory.GetEquippedItem(InventoryManager.SLOT_E);
            if (equippedQ == type || equippedE == type) return;

            if (equippedQ == ConsumableItemType.None)
            {
                inventory.SetEquippedItem(InventoryManager.SLOT_Q, type);
            }
            else if (equippedE == ConsumableItemType.None)
            {
                inventory.SetEquippedItem(InventoryManager.SLOT_E, type);
            }
            else if (inventory.GetOwnedCount(equippedQ) <= 0)
            {
                inventory.SetEquippedItem(InventoryManager.SLOT_Q, type);
            }
            else if (inventory.GetOwnedCount(equippedE) <= 0)
            {
                inventory.SetEquippedItem(InventoryManager.SLOT_E, type);
            }
        }

        private void BindRowButtons()
        {
            if (rowsBound || shopRows == null) return;

            for (int i = 0; i < shopRows.Length; i++)
            {
                ShopItemRow row = shopRows[i];
                if (row == null || row.itemType == ConsumableItemType.None) continue;

                ConsumableItemType capturedType = row.itemType;

                if (row.buyButton != null)
                {
                    row.buyButton.onClick.RemoveAllListeners();
                    row.buyButton.onClick.AddListener(() => PurchaseItem(capturedType));
                }

                if (row.equipSlotQButton != null)
                {
                    row.equipSlotQButton.onClick.RemoveAllListeners();
                    row.equipSlotQButton.onClick.AddListener(() => EquipSlotQ(capturedType));
                }

                if (row.equipSlotEButton != null)
                {
                    row.equipSlotEButton.onClick.RemoveAllListeners();
                    row.equipSlotEButton.onClick.AddListener(() => EquipSlotE(capturedType));
                }
            }

            rowsBound = true;
        }

        private static void UpdateSlotSummary(
            Text summaryText,
            Image summaryIcon,
            string keyLabel,
            ConsumableItemType itemType,
            InventoryManager inventory)
        {
            ConsumableItemSO data = inventory.GetItemData(itemType);
            int count = inventory.GetOwnedCount(itemType);

            if (summaryText != null)
            {
                string itemName = data != null ? data.itemName : "Trống";
                summaryText.text = $"[{keyLabel}] {itemName} (x{count})";
            }

            if (summaryIcon != null)
            {
                if (data != null && data.icon != null)
                {
                    summaryIcon.sprite = data.icon;
                    summaryIcon.enabled = true;
                    summaryIcon.preserveAspect = true;
                }
                else
                {
                    summaryIcon.enabled = false;
                }
            }
        }

        private void ShowFeedback(string message, bool isSuccess)
        {
            if (feedbackText == null) return;

            feedbackText.text = message;
            feedbackText.color = isSuccess
                ? new Color(0.42f, 0.95f, 0.52f, 1f)
                : new Color(1f, 0.36f, 0.36f, 1f);
            feedbackTimer = feedbackDisplayDuration;
        }

        private void HandleCoinsChanged(int _)
        {
            RefreshShopUI();
        }

        private void HandleInventoryChanged()
        {
            RefreshShopUI();
        }
    }
}
