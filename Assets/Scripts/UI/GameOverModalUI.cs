using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Economy;
using StickmanBeatEmUp.GameModes;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Nâng cấp VictoryPanel & DefeatPanel ("THẤT BẠI") trên GameCanvas thành Floating Dark Glassmorphism Modal:
    /// - Tự động gắn lên GameCanvas qua RuntimeInitializeOnLoadMethod(AfterSceneLoad) + SceneManager.sceneLoaded
    /// - Theo dõi coinsEarnedThisRun chính xác qua CurrencyManager.OnCoinsChanged
    /// - Thống nhất Defeat modal cho cả Campaign và Endless Mode với thống kê Wave/Stage & Xu nhận được
    /// - Phát animation pop-in mượt theo Time.unscaledDeltaTime khi mở màn hình kết thúc.
    /// </summary>
    public class GameOverModalUI : MonoBehaviour
    {
        private const float POP_IN_DURATION = 0.20f;
        private const float POP_IN_START_SCALE = 0.90f;
        private const string SELECTED_STAGE_INDEX_KEY = "SelectedStageIndex";

        private static readonly Color VictoryBackdropColor = new Color(0.02f, 0.04f, 0.09f, 0.56f);
        private static readonly Color DefeatBackdropColor = new Color(0.04f, 0.02f, 0.04f, 0.58f);
        private static readonly Color VictoryTitleGoldColor = new Color(1f, 0.8862745f, 0.2901961f, 1f); // #FFE24A
        private static readonly Color DefeatTitleCrimsonColor = new Color(1f, 0.28f, 0.38f, 1f);

        private static bool sceneHookRegistered;

        private GameObject victoryPanel;
        private GameObject defeatPanel;

        private RectTransform victoryCardRect;
        private CanvasGroup victoryCardCanvasGroup;
        private TMP_Text victoryTitleText;
        private TMP_Text victoryRewardText;
        private Button victoryNextButton;
        private Button victoryRetryButton;
        private Button victoryMenuButton;

        private RectTransform defeatCardRect;
        private CanvasGroup defeatCardCanvasGroup;
        private TMP_Text defeatTitleText;
        private TMP_Text defeatSummaryText;
        private Button defeatRetryButton;
        private Button defeatMenuButton;

        private int lastSeenCoins;
        private int coinsEarnedThisRun;
        private int latestWaveFromEvent = 1;

        private bool isCurrencySubscribed;
        private bool isWaveSubscribed;
        private bool wasVictoryActive;
        private bool wasDefeatActive;
        private bool hasInitializedPanels;

        private Coroutine victoryPopInCoroutine;
        private Coroutine defeatPopInCoroutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sceneHookRegistered = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeOnSceneLoad()
        {
            if (!sceneHookRegistered)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                SceneManager.sceneLoaded += HandleSceneLoaded;
                sceneHookRegistered = true;
            }

            AttachToActiveGameCanvas();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AttachToActiveGameCanvas();
        }

        public static GameOverModalUI AttachToActiveGameCanvas()
        {
            GameUIManager uiManager = GameUIManager.Instance != null
                ? GameUIManager.Instance
                : FindAnyObjectByType<GameUIManager>(FindObjectsInactive.Include);
            if (uiManager == null) return null;

            GameOverModalUI existing = uiManager.GetComponent<GameOverModalUI>();
            if (existing != null)
            {
                existing.ResetRunTracker();
                return existing;
            }

            return uiManager.gameObject.AddComponent<GameOverModalUI>();
        }

        public void ResetRunTracker()
        {
            coinsEarnedThisRun = 0;
            latestWaveFromEvent = 1;
            SyncLastSeenCoinsFromManager();
        }

        private void Awake()
        {
            GameOverModalUI[] instances = GetComponents<GameOverModalUI>();
            if (instances.Length > 1 && instances[0] != this)
            {
                Destroy(this);
                return;
            }

            ResetRunTracker();
            ResolvePanelReferences();
        }

        private void OnEnable()
        {
            SubscribeCurrencyEvents();
            SubscribeWaveEvents();
        }

        private void Start()
        {
            SubscribeCurrencyEvents();
            SubscribeWaveEvents();

            ResolvePanelReferences();
            ApplyVictoryModal(playPopIn: false);
            ApplyDefeatModal(playPopIn: false);

            wasVictoryActive = victoryPanel != null && victoryPanel.activeSelf;
            wasDefeatActive = defeatPanel != null && defeatPanel.activeSelf;
            hasInitializedPanels = true;
        }

        private void Update()
        {
            if (!isWaveSubscribed)
            {
                SubscribeWaveEvents();
            }
            RefreshActiveTransitions();
        }

        private void LateUpdate()
        {
            RefreshActiveTransitions();
        }

        private void OnDisable()
        {
            StopAllPopInCoroutines();
            UnsubscribeCurrencyEvents();
            UnsubscribeWaveEvents();
        }

        private void OnDestroy()
        {
            StopAllPopInCoroutines();
            UnsubscribeCurrencyEvents();
            UnsubscribeWaveEvents();
        }

        private void RefreshActiveTransitions()
        {
            if (!hasInitializedPanels) return;

            if (victoryPanel != null)
            {
                bool isVictoryActive = victoryPanel.activeSelf;
                if (isVictoryActive && !wasVictoryActive)
                {
                    ApplyVictoryModal(playPopIn: true);
                }
                else if (!isVictoryActive && wasVictoryActive)
                {
                    StopCoroutineIfActive(ref victoryPopInCoroutine, victoryCardRect, victoryCardCanvasGroup);
                }
                wasVictoryActive = isVictoryActive;
            }

            if (defeatPanel != null)
            {
                bool isDefeatActive = defeatPanel.activeSelf;
                if (isDefeatActive && !wasDefeatActive)
                {
                    ApplyDefeatModal(playPopIn: true);
                }
                else if (!isDefeatActive && wasDefeatActive)
                {
                    StopCoroutineIfActive(ref defeatPopInCoroutine, defeatCardRect, defeatCardCanvasGroup);
                }
                wasDefeatActive = isDefeatActive;
            }
        }

        private void SyncLastSeenCoinsFromManager()
        {
            CurrencyManager cm = CurrencyManager.HasInstance ? CurrencyManager.Instance : FindAnyObjectByType<CurrencyManager>();
            if (cm != null)
            {
                lastSeenCoins = cm.CurrentCoins;
            }
        }

        private void SubscribeCurrencyEvents()
        {
            if (isCurrencySubscribed) return;

            CurrencyManager cm = CurrencyManager.Instance;
            if (cm == null) return;

            lastSeenCoins = cm.CurrentCoins;
            cm.OnCoinsChanged -= HandleCoinsChanged;
            cm.OnCoinsChanged += HandleCoinsChanged;
            isCurrencySubscribed = true;
        }

        private void UnsubscribeCurrencyEvents()
        {
            if (!isCurrencySubscribed) return;
            CurrencyManager cm = CurrencyManager.HasInstance ? CurrencyManager.Instance : null;
            if (cm != null)
            {
                cm.OnCoinsChanged -= HandleCoinsChanged;
            }
            isCurrencySubscribed = false;
        }

        private void HandleCoinsChanged(int newCoins)
        {
            if (newCoins > lastSeenCoins)
            {
                coinsEarnedThisRun += newCoins - lastSeenCoins;
            }
            lastSeenCoins = newCoins;

            if (victoryPanel != null && victoryPanel.activeSelf)
            {
                UpdateVictoryRewardBadgeText();
            }
            if (defeatPanel != null && defeatPanel.activeSelf)
            {
                UpdateDefeatSummaryPillText();
            }
        }

        private void SubscribeWaveEvents()
        {
            if (isWaveSubscribed || LevelManager.Instance == null) return;
            LevelManager.Instance.OnWaveChanged -= HandleWaveChanged;
            LevelManager.Instance.OnWaveChanged += HandleWaveChanged;
            isWaveSubscribed = true;
        }

        private void UnsubscribeWaveEvents()
        {
            if (!isWaveSubscribed) return;
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnWaveChanged -= HandleWaveChanged;
            }
            isWaveSubscribed = false;
        }

        private void HandleWaveChanged(int currentWave, int totalWaves)
        {
            if (currentWave > 0)
            {
                latestWaveFromEvent = currentWave;
            }
        }

        private void ResolvePanelReferences()
        {
            if (victoryPanel == null)
            {
                Transform vt = FindDescendant(transform, "VictoryPanel");
                if (vt != null) victoryPanel = vt.gameObject;
            }

            if (defeatPanel == null)
            {
                Transform dt = FindDescendant(transform, "DefeatPanel");
                if (dt != null) defeatPanel = dt.gameObject;
            }

            if (victoryPanel != null)
            {
                Transform vRoot = victoryPanel.transform;
                victoryTitleText = FindDescendant(vRoot, "TitleText")?.GetComponent<TMP_Text>();
                victoryNextButton = FindDescendant(vRoot, "NextStageButton")?.GetComponent<Button>();
                victoryRetryButton = FindDescendant(vRoot, "VictoryRetryButton")?.GetComponent<Button>();
                victoryMenuButton = FindDescendant(vRoot, "VictoryMenuButton")?.GetComponent<Button>();
            }

            if (defeatPanel != null)
            {
                Transform dRoot = defeatPanel.transform;
                defeatTitleText = FindDescendant(dRoot, "TitleText")?.GetComponent<TMP_Text>();
                defeatRetryButton = FindDescendant(dRoot, "DefeatRetryButton")?.GetComponent<Button>();
                defeatMenuButton = FindDescendant(dRoot, "DefeatMenuButton")?.GetComponent<Button>();
            }
        }

        private void ApplyVictoryModal(bool playPopIn)
        {
            ResolvePanelReferences();
            if (victoryPanel == null) return;

            ConfigureFullScreenBackdrop(victoryPanel, VictoryBackdropColor);

            Transform cardTr = EnsureModalCard(
                victoryPanel.transform,
                "VictoryModalCard",
                new Vector2(640f, 470f),
                "ModalCard_Victory_Gold.png",
                new Color(0.14f, 0.11f, 0.05f, 0.94f),
                out victoryCardRect,
                out victoryCardCanvasGroup);

            if (victoryTitleText != null)
            {
                if (victoryTitleText.transform.parent != cardTr)
                {
                    victoryTitleText.transform.SetParent(cardTr, false);
                }
                victoryTitleText.gameObject.SetActive(true);
                victoryTitleText.text = "CHIẾN THẮNG";
                victoryTitleText.alignment = TextAlignmentOptions.Center;
                victoryTitleText.raycastTarget = false;

                RectTransform titleRt = victoryTitleText.GetComponent<RectTransform>();
                if (titleRt != null)
                {
                    titleRt.anchorMin = new Vector2(0.5f, 0.5f);
                    titleRt.anchorMax = new Vector2(0.5f, 0.5f);
                    titleRt.pivot = new Vector2(0.5f, 0.5f);
                    titleRt.anchoredPosition = new Vector2(0f, 158f);
                    titleRt.sizeDelta = new Vector2(560f, 76f);
                }

                GlassHudTheme.StyleTmpText(victoryTitleText, 52f, VictoryTitleGoldColor, true, true);
                if (victoryTitleText.TryGetComponent(out Outline titleOutline))
                {
                    titleOutline.effectColor = new Color(0f, 0f, 0f, 0.96f);
                    titleOutline.effectDistance = new Vector2(1.8f, -1.8f);
                }
            }

            EnsureVictoryRewardBadge(cardTr);
            UpdateVictoryRewardBadgeText();

            ReparentIntoCard(victoryNextButton, cardTr);
            ReparentIntoCard(victoryRetryButton, cardTr);
            ReparentIntoCard(victoryMenuButton, cardTr);

            bool hasNextStage = victoryNextButton != null && victoryNextButton.gameObject.activeSelf;
            if (hasNextStage)
            {
                StylePillButton(
                    victoryNextButton,
                    "PillBtn_GreenConfirm.png",
                    "MÀN TIẾP THEO",
                    new Vector2(0f, -18f),
                    new Vector2(280f, 58f),
                    22f);
                StylePillButton(
                    victoryRetryButton,
                    "PillBtn_OrangePrimary.png",
                    "CHƠI LẠI",
                    new Vector2(-134f, -104f),
                    new Vector2(240f, 56f),
                    22f);
                StylePillButton(
                    victoryMenuButton,
                    "PillBtn_GlassNeutral.png",
                    "VỀ MENU",
                    new Vector2(134f, -104f),
                    new Vector2(240f, 56f),
                    22f);
            }
            else
            {
                StylePillButton(
                    victoryRetryButton,
                    "PillBtn_OrangePrimary.png",
                    "CHƠI LẠI",
                    new Vector2(-136f, -54f),
                    new Vector2(244f, 60f),
                    22f);
                StylePillButton(
                    victoryMenuButton,
                    "PillBtn_GlassNeutral.png",
                    "VỀ MENU",
                    new Vector2(136f, -54f),
                    new Vector2(244f, 60f),
                    22f);
            }

            if (playPopIn)
            {
                StartPopInAnimation(ref victoryPopInCoroutine, victoryCardRect, victoryCardCanvasGroup);
            }
            else
            {
                ResetCardTransform(victoryCardRect, victoryCardCanvasGroup);
            }
        }

        private void ApplyDefeatModal(bool playPopIn)
        {
            ResolvePanelReferences();
            if (defeatPanel == null) return;

            ConfigureFullScreenBackdrop(defeatPanel, DefeatBackdropColor);

            Transform cardTr = EnsureModalCard(
                defeatPanel.transform,
                "DefeatModalCard",
                new Vector2(600f, 420f),
                "ModalCard_Defeat_Crimson.png",
                new Color(0.14f, 0.05f, 0.07f, 0.94f),
                out defeatCardRect,
                out defeatCardCanvasGroup);

            if (defeatTitleText != null)
            {
                if (defeatTitleText.transform.parent != cardTr)
                {
                    defeatTitleText.transform.SetParent(cardTr, false);
                }
                defeatTitleText.gameObject.SetActive(true);
                defeatTitleText.text = "THẤT BẠI";
                defeatTitleText.alignment = TextAlignmentOptions.Center;
                defeatTitleText.raycastTarget = false;

                RectTransform titleRt = defeatTitleText.GetComponent<RectTransform>();
                if (titleRt != null)
                {
                    titleRt.anchorMin = new Vector2(0.5f, 0.5f);
                    titleRt.anchorMax = new Vector2(0.5f, 0.5f);
                    titleRt.pivot = new Vector2(0.5f, 0.5f);
                    titleRt.anchoredPosition = new Vector2(0f, 134f);
                    titleRt.sizeDelta = new Vector2(540f, 76f);
                }

                GlassHudTheme.StyleTmpText(defeatTitleText, 52f, DefeatTitleCrimsonColor, true, true);
                if (defeatTitleText.TryGetComponent(out Outline defeatOutline))
                {
                    defeatOutline.effectColor = new Color(0f, 0f, 0f, 0.98f);
                    defeatOutline.effectDistance = new Vector2(1.8f, -1.8f);
                }
            }

            EnsureDefeatSummaryPill(cardTr);
            UpdateDefeatSummaryPillText();

            ReparentIntoCard(defeatRetryButton, cardTr);
            ReparentIntoCard(defeatMenuButton, cardTr);

            StylePillButton(
                defeatRetryButton,
                "PillBtn_OrangePrimary.png",
                "CHƠI LẠI",
                new Vector2(-132f, -88f),
                new Vector2(238f, 62f),
                22f);
            StylePillButton(
                defeatMenuButton,
                "PillBtn_GlassNeutral.png",
                "VỀ MENU",
                new Vector2(132f, -88f),
                new Vector2(238f, 62f),
                22f);

            if (playPopIn)
            {
                StartPopInAnimation(ref defeatPopInCoroutine, defeatCardRect, defeatCardCanvasGroup);
            }
            else
            {
                ResetCardTransform(defeatCardRect, defeatCardCanvasGroup);
            }
        }

        private void EnsureVictoryRewardBadge(Transform cardTr)
        {
            TMP_FontAsset sharedFont = victoryTitleText != null ? victoryTitleText.font : null;
            victoryRewardText = EnsurePillWithCoinIcon(
                cardTr,
                "VictoryRewardBadge",
                "RewardText",
                new Vector2(0f, 76f),
                new Vector2(390f, 50f),
                "CurrencyBadge_GoldGlass.png",
                new Color(0.22f, 0.16f, 0.06f, 0.90f),
                30f,
                28f,
                50f,
                19f,
                new Color(1f, 0.94f, 0.72f, 1f),
                sharedFont);
        }

        private void EnsureDefeatSummaryPill(Transform cardTr)
        {
            TMP_FontAsset sharedFont = defeatTitleText != null ? defeatTitleText.font : null;
            defeatSummaryText = EnsurePillWithCoinIcon(
                cardTr,
                "DefeatSummaryPill",
                "SummaryText",
                new Vector2(0f, 38f),
                new Vector2(450f, 52f),
                "ControlHints_PillStrip.png",
                new Color(0.10f, 0.08f, 0.12f, 0.90f),
                26f,
                28f,
                48f,
                19f,
                new Color(0.96f, 0.97f, 1f, 1f),
                sharedFont);
        }

        private void UpdateVictoryRewardBadgeText()
        {
            if (victoryRewardText == null) return;

            int totalCoins = CurrencyManager.HasInstance
                ? CurrencyManager.Instance.CurrentCoins
                : (CurrencyManager.Instance != null ? CurrencyManager.Instance.CurrentCoins : lastSeenCoins);
            victoryRewardText.text = $"+{coinsEarnedThisRun} Xu   •   Tổng: {totalCoins} Xu";
        }

        private void UpdateDefeatSummaryPillText()
        {
            if (defeatSummaryText == null) return;

            EndlessModeManager endlessMgr = FindAnyObjectByType<EndlessModeManager>();
            bool isEndless = IsEndlessModeActive(endlessMgr);

            if (isEndless)
            {
                int endlessWave = (endlessMgr != null && endlessMgr.CurrentWave > 0)
                    ? endlessMgr.CurrentWave
                    : Mathf.Max(1, latestWaveFromEvent);
                defeatSummaryText.text = $"Đạt Wave {endlessWave}   •   Nhận được: +{coinsEarnedThisRun} Xu";
            }
            else
            {
                StageDataSO st = LevelManager.Instance != null ? LevelManager.Instance.CurrentStageData : null;
                int stageNum = st != null
                    ? Mathf.Max(1, st.stageNumber)
                    : ((PlayerPrefs.GetInt(SELECTED_STAGE_INDEX_KEY, 0) % 4) + 1);
                int waveIdx = Mathf.Max(1, latestWaveFromEvent);
                defeatSummaryText.text = $"Màn {stageNum} - Đợt {waveIdx}   •   Nhận được: +{coinsEarnedThisRun} Xu";
            }
        }

        private static bool IsEndlessModeActive(EndlessModeManager endlessMgr)
        {
            if (GameUIManager.Instance != null && GameUIManager.Instance.CurrentGameMode == GameMode.Endless)
            {
                return true;
            }
            if (LevelManager.Instance != null && LevelManager.Instance.CurrentGameMode == GameMode.Endless)
            {
                return true;
            }
            if (StageManager.CurrentGameMode == GameMode.Endless)
            {
                return true;
            }
            return endlessMgr != null && endlessMgr.enabled && (endlessMgr.IsRunning || endlessMgr.CurrentWave > 0);
        }

        private static TMP_Text EnsurePillWithCoinIcon(
            Transform parentCard,
            string pillName,
            string labelName,
            Vector2 anchoredPos,
            Vector2 size,
            string pillSpriteName,
            Color fallbackBgColor,
            float iconSize,
            float iconCenterX,
            float textLeftOffset,
            float fontSize,
            Color textColor,
            TMP_FontAsset sharedFont)
        {
            Transform pillTr = parentCard.Find(pillName);
            if (pillTr == null)
            {
                GameObject pillObj = new GameObject(pillName, typeof(RectTransform), typeof(Image));
                pillObj.transform.SetParent(parentCard, false);
                pillTr = pillObj.transform;
            }

            RectTransform pillRt = pillTr.GetComponent<RectTransform>();
            if (pillRt != null)
            {
                pillRt.anchorMin = new Vector2(0.5f, 0.5f);
                pillRt.anchorMax = new Vector2(0.5f, 0.5f);
                pillRt.pivot = new Vector2(0.5f, 0.5f);
                pillRt.anchoredPosition = anchoredPos;
                pillRt.sizeDelta = size;
            }

            Image pillBg = pillTr.GetComponent<Image>();
            if (pillBg == null)
            {
                pillBg = pillTr.gameObject.AddComponent<Image>();
            }
            GlassHudTheme.ApplySprite(pillBg, pillSpriteName);
            if (pillBg.sprite != null)
            {
                pillBg.type = Image.Type.Sliced;
                pillBg.color = Color.white;
            }
            else
            {
                pillBg.color = fallbackBgColor;
            }
            pillBg.raycastTarget = false;

            Transform iconTr = pillTr.Find("CoinIcon");
            if (iconTr == null)
            {
                GameObject iconObj = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(pillTr, false);
                iconTr = iconObj.transform;
            }

            RectTransform iconRt = iconTr.GetComponent<RectTransform>();
            if (iconRt != null)
            {
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = new Vector2(iconCenterX, 0f);
                iconRt.sizeDelta = new Vector2(iconSize, iconSize);
            }

            Image iconImg = iconTr.GetComponent<Image>();
            if (iconImg == null)
            {
                iconImg = iconTr.gameObject.AddComponent<Image>();
            }
            GlassHudTheme.ApplySprite(iconImg, "Icon_CoinXu.png", default, false, true);
            iconImg.raycastTarget = false;

            Transform txtTr = pillTr.Find(labelName);
            if (txtTr == null)
            {
                GameObject txtObj = new GameObject(labelName, typeof(RectTransform), typeof(TextMeshProUGUI));
                txtObj.transform.SetParent(pillTr, false);
                txtTr = txtObj.transform;
            }

            RectTransform txtRt = txtTr.GetComponent<RectTransform>();
            if (txtRt != null)
            {
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = new Vector2(textLeftOffset, 0f);
                txtRt.offsetMax = new Vector2(-14f, 0f);
            }

            TextMeshProUGUI tmp = txtTr.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                tmp = txtTr.gameObject.AddComponent<TextMeshProUGUI>();
            }
            if (sharedFont != null)
            {
                tmp.font = sharedFont;
            }
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 13f;
            tmp.fontSizeMax = fontSize + 1f;
            tmp.raycastTarget = false;
            GlassHudTheme.StyleTmpText(tmp, fontSize, textColor, true, true);

            return tmp;
        }

        private static void ConfigureFullScreenBackdrop(GameObject panel, Color backdropColor)
        {
            RectTransform panelRt = panel.GetComponent<RectTransform>();
            if (panelRt != null)
            {
                panelRt.anchorMin = Vector2.zero;
                panelRt.anchorMax = Vector2.one;
                panelRt.offsetMin = Vector2.zero;
                panelRt.offsetMax = Vector2.zero;
            }

            Image dimImg = panel.GetComponent<Image>();
            if (dimImg != null)
            {
                dimImg.sprite = null;
                dimImg.type = Image.Type.Simple;
                dimImg.color = backdropColor;
                dimImg.raycastTarget = true;
            }
        }

        private static Transform EnsureModalCard(
            Transform parentPanel,
            string cardName,
            Vector2 size,
            string spriteName,
            Color fallbackColor,
            out RectTransform cardRect,
            out CanvasGroup cardCanvasGroup)
        {
            Transform cardTr = parentPanel.Find(cardName);
            if (cardTr == null)
            {
                GameObject cardObj = new GameObject(cardName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                cardObj.transform.SetParent(parentPanel, false);
                cardObj.transform.SetAsFirstSibling();
                cardTr = cardObj.transform;
            }

            cardRect = cardTr.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.anchoredPosition = Vector2.zero;
                cardRect.sizeDelta = size;
            }

            RemoveFlatOutlines(cardTr.gameObject);

            Image cardImg = cardTr.GetComponent<Image>();
            if (cardImg == null)
            {
                cardImg = cardTr.gameObject.AddComponent<Image>();
            }
            GlassHudTheme.ApplySprite(cardImg, spriteName);
            if (cardImg.sprite != null)
            {
                cardImg.type = Image.Type.Sliced;
                cardImg.color = Color.white;
            }
            else
            {
                cardImg.color = fallbackColor;
            }
            cardImg.raycastTarget = true;

            cardCanvasGroup = cardTr.GetComponent<CanvasGroup>();
            if (cardCanvasGroup == null)
            {
                cardCanvasGroup = cardTr.gameObject.AddComponent<CanvasGroup>();
            }
            cardCanvasGroup.interactable = true;
            cardCanvasGroup.blocksRaycasts = true;

            return cardTr;
        }

        private static void ReparentIntoCard(Button btn, Transform cardTr)
        {
            if (btn == null || cardTr == null) return;
            if (btn.transform.parent != cardTr)
            {
                btn.transform.SetParent(cardTr, false);
            }
        }

        private static void StylePillButton(
            Button btn,
            string spriteFileName,
            string labelText,
            Vector2 anchoredPos,
            Vector2 size,
            float fontSize)
        {
            if (btn == null) return;

            RemoveFlatOutlines(btn.gameObject);

            RectTransform rt = btn.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = anchoredPos;
                rt.sizeDelta = size;
            }

            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                GlassHudTheme.ApplySprite(img, spriteFileName);
                if (img.sprite != null)
                {
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                }
                img.raycastTarget = true;
                btn.targetGraphic = img;
            }

            btn.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.93f, 0.97f, 1f, 1f);
            cb.pressedColor = new Color(0.76f, 0.81f, 0.88f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            TMP_Text label = btn.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = labelText;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;

                RectTransform labelRt = label.GetComponent<RectTransform>();
                if (labelRt != null)
                {
                    labelRt.anchorMin = Vector2.zero;
                    labelRt.anchorMax = Vector2.one;
                    labelRt.offsetMin = new Vector2(12f, 0f);
                    labelRt.offsetMax = new Vector2(-12f, 0f);
                }

                GlassHudTheme.StyleTmpText(label, fontSize, Color.white, true, true);
            }
        }

        private static void RemoveFlatOutlines(GameObject go)
        {
            if (go == null) return;
            Outline[] outlines = go.GetComponents<Outline>();
            for (int i = 0; i < outlines.Length; i++)
            {
                if (outlines[i] == null) continue;
                outlines[i].enabled = false;
                if (Application.isPlaying)
                {
                    Destroy(outlines[i]);
                }
                else
                {
                    DestroyImmediate(outlines[i]);
                }
            }
        }

        private static Transform FindDescendant(Transform root, string exactName)
        {
            if (root == null) return null;
            if (root.name == exactName) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(root.GetChild(i), exactName);
                if (found != null) return found;
            }
            return null;
        }

        private void StartPopInAnimation(ref Coroutine routineSlot, RectTransform cardRect, CanvasGroup cardCanvasGroup)
        {
            if (cardRect == null) return;

            StopCoroutineIfActive(ref routineSlot, cardRect, cardCanvasGroup);

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                ResetCardTransform(cardRect, cardCanvasGroup);
                return;
            }

            routineSlot = StartCoroutine(AnimateCardPopIn(cardRect, cardCanvasGroup));
        }

        private void StopAllPopInCoroutines()
        {
            StopCoroutineIfActive(ref victoryPopInCoroutine, victoryCardRect, victoryCardCanvasGroup);
            StopCoroutineIfActive(ref defeatPopInCoroutine, defeatCardRect, defeatCardCanvasGroup);
        }

        private void StopCoroutineIfActive(ref Coroutine routineSlot, RectTransform cardRect, CanvasGroup cardCanvasGroup)
        {
            if (routineSlot != null)
            {
                StopCoroutine(routineSlot);
                routineSlot = null;
            }
            ResetCardTransform(cardRect, cardCanvasGroup);
        }

        private static void ResetCardTransform(RectTransform cardRect, CanvasGroup cardCanvasGroup)
        {
            if (cardRect != null)
            {
                cardRect.localScale = Vector3.one;
            }
            if (cardCanvasGroup != null)
            {
                cardCanvasGroup.alpha = 1f;
            }
        }

        private static IEnumerator AnimateCardPopIn(RectTransform cardRect, CanvasGroup cardCanvasGroup)
        {
            if (cardRect == null) yield break;

            cardRect.localScale = new Vector3(POP_IN_START_SCALE, POP_IN_START_SCALE, 1f);
            if (cardCanvasGroup != null)
            {
                cardCanvasGroup.alpha = 0f;
            }

            float elapsed = 0f;
            while (elapsed < POP_IN_DURATION)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / POP_IN_DURATION);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float scale = Mathf.Lerp(POP_IN_START_SCALE, 1f, eased);

                if (cardRect != null)
                {
                    cardRect.localScale = new Vector3(scale, scale, 1f);
                }
                if (cardCanvasGroup != null)
                {
                    cardCanvasGroup.alpha = eased;
                }

                yield return null;
            }

            ResetCardTransform(cardRect, cardCanvasGroup);
        }
    }
}
