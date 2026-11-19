using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Economy;
using StickmanBeatEmUp.Enemies;
using StickmanBeatEmUp.Items;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Quản lý giao diện HUD (UI):
    /// - Thanh Máu Player & Giáp Ảo (Spinal Fluid) + Trạng thái Đỡ đòn [S]
    /// - Thanh Nộ 30 điểm chia làm 3 mốc (mỗi mốc 10 điểm)
    /// - Đèn báo kích hoạt Kỹ năng I (10 nộ), Biến hình (30 nộ) và Combo Đặc Biệt (Dash + I)
    /// - Thanh Máu Boss 2 Phase
    /// - Số Wave và số quái còn lại (Campaign, Endless, Training)
    /// </summary>
    public class GameUIManager : MonoBehaviour
    {
        public static GameUIManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        [Header("Player Health & Info")]
        [SerializeField] private Slider playerHealthSlider;
        [SerializeField] private TextMeshProUGUI playerHealthText;

        [Header("Shield & Block HUD (Optional)")]
        [SerializeField] private GameObject shieldBarContainer;
        [SerializeField] private Image shieldBarFill;
        [SerializeField] private TextMeshProUGUI shieldText;
        [SerializeField] private TextMeshProUGUI blockStatusText;

        [Header("3-Segment Rage Bar (30 Points Max)")]
        [Tooltip("3 Slider hoặc Image fill đại diện cho 3 mốc nộ (Mỗi mốc 10 điểm)")]
        [SerializeField] private Slider[] rageSegmentSliders; // Size 3
        [SerializeField] private TextMeshProUGUI ragePointsText;
        [SerializeField] private GameObject transformReadyPrompt;
        [SerializeField] private GameObject specialComboReadyPrompt;

        [Header("Boss Health Bar")]
        [SerializeField] private GameObject bossHealthPanel;
        [SerializeField] private Slider bossHealthSlider;
        [SerializeField] private TextMeshProUGUI bossNameText;
        [SerializeField] private TextMeshProUGUI bossStatusText;

        [Header("Level & Wave Progression")]
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private TextMeshProUGUI enemiesRemainingText;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;

        [Header("Mode & End Screen Overrides")]
        [SerializeField] private TextMeshProUGUI victoryTitleText;
        [SerializeField] private TextMeshProUGUI defeatTitleText;
        [SerializeField] private GameObject endlessWaveBanner;
        [SerializeField] private TextMeshProUGUI endlessBannerText;

        [Header("Modular HUD Widgets (Optional)")]
        [SerializeField] private HudItemSlotsUI hudItemSlots;
        [SerializeField] private CoinCounterUI coinCounterUI;
        [SerializeField] private PauseMenuUI pauseMenuUI;

        [Header("Navigation Buttons")]
        [SerializeField] private Button hudMenuButton;
        [SerializeField] private Button victoryNextButton;
        [SerializeField] private Button victoryRetryButton;
        [SerializeField] private Button victoryMenuButton;
        [SerializeField] private Button defeatRetryButton;
        [SerializeField] private Button defeatMenuButton;

        [Header("Typography")]
        [SerializeField] private Font vietnameseFont;

        [Header("08_BattleTopHUD Sprites")]
        [SerializeField] private Sprite waveBannerSprite;
        [SerializeField] private Sprite topRightClusterSprite;
        [SerializeField] private Sprite pauseButtonSprite;
        [SerializeField] private Sprite exitButtonSprite;
        [SerializeField] private Sprite pauseIconSprite;
        [SerializeField] private Sprite exitIconSprite;

        private TMP_FontAsset runtimeTmpFont;
        private bool ownsRuntimeTmpFont;
        private BaseCharacter activePlayer;
        private BossController activeBoss;
        private BossController boundLegacyBoss;
        private BossBase activeWorldBoss;
        private EnemyBase activeElite;
        private GameMode currentGameMode = GameMode.Campaign;
        private int latestEndlessWave = 1;

        public bool IsEndScreenOpen =>
            (victoryPanel != null && victoryPanel.activeSelf) ||
            (defeatPanel != null && defeatPanel.activeSelf);

        public GameMode CurrentGameMode => currentGameMode;
        public int LatestEndlessWave => latestEndlessWave;
        public HudItemSlotsUI HudItemSlots => hudItemSlots;
        public CoinCounterUI CoinCounter => coinCounterUI;
        public PauseMenuUI PauseMenu => pauseMenuUI;

        public void ConfigureExpandedHud(
            Image shieldFillImg,
            TextMeshProUGUI shieldTxt,
            TextMeshProUGUI blockTxt,
            GameObject shieldContainer = null,
            TextMeshProUGUI victoryTitle = null,
            TextMeshProUGUI defeatTitle = null,
            GameObject endlessBanner = null,
            TextMeshProUGUI endlessBannerTxt = null,
            HudItemSlotsUI itemSlots = null,
            CoinCounterUI coinUi = null,
            PauseMenuUI pauseUi = null)
        {
            shieldBarFill = shieldFillImg;
            shieldText = shieldTxt;
            blockStatusText = blockTxt;
            shieldBarContainer = shieldContainer;
            if (victoryTitle != null) victoryTitleText = victoryTitle;
            if (defeatTitle != null) defeatTitleText = defeatTitle;
            endlessWaveBanner = endlessBanner;
            endlessBannerText = endlessBannerTxt;
            if (itemSlots != null) hudItemSlots = itemSlots;
            if (coinUi != null) coinCounterUI = coinUi;
            if (pauseUi != null) pauseMenuUI = pauseUi;

            if (Application.isPlaying)
            {
                ApplyVietnameseTmpFont();
                ApplyTopClusterAndBannerTheme();
            }
            UpdateShieldBar(0f, 0f);
        }

        public void ConfigureModeHud(GameMode mode)
        {
            currentGameMode = mode;

            if (endlessWaveBanner != null)
            {
                endlessWaveBanner.SetActive(mode != GameMode.Training && !IsEndScreenOpen);
            }

            if (mode == GameMode.Endless)
            {
                if (victoryNextButton != null) victoryNextButton.gameObject.SetActive(false);
                if (waveText != null)
                {
                    waveText.text = $"VÔ TẬN — WAVE {Mathf.Max(1, latestEndlessWave)}";
                }
                if (enemiesRemainingText != null && string.IsNullOrEmpty(enemiesRemainingText.text))
                {
                    enemiesRemainingText.text = "Quái còn lại: 0";
                }
            }
            else if (mode == GameMode.Training)
            {
                if (victoryNextButton != null) victoryNextButton.gameObject.SetActive(false);
                if (endlessWaveBanner != null)
                {
                    endlessWaveBanner.SetActive(false);
                }
            }
            else
            {
                UpdateWaveDisplay(0, 0);
                if (enemiesRemainingText != null && string.IsNullOrEmpty(enemiesRemainingText.text))
                {
                    enemiesRemainingText.text = "Quái còn lại: 0";
                }
            }
        }

        public void UpdateEndlessWave(int wave, int enemiesRemaining = -1)
        {
            latestEndlessWave = Mathf.Max(1, wave);
            if (waveText != null)
            {
                waveText.text = $"VÔ TẬN — WAVE {latestEndlessWave}";
            }
            if (endlessWaveBanner != null && currentGameMode != GameMode.Training && !IsEndScreenOpen)
            {
                endlessWaveBanner.SetActive(true);
            }
            if (enemiesRemaining >= 0)
            {
                UpdateEnemiesRemaining(enemiesRemaining);
            }
        }

        public void HideEndlessWaveBanner()
        {
            if (endlessWaveBanner != null)
            {
                endlessWaveBanner.SetActive(false);
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            ResolveEndScreenTitleFallback();
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (defeatPanel != null) defeatPanel.SetActive(false);
            ApplyVietnameseTmpFont();
            WireNavigationButtons();
        }

        private void ResolveEndScreenTitleFallback()
        {
            if (victoryTitleText == null && victoryPanel != null)
            {
                Transform titleTr = victoryPanel.transform.Find("TitleText");
                if (titleTr != null) victoryTitleText = titleTr.GetComponent<TextMeshProUGUI>();
            }
            if (defeatTitleText == null && defeatPanel != null)
            {
                Transform titleTr = defeatPanel.transform.Find("TitleText");
                if (titleTr != null) defeatTitleText = titleTr.GetComponent<TextMeshProUGUI>();
            }
        }

        private void ApplyVietnameseTmpFont()
        {
            if (runtimeTmpFont == null)
            {
                TMP_FontAsset sharedTmp = GlassHudTheme.ResolveSharedTmpFont(this);
                if (sharedTmp != null &&
                    !string.Equals(sharedTmp.name, "LiberationSans SDF", System.StringComparison.OrdinalIgnoreCase))
                {
                    runtimeTmpFont = sharedTmp;
                    ownsRuntimeTmpFont = false;
                }
                else
                {
                    Font sourceFont = vietnameseFont != null
                        ? vietnameseFont
                        : Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 32);
                    if (sourceFont == null) return;

                    runtimeTmpFont = TMP_FontAsset.CreateFontAsset(sourceFont);
                    if (runtimeTmpFont == null) return;
                    ownsRuntimeTmpFont = true;
                }
            }

            TextMeshProUGUI[] allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < allTexts.Length; i++)
            {
                if (allTexts[i] != null)
                {
                    allTexts[i].font = runtimeTmpFont;
                }
            }
        }

        private void Start()
        {
            BindLevelManager();

            if (LevelManager.Instance != null && LevelManager.Instance.ActivePlayer != null)
            {
                BindPlayer(LevelManager.Instance.ActivePlayer);
            }
            else
            {
                FindAndBindPlayer();
            }

            ApplyTopClusterAndBannerTheme();
            WireNavigationButtons();

            if (bossHealthPanel != null) bossHealthPanel.SetActive(false);
            if (transformReadyPrompt != null) transformReadyPrompt.SetActive(false);
            if (specialComboReadyPrompt != null) specialComboReadyPrompt.SetActive(false);
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (defeatPanel != null) defeatPanel.SetActive(false);
            UpdateShieldBar(0f, 0f);

            GameMode selectedMode = StageManager.LoadSelectedGameMode();
            ConfigureModeHud(selectedMode);
            UpdateWaveDisplay(0, 0);
        }

        private void ApplyTopClusterAndBannerTheme()
        {
            CacheTopHudSpritesIfMissing();
            ApplyUnifiedWaveBannerTheme();
            ApplyTopRightClusterTheme();
            ApplyControlsHintStripAndToggle();
        }

        private void CacheTopHudSpritesIfMissing()
        {
            if (waveBannerSprite == null) waveBannerSprite = GlassHudTheme.LoadSprite("08_BattleTopHUD/Frame_HUD_StageWaveBanner_Clean.png");
            if (topRightClusterSprite == null) topRightClusterSprite = GlassHudTheme.LoadSprite("08_BattleTopHUD/Frame_HUD_TopRightCluster_Clean.png");
            if (pauseButtonSprite == null) pauseButtonSprite = GlassHudTheme.LoadSprite("08_BattleTopHUD/Btn_HUD_Pause_Clean.png");
            if (exitButtonSprite == null) exitButtonSprite = GlassHudTheme.LoadSprite("08_BattleTopHUD/Btn_HUD_ExitMatch_Clean.png");
            if (pauseIconSprite == null) pauseIconSprite = GlassHudTheme.LoadSprite("Icon_Pause_Bars.png");
            if (exitIconSprite == null) exitIconSprite = GlassHudTheme.LoadSprite("Icon_Exit_Door.png");
        }

        private void ApplyUnifiedWaveBannerTheme()
        {
            if (endlessWaveBanner == null)
            {
                Transform existingBanner = transform.Find("EndlessWaveBanner") ?? transform.Find("WaveBanner");
                if (existingBanner != null)
                {
                    endlessWaveBanner = existingBanner.gameObject;
                }
                else
                {
                    endlessWaveBanner = new GameObject("EndlessWaveBanner", typeof(RectTransform), typeof(Image));
                    endlessWaveBanner.transform.SetParent(transform, false);
                }
            }

            PlaceBeforeEndScreens(endlessWaveBanner.transform);

            RectTransform bannerRt = endlessWaveBanner.GetComponent<RectTransform>();
            if (bannerRt != null)
            {
                bannerRt.anchorMin = new Vector2(0.5f, 1f);
                bannerRt.anchorMax = new Vector2(0.5f, 1f);
                bannerRt.pivot = new Vector2(0.5f, 1f);
                bannerRt.anchoredPosition = new Vector2(0f, -10f);
                bannerRt.sizeDelta = new Vector2(540f, 82f);
            }

            Image bannerBg = GetOrAdd<Image>(endlessWaveBanner);
            bannerBg.color = Color.white;
            bannerBg.raycastTarget = false;
            Sprite bannerSp = waveBannerSprite != null
                ? waveBannerSprite
                : GlassHudTheme.LoadSprite("08_BattleTopHUD/Frame_HUD_StageWaveBanner_Clean.png");
            if (bannerSp != null)
            {
                GlassHudTheme.ApplySlicedSprite(bannerBg, bannerSp, 540f, 82f, Color.white);
            }
            else
            {
                GlassHudTheme.ApplySprite(bannerBg, "WaveBanner_TechGlass.png", default, true, false);
            }
            bannerBg.color = Color.white;

            Transform oldWavePanelTr = transform.Find("WaveInfoPanel");
            if (oldWavePanelTr == null &&
                waveText != null &&
                waveText.transform.parent != null &&
                waveText.transform.parent != endlessWaveBanner.transform &&
                waveText.transform.parent != transform)
            {
                oldWavePanelTr = waveText.transform.parent;
            }

            if (waveText == null)
            {
                Transform foundWave = oldWavePanelTr != null
                    ? oldWavePanelTr.Find("WaveText")
                    : endlessWaveBanner.transform.Find("WaveText");
                if (foundWave != null)
                {
                    waveText = foundWave.GetComponent<TextMeshProUGUI>();
                }
                if (waveText == null)
                {
                    GameObject waveObj = new GameObject("WaveText", typeof(RectTransform), typeof(TextMeshProUGUI));
                    waveObj.transform.SetParent(endlessWaveBanner.transform, false);
                    waveText = waveObj.GetComponent<TextMeshProUGUI>();
                }
            }

            if (enemiesRemainingText == null)
            {
                Transform foundEnemies = oldWavePanelTr != null
                    ? oldWavePanelTr.Find("EnemiesText")
                    : endlessWaveBanner.transform.Find("EnemiesText");
                if (foundEnemies != null)
                {
                    enemiesRemainingText = foundEnemies.GetComponent<TextMeshProUGUI>();
                }
                if (enemiesRemainingText == null)
                {
                    GameObject enemiesObj = new GameObject("EnemiesText", typeof(RectTransform), typeof(TextMeshProUGUI));
                    enemiesObj.transform.SetParent(endlessWaveBanner.transform, false);
                    enemiesRemainingText = enemiesObj.GetComponent<TextMeshProUGUI>();
                }
            }

            if (waveText.transform.parent != endlessWaveBanner.transform)
            {
                waveText.transform.SetParent(endlessWaveBanner.transform, false);
            }
            if (enemiesRemainingText.transform.parent != endlessWaveBanner.transform)
            {
                enemiesRemainingText.transform.SetParent(endlessWaveBanner.transform, false);
            }

            Transform legacyBannerTextTr = endlessWaveBanner.transform.Find("EndlessWaveBannerText");
            if (legacyBannerTextTr != null && legacyBannerTextTr != waveText.transform)
            {
                legacyBannerTextTr.gameObject.SetActive(false);
            }
            if (endlessBannerText != null && endlessBannerText != waveText)
            {
                endlessBannerText.gameObject.SetActive(false);
                endlessBannerText = waveText;
            }

            if (oldWavePanelTr != null && oldWavePanelTr != endlessWaveBanner.transform && oldWavePanelTr != transform)
            {
                oldWavePanelTr.gameObject.SetActive(false);
            }

            RectTransform wRt = waveText.rectTransform;
            wRt.anchorMin = new Vector2(0f, 0.46f);
            wRt.anchorMax = new Vector2(1f, 0.84f);
            wRt.pivot = new Vector2(0.5f, 0.5f);
            wRt.offsetMin = new Vector2(26f, 0f);
            wRt.offsetMax = new Vector2(-26f, 0f);
            waveText.gameObject.SetActive(true);
            waveText.alignment = TextAlignmentOptions.Center;
            waveText.textWrappingMode = TextWrappingModes.NoWrap;
            waveText.enableAutoSizing = true;
            waveText.fontSizeMin = 14f;
            waveText.fontSizeMax = 20.5f;
            waveText.raycastTarget = false;
            ApplyRuntimeFontFallback(waveText);
            GlassHudTheme.StyleTmpText(waveText, 20f, new Color(1f, 0.847f, 0.294f, 1f), true, true);

            RectTransform eRt = enemiesRemainingText.rectTransform;
            eRt.anchorMin = new Vector2(0f, 0.17f);
            eRt.anchorMax = new Vector2(1f, 0.46f);
            eRt.pivot = new Vector2(0.5f, 0.5f);
            eRt.offsetMin = new Vector2(26f, 0f);
            eRt.offsetMax = new Vector2(-26f, 0f);
            enemiesRemainingText.gameObject.SetActive(true);
            enemiesRemainingText.alignment = TextAlignmentOptions.Center;
            enemiesRemainingText.textWrappingMode = TextWrappingModes.NoWrap;
            enemiesRemainingText.enableAutoSizing = true;
            enemiesRemainingText.fontSizeMin = 12f;
            enemiesRemainingText.fontSizeMax = 16f;
            enemiesRemainingText.raycastTarget = false;
            ApplyRuntimeFontFallback(enemiesRemainingText);
            GlassHudTheme.StyleTmpText(enemiesRemainingText, 15.5f, new Color(1f, 0.502f, 0.259f, 1f), true, true);
        }

        private void ApplyTopRightClusterTheme()
        {
            Transform clusterBgTr = transform.Find("TopRightClusterBg");
            if (clusterBgTr == null)
            {
                GameObject bgObj = new GameObject("TopRightClusterBg", typeof(RectTransform), typeof(Image));
                bgObj.transform.SetParent(transform, false);
                clusterBgTr = bgObj.transform;
            }
            clusterBgTr.SetAsFirstSibling();

            RectTransform clusterRt = clusterBgTr.GetComponent<RectTransform>();
            clusterRt.anchorMin = new Vector2(1f, 1f);
            clusterRt.anchorMax = new Vector2(1f, 1f);
            clusterRt.pivot = new Vector2(1f, 1f);
            clusterRt.anchoredPosition = new Vector2(-14f, -8f);
            clusterRt.sizeDelta = new Vector2(466f, 72f);

            Image clusterImg = GetOrAdd<Image>(clusterBgTr.gameObject);
            clusterImg.color = Color.white;
            clusterImg.raycastTarget = false;
            Sprite clusterSp = topRightClusterSprite != null
                ? topRightClusterSprite
                : GlassHudTheme.LoadSprite("08_BattleTopHUD/Frame_HUD_TopRightCluster_Clean.png");
            if (clusterSp != null)
            {
                GlassHudTheme.ApplySlicedSprite(clusterImg, clusterSp, 466f, 72f, Color.white);
            }
            else
            {
                GlassHudTheme.ApplySprite(clusterImg, "TopRight_InfoCluster_Glass.png", default, true, false);
            }

            GameObject coinBadgeObj = coinCounterUI != null
                ? coinCounterUI.gameObject
                : transform.Find("BattleCoinBadge")?.gameObject;
            if (coinBadgeObj != null)
            {
                if (coinCounterUI != null)
                {
                    if (coinCounterUI.CoinLabel != null)
                    {
                        ApplyRuntimeFontFallback(coinCounterUI.CoinLabel);
                    }
                    coinCounterUI.ApplyBattleGlassTheme();
                    coinCounterUI.RefreshImmediate();
                }
                else
                {
                    RectTransform cRt = coinBadgeObj.GetComponent<RectTransform>();
                    cRt.anchorMin = new Vector2(1f, 1f);
                    cRt.anchorMax = new Vector2(1f, 1f);
                    cRt.pivot = new Vector2(1f, 1f);
                    cRt.anchoredPosition = new Vector2(-308f, -22f);
                    cRt.sizeDelta = new Vector2(156f, 44f);

                    Image cBg = GetOrAdd<Image>(coinBadgeObj);
                    cBg.raycastTarget = false;
                    Sprite coinBadgeSp = GlassHudTheme.LoadSprite("08_BattleTopHUD/Badge_HUD_InGameCoin_Clean.png");
                    if (coinBadgeSp != null)
                    {
                        GlassHudTheme.ApplySlicedSprite(cBg, coinBadgeSp, 156f, 44f, Color.white);
                    }
                    else
                    {
                        GlassHudTheme.ApplySprite(cBg, "CurrencyBadge_GoldGlass.png", default, true, false);
                    }

                    Transform iconTr = coinBadgeObj.transform.Find("CoinIcon");
                    if (iconTr != null && iconTr.TryGetComponent(out Image iconImg))
                    {
                        RectTransform iconRt = iconImg.rectTransform;
                        iconRt.anchorMin = new Vector2(0f, 0.5f);
                        iconRt.anchorMax = new Vector2(0f, 0.5f);
                        iconRt.pivot = new Vector2(0.5f, 0.5f);
                        iconRt.anchoredPosition = new Vector2(22f, 0f);
                        iconRt.sizeDelta = new Vector2(24f, 24f);
                        iconImg.color = Color.white;
                        iconImg.preserveAspect = true;
                        iconImg.raycastTarget = false;
                        Sprite xuSprite = GlassHudTheme.LoadSprite("02_ModeSelect/Icon_Currency_CoinXu.png")
                            ?? GlassHudTheme.LoadSprite("Icon_CoinXu.png")
                            ?? UIManager.LoadItemIconSprite("Icon_CoinXu.png");
                        if (xuSprite != null) iconImg.sprite = xuSprite;
                    }

                    TMP_Text coinTmp = coinBadgeObj.GetComponentInChildren<TMP_Text>(true);
                    if (coinTmp != null)
                    {
                        RectTransform lblRt = coinTmp.rectTransform;
                        lblRt.anchorMin = Vector2.zero;
                        lblRt.anchorMax = Vector2.one;
                        lblRt.offsetMin = new Vector2(38f, 0f);
                        lblRt.offsetMax = new Vector2(-12f, 0f);
                        coinTmp.alignment = TextAlignmentOptions.MidlineRight;
                        coinTmp.textWrappingMode = TextWrappingModes.NoWrap;
                        coinTmp.enableAutoSizing = true;
                        coinTmp.fontSizeMin = 12f;
                        coinTmp.fontSizeMax = 18.5f;
                        coinTmp.raycastTarget = false;
                        ApplyRuntimeFontFallback(coinTmp);
                        GlassHudTheme.StyleTmpText(coinTmp, 18f, new Color(1f, 0.847f, 0.302f, 1f), true, true);
                    }
                }
            }

            Transform pauseTr = transform.Find("PauseHudButton");
            if (pauseTr != null)
            {
                ApplyTopRightPillButton(
                    pauseTr,
                    pauseButtonSprite,
                    "08_BattleTopHUD/Btn_HUD_Pause_Clean.png",
                    144f,
                    new Vector2(-228f, -44f),
                    pauseIconSprite,
                    "Icon_Pause_Bars.png",
                    "TẠM DỪNG");
            }

            if (hudMenuButton == null)
            {
                Transform menuTr = transform.Find("HudMenuButton");
                if (menuTr != null) hudMenuButton = menuTr.GetComponent<Button>();
            }
            if (hudMenuButton != null)
            {
                ApplyTopRightPillButton(
                    hudMenuButton.transform,
                    exitButtonSprite,
                    "08_BattleTopHUD/Btn_HUD_ExitMatch_Clean.png",
                    116f,
                    new Vector2(-90f, -44f),
                    exitIconSprite,
                    "Icon_Exit_Door.png",
                    "THOÁT");
            }
        }

        private void ApplyTopRightPillButton(
            Transform btnTr,
            Sprite explicitBgSprite,
            string bgFileName,
            float width,
            Vector2 centeredPos,
            Sprite explicitIconSprite,
            string iconFileName,
            string labelStr)
        {
            RectTransform rt = btnTr.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = centeredPos;
                rt.sizeDelta = new Vector2(width, 44f);
            }

            Image bgImg = GetOrAdd<Image>(btnTr.gameObject);
            bgImg.color = Color.white;
            bgImg.raycastTarget = true;
            Sprite resolvedBg = explicitBgSprite != null ? explicitBgSprite : GlassHudTheme.LoadSprite(bgFileName);
            if (resolvedBg != null)
            {
                GlassHudTheme.ApplySlicedSprite(bgImg, resolvedBg, width, 44f, Color.white);
            }
            else
            {
                GlassHudTheme.ApplySprite(bgImg, bgFileName, default, true, false);
            }

            Transform iconTr = btnTr.Find("Icon");
            if (iconTr == null)
            {
                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(btnTr, false);
                iconTr = iconObj.transform;
            }

            RectTransform iconRt = iconTr.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(18f, 0f);
            iconRt.sizeDelta = new Vector2(16f, 16f);

            Image iconImg = GetOrAdd<Image>(iconTr.gameObject);
            iconImg.color = Color.white;
            iconImg.raycastTarget = false;
            Sprite resolvedIcon = explicitIconSprite != null ? explicitIconSprite : GlassHudTheme.LoadSprite(iconFileName);
            if (resolvedIcon != null)
            {
                GlassHudTheme.ApplySprite(iconImg, resolvedIcon, false, true);
            }
            else
            {
                GlassHudTheme.ApplySprite(iconImg, iconFileName, default, false, true);
            }

            TMP_Text lblTmp = btnTr.GetComponentInChildren<TMP_Text>(true);
            if (lblTmp != null)
            {
                lblTmp.text = labelStr;
                RectTransform lblRt = lblTmp.rectTransform;
                lblRt.anchorMin = Vector2.zero;
                lblRt.anchorMax = Vector2.one;
                lblRt.offsetMin = new Vector2(26f, 0f);
                lblRt.offsetMax = new Vector2(-8f, 0f);
                lblTmp.alignment = TextAlignmentOptions.Center;
                lblTmp.textWrappingMode = TextWrappingModes.NoWrap;
                lblTmp.enableAutoSizing = true;
                lblTmp.fontSizeMin = 11f;
                lblTmp.fontSizeMax = 15.5f;
                lblTmp.raycastTarget = false;
                ApplyRuntimeFontFallback(lblTmp);
                GlassHudTheme.StyleTmpText(lblTmp, 15f, Color.white, true, true);
            }
        }

        private void ApplyControlsHintStripAndToggle()
        {
            Transform hintTr = transform.Find("ControlsHint");
            if (hintTr == null)
            {
                GameObject hintObj = new GameObject("ControlsHint", typeof(RectTransform), typeof(Image));
                hintObj.transform.SetParent(transform, false);
                hintTr = hintObj.transform;
            }
            PlaceBeforeEndScreens(hintTr);

            RectTransform hintRt = hintTr.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0.5f, 0f);
            hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.anchoredPosition = new Vector2(64f, 12f);
            hintRt.sizeDelta = new Vector2(1380f, 38f);

            Image hintBg = GetOrAdd<Image>(hintTr.gameObject);
            hintBg.color = Color.white;
            hintBg.raycastTarget = false;
            GlassHudTheme.ApplySprite(hintBg, "ControlHints_PillStrip.png", default, true, false);
            hintBg.color = Color.white;

            TMP_Text hintTmp = hintTr.GetComponentInChildren<TMP_Text>(true);
            if (hintTmp == null)
            {
                GameObject txtObj = new GameObject("ControlsHintText", typeof(RectTransform), typeof(TextMeshProUGUI));
                txtObj.transform.SetParent(hintTr, false);
                hintTmp = txtObj.GetComponent<TextMeshProUGUI>();
            }

            RectTransform tRt = hintTmp.rectTransform;
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(16f, 2f);
            tRt.offsetMax = new Vector2(-16f, -2f);
            hintTmp.alignment = TextAlignmentOptions.Center;
            hintTmp.richText = true;
            hintTmp.raycastTarget = false;
            hintTmp.text = "<color=#FFD84D>[A/D]</color> Di chuyển   •   <color=#FFD84D>[2x A/D]</color> Lướt   •   <color=#FFD84D>[W/Space]</color> Nhảy   •   <color=#FFD84D>[S+Space]</color> Rơi thềm   •   <color=#FFD84D>[J]</color> Đánh   •   <color=#FFD84D>[S]</color> Khiên   •   <color=#FFD84D>[U/I]</color> Kỹ năng   •   <color=#FFD84D>[O]</color> Biến hình   •   <color=#FFD84D>[Q/E]</color> Vật phẩm";
            ApplyRuntimeFontFallback(hintTmp);
            GlassHudTheme.StyleTmpText(hintTmp, 14.5f, Color.white, true, true);

            Transform toggleTr = transform.Find("ControlsHintToggleButton");
            if (toggleTr == null)
            {
                GameObject toggleObj = new GameObject("ControlsHintToggleButton", typeof(RectTransform), typeof(Image), typeof(Button));
                toggleObj.transform.SetParent(transform, false);
                toggleTr = toggleObj.transform;
            }
            PlaceBeforeEndScreens(toggleTr);

            RectTransform tgRt = toggleTr.GetComponent<RectTransform>();
            tgRt.anchorMin = new Vector2(1f, 0f);
            tgRt.anchorMax = new Vector2(1f, 0f);
            tgRt.pivot = new Vector2(0.5f, 0.5f);
            tgRt.anchoredPosition = new Vector2(-39f, 31f);
            tgRt.sizeDelta = new Vector2(38f, 38f);

            Image tgImg = GetOrAdd<Image>(toggleTr.gameObject);
            tgImg.color = Color.white;
            tgImg.raycastTarget = true;
            GlassHudTheme.ApplySprite(tgImg, "Keycap_Badge_Dark.png", default, true, false);

            Button tgBtn = GetOrAdd<Button>(toggleTr.gameObject);
            tgBtn.targetGraphic = tgImg;

            TMP_Text tgLbl = toggleTr.GetComponentInChildren<TMP_Text>(true);
            if (tgLbl == null)
            {
                GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                lblObj.transform.SetParent(toggleTr, false);
                tgLbl = lblObj.GetComponent<TextMeshProUGUI>();
            }
            RectTransform tgLblRt = tgLbl.rectTransform;
            tgLblRt.anchorMin = Vector2.zero;
            tgLblRt.anchorMax = Vector2.one;
            tgLblRt.offsetMin = Vector2.zero;
            tgLblRt.offsetMax = Vector2.zero;
            tgLbl.text = "?";
            tgLbl.alignment = TextAlignmentOptions.Center;
            tgLbl.raycastTarget = false;
            ApplyRuntimeFontFallback(tgLbl);
            GlassHudTheme.StyleTmpText(tgLbl, 18f, new Color(1f, 0.847f, 0.302f, 1f), true, true);

            GameObject hintGo = hintTr.gameObject;
            tgBtn.onClick.RemoveAllListeners();
            tgBtn.onClick.AddListener(() =>
            {
                if (hintGo != null)
                {
                    hintGo.SetActive(!hintGo.activeSelf);
                }
            });
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            if (go == null) return null;
            return go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();
        }

        private void ApplyRuntimeFontFallback(TMP_Text tmp)
        {
            if (tmp != null && runtimeTmpFont != null)
            {
                tmp.font = runtimeTmpFont;
            }
        }

        private void PlaceBeforeEndScreens(Transform hudElement)
        {
            if (hudElement == null || hudElement.parent != transform) return;
            int targetIndex = transform.childCount - 1;
            if (victoryPanel != null && victoryPanel.transform.parent == transform)
            {
                targetIndex = Mathf.Min(targetIndex, victoryPanel.transform.GetSiblingIndex());
            }
            if (defeatPanel != null && defeatPanel.transform.parent == transform)
            {
                targetIndex = Mathf.Min(targetIndex, defeatPanel.transform.GetSiblingIndex());
            }
            Transform pauseModalTr = transform.Find("PauseModalPanel");
            if (pauseModalTr != null)
            {
                targetIndex = Mathf.Min(targetIndex, pauseModalTr.GetSiblingIndex());
            }
            if (hudElement.GetSiblingIndex() > targetIndex)
            {
                hudElement.SetSiblingIndex(Mathf.Max(0, targetIndex));
            }
        }

        private void Update()
        {
            UpdateBlockStatusHud();
        }

        private void OnDestroy()
        {
            UnbindCurrentPlayer();

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnPlayerSpawned -= BindPlayer;
                LevelManager.Instance.OnEliteSpawned -= BindElite;
                LevelManager.Instance.OnBossSpawned -= HandleBossSpawned;
                LevelManager.Instance.OnWorldBossSpawned -= BindWorldBoss;
                LevelManager.Instance.OnWaveChanged -= UpdateWaveDisplay;
                LevelManager.Instance.OnEnemiesRemainingChanged -= UpdateEnemiesRemaining;
                LevelManager.Instance.OnStageCompleted -= ShowVictory;
                LevelManager.Instance.OnStageFailed -= ShowDefeat;
            }
            UnbindWorldBoss();
            UnbindElite();
            UnbindLegacyBoss();
            if (Instance == this)
            {
                Instance = null;
            }
            if (runtimeTmpFont != null)
            {
                if (ownsRuntimeTmpFont)
                {
                    Destroy(runtimeTmpFont);
                }
                runtimeTmpFont = null;
                ownsRuntimeTmpFont = false;
            }
        }

        private void WireNavigationButtons()
        {
            if (hudMenuButton != null)
            {
                hudMenuButton.onClick.RemoveAllListeners();
                hudMenuButton.onClick.AddListener(OnClickReturnToMenu);
            }
            if (victoryNextButton != null)
            {
                victoryNextButton.onClick.RemoveAllListeners();
                victoryNextButton.onClick.AddListener(OnClickNextStage);
            }
            if (victoryRetryButton != null)
            {
                victoryRetryButton.onClick.RemoveAllListeners();
                victoryRetryButton.onClick.AddListener(OnClickRetry);
            }
            if (victoryMenuButton != null)
            {
                victoryMenuButton.onClick.RemoveAllListeners();
                victoryMenuButton.onClick.AddListener(OnClickReturnToMenu);
            }
            if (defeatRetryButton != null)
            {
                defeatRetryButton.onClick.RemoveAllListeners();
                defeatRetryButton.onClick.AddListener(OnClickRetry);
            }
            if (defeatMenuButton != null)
            {
                defeatMenuButton.onClick.RemoveAllListeners();
                defeatMenuButton.onClick.AddListener(OnClickReturnToMenu);
            }
        }

        private void FindAndBindPlayer()
        {
            BindPlayer(FindAnyObjectByType<BaseCharacter>());
        }

        private void BindPlayer(BaseCharacter player)
        {
            UnbindCurrentPlayer();
            activePlayer = player;
            if (activePlayer != null)
            {
                activePlayer.OnHealthChanged += UpdatePlayerHealth;
                activePlayer.OnShieldChanged += UpdateShieldBar;
                activePlayer.OnTransformedChanged += HandlePlayerTransformed;
                if (activePlayer.RageManager != null)
                {
                    activePlayer.RageManager.OnRageChanged += UpdateRageBar;
                }

                UpdatePlayerHealth(activePlayer.CurrentHealth, activePlayer.MaxHealth);
                float maxShield = activePlayer.HasSpinalFluidBuff ? Mathf.Max(activePlayer.VirtualShieldHP, 40f) : 0f;
                UpdateShieldBar(activePlayer.VirtualShieldHP, maxShield);

                if (activePlayer.RageManager != null)
                {
                    UpdateRageBar(activePlayer.RageManager.CurrentRage, RageManager.MAX_RAGE);
                }

                if (hudItemSlots != null)
                {
                    hudItemSlots.BindPlayer(activePlayer);
                }
                if (pauseMenuUI != null)
                {
                    pauseMenuUI.BindInputHandler(activePlayer.GetComponent<InputHandler>());
                }
            }
            UpdateBlockStatusHud();
        }

        private void UnbindCurrentPlayer()
        {
            if (activePlayer != null)
            {
                activePlayer.OnHealthChanged -= UpdatePlayerHealth;
                activePlayer.OnShieldChanged -= UpdateShieldBar;
                activePlayer.OnTransformedChanged -= HandlePlayerTransformed;
                if (activePlayer.RageManager != null)
                {
                    activePlayer.RageManager.OnRageChanged -= UpdateRageBar;
                }
                activePlayer = null;
            }
        }

        private void HandlePlayerTransformed(bool transformed)
        {
            if (activePlayer != null && activePlayer.RageManager != null)
            {
                UpdateRageBar(activePlayer.RageManager.CurrentRage, RageManager.MAX_RAGE);
            }
        }

        private void BindLevelManager()
        {
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnPlayerSpawned += BindPlayer;
                LevelManager.Instance.OnEliteSpawned += BindElite;
                LevelManager.Instance.OnBossSpawned += HandleBossSpawned;
                LevelManager.Instance.OnWorldBossSpawned += BindWorldBoss;
                LevelManager.Instance.OnWaveChanged += UpdateWaveDisplay;
                LevelManager.Instance.OnEnemiesRemainingChanged += UpdateEnemiesRemaining;
                LevelManager.Instance.OnStageCompleted += ShowVictory;
                LevelManager.Instance.OnStageFailed += ShowDefeat;
            }
        }

        public void BindElite(EnemyBase elite)
        {
            if (elite == null || activeWorldBoss != null || activeBoss != null) return;
            UnbindElite();

            activeElite = elite;
            if (bossHealthPanel != null) bossHealthPanel.SetActive(true);
            string eliteName = elite.Data != null ? elite.Data.enemyName : elite.gameObject.name.Replace("(Clone)", "").Trim();
            if (bossNameText != null) bossNameText.text = $"TINH ANH: {eliteName.ToUpper()}";
            if (bossStatusText != null) bossStatusText.text = "KẺ THÁCH THỨC TINH ANH";

            float maxHp = elite.MaxHealth;
            float curHp = elite.CurrentHealth > 0f ? elite.CurrentHealth : maxHp;
            if (bossHealthSlider != null)
            {
                bossHealthSlider.maxValue = maxHp;
                bossHealthSlider.value = curHp;
            }

            elite.OnHealthChanged += HandleEliteHealthChanged;
            elite.OnDied += HandleEliteDefeated;
        }

        private void UnbindElite()
        {
            if (activeElite == null) return;
            activeElite.OnHealthChanged -= HandleEliteHealthChanged;
            activeElite.OnDied -= HandleEliteDefeated;
            activeElite = null;
        }

        private void HandleEliteHealthChanged(float hp, float max)
        {
            if (activeWorldBoss != null) return;
            if (bossHealthSlider != null)
            {
                bossHealthSlider.maxValue = max;
                bossHealthSlider.value = hp;
            }
        }

        private void HandleEliteDefeated(EnemyBase elite)
        {
            if (activeWorldBoss == null && bossHealthPanel != null) bossHealthPanel.SetActive(false);
            if (activeWorldBoss == null && bossStatusText != null) bossStatusText.text = string.Empty;
            UnbindElite();
        }

        private void BindWorldBoss(BossBase boss)
        {
            UnbindElite();
            UnbindWorldBoss();
            if (boss == null) return;

            activeWorldBoss = boss;
            if (bossHealthPanel != null) bossHealthPanel.SetActive(true);
            if (bossNameText != null) bossNameText.text = $"TRÙM THẾ GIỚI: {boss.BossName.ToUpper()}";
            if (bossStatusText != null) bossStatusText.text = string.Empty;

            float maxHp = boss.MaxHealth;
            float curHp = boss.CurrentHealth > 0f ? boss.CurrentHealth : maxHp;
            if (bossHealthSlider != null)
            {
                bossHealthSlider.maxValue = maxHp;
                bossHealthSlider.value = curHp;
            }

            boss.OnHealthChanged += HandleWorldBossHealthChanged;
            boss.OnStatusChanged += HandleWorldBossStatusChanged;
            boss.OnBossDefeated += HandleWorldBossDefeated;
        }

        private void UnbindWorldBoss()
        {
            if (activeWorldBoss == null) return;
            activeWorldBoss.OnHealthChanged -= HandleWorldBossHealthChanged;
            activeWorldBoss.OnStatusChanged -= HandleWorldBossStatusChanged;
            activeWorldBoss.OnBossDefeated -= HandleWorldBossDefeated;
            activeWorldBoss = null;
        }

        private void HandleWorldBossHealthChanged(float hp, float max)
        {
            if (bossHealthSlider != null)
            {
                bossHealthSlider.maxValue = max;
                bossHealthSlider.value = hp;
            }
        }

        private void HandleWorldBossStatusChanged(string msg)
        {
            if (bossStatusText != null) bossStatusText.text = msg;
        }

        private void HandleWorldBossDefeated(BossBase boss)
        {
            if (bossHealthPanel != null) bossHealthPanel.SetActive(false);
            if (bossStatusText != null) bossStatusText.text = string.Empty;
            UnbindWorldBoss();
        }

        private void HandleBossSpawned(BaseEnemy enemy)
        {
            BossController boss = enemy as BossController;
            if (boss != null)
            {
                BindBoss(boss);
            }
        }

        private void UpdatePlayerHealth(float current, float max)
        {
            if (playerHealthSlider != null)
            {
                playerHealthSlider.maxValue = max;
                playerHealthSlider.value = current;
            }

            if (playerHealthText != null)
            {
                playerHealthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }

        private void UpdateShieldBar(float current, float max)
        {
            bool hasShield = current > 0.01f && max > 0.01f;
            if (shieldBarContainer != null)
            {
                shieldBarContainer.SetActive(hasShield);
            }

            if (shieldBarFill != null)
            {
                shieldBarFill.fillAmount = hasShield ? Mathf.Clamp01(current / max) : 0f;
            }

            if (shieldText != null)
            {
                shieldText.text = hasShield
                    ? $"GIÁP ẢO: {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}"
                    : string.Empty;
            }
        }

        private void UpdateBlockStatusHud()
        {
            if (PlayerVitalsHudUI.Instance != null && PlayerVitalsHudUI.Instance.isActiveAndEnabled) return;
            if (blockStatusText == null || activePlayer == null) return;

            if (activePlayer.IsBlocking)
            {
                blockStatusText.text = "[S] ĐANG ĐỠ (-80% DMG)";
                blockStatusText.color = new Color(0.35f, 0.95f, 1f, 1f);
            }
            else if (activePlayer.BlockCooldownRemaining > 0.05f)
            {
                blockStatusText.text = $"[S] ĐỠ: {activePlayer.BlockCooldownRemaining:0.0}s";
                blockStatusText.color = new Color(0.85f, 0.85f, 0.88f, 0.85f);
            }
            else
            {
                blockStatusText.text = "[S] ĐỠ: SẴN SÀNG";
                blockStatusText.color = new Color(0.45f, 1f, 0.6f, 1f);
            }
        }

        /// <summary>
        /// Cập nhật thanh nộ 30 điểm chia đều trên 3 đoạn (mỗi đoạn 10 điểm)
        /// </summary>
        private void UpdateRageBar(int currentRage, int maxRage)
        {
            float rageFloat = (activePlayer != null && activePlayer.RageManager != null)
                ? activePlayer.RageManager.CurrentRageFloat
                : currentRage;

            if (ragePointsText != null)
            {
                string rageStr = Mathf.Abs(rageFloat - Mathf.Round(rageFloat)) > 0.05f
                    ? rageFloat.ToString("0.0")
                    : Mathf.RoundToInt(rageFloat).ToString();
                ragePointsText.text = $"NỘ: {rageStr} / {maxRage}";
            }

            // Cập nhật 3 segment (Segment 0: 0..10, Segment 1: 10..20, Segment 2: 20..30)
            if (rageSegmentSliders != null && rageSegmentSliders.Length >= 3)
            {
                for (int i = 0; i < 3; i++)
                {
                    float segmentMin = i * 10f;
                    float segmentFill = Mathf.Clamp(rageFloat - segmentMin, 0f, 10f);
                    rageSegmentSliders[i].maxValue = 10f;
                    rageSegmentSliders[i].value = segmentFill;
                }
            }

            // Hiển thị gợi ý phím khi đầy 30 nộ
            bool isFull = rageFloat >= 30f;
            if (transformReadyPrompt != null && activePlayer != null)
            {
                transformReadyPrompt.SetActive(isFull && !activePlayer.IsTransformed);
            }

            if (specialComboReadyPrompt != null && activePlayer != null)
            {
                specialComboReadyPrompt.SetActive(isFull && activePlayer.IsTransformed);
            }
        }

        public void BindBoss(BossController boss)
        {
            UnbindLegacyBoss();
            if (boss == null) return;

            activeBoss = boss;
            boundLegacyBoss = boss;
            if (bossHealthPanel != null) bossHealthPanel.SetActive(true);

            if (bossNameText != null)
            {
                bossNameText.text = $"TRÙM THẾ GIỚI: {boss.gameObject.name.Replace("(Clone)", "").Trim().ToUpper()}";
            }

            if (bossHealthSlider != null)
            {
                bossHealthSlider.maxValue = boss.MaxHealth;
                bossHealthSlider.value = boss.CurrentHealth;
            }

            boss.OnHealthChangedEvent += HandleLegacyBossHealthChanged;
            boss.OnDeathEvent += HandleLegacyBossDeath;
        }

        private void UnbindLegacyBoss()
        {
            if (boundLegacyBoss == null) return;
            boundLegacyBoss.OnHealthChangedEvent -= HandleLegacyBossHealthChanged;
            boundLegacyBoss.OnDeathEvent -= HandleLegacyBossDeath;
            boundLegacyBoss = null;
        }

        private void HandleLegacyBossHealthChanged(float hp, float max)
        {
            if (bossHealthSlider != null)
            {
                bossHealthSlider.maxValue = max;
                bossHealthSlider.value = hp;
            }
        }

        private void HandleLegacyBossDeath(BaseEnemy b)
        {
            if (activeBoss == b) activeBoss = null;
            if (bossHealthPanel != null) bossHealthPanel.SetActive(false);
            UnbindLegacyBoss();
        }

        public void UnbindBoss()
        {
            UnbindWorldBoss();
            UnbindElite();
            UnbindLegacyBoss();
            activeBoss = null;
            if (bossHealthPanel != null)
            {
                bossHealthPanel.SetActive(false);
            }
            if (bossStatusText != null)
            {
                bossStatusText.text = string.Empty;
            }
        }

        private static int GetCampaignStageNumber(StageDataSO st)
        {
            return st != null ? Mathf.Max(1, st.stageNumber) : 1;
        }

        private void UpdateWaveDisplay(int currentWave, int totalWaves)
        {
            if (currentGameMode == GameMode.Endless)
            {
                if (currentWave > 0)
                {
                    UpdateEndlessWave(currentWave);
                }
                else
                {
                    if (endlessWaveBanner != null && !IsEndScreenOpen)
                    {
                        endlessWaveBanner.SetActive(true);
                    }
                    if (waveText != null)
                    {
                        waveText.text = $"VÔ TẬN — WAVE {Mathf.Max(1, latestEndlessWave)}";
                    }
                }
                return;
            }

            if (currentGameMode == GameMode.Training)
            {
                if (endlessWaveBanner != null)
                {
                    endlessWaveBanner.SetActive(false);
                }
                return;
            }

            if (endlessWaveBanner != null && !IsEndScreenOpen)
            {
                endlessWaveBanner.SetActive(true);
            }

            if (waveText == null) return;
            StageDataSO st = LevelManager.Instance != null ? LevelManager.Instance.CurrentStageData : null;
            int stageNum = GetCampaignStageNumber(st);
            if (currentWave <= 0)
            {
                waveText.text = $"CHIẾN DỊCH - MÀN {stageNum} | CHUẨN BỊ";
            }
            else if (st != null && st.isBossStage && st.waves != null && currentWave > st.waves.Count)
            {
                waveText.text = $"CHIẾN DỊCH - MÀN {stageNum} | ĐẠI CHIẾN BOSS";
            }
            else
            {
                waveText.text = $"CHIẾN DỊCH - MÀN {stageNum} | WAVE {currentWave}/{Mathf.Max(1, totalWaves)}";
            }
        }

        private void UpdateEnemiesRemaining(int count)
        {
            if (currentGameMode == GameMode.Training) return;

            if (enemiesRemainingText != null)
            {
                enemiesRemainingText.text = $"Quái còn lại: {Mathf.Max(0, count)}";
            }
        }

        private void ShowVictory()
        {
            if (currentGameMode == GameMode.Endless || currentGameMode == GameMode.Training)
            {
                return;
            }

            StageDataSO st = LevelManager.Instance != null ? LevelManager.Instance.CurrentStageData : null;
            bool isStage4Final = st != null && (st.stageNumber >= 4 || st.isBossStage);
            if (victoryNextButton != null)
            {
                victoryNextButton.gameObject.SetActive(!isStage4Final);
            }
            if (victoryTitleText != null)
            {
                victoryTitleText.text = "CHIẾN THẮNG!";
            }
            if (victoryPanel != null) victoryPanel.SetActive(true);
        }

        private void ShowDefeat()
        {
            ResolveEndScreenTitleFallback();
            HideEndlessWaveBanner();
            UnbindBoss();

            if (defeatTitleText != null)
            {
                defeatTitleText.gameObject.SetActive(true);
                defeatTitleText.text = "THẤT BẠI";
            }

            ResetDefeatButtonsToNormalPositions();

            if (defeatPanel != null) defeatPanel.SetActive(true);
        }

        private void ResetDefeatButtonsToNormalPositions()
        {
            if (defeatPanel == null) return;

            ResetDefeatButtonYIfCentered(defeatRetryButton);
            ResetDefeatButtonYIfCentered(defeatMenuButton);
        }

        private void ResetDefeatButtonYIfCentered(Button button)
        {
            if (button == null || defeatPanel == null) return;
            RectTransform rt = button.GetComponent<RectTransform>();
            if (rt == null || rt.parent != defeatPanel.transform) return;

            if (Mathf.Approximately(rt.anchoredPosition.y, 0f))
            {
                Vector2 pos = rt.anchoredPosition;
                pos.y = -70f;
                rt.anchoredPosition = pos;
            }
        }

        public void OnClickRetry()
        {
            Time.timeScale = 1f;
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.RetryCurrentStage();
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        public void OnClickNextStage()
        {
            Time.timeScale = 1f;
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.LoadNextStage();
            }
        }

        public void OnClickReturnToMenu()
        {
            Time.timeScale = 1f;

            if (CurrencyManager.HasInstance)
            {
                CurrencyManager.Instance.SuppressEnemyRewards = false;
            }
            if (InventoryManager.HasInstance)
            {
                InventoryManager.Instance.IsTrainingFreeUseMode = false;
            }

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.ReturnToCharacterSelect();
            }
            else if (Application.CanStreamedLevelBeLoaded("MainMenu"))
            {
                SceneManager.LoadScene("MainMenu");
            }
            else if (SceneManager.sceneCountInBuildSettings > 0)
            {
                SceneManager.LoadScene(0);
            }
        }
    }
}
