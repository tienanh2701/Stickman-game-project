using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Items;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.UI
{
    [Serializable]
    public class ItemSlotDisplay
    {
        public Image slotBorder;
        public Image iconImage;
        public TMP_Text keyBadgeText;
        public TMP_Text countBadgeText;
    }

    /// <summary>
    /// Quản lý 2 ô vật phẩm chiến đấu [Q] (Slot 0) & [E] (Slot 1) trên Battle HUD,
    /// kèm đồng hồ hồi chiêu đỡ đòn [S] tùy chọn. Tự động làm mờ ô khi số lượng về 0.
    /// </summary>
    public class HudItemSlotsUI : MonoBehaviour
    {
        [Header("Item Slots ([Q] Slot 0, [E] Slot 1)")]
        [SerializeField] private ItemSlotDisplay slotQ = new ItemSlotDisplay();
        [SerializeField] private ItemSlotDisplay slotE = new ItemSlotDisplay();
        [SerializeField] private Sprite emptySlotSprite;
        [SerializeField] private Color activeBorderColor = new Color(0.95f, 0.78f, 0.25f, 0.96f);
        [SerializeField] private Color dimmedIconColor = new Color(0.45f, 0.45f, 0.45f, 0.42f);
        [SerializeField] private Color dimmedBorderColor = new Color(0.45f, 0.45f, 0.45f, 0.45f);

        [Header("Optional [S] Block Cooldown Indicator")]
        [SerializeField] private TMP_Text blockCooldownText;
        [SerializeField] private Image blockFillImage;
        [SerializeField] private GameObject blockGroup;

        [Header("Sliced/Glass Sprites (Standalone Safety)")]
        [SerializeField] private Sprite hudOrbFrameSprite;
        [SerializeField] private Sprite miniPillBadgeSprite;

        private BaseCharacter activePlayer;
        private float nextPlayerLookupTime;
        private bool isInventorySubscribed;
        private bool isLevelSubscribed;
        private bool lastKnownFreeUseMode;
        private bool hasAppliedGlassTheme;
        private Coroutine pulseQCoroutine;
        private Coroutine pulseECoroutine;

        public void Configure(
            ItemSlotDisplay qSlot,
            ItemSlotDisplay eSlot,
            TMP_Text blockStatusTxt = null,
            Image blockFillImg = null,
            GameObject blockContainer = null,
            Sprite emptySprite = null)
        {
            slotQ = qSlot ?? new ItemSlotDisplay();
            slotE = eSlot ?? new ItemSlotDisplay();
            blockCooldownText = blockStatusTxt;
            blockFillImage = blockFillImg;
            blockGroup = blockContainer;
            if (emptySprite != null)
            {
                emptySlotSprite = emptySprite;
            }

            hasAppliedGlassTheme = false;
            RefreshSlots();
        }

        public void ConfigureSlots(
            Image qBorder, Image qIcon, TMP_Text qKeyText, TMP_Text qCountText,
            Image eBorder, Image eIcon, TMP_Text eKeyText, TMP_Text eCountText,
            TMP_Text blockStatusTxt = null, Image blockFillImg = null, GameObject blockContainer = null,
            Sprite emptySprite = null)
        {
            slotQ = new ItemSlotDisplay
            {
                slotBorder = qBorder,
                iconImage = qIcon,
                keyBadgeText = qKeyText,
                countBadgeText = qCountText
            };
            slotE = new ItemSlotDisplay
            {
                slotBorder = eBorder,
                iconImage = eIcon,
                keyBadgeText = eKeyText,
                countBadgeText = eCountText
            };
            blockCooldownText = blockStatusTxt;
            blockFillImage = blockFillImg;
            blockGroup = blockContainer;
            if (emptySprite != null)
            {
                emptySlotSprite = emptySprite;
            }

            hasAppliedGlassTheme = false;
            RefreshSlots();
        }

        public void BindPlayer(BaseCharacter player)
        {
            activePlayer = player;
            UpdateBlockIndicator();
        }

        public void RefreshSlots()
        {
            if (Application.isPlaying)
            {
                EnsureGlassThemeApplied();
            }
            RefreshSlot(slotQ, InventoryManager.SLOT_Q, "Q");
            RefreshSlot(slotE, InventoryManager.SLOT_E, "E");
        }

        private void OnEnable()
        {
            SubscribeEvents();
            RefreshSlots();
            UpdateBlockIndicator();
        }

        private void Start()
        {
            SubscribeEvents();
            ResolveActivePlayer();
            EnsureGlassThemeApplied();
            RefreshSlots();
            UpdateBlockIndicator();
        }

        private void EnsureGlassThemeApplied()
        {
            if (hasAppliedGlassTheme) return;

            if (transform is RectTransform panelRt)
            {
                panelRt.anchorMin = Vector2.zero;
                panelRt.anchorMax = Vector2.zero;
                panelRt.pivot = Vector2.zero;
                panelRt.anchoredPosition = new Vector2(22f, 16f);
                panelRt.sizeDelta = new Vector2(230f, 96f);
            }

            Sprite orbSprite = hudOrbFrameSprite != null
                ? hudOrbFrameSprite
                : GlassHudTheme.LoadSprite("HudOrb_Frame_GoldGlass.png");
            if (hudOrbFrameSprite == null && orbSprite != null)
            {
                hudOrbFrameSprite = orbSprite;
            }

            Sprite miniPillSprite = miniPillBadgeSprite != null
                ? miniPillBadgeSprite
                : GlassHudTheme.LoadSprite("MiniPill_GoldBadge.png");
            if (miniPillBadgeSprite == null && miniPillSprite != null)
            {
                miniPillBadgeSprite = miniPillSprite;
            }

            if (orbSprite != null)
            {
                activeBorderColor = Color.white;
                dimmedBorderColor = new Color(0.55f, 0.55f, 0.60f, 0.58f);
            }

            ApplySlotGlassStyle(slotQ, orbSprite, miniPillSprite);
            ApplySlotGlassStyle(slotE, orbSprite, miniPillSprite);

            if (orbSprite != null)
            {
                hasAppliedGlassTheme = true;
            }
        }

        private static void ApplySlotGlassStyle(ItemSlotDisplay ui, Sprite orbSprite, Sprite miniPillSprite)
        {
            if (ui == null) return;

            if (ui.slotBorder != null)
            {
                if (orbSprite != null)
                {
                    GlassHudTheme.ApplySprite(ui.slotBorder, orbSprite, sliced: false, preserveAspect: true);
                }
                else
                {
                    ui.slotBorder.type = Image.Type.Simple;
                    ui.slotBorder.preserveAspect = true;
                }
                ui.slotBorder.raycastTarget = false;

                Transform innerBgTr = ui.slotBorder.transform.Find("InnerBg");
                if (innerBgTr != null && innerBgTr.TryGetComponent(out Image innerImg))
                {
                    innerImg.enabled = orbSprite == null;
                    innerImg.color = new Color(0.05f, 0.08f, 0.14f, 0.30f);
                    innerImg.rectTransform.sizeDelta = new Vector2(-16f, -16f);
                    innerImg.raycastTarget = false;
                }
            }

            if (ui.iconImage != null)
            {
                ui.iconImage.preserveAspect = true;
                ui.iconImage.raycastTarget = false;
                ui.iconImage.rectTransform.sizeDelta = new Vector2(-10f, -10f);
            }

            if (ui.keyBadgeText != null)
            {
                Transform keyParent = ui.keyBadgeText.transform.parent;
                if (keyParent != null && (ui.slotBorder == null || keyParent != ui.slotBorder.transform))
                {
                    if (keyParent is RectTransform keyRt)
                    {
                        keyRt.sizeDelta = new Vector2(30f, 26f);
                        keyRt.anchoredPosition = new Vector2(12f, -10f);
                    }
                    if (keyParent.TryGetComponent(out Image keyBg))
                    {
                        if (miniPillSprite != null)
                        {
                            GlassHudTheme.ApplySlicedSprite(keyBg, miniPillSprite, 30f, 26f);
                        }
                        else
                        {
                            GlassHudTheme.ApplySprite(keyBg, "MiniPill_GoldBadge.png", default, true, false);
                        }
                        keyBg.raycastTarget = false;
                    }
                }

                ui.keyBadgeText.alignment = TextAlignmentOptions.Center;
                ui.keyBadgeText.raycastTarget = false;
                GlassHudTheme.StyleTmpText(ui.keyBadgeText, 14f, Color.white, true, true);
            }

            if (ui.countBadgeText != null)
            {
                Transform countParent = ui.countBadgeText.transform.parent;
                if (countParent != null && (ui.slotBorder == null || countParent != ui.slotBorder.transform))
                {
                    if (countParent is RectTransform countRt)
                    {
                        countRt.sizeDelta = new Vector2(48f, 24f);
                        countRt.anchoredPosition = new Vector2(-10f, 8f);
                    }
                    if (countParent.TryGetComponent(out Image countBg))
                    {
                        if (miniPillSprite != null)
                        {
                            GlassHudTheme.ApplySlicedSprite(countBg, miniPillSprite, 48f, 24f);
                        }
                        else
                        {
                            GlassHudTheme.ApplySprite(countBg, "MiniPill_GoldBadge.png", default, true, false);
                        }
                        countBg.raycastTarget = false;
                    }
                }

                ui.countBadgeText.alignment = TextAlignmentOptions.Center;
                ui.countBadgeText.raycastTarget = false;
                GlassHudTheme.StyleTmpText(ui.countBadgeText, 13f, null, true, true);
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            StopPulseCoroutines();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            StopPulseCoroutines();
        }

        private void Update()
        {
            if (activePlayer == null && Time.unscaledTime >= nextPlayerLookupTime)
            {
                nextPlayerLookupTime = Time.unscaledTime + 0.5f;
                ResolveActivePlayer();
            }

            if (InventoryManager.HasInstance && InventoryManager.Instance.IsTrainingFreeUseMode != lastKnownFreeUseMode)
            {
                RefreshSlots();
            }

            UpdateBlockIndicator();
        }

        private void SubscribeEvents()
        {
            if (!isInventorySubscribed && InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged -= HandleInventoryChanged;
                InventoryManager.Instance.OnSlotConsumed -= HandleSlotConsumed;
                InventoryManager.Instance.OnInventoryChanged += HandleInventoryChanged;
                InventoryManager.Instance.OnSlotConsumed += HandleSlotConsumed;
                isInventorySubscribed = true;
            }

            if (!isLevelSubscribed && LevelManager.Instance != null)
            {
                LevelManager.Instance.OnPlayerSpawned -= BindPlayer;
                LevelManager.Instance.OnPlayerSpawned += BindPlayer;
                isLevelSubscribed = true;
            }
        }

        private void UnsubscribeEvents()
        {
            if (isInventorySubscribed)
            {
                InventoryManager inv = InventoryManager.HasInstance ? InventoryManager.Instance : null;
                if (inv != null)
                {
                    inv.OnInventoryChanged -= HandleInventoryChanged;
                    inv.OnSlotConsumed -= HandleSlotConsumed;
                }
            }
            isInventorySubscribed = false;

            if (isLevelSubscribed && LevelManager.Instance != null)
            {
                LevelManager.Instance.OnPlayerSpawned -= BindPlayer;
            }
            isLevelSubscribed = false;
        }

        private void ResolveActivePlayer()
        {
            BaseCharacter found = (LevelManager.Instance != null && LevelManager.Instance.ActivePlayer != null)
                ? LevelManager.Instance.ActivePlayer
                : FindAnyObjectByType<BaseCharacter>();

            if (found != null)
            {
                BindPlayer(found);
            }
        }

        private void HandleInventoryChanged()
        {
            RefreshSlots();
        }

        private void HandleSlotConsumed(int slotIndex, ConsumableItemType _)
        {
            RefreshSlots();

            if (!gameObject.activeInHierarchy) return;

            if (slotIndex == InventoryManager.SLOT_Q && slotQ != null && slotQ.slotBorder != null)
            {
                if (pulseQCoroutine != null) StopCoroutine(pulseQCoroutine);
                pulseQCoroutine = StartCoroutine(PulseSlotRoutine(slotQ.slotBorder.transform, true));
            }
            else if (slotIndex == InventoryManager.SLOT_E && slotE != null && slotE.slotBorder != null)
            {
                if (pulseECoroutine != null) StopCoroutine(pulseECoroutine);
                pulseECoroutine = StartCoroutine(PulseSlotRoutine(slotE.slotBorder.transform, false));
            }
        }

        private void RefreshSlot(ItemSlotDisplay ui, int slotIndex, string keyLabel)
        {
            if (ui == null) return;

            InventoryManager inv = (Application.isPlaying || InventoryManager.HasInstance)
                ? InventoryManager.Instance
                : null;

            ConsumableItemType itemType = inv != null ? inv.GetEquippedItem(slotIndex) : ConsumableItemType.None;
            ConsumableItemSO data = inv != null ? inv.GetItemData(itemType) : null;
            bool freeUse = inv != null && inv.IsTrainingFreeUseMode;
            lastKnownFreeUseMode = freeUse;
            int ownedCount = inv != null ? inv.GetOwnedCount(itemType) : 0;

            bool isDimmed = itemType == ConsumableItemType.None || (!freeUse && ownedCount <= 0);

            if (ui.keyBadgeText != null)
            {
                ui.keyBadgeText.text = keyLabel;
            }

            if (ui.iconImage != null)
            {
                if (itemType != ConsumableItemType.None && data != null && data.icon != null)
                {
                    ui.iconImage.sprite = data.icon;
                    ui.iconImage.enabled = true;
                    ui.iconImage.preserveAspect = true;
                    ui.iconImage.color = isDimmed ? dimmedIconColor : Color.white;
                }
                else if (emptySlotSprite != null)
                {
                    ui.iconImage.sprite = emptySlotSprite;
                    ui.iconImage.enabled = true;
                    ui.iconImage.preserveAspect = true;
                    ui.iconImage.color = dimmedIconColor;
                }
                else
                {
                    ui.iconImage.enabled = false;
                }
            }

            if (ui.slotBorder != null)
            {
                ui.slotBorder.color = isDimmed ? dimmedBorderColor : activeBorderColor;
            }

            if (ui.countBadgeText != null)
            {
                if (itemType == ConsumableItemType.None)
                {
                    ui.countBadgeText.text = "-";
                    ui.countBadgeText.color = new Color(0.65f, 0.65f, 0.72f, 0.78f);
                }
                else if (freeUse)
                {
                    ui.countBadgeText.text = "FREE";
                    ui.countBadgeText.color = new Color(0.45f, 1f, 0.72f, 1f);
                }
                else
                {
                    ui.countBadgeText.text = ownedCount.ToString();
                    ui.countBadgeText.color = ownedCount > 0
                        ? Color.white
                        : new Color(0.95f, 0.42f, 0.42f, 0.85f);
                }
            }
        }

        private void UpdateBlockIndicator()
        {
            if (PlayerVitalsHudUI.Instance != null && PlayerVitalsHudUI.Instance.isActiveAndEnabled) return;
            if (blockCooldownText == null && blockFillImage == null) return;
            if (blockGroup != null && !blockGroup.activeInHierarchy) return;

            if (activePlayer == null)
            {
                if (blockCooldownText != null)
                {
                    blockCooldownText.text = "KHIÊN [S]: SẴN SÀNG";
                    blockCooldownText.color = new Color(0.45f, 1f, 0.6f, 1f);
                }
                if (blockFillImage != null)
                {
                    blockFillImage.fillAmount = 1f;
                    blockFillImage.color = new Color(0.30f, 0.90f, 0.55f, 1f);
                }
                return;
            }

            if (activePlayer.IsBlocking)
            {
                if (blockCooldownText != null)
                {
                    blockCooldownText.text = $"KHIÊN [S]: ĐANG BẬT ({activePlayer.BlockRemaining:0.00}s)";
                    blockCooldownText.color = new Color(0.35f, 0.95f, 1f, 1f);
                }
                if (blockFillImage != null)
                {
                    float dur = Mathf.Max(0.01f, activePlayer.BlockDuration);
                    blockFillImage.fillAmount = Mathf.Clamp01(activePlayer.BlockRemaining / dur);
                    blockFillImage.color = new Color(0.35f, 0.95f, 1f, 1f);
                }
            }
            else
            {
                if (blockCooldownText != null)
                {
                    blockCooldownText.text = "KHIÊN [S]: SẴN SÀNG";
                    blockCooldownText.color = new Color(0.45f, 1f, 0.6f, 1f);
                }
                if (blockFillImage != null)
                {
                    blockFillImage.fillAmount = 1f;
                    blockFillImage.color = new Color(0.30f, 0.90f, 0.55f, 1f);
                }
            }
        }

        private IEnumerator PulseSlotRoutine(Transform slotTransform, bool isQ)
        {
            if (slotTransform == null) yield break;

            const float duration = 0.16f;
            float half = duration * 0.5f;
            Vector3 peak = new Vector3(1.18f, 1.18f, 1f);

            float elapsed = 0f;
            while (elapsed < half && slotTransform != null)
            {
                elapsed += Time.unscaledDeltaTime;
                slotTransform.localScale = Vector3.Lerp(Vector3.one, peak, Mathf.Clamp01(elapsed / half));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half && slotTransform != null)
            {
                elapsed += Time.unscaledDeltaTime;
                slotTransform.localScale = Vector3.Lerp(peak, Vector3.one, Mathf.Clamp01(elapsed / half));
                yield return null;
            }

            if (slotTransform != null)
            {
                slotTransform.localScale = Vector3.one;
            }

            if (isQ) pulseQCoroutine = null;
            else pulseECoroutine = null;
        }

        private void StopPulseCoroutines()
        {
            if (pulseQCoroutine != null)
            {
                StopCoroutine(pulseQCoroutine);
                pulseQCoroutine = null;
            }
            if (pulseECoroutine != null)
            {
                StopCoroutine(pulseECoroutine);
                pulseECoroutine = null;
            }

            if (slotQ != null && slotQ.slotBorder != null) slotQ.slotBorder.transform.localScale = Vector3.one;
            if (slotE != null && slotE.slotBorder != null) slotE.slotBorder.transform.localScale = Vector3.one;
        }
    }
}
