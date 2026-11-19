using System;
using UnityEngine;
using UnityEngine.UI;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Items;
using StickmanBeatEmUp.UI;

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
        public Image rowBackground;
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

        [Header("Glassmorphism Styling (Optional)")]
        [SerializeField] private Sprite rowGlassStripSprite;
        [SerializeField] private Sprite greenBuyButtonSprite;
        [SerializeField] private VerticalLayoutGroup rowsLayoutGroup;

        [Header("SlicedUI Styling (05_Shop)")]
        [SerializeField] private Sprite shopMainPanelSprite;
        [SerializeField] private Sprite summaryBarSprite;
        [SerializeField] private Sprite iconSlotBlueGlowSprite;

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

        private void Start()
        {
            ApplyShopGlassStyling();
            RefreshShopUI();
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
            ApplyShopGlassStyling();
            RefreshShopUI();
        }

        private void OnDisable()
        {
            rowsBound = false;
            CurrencyManager cm = CurrencyManager.HasInstance ? CurrencyManager.Instance : null;
            if (cm != null)
            {
                cm.OnCoinsChanged -= HandleCoinsChanged;
            }

            InventoryManager inv = InventoryManager.HasInstance ? InventoryManager.Instance : null;
            if (inv != null)
            {
                inv.OnInventoryChanged -= HandleInventoryChanged;
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
            ApplyShopGlassStyling();
            RefreshShopUI();
        }

        public void ConfigureGlassSprites(Sprite rowStrip, Sprite buyBtnSprite, VerticalLayoutGroup layoutGroup = null)
        {
            if (rowStrip != null) rowGlassStripSprite = rowStrip;
            if (buyBtnSprite != null) greenBuyButtonSprite = buyBtnSprite;
            if (layoutGroup != null) rowsLayoutGroup = layoutGroup;
            ApplyShopGlassStyling();
        }

        public void ConfigureThemeSprites(
            Sprite mainFrame,
            Sprite summaryBar,
            Sprite rowStrip,
            Sprite iconSlot,
            Sprite buyBtn,
            VerticalLayoutGroup layoutGroup = null)
        {
            if (mainFrame != null) shopMainPanelSprite = mainFrame;
            if (summaryBar != null) summaryBarSprite = summaryBar;
            if (rowStrip != null) rowGlassStripSprite = rowStrip;
            if (iconSlot != null) iconSlotBlueGlowSprite = iconSlot;
            if (buyBtn != null) greenBuyButtonSprite = buyBtn;
            if (layoutGroup != null) rowsLayoutGroup = layoutGroup;
            ApplyShopGlassStyling();
        }

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
            if (slotIndex != InventoryManager.SLOT_Q && slotIndex != InventoryManager.SLOT_E) return;

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null) return;

            inventory.SetEquippedItem(slotIndex, type);
            ConsumableItemSO data = inventory.GetItemData(type);
            string slotLabel = slotIndex == InventoryManager.SLOT_Q ? "Q" : "E";
            if (type == ConsumableItemType.None)
            {
                ShowFeedback($"Đã bỏ trang bị ô {slotLabel}!", true);
            }
            else
            {
                string itemLabel = data != null && !string.IsNullOrEmpty(data.itemName) ? data.itemName : type.ToString();
                ShowFeedback($"Đã trang bị {itemLabel} vào ô {slotLabel}!", true);
            }
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

                if (row.iconImage != null)
                {
                    Sprite resolvedIcon = (data != null && data.icon != null)
                        ? data.icon
                        : GetFallbackItemIcon(row.itemType);
                    if (resolvedIcon != null)
                    {
                        row.iconImage.sprite = resolvedIcon;
                    }
                    row.iconImage.preserveAspect = true;
                    row.iconImage.color = Color.white;
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
                    row.buyButton.interactable = true;
                    if (row.buyButton.TryGetComponent(out Image buyBtnImg))
                    {
                        buyBtnImg.color = (coins >= price)
                            ? Color.white
                            : new Color(0.72f, 0.76f, 0.82f, 0.88f);
                    }
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
                Sprite resolvedIcon = (data != null && data.icon != null)
                    ? data.icon
                    : GetFallbackItemIcon(itemType);
                if (resolvedIcon != null)
                {
                    summaryIcon.sprite = resolvedIcon;
                    summaryIcon.enabled = true;
                    summaryIcon.preserveAspect = true;
                    summaryIcon.color = Color.white;
                }
                else
                {
                    summaryIcon.enabled = false;
                }
            }
        }

        private static Sprite ResolveShopSprite(Sprite current, string relPath, string expectedName)
        {
            if (current != null && string.Equals(current.name, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }
            Sprite loaded = GlassHudTheme.LoadSprite(relPath);
            return loaded != null ? loaded : current;
        }

        private void EnsureShopThemeSprites()
        {
            shopMainPanelSprite = ResolveShopSprite(shopMainPanelSprite, "05_Shop/Frame_Shop_MainPanel_Clean", "Frame_Shop_MainPanel_Clean");
            summaryBarSprite = ResolveShopSprite(summaryBarSprite, "05_Shop/Bar_Shop_InventorySummary_Clean", "Bar_Shop_InventorySummary_Clean");
            rowGlassStripSprite = ResolveShopSprite(rowGlassStripSprite, "05_Shop/Row_Shop_ItemStrip_Clean", "Row_Shop_ItemStrip_Clean");
            iconSlotBlueGlowSprite = ResolveShopSprite(iconSlotBlueGlowSprite, "05_Shop/Slot_Shop_ItemIcon_BlueGlow", "Slot_Shop_ItemIcon_BlueGlow");
            greenBuyButtonSprite = ResolveShopSprite(greenBuyButtonSprite, "05_Shop/Btn_Shop_Buy_Clean", "Btn_Shop_Buy_Clean");
        }

        public void ApplyShopGlassStyling()
        {
            EnsureShopThemeSprites();

            if (TryGetComponent(out Image panelImg))
            {
                panelImg.color = Color.clear;
                panelImg.raycastTarget = false;
            }

            Transform shopTitleTf = transform.Find("ShopTitle");
            if (shopTitleTf != null)
            {
                if (shopTitleTf.TryGetComponent(out RectTransform titleRt))
                {
                    titleRt.anchoredPosition = new Vector2(0f, 456f);
                    titleRt.sizeDelta = new Vector2(1200f, 64f);
                }
                if (shopTitleTf.TryGetComponent(out Text shopTitleTxt))
                {
                    StyleLegacyTextCrisp(shopTitleTxt, shopTitleTxt.color);
                }
            }

            Transform summaryBarTf = transform.Find("ShopSummaryBar");
            if (summaryBarTf != null)
            {
                if (summaryBarTf.TryGetComponent(out RectTransform summaryRt))
                {
                    summaryRt.anchorMin = new Vector2(0.5f, 0.5f);
                    summaryRt.anchorMax = new Vector2(0.5f, 0.5f);
                    summaryRt.pivot = new Vector2(0.5f, 0.5f);
                    summaryRt.anchoredPosition = new Vector2(0f, 368f);
                    summaryRt.sizeDelta = new Vector2(1460f, 76f);
                }
                if (summaryBarTf.TryGetComponent(out Image summaryBg))
                {
                    if (summaryBarSprite != null)
                    {
                        GlassHudTheme.ApplySlicedSprite(summaryBg, summaryBarSprite, 1460f, 76f, Color.white);
                    }
                    else
                    {
                        summaryBg.color = new Color(0.06f, 0.09f, 0.16f, 0.56f);
                    }
                    if (summaryBarTf.TryGetComponent(out Outline summaryOutline))
                    {
                        summaryOutline.enabled = false;
                    }
                }
            }

            EnsureRowsLayoutContainer();
            EnsureShopMainFrame();

            if (feedbackText != null)
            {
                RectTransform fbRt = feedbackText.rectTransform;
                fbRt.anchoredPosition = new Vector2(0f, -424f);
                fbRt.sizeDelta = new Vector2(1200f, 48f);
            }

            StyleLegacyTextCrisp(coinsBalanceText, coinsBalanceText != null ? coinsBalanceText.color : Color.white);
            StyleLegacyTextCrisp(slotQSummaryText, slotQSummaryText != null ? slotQSummaryText.color : Color.white);
            StyleLegacyTextCrisp(slotESummaryText, slotESummaryText != null ? slotESummaryText.color : Color.white);
            StyleLegacyTextCrisp(feedbackText, feedbackText != null ? feedbackText.color : Color.white);

            if (shopRows == null) return;
            for (int i = 0; i < shopRows.Length; i++)
            {
                ShopItemRow row = shopRows[i];
                if (row == null) continue;

                Image rowBg = row.rowBackground;
                if (rowBg == null && row.buyButton != null && row.buyButton.transform.parent != null)
                {
                    rowBg = row.buyButton.transform.parent.GetComponent<Image>();
                }
                if (rowBg != null)
                {
                    RectTransform rowRt = rowBg.rectTransform;
                    rowRt.sizeDelta = new Vector2(1440f, 168f);
                    if (rowBg.TryGetComponent(out LayoutElement le))
                    {
                        le.preferredWidth = 1440f;
                        le.preferredHeight = 168f;
                    }

                    if (rowGlassStripSprite != null)
                    {
                        GlassHudTheme.ApplySlicedSprite(rowBg, rowGlassStripSprite, 1440f, 168f, Color.white);
                    }
                }

                if (row.nameText != null)
                {
                    RectTransform nrt = row.nameText.rectTransform;
                    nrt.anchoredPosition = new Vector2(-280f, 0f);
                    nrt.sizeDelta = new Vector2(430f, 52f);
                }
                if (row.priceText != null)
                {
                    RectTransform prt = row.priceText.rectTransform;
                    prt.anchoredPosition = new Vector2(65f, 0f);
                    prt.sizeDelta = new Vector2(170f, 44f);
                }
                if (row.ownedCountText != null)
                {
                    RectTransform ort = row.ownedCountText.rectTransform;
                    ort.anchoredPosition = new Vector2(265f, 0f);
                    ort.sizeDelta = new Vector2(210f, 44f);
                }

                if (row.buyButton != null)
                {
                    RectTransform buyRt = row.buyButton.GetComponent<RectTransform>();
                    if (buyRt != null)
                    {
                        buyRt.anchorMin = new Vector2(1f, 0.5f);
                        buyRt.anchorMax = new Vector2(1f, 0.5f);
                        buyRt.pivot = new Vector2(0.5f, 0.5f);
                        buyRt.anchoredPosition = new Vector2(-144f, 0f);
                        buyRt.sizeDelta = new Vector2(224f, 84f);
                    }

                    if (row.buyButton.TryGetComponent(out Image buyImg) && greenBuyButtonSprite != null)
                    {
                        GlassHudTheme.ApplySlicedSprite(buyImg, greenBuyButtonSprite, 224f, 84f, Color.white);
                    }

                    Text buyLabel = row.buyButton.GetComponentInChildren<Text>(true);
                    if (buyLabel != null)
                    {
                        if (string.IsNullOrEmpty(buyLabel.text))
                        {
                            buyLabel.text = "MUA";
                        }
                        StyleLegacyTextCrisp(buyLabel, Color.white);
                    }

                    ModeCardHoverEffect buyHover = UIManager.EnsureButtonHoverEffect(row.buyButton, 3f, 1.035f, 15f);
                    if (buyHover != null)
                    {
                        buyHover.SetBaseAnchoredPosition(new Vector2(-144f, 0f));
                    }
                }

                if (row.iconImage != null)
                {
                    Sprite icon = GetFallbackItemIcon(row.itemType);
                    if (row.iconImage.sprite == null && icon != null)
                    {
                        row.iconImage.sprite = icon;
                    }
                    row.iconImage.preserveAspect = true;
                    row.iconImage.color = Color.white;

                    RectTransform itemIconRt = row.iconImage.rectTransform;
                    itemIconRt.anchorMin = Vector2.zero;
                    itemIconRt.anchorMax = Vector2.one;
                    itemIconRt.pivot = new Vector2(0.5f, 0.5f);
                    itemIconRt.anchoredPosition = Vector2.zero;
                    itemIconRt.offsetMin = new Vector2(18f, 18f);
                    itemIconRt.offsetMax = new Vector2(-18f, -18f);

                    if (row.iconImage.transform.parent != null &&
                        row.iconImage.transform.parent.name == "IconFrame")
                    {
                        Transform iconFrameTf = row.iconImage.transform.parent;
                        if (iconFrameTf.TryGetComponent(out RectTransform frameRt))
                        {
                            frameRt.anchorMin = new Vector2(0f, 0.5f);
                            frameRt.anchorMax = new Vector2(0f, 0.5f);
                            frameRt.pivot = new Vector2(0f, 0.5f);
                            frameRt.anchoredPosition = new Vector2(34f, 0f);
                            frameRt.sizeDelta = new Vector2(116f, 116f);
                        }

                        if (iconFrameTf.TryGetComponent(out Image iconFrameBg))
                        {
                            if (iconSlotBlueGlowSprite != null)
                            {
                                GlassHudTheme.ApplySlicedSprite(iconFrameBg, iconSlotBlueGlowSprite, 116f, 116f, Color.white);
                            }
                            else
                            {
                                iconFrameBg.color = new Color(0.05f, 0.08f, 0.14f, 0.42f);
                            }
                        }
                    }
                }

                if (row.nameText != null) StyleLegacyTextCrisp(row.nameText, row.nameText.color);
                if (row.priceText != null) StyleLegacyTextCrisp(row.priceText, row.priceText.color);
                if (row.ownedCountText != null) StyleLegacyTextCrisp(row.ownedCountText, row.ownedCountText.color);
                if (row.slotQButtonText != null) StyleLegacyTextCrisp(row.slotQButtonText, row.slotQButtonText.color);
                if (row.slotEButtonText != null) StyleLegacyTextCrisp(row.slotEButtonText, row.slotEButtonText.color);
            }
        }

        private void EnsureShopMainFrame()
        {
            Transform mainFrameTf = transform.Find("ShopMainFrame");
            GameObject mainFrameObj;
            if (mainFrameTf != null)
            {
                mainFrameObj = mainFrameTf.gameObject;
            }
            else
            {
                mainFrameObj = new GameObject("ShopMainFrame", typeof(RectTransform), typeof(Image));
                mainFrameObj.transform.SetParent(transform, false);
                Transform rowsContainerTf = rowsLayoutGroup != null ? rowsLayoutGroup.transform : transform.Find("ShopRowsContainer");
                if (rowsContainerTf != null && rowsContainerTf.parent == transform)
                {
                    mainFrameObj.transform.SetSiblingIndex(rowsContainerTf.GetSiblingIndex());
                }
            }

            RectTransform mfRt = mainFrameObj.GetComponent<RectTransform>();
            if (mfRt != null)
            {
                mfRt.anchorMin = new Vector2(0.5f, 0.5f);
                mfRt.anchorMax = new Vector2(0.5f, 0.5f);
                mfRt.pivot = new Vector2(0.5f, 0.5f);
                mfRt.anchoredPosition = new Vector2(0f, -32f);
                mfRt.sizeDelta = new Vector2(1640f, 700f);
            }

            if (mainFrameObj.TryGetComponent(out Image mfImg))
            {
                mfImg.raycastTarget = false;
                if (shopMainPanelSprite != null)
                {
                    GlassHudTheme.ApplySlicedSprite(mfImg, shopMainPanelSprite, 1640f, 700f, Color.white);
                }
                else
                {
                    mfImg.color = new Color(0.06f, 0.09f, 0.16f, 0.46f);
                }
            }
        }

        private static void StyleLegacyTextCrisp(Text txt, Color targetColor)
        {
            if (txt == null) return;
            GlassHudTheme.StyleLegacyText(txt, -1, targetColor, bold: true, addShadow: true);
            if ((targetColor.r + targetColor.g + targetColor.b) > 0.45f && txt.GetComponent<Outline>() == null)
            {
                Outline outline = txt.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }

        private void EnsureRowsLayoutContainer()
        {
            if (rowsLayoutGroup == null)
            {
                Transform existing = transform.Find("ShopRowsContainer");
                if (existing != null)
                {
                    rowsLayoutGroup = existing.GetComponent<VerticalLayoutGroup>();
                }
            }

            if (rowsLayoutGroup == null && Application.isPlaying && shopRows != null && shopRows.Length > 0)
            {
                Transform firstRow = shopRows[0]?.rowBackground != null
                    ? shopRows[0].rowBackground.transform
                    : shopRows[0]?.buyButton?.transform.parent;

                if (firstRow != null && firstRow.parent == transform)
                {
                    GameObject containerObj = new GameObject("ShopRowsContainer", typeof(RectTransform));
                    containerObj.transform.SetParent(transform, false);
                    RectTransform rt = containerObj.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0f, -32f);
                    rt.sizeDelta = new Vector2(1440f, 548f);

                    rowsLayoutGroup = containerObj.AddComponent<VerticalLayoutGroup>();
                    rowsLayoutGroup.childAlignment = TextAnchor.UpperCenter;
                    rowsLayoutGroup.childControlWidth = true;
                    rowsLayoutGroup.childControlHeight = false;
                    rowsLayoutGroup.childForceExpandWidth = true;
                    rowsLayoutGroup.childForceExpandHeight = false;

                    for (int i = 0; i < shopRows.Length; i++)
                    {
                        Transform rowTf = shopRows[i]?.rowBackground != null
                            ? shopRows[i].rowBackground.transform
                            : shopRows[i]?.buyButton?.transform.parent;
                        if (rowTf == null || rowTf.parent != transform) continue;

                        rowTf.SetParent(containerObj.transform, false);
                        LayoutElement le = rowTf.GetComponent<LayoutElement>();
                        if (le == null) le = rowTf.gameObject.AddComponent<LayoutElement>();
                        le.preferredWidth = 1440f;
                        le.preferredHeight = 168f;
                    }
                }
            }

            if (rowsLayoutGroup != null)
            {
                if (rowsLayoutGroup.TryGetComponent(out RectTransform rowsRt))
                {
                    rowsRt.anchoredPosition = new Vector2(0f, -32f);
                    rowsRt.sizeDelta = new Vector2(1440f, 548f);
                }
                rowsLayoutGroup.spacing = 16f;
                if (rowsLayoutGroup.TryGetComponent(out Image containerImg))
                {
                    containerImg.color = Color.clear;
                    containerImg.raycastTarget = false;
                }
            }
        }

        private static Sprite GetFallbackItemIcon(ConsumableItemType itemType)
        {
            return itemType switch
            {
                ConsumableItemType.SenzuBean => UIManager.LoadItemIconSprite("Icon_SenzuBean.png"),
                ConsumableItemType.SpinalFluid => UIManager.LoadItemIconSprite("Icon_SpinalFluid.png"),
                ConsumableItemType.UltraDivineWater => UIManager.LoadItemIconSprite("Icon_UltraDivineWater.png"),
                _ => null
            };
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
