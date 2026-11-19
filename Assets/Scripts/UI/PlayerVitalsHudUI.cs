using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Enemies;
using StickmanBeatEmUp.GameModes;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Upgrades the Battle HUD Player Vitals cluster, 3-tier Rage bar, status pills,
    /// Boss Health panel, and overhead EnemyWorldHealthBar attachment with Glassmorphism styling,
    /// 15-degree skewed filled bars, and delayed white/amber damage trails.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerVitalsHudUI : MonoBehaviour
    {
        private const float PlayerTrailHoldSeconds = 0.30f;
        private const float PlayerTrailDecaySeconds = 0.45f;
        private const float BossTrailHoldSeconds = 0.30f;
        private const float BossTrailDecaySeconds = 0.45f;
        private const float EnemyScanInterval = 0.15f;

        public static PlayerVitalsHudUI Instance { get; private set; }

        private static bool sceneHookRegistered;
        private static Sprite fallbackWhiteSprite;

        [Header("Player HUD Cluster")]
        [SerializeField] private RectTransform playerHudRect;
        [SerializeField] private Image vitalsBackplate;

        [Header("Player HP & Delayed Trail")]
        [SerializeField] private Slider playerHpSlider;
        [SerializeField] private Image hpFrameImage;
        [SerializeField] private Image hpTrailImage;
        [SerializeField] private Image hpFillImage;
        [SerializeField] private Image hpHeartIcon;
        [SerializeField] private TMP_Text hpText;

        [Header("Spinal Fluid Shield")]
        [SerializeField] private GameObject shieldBarObject;
        [SerializeField] private Image shieldFrameImage;
        [SerializeField] private Image shieldFillImage;
        [SerializeField] private TMP_Text shieldText;

        [Header("3-Segment Skewed Rage")]
        [SerializeField] private RectTransform rageContainerRect;
        [SerializeField] private Slider[] rageSegmentSliders = new Slider[3];
        [SerializeField] private Image[] rageSegmentFills = new Image[3];
        [SerializeField] private Image[] rageSegmentGlowRims = new Image[3];
        [SerializeField] private TMP_Text ragePointsText;

        [Header("Status Pills")]
        [SerializeField] private GameObject blockBadgeObject;
        [SerializeField] private Image blockBadgeBg;
        [SerializeField] private Image blockFillImage;
        [SerializeField] private Image blockShieldIcon;
        [SerializeField] private TMP_Text blockStatusText;

        [SerializeField] private GameObject transformPromptObject;
        [SerializeField] private Image transformPromptBg;
        [SerializeField] private Image transformBoltIcon;
        [SerializeField] private TMP_Text transformPromptText;
        [SerializeField] private CanvasGroup transformPromptCanvasGroup;

        [SerializeField] private GameObject specialComboPromptObject;
        [SerializeField] private Image specialComboPromptBg;
        [SerializeField] private Image specialComboBoltIcon;
        [SerializeField] private TMP_Text specialComboPromptText;
        [SerializeField] private CanvasGroup specialComboCanvasGroup;

        [Header("Boss Health Panel")]
        [SerializeField] private GameObject bossHealthPanel;
        [SerializeField] private Slider bossSlider;
        [SerializeField] private Image bossFrameImage;
        [SerializeField] private Image bossTrailImage;
        [SerializeField] private Image bossFillImage;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private TMP_Text bossStatusText;

        [Header("SlicedUI 07 Sprites (Standalone Safety)")]
        [SerializeField] private Sprite vitalsBackplateSprite;
        [SerializeField] private Sprite hpHeartIconSprite;
        [SerializeField] private Sprite hpFrameSprite;
        [SerializeField] private Sprite hpFillSprite;
        [SerializeField] private Sprite rage3SegFrameSprite;
        [SerializeField] private Sprite rageSegmentCyanGlowSprite;
        [SerializeField] private Sprite rageSegmentDarkSprite;
        [SerializeField] private Sprite shieldOrbIconSprite;
        [SerializeField] private Sprite shieldPillReadySprite;
        [SerializeField] private Sprite shieldPillBlankSprite;

        [Header("Legacy Helper Sprites")]
        [SerializeField] private Sprite shieldBarFillSprite;
        [SerializeField] private Sprite promptPillSprite;
        [SerializeField] private Sprite promptBoltIconSprite;
        [SerializeField] private Sprite bossFrameSprite;
        [SerializeField] private Sprite bossFillSprite;
        [SerializeField] private Sprite hpTrailSprite;

        private Image rage3SegFrameImage;
        private bool isStyled;
        private bool wasBossPanelActive;
        private bool? lastBlockBadgeCooldownState;
        private float unscaledPulseClock;
        private float nextEnemyScanTime;
        private BaseCharacter trackedPlayer;
        private DelayedHealthTrail playerHpTrail;
        private DelayedHealthTrail bossHpTrail;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (sceneHookRegistered)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                sceneHookRegistered = false;
            }
            Instance = null;

            if (fallbackWhiteSprite != null)
            {
                if (fallbackWhiteSprite.texture != null)
                {
                    DestroyImmediate(fallbackWhiteSprite.texture);
                }
                DestroyImmediate(fallbackWhiteSprite);
                fallbackWhiteSprite = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttachOnLoad()
        {
            if (!sceneHookRegistered)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneLoaded += OnSceneLoaded;
                sceneHookRegistered = true;
            }

            TryAttachToActiveGameCanvas();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryAttachToActiveGameCanvas();
        }

        /// <summary>
        /// Ensures a single PlayerVitalsHudUI exists on the active GameCanvas (GameUIManager).
        /// Safe to call from both runtime bootstrap and Editor scene builders.
        /// </summary>
        public static PlayerVitalsHudUI TryAttachToActiveGameCanvas()
        {
            GameUIManager ui = GameUIManager.Instance != null
                ? GameUIManager.Instance
                : FindAnyObjectByType<GameUIManager>();
            if (ui == null) return null;

            PlayerVitalsHudUI existing = ui.GetComponent<PlayerVitalsHudUI>();
            if (existing != null)
            {
                return existing;
            }

            return ui.gameObject.AddComponent<PlayerVitalsHudUI>();
        }

        private void Awake()
        {
            PlayerVitalsHudUI[] duplicates = GetComponents<PlayerVitalsHudUI>();
            if (duplicates.Length > 1 && duplicates[0] != this)
            {
                Destroy(this);
                return;
            }

            if (Instance != null && Instance != this && Instance.gameObject != gameObject)
            {
                GameUIManager myUi = GetComponent<GameUIManager>();
                if (myUi == null || (GameUIManager.Instance != null && GameUIManager.Instance != myUi))
                {
                    Destroy(this);
                    return;
                }
            }

            Instance = this;
        }

        private void Start()
        {
            ApplyHudStyle();
            ScanAndAttachEnemyWorldBars();
        }

        private void OnEnable()
        {
            if (isStyled)
            {
                RefreshPromptAndPlayerSync();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Resolves all GameCanvas child references, restructures layout geometry cleanly,
        /// and applies GlassHUD sprites, skewed fills, icons, and Vietnamese-safe TMP styles.
        /// Idempotent: safe to invoke repeatedly at edit time or runtime.
        /// </summary>
        public void ApplyHudStyle()
        {
            CacheSpritesIfMissing();
            ResolveReferences();
            ConfigurePlayerHudContainer();
            ConfigurePlayerHpBar();
            ConfigureShieldBar();
            ConfigureRageCluster();
            ConfigureBlockStatusPill();
            ConfigurePromptPills();
            ConfigureBossHealthPanel();

            isStyled = true;
            lastBlockBadgeCooldownState = null;

            if (playerHpSlider != null)
            {
                playerHpTrail.Reset(playerHpSlider.normalizedValue);
                if (hpFillImage != null) hpFillImage.fillAmount = playerHpTrail.CurrentFill;
                if (hpTrailImage != null)
                {
                    hpTrailImage.fillAmount = playerHpTrail.TrailFill;
                    hpTrailImage.enabled = false;
                }
            }

            wasBossPanelActive = bossHealthPanel != null && bossHealthPanel.activeSelf;
            if (bossSlider != null)
            {
                bossHpTrail.Reset(bossSlider.normalizedValue);
                if (bossFillImage != null) bossFillImage.fillAmount = bossHpTrail.CurrentFill;
                if (bossTrailImage != null) bossTrailImage.fillAmount = bossHpTrail.TrailFill;
            }

            SyncAllRageSegments(0f);
            RefreshPromptAndPlayerSync();
            UpdateBlockBadgeVisuals();
        }

        private void CacheSpritesIfMissing()
        {
            if (vitalsBackplateSprite == null) vitalsBackplateSprite = GlassHudTheme.LoadSprite("07_BattleVitalsHUD/Frame_HUD_VitalsBackplate_Clean.png");
            if (hpHeartIconSprite == null) hpHeartIconSprite = GlassHudTheme.LoadSprite("07_BattleVitalsHUD/Icon_HUD_HeartCrystal.png");
            if (hpFrameSprite == null) hpFrameSprite = GlassHudTheme.LoadSprite("07_BattleVitalsHUD/Bar_HUD_HP_Frame_Clean.png");
            if (hpFillSprite == null) hpFillSprite = GlassHudTheme.LoadSprite("07_BattleVitalsHUD/Bar_HUD_HP_Fill_RedOrange_Clean.png");
            if (rage3SegFrameSprite == null) rage3SegFrameSprite = GlassHudTheme.LoadSprite("07_BattleVitalsHUD/Bar_HUD_Rage_3SegFrame_Clean.png");
            if (rageSegmentCyanGlowSprite == null) rageSegmentCyanGlowSprite = GlassHudTheme.LoadSprite("07_BattleVitalsHUD/Bar_HUD_Rage_SegmentCyanGlow_Clean.png");
            if (rageSegmentDarkSprite == null) rageSegmentDarkSprite = GlassHudTheme.LoadSprite("07_BattleVitalsHUD/Bar_HUD_Rage_SegmentDark_Clean.png");
            if (shieldOrbIconSprite == null) shieldOrbIconSprite = GlassHudTheme.LoadSprite("07_BattleVitalsHUD/Icon_HUD_ShieldOrb_Green.png");
            if (shieldPillReadySprite == null) shieldPillReadySprite = GlassHudTheme.LoadSprite("07_BattleVitalsHUD/Pill_HUD_ShieldReady_Clean.png");
            if (shieldPillBlankSprite == null) shieldPillBlankSprite = GlassHudTheme.LoadSprite("07_BattleVitalsHUD/Pill_HUD_ShieldReady_Blank.png");

            if (shieldBarFillSprite == null) shieldBarFillSprite = GlassHudTheme.LoadSprite("ShieldBar_Fill_CyanGlass.png");
            if (promptPillSprite == null) promptPillSprite = GlassHudTheme.LoadSprite("StatusPill_CyanGlow.png");
            if (promptBoltIconSprite == null) promptBoltIconSprite = GlassHudTheme.LoadSprite("Icon_Bolt_Cyan.png");
            if (bossFrameSprite == null) bossFrameSprite = GlassHudTheme.LoadSprite("BossHPBar_Frame.png");
            if (bossFillSprite == null) bossFillSprite = GlassHudTheme.LoadSprite("BossHPBar_Fill_PurpleRed.png");
            if (hpTrailSprite == null) hpTrailSprite = GlassHudTheme.LoadSprite("HPBar_Trail_WhiteAmber.png");
        }

        private static Sprite ResolveSprite(Sprite serialized, string fileName)
        {
            return serialized != null ? serialized : GlassHudTheme.LoadSprite(fileName);
        }

        private void LateUpdate()
        {
            if (!isStyled)
            {
                ApplyHudStyle();
            }

            float dt = Time.unscaledDeltaTime;
            unscaledPulseClock += dt;

            RefreshPromptAndPlayerSync();
            UpdatePlayerHpVisuals(dt);
            UpdateRageVisuals();
            UpdateBlockBadgeVisuals();
            UpdatePromptPulseVisuals();
            UpdateBossHpVisuals(dt);

            if (Time.unscaledTime >= nextEnemyScanTime)
            {
                nextEnemyScanTime = Time.unscaledTime + EnemyScanInterval;
                ScanAndAttachEnemyWorldBars();
            }
        }

        private void ResolveReferences()
        {
            if (playerHudRect == null)
            {
                Transform hudTr = FindChildRecursive(transform, "PlayerHUD");
                if (hudTr != null) playerHudRect = hudTr.GetComponent<RectTransform>();
            }

            Transform hudRoot = playerHudRect != null ? playerHudRect : transform;

            if (playerHpSlider == null)
            {
                Transform hpTr = FindChildRecursive(hudRoot, "PlayerHPBar");
                if (hpTr != null) playerHpSlider = hpTr.GetComponent<Slider>();
            }

            if (playerHpSlider != null)
            {
                if (hpText == null)
                {
                    Transform t = FindChildRecursive(playerHpSlider.transform, "HPText");
                    if (t != null) hpText = t.GetComponent<TMP_Text>();
                }
            }

            if (shieldBarObject == null)
            {
                Transform shieldTr = FindChildRecursive(hudRoot, "ShieldBar");
                if (shieldTr != null) shieldBarObject = shieldTr.gameObject;
            }
            if (shieldBarObject != null)
            {
                if (shieldFrameImage == null) shieldFrameImage = shieldBarObject.GetComponent<Image>();
                if (shieldFillImage == null)
                {
                    Transform sf = shieldBarObject.transform.Find("ShieldFill");
                    if (sf != null) shieldFillImage = sf.GetComponent<Image>();
                }
                if (shieldText == null)
                {
                    Transform st = shieldBarObject.transform.Find("ShieldText");
                    if (st != null) shieldText = st.GetComponent<TMP_Text>();
                }
            }

            if (rageContainerRect == null)
            {
                Transform rageTr = FindChildRecursive(hudRoot, "RageContainer");
                if (rageTr != null) rageContainerRect = rageTr.GetComponent<RectTransform>();
            }
            if (rageContainerRect != null)
            {
                if (rageSegmentSliders == null || rageSegmentSliders.Length < 3)
                {
                    rageSegmentSliders = new Slider[3];
                }
                for (int i = 0; i < 3; i++)
                {
                    if (rageSegmentSliders[i] == null)
                    {
                        Transform segTr = rageContainerRect.Find($"RageSegment_{i + 1}");
                        if (segTr != null) rageSegmentSliders[i] = segTr.GetComponent<Slider>();
                    }
                }
                if (ragePointsText == null)
                {
                    Transform rt = rageContainerRect.Find("RagePointsText");
                    if (rt != null) ragePointsText = rt.GetComponent<TMP_Text>();
                }
            }

            if (blockBadgeObject == null)
            {
                Transform blockTr = FindChildRecursive(hudRoot, "BlockStatusBadge");
                if (blockTr != null) blockBadgeObject = blockTr.gameObject;
            }
            if (blockBadgeObject != null)
            {
                if (blockBadgeBg == null) blockBadgeBg = blockBadgeObject.GetComponent<Image>();
                if (blockFillImage == null)
                {
                    Transform bf = blockBadgeObject.transform.Find("BlockFill");
                    if (bf != null) blockFillImage = bf.GetComponent<Image>();
                }
                if (blockStatusText == null)
                {
                    Transform bt = blockBadgeObject.transform.Find("BlockStatusText");
                    if (bt != null) blockStatusText = bt.GetComponent<TMP_Text>();
                }
            }

            if (transformPromptObject == null)
            {
                Transform tp = FindChildRecursive(hudRoot, "TransformReadyPrompt");
                if (tp != null) transformPromptObject = tp.gameObject;
            }
            if (specialComboPromptObject == null)
            {
                Transform sp = FindChildRecursive(hudRoot, "SpecialComboReadyPrompt");
                if (sp != null) specialComboPromptObject = sp.gameObject;
            }

            if (bossHealthPanel == null)
            {
                Transform bp = FindChildRecursive(transform, "BossHealthPanel");
                if (bp != null) bossHealthPanel = bp.gameObject;
            }
            if (bossHealthPanel != null)
            {
                if (bossSlider == null)
                {
                    Transform bs = bossHealthPanel.transform.Find("BossHPSlider");
                    if (bs != null) bossSlider = bs.GetComponent<Slider>();
                }
                if (bossNameText == null)
                {
                    Transform bn = bossHealthPanel.transform.Find("BossNameText");
                    if (bn != null) bossNameText = bn.GetComponent<TMP_Text>();
                }
                if (bossStatusText == null)
                {
                    Transform bst = bossHealthPanel.transform.Find("BossStatusText");
                    if (bst != null) bossStatusText = bst.GetComponent<TMP_Text>();
                }
            }
        }

        private void ConfigurePlayerHudContainer()
        {
            if (playerHudRect == null) return;

            playerHudRect.anchorMin = new Vector2(0f, 1f);
            playerHudRect.anchorMax = new Vector2(0f, 1f);
            playerHudRect.pivot = new Vector2(0f, 1f);
            playerHudRect.anchoredPosition = new Vector2(20f, -14f);
            playerHudRect.sizeDelta = new Vector2(432f, 196f);

            if (vitalsBackplate == null)
            {
                Transform existingBp = playerHudRect.Find("VitalsBackplate");
                GameObject bpObj = existingBp != null
                    ? existingBp.gameObject
                    : new GameObject("VitalsBackplate", typeof(RectTransform), typeof(Image));
                if (existingBp == null)
                {
                    bpObj.transform.SetParent(playerHudRect, false);
                }
                vitalsBackplate = bpObj.GetComponent<Image>();
            }

            if (vitalsBackplate != null)
            {
                vitalsBackplate.transform.SetAsFirstSibling();
                SetFullStretch(vitalsBackplate.rectTransform);
                ApplySlicedSpriteWithFallback(
                    vitalsBackplate,
                    ResolveSprite(vitalsBackplateSprite, "07_BattleVitalsHUD/Frame_HUD_VitalsBackplate_Clean.png"),
                    432f,
                    196f,
                    Color.white,
                    fallbackColor: new Color(0.06f, 0.09f, 0.14f, 0.72f));
                vitalsBackplate.raycastTarget = false;
            }
        }

        private void ConfigurePlayerHpBar()
        {
            if (playerHudRect != null)
            {
                if (hpHeartIcon == null)
                {
                    Transform existingHeart = playerHudRect.Find("HeartIcon");
                    GameObject heartObj = existingHeart != null
                        ? existingHeart.gameObject
                        : new GameObject("HeartIcon", typeof(RectTransform), typeof(Image));
                    if (existingHeart == null)
                    {
                        heartObj.transform.SetParent(playerHudRect, false);
                    }
                    hpHeartIcon = heartObj.GetComponent<Image>();
                }

                if (hpHeartIcon != null)
                {
                    RectTransform hrt = hpHeartIcon.rectTransform;
                    hrt.anchorMin = new Vector2(0f, 1f);
                    hrt.anchorMax = new Vector2(0f, 1f);
                    hrt.pivot = new Vector2(0.5f, 0.5f);
                    hrt.anchoredPosition = new Vector2(38f, -42f);
                    hrt.sizeDelta = new Vector2(28f, 28f);
                    ApplySimpleSpriteWithFallback(
                        hpHeartIcon,
                        ResolveSprite(hpHeartIconSprite, "07_BattleVitalsHUD/Icon_HUD_HeartCrystal.png"),
                        preserveAspect: true,
                        fallbackColor: new Color(0.95f, 0.20f, 0.30f, 1f));
                    hpHeartIcon.raycastTarget = false;
                }
            }

            if (playerHpSlider == null) return;

            RectTransform hpRt = playerHpSlider.GetComponent<RectTransform>();
            hpRt.anchorMin = new Vector2(0f, 1f);
            hpRt.anchorMax = new Vector2(0f, 1f);
            hpRt.pivot = new Vector2(0f, 1f);
            hpRt.anchoredPosition = new Vector2(56f, -26f);
            hpRt.sizeDelta = new Vector2(350f, 32f);

            PrepareSliderForFilledVisuals(
                playerHpSlider,
                ResolveSprite(hpFrameSprite, "07_BattleVitalsHUD/Bar_HUD_HP_Frame_Clean.png"),
                frameSliced: true,
                350f,
                32f,
                ResolveSprite(hpFillSprite, "07_BattleVitalsHUD/Bar_HUD_HP_Fill_RedOrange_Clean.png"),
                "HPTrailImage",
                GetFallbackWhiteSprite(),
                new Vector2(5f, 4f),
                new Vector2(-5f, -4f),
                new Color(0.10f, 0.13f, 0.18f, 0.88f),
                Color.white,
                new Color(1f, 0.90f, 0.56f, 0.94f),
                out hpFrameImage,
                out hpTrailImage,
                out hpFillImage);

            if (hpTrailImage != null)
            {
                hpTrailImage.enabled = false;
            }

            if (hpText != null)
            {
                hpText.transform.SetAsLastSibling();
                SetFullStretch(hpText.rectTransform, new Vector2(8f, 0f), new Vector2(-8f, 0f));
                GlassHudTheme.StyleTmpText(hpText, 18f, Color.white, bold: true, addShadow: true);
                hpText.alignment = TextAlignmentOptions.Center;
                hpText.textWrappingMode = TextWrappingModes.NoWrap;
                hpText.enableAutoSizing = true;
                hpText.fontSizeMin = 12.5f;
                hpText.fontSizeMax = 18f;
                hpText.raycastTarget = false;
                if (string.IsNullOrEmpty(hpText.text))
                {
                    hpText.text = "100 / 100";
                }
            }
        }

        private void ConfigureShieldBar()
        {
            if (shieldBarObject == null) return;

            RectTransform rt = shieldBarObject.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(56f, -61f);
            rt.sizeDelta = new Vector2(350f, 18f);

            if (shieldFrameImage != null)
            {
                ApplySlicedSpriteWithFallback(
                    shieldFrameImage,
                    ResolveSprite(hpFrameSprite, "07_BattleVitalsHUD/Bar_HUD_HP_Frame_Clean.png"),
                    350f,
                    18f,
                    new Color(0.55f, 0.92f, 1f, 0.92f),
                    fallbackColor: new Color(0.08f, 0.16f, 0.22f, 0.90f));
                shieldFrameImage.raycastTarget = false;
            }

            if (shieldFillImage != null)
            {
                SetFullStretch(shieldFillImage.rectTransform, new Vector2(5f, 2.5f), new Vector2(-5f, -2.5f));
                ApplyFilledBarWithFallback(
                    shieldFillImage,
                    ResolveSprite(shieldBarFillSprite, "ShieldBar_Fill_CyanGlass.png"),
                    new Color(0.22f, 0.90f, 1f, 0.95f));
                shieldFillImage.raycastTarget = false;
            }

            if (shieldText != null)
            {
                shieldText.transform.SetAsLastSibling();
                SetFullStretch(shieldText.rectTransform, new Vector2(6f, 0f), new Vector2(-6f, 0f));
                GlassHudTheme.StyleTmpText(shieldText, 13f, Color.white, bold: true, addShadow: true);
                shieldText.alignment = TextAlignmentOptions.Center;
                shieldText.textWrappingMode = TextWrappingModes.NoWrap;
                shieldText.raycastTarget = false;
            }
        }

        private void ConfigureRageCluster()
        {
            if (rageContainerRect == null) return;

            rageContainerRect.anchorMin = new Vector2(0f, 1f);
            rageContainerRect.anchorMax = new Vector2(0f, 1f);
            rageContainerRect.pivot = new Vector2(0f, 1f);
            rageContainerRect.anchoredPosition = new Vector2(24f, -84f);
            rageContainerRect.sizeDelta = new Vector2(384f, 34f);

            if (rageSegmentFills == null || rageSegmentFills.Length < 3)
            {
                rageSegmentFills = new Image[3];
            }
            if (rageSegmentGlowRims == null || rageSegmentGlowRims.Length < 3)
            {
                rageSegmentGlowRims = new Image[3];
            }

            if (rage3SegFrameImage == null)
            {
                Transform existingFrame = rageContainerRect.Find("Rage3SegFrame");
                GameObject frameObj = existingFrame != null
                    ? existingFrame.gameObject
                    : new GameObject("Rage3SegFrame", typeof(RectTransform), typeof(Image));
                if (existingFrame == null)
                {
                    frameObj.transform.SetParent(rageContainerRect, false);
                }
                rage3SegFrameImage = frameObj.GetComponent<Image>();
            }

            if (rage3SegFrameImage != null)
            {
                rage3SegFrameImage.transform.SetAsFirstSibling();
                RectTransform frt = rage3SegFrameImage.rectTransform;
                frt.anchorMin = new Vector2(0f, 0.5f);
                frt.anchorMax = new Vector2(0f, 0.5f);
                frt.pivot = new Vector2(0f, 0.5f);
                frt.anchoredPosition = Vector2.zero;
                frt.sizeDelta = new Vector2(304f, 28f);
                ApplySlicedSpriteWithFallback(
                    rage3SegFrameImage,
                    ResolveSprite(rage3SegFrameSprite, "07_BattleVitalsHUD/Bar_HUD_Rage_3SegFrame_Clean.png"),
                    304f,
                    28f,
                    Color.white,
                    fallbackColor: new Color(0.08f, 0.11f, 0.16f, 0.90f));
                rage3SegFrameImage.raycastTarget = false;
            }

            const float segWidth = 95f;
            const float segHeight = 21f;
            const float segStartX = 6f;
            const float segStepX = 98.5f;

            Sprite segDarkSp = ResolveSprite(rageSegmentDarkSprite, "07_BattleVitalsHUD/Bar_HUD_Rage_SegmentDark_Clean.png");
            Sprite segCyanSp = ResolveSprite(rageSegmentCyanGlowSprite, "07_BattleVitalsHUD/Bar_HUD_Rage_SegmentCyanGlow_Clean.png");

            for (int i = 0; i < 3; i++)
            {
                Slider segSlider = rageSegmentSliders != null && i < rageSegmentSliders.Length
                    ? rageSegmentSliders[i]
                    : null;
                if (segSlider == null) continue;

                RectTransform segRt = segSlider.GetComponent<RectTransform>();
                segRt.anchorMin = new Vector2(0f, 0.5f);
                segRt.anchorMax = new Vector2(0f, 0.5f);
                segRt.pivot = new Vector2(0f, 0.5f);
                segRt.anchoredPosition = new Vector2(segStartX + i * segStepX, 0f);
                segRt.sizeDelta = new Vector2(segWidth, segHeight);

                PrepareSliderForFilledVisuals(
                    segSlider,
                    segDarkSp,
                    frameSliced: true,
                    segWidth,
                    segHeight,
                    segCyanSp,
                    null,
                    null,
                    Vector2.zero,
                    Vector2.zero,
                    new Color(0.12f, 0.13f, 0.18f, 0.88f),
                    Color.white,
                    Color.white,
                    out _,
                    out _,
                    out Image segFill);

                rageSegmentFills[i] = segFill;

                Transform existingRim = segRt.Find("FullGlowRim");
                GameObject rimObj = existingRim != null
                    ? existingRim.gameObject
                    : new GameObject("FullGlowRim", typeof(RectTransform), typeof(Image));
                if (existingRim == null)
                {
                    rimObj.transform.SetParent(segRt, false);
                }

                Image rimImg = rimObj.GetComponent<Image>();
                rimImg.transform.SetAsLastSibling();
                SetFullStretch(rimImg.rectTransform, new Vector2(-1.5f, -1.5f), new Vector2(1.5f, 1.5f));
                ApplySlicedSpriteWithFallback(
                    rimImg,
                    segCyanSp,
                    segWidth + 3f,
                    segHeight + 3f,
                    new Color(0.65f, 0.96f, 1f, 0.72f),
                    fallbackColor: new Color(0.45f, 0.95f, 1f, 0.65f));
                rimImg.raycastTarget = false;
                rimImg.enabled = segSlider.normalizedValue >= 0.995f;
                rageSegmentGlowRims[i] = rimImg;
            }

            if (ragePointsText != null)
            {
                RectTransform rpt = ragePointsText.rectTransform;
                rpt.anchorMin = new Vector2(1f, 0.5f);
                rpt.anchorMax = new Vector2(1f, 0.5f);
                rpt.pivot = new Vector2(1f, 0.5f);
                rpt.anchoredPosition = Vector2.zero;
                rpt.sizeDelta = new Vector2(76f, 30f);

                Color rageGold = new Color(1f, 0.847f, 0.302f, 1f);
                GlassHudTheme.StyleTmpText(ragePointsText, 15.5f, rageGold, bold: true, addShadow: true);
                ragePointsText.alignment = TextAlignmentOptions.MidlineRight;
                ragePointsText.textWrappingMode = TextWrappingModes.NoWrap;
                ragePointsText.enableAutoSizing = true;
                ragePointsText.fontSizeMin = 11f;
                ragePointsText.fontSizeMax = 15.5f;
                ragePointsText.raycastTarget = false;
            }
        }

        private void ConfigureBlockStatusPill()
        {
            if (blockBadgeObject == null) return;

            RectTransform brt = blockBadgeObject.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 1f);
            brt.anchoredPosition = new Vector2(24f, -126f);
            brt.sizeDelta = new Vector2(188f, 36f);

            Sprite pillReadySp = ResolveSprite(shieldPillReadySprite, "07_BattleVitalsHUD/Pill_HUD_ShieldReady_Clean.png");
            Sprite pillBlankSp = ResolveSprite(shieldPillBlankSprite, "07_BattleVitalsHUD/Pill_HUD_ShieldReady_Blank.png");
            Sprite orbIconSp = ResolveSprite(shieldOrbIconSprite, "07_BattleVitalsHUD/Icon_HUD_ShieldOrb_Green.png");

            if (blockBadgeBg != null)
            {
                ApplySlicedSpriteWithFallback(
                    blockBadgeBg,
                    pillReadySp != null ? pillReadySp : pillBlankSp,
                    188f,
                    36f,
                    Color.white,
                    fallbackColor: new Color(0.08f, 0.24f, 0.18f, 0.90f));
                blockBadgeBg.raycastTarget = false;
            }

            if (blockFillImage != null)
            {
                SetFullStretch(blockFillImage.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));
                ApplyFilledBarWithFallback(
                    blockFillImage,
                    pillBlankSp != null ? pillBlankSp : pillReadySp,
                    new Color(0.30f, 0.92f, 0.58f, 0.34f));
                blockFillImage.color = new Color(0.30f, 0.92f, 0.58f, 0.34f);
                blockFillImage.raycastTarget = false;
            }

            if (blockShieldIcon == null)
            {
                Transform existingIcon = brt.Find("ShieldIcon");
                GameObject iconObj = existingIcon != null
                    ? existingIcon.gameObject
                    : new GameObject("ShieldIcon", typeof(RectTransform), typeof(Image));
                if (existingIcon == null)
                {
                    iconObj.transform.SetParent(brt, false);
                }
                blockShieldIcon = iconObj.GetComponent<Image>();
            }

            if (blockShieldIcon != null)
            {
                RectTransform irt = blockShieldIcon.rectTransform;
                irt.anchorMin = new Vector2(0f, 0.5f);
                irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = new Vector2(17f, 0f);
                irt.sizeDelta = new Vector2(24f, 24f);
                ApplySimpleSpriteWithFallback(
                    blockShieldIcon,
                    orbIconSp,
                    preserveAspect: true,
                    fallbackColor: new Color(0.35f, 1f, 0.65f, 1f));
                blockShieldIcon.raycastTarget = false;
                // Pill_HUD_ShieldReady_Clean has a baked-in green orb at left; keep ShieldIcon hidden while Clean pill is shown
                blockShieldIcon.enabled = pillReadySp == null && blockShieldIcon.sprite != null;
            }

            if (blockStatusText != null)
            {
                blockStatusText.transform.SetAsLastSibling();
                SetFullStretch(blockStatusText.rectTransform, new Vector2(34f, 0f), new Vector2(-8f, 0f));
                GlassHudTheme.StyleTmpText(blockStatusText, 13f, new Color(0.72f, 1f, 0.82f, 1f), bold: true, addShadow: true);
                blockStatusText.alignment = TextAlignmentOptions.Center;
                blockStatusText.textWrappingMode = TextWrappingModes.NoWrap;
                blockStatusText.enableAutoSizing = true;
                blockStatusText.fontSizeMin = 9.5f;
                blockStatusText.fontSizeMax = 13f;
                blockStatusText.raycastTarget = false;
            }
        }

        private void ConfigurePromptPills()
        {
            ConfigureSinglePromptPill(
                transformPromptObject,
                "[O] BIẾN HÌNH",
                Color.white,
                new Color(0.68f, 0.98f, 1f, 1f),
                ref transformPromptBg,
                ref transformBoltIcon,
                ref transformPromptText,
                ref transformPromptCanvasGroup);

            ConfigureSinglePromptPill(
                specialComboPromptObject,
                "DASH+[I] TUYỆT KỸ",
                new Color(0.92f, 0.90f, 1f, 1f),
                new Color(1f, 0.88f, 0.45f, 1f),
                ref specialComboPromptBg,
                ref specialComboBoltIcon,
                ref specialComboPromptText,
                ref specialComboCanvasGroup);
        }

        private void ConfigureSinglePromptPill(
            GameObject promptObj,
            string cleanLabel,
            Color pillTint,
            Color textColor,
            ref Image bgImg,
            ref Image boltIcon,
            ref TMP_Text promptTmp,
            ref CanvasGroup canvasGroup)
        {
            if (promptObj == null) return;

            RectTransform prt = promptObj.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0f, 1f);
            prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = new Vector2(310f, -144f);
            prt.sizeDelta = new Vector2(192f, 34f);

            if (bgImg == null) bgImg = promptObj.GetComponent<Image>();
            if (bgImg != null)
            {
                ApplySlicedSpriteWithFallback(
                    bgImg,
                    ResolveSprite(promptPillSprite, "StatusPill_CyanGlow.png"),
                    192f,
                    34f,
                    pillTint,
                    fallbackColor: new Color(0.08f, 0.25f, 0.34f, 0.92f));
                bgImg.raycastTarget = false;
            }

            if (boltIcon == null)
            {
                Transform existingBolt = prt.Find("BoltIcon");
                GameObject boltObj = existingBolt != null
                    ? existingBolt.gameObject
                    : new GameObject("BoltIcon", typeof(RectTransform), typeof(Image));
                if (existingBolt == null)
                {
                    boltObj.transform.SetParent(prt, false);
                }
                boltIcon = boltObj.GetComponent<Image>();
            }

            if (boltIcon != null)
            {
                RectTransform brt = boltIcon.rectTransform;
                brt.anchorMin = new Vector2(0f, 0.5f);
                brt.anchorMax = new Vector2(0f, 0.5f);
                brt.pivot = new Vector2(0f, 0.5f);
                brt.anchoredPosition = new Vector2(10f, 0f);
                brt.sizeDelta = new Vector2(18f, 18f);
                ApplySimpleSpriteWithFallback(
                    boltIcon,
                    ResolveSprite(promptBoltIconSprite, "Icon_Bolt_Cyan.png"),
                    preserveAspect: true,
                    fallbackColor: new Color(0.35f, 0.95f, 1f, 1f));
                boltIcon.raycastTarget = false;
            }

            if (promptTmp == null)
            {
                Transform txtTr = prt.Find("PromptText");
                if (txtTr != null) promptTmp = txtTr.GetComponent<TMP_Text>();
                if (promptTmp == null) promptTmp = promptObj.GetComponentInChildren<TMP_Text>(true);
            }

            if (promptTmp != null)
            {
                promptTmp.transform.SetAsLastSibling();
                SetFullStretch(promptTmp.rectTransform, new Vector2(30f, 0f), new Vector2(-8f, 0f));
                promptTmp.text = cleanLabel;
                GlassHudTheme.StyleTmpText(promptTmp, 13f, textColor, bold: true, addShadow: true);
                promptTmp.alignment = TextAlignmentOptions.Center;
                promptTmp.textWrappingMode = TextWrappingModes.NoWrap;
                promptTmp.enableAutoSizing = true;
                promptTmp.fontSizeMin = 9.5f;
                promptTmp.fontSizeMax = 13f;
                promptTmp.raycastTarget = false;
            }

            if (canvasGroup == null)
            {
                canvasGroup = promptObj.GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = promptObj.AddComponent<CanvasGroup>();
            }
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 1f;
        }

        private void ConfigureBossHealthPanel()
        {
            if (bossHealthPanel == null) return;

            RectTransform bprt = bossHealthPanel.GetComponent<RectTransform>();
            bprt.anchorMin = new Vector2(0.5f, 1f);
            bprt.anchorMax = new Vector2(0.5f, 1f);
            bprt.pivot = new Vector2(0.5f, 1f);
            bool isTrainingMode = (TrainingModeManager.Instance != null && TrainingModeManager.Instance.IsActive)
                || StageManager.LoadSelectedGameMode() == GameMode.Training;
            float targetY = isTrainingMode
                ? (bprt.anchoredPosition.y <= -114f ? bprt.anchoredPosition.y : -114f)
                : -106f;
            bprt.anchoredPosition = new Vector2(0f, targetY);
            bprt.sizeDelta = new Vector2(680f, 70f);

            if (bossNameText != null)
            {
                RectTransform bnrt = bossNameText.rectTransform;
                bnrt.anchorMin = new Vector2(0.5f, 1f);
                bnrt.anchorMax = new Vector2(0.5f, 1f);
                bnrt.pivot = new Vector2(0.5f, 1f);
                bnrt.anchoredPosition = Vector2.zero;
                bnrt.sizeDelta = new Vector2(660f, 25f);

                GlassHudTheme.StyleTmpText(bossNameText, 20f, new Color(1f, 0.94f, 0.72f, 1f), bold: true, addShadow: true);
                Outline thickOutline = bossNameText.GetComponent<Outline>();
                if (thickOutline != null)
                {
                    thickOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
                    thickOutline.effectDistance = new Vector2(1.6f, -1.6f);
                }
                bossNameText.alignment = TextAlignmentOptions.Center;
                bossNameText.textWrappingMode = TextWrappingModes.NoWrap;
                bossNameText.raycastTarget = false;
            }

            if (bossSlider != null)
            {
                RectTransform bsrt = bossSlider.GetComponent<RectTransform>();
                bsrt.anchorMin = new Vector2(0.5f, 1f);
                bsrt.anchorMax = new Vector2(0.5f, 1f);
                bsrt.pivot = new Vector2(0.5f, 1f);
                bsrt.anchoredPosition = new Vector2(0f, -25f);
                bsrt.sizeDelta = new Vector2(660f, 26f);

                PrepareSliderForFilledVisuals(
                    bossSlider,
                    ResolveSprite(bossFrameSprite, "BossHPBar_Frame.png"),
                    frameSliced: true,
                    660f,
                    26f,
                    ResolveSprite(bossFillSprite, "BossHPBar_Fill_PurpleRed.png"),
                    "BossHPTrailImage",
                    ResolveSprite(hpTrailSprite, "HPBar_Trail_WhiteAmber.png"),
                    new Vector2(10f, 4.5f),
                    new Vector2(-10f, -4.5f),
                    new Color(0.14f, 0.06f, 0.14f, 0.92f),
                    new Color(0.84f, 0.14f, 0.36f, 1f),
                    new Color(1f, 0.88f, 0.56f, 0.94f),
                    out bossFrameImage,
                    out bossTrailImage,
                    out bossFillImage);
            }

            if (bossStatusText != null)
            {
                RectTransform bsTxtRt = bossStatusText.rectTransform;
                bsTxtRt.anchorMin = new Vector2(0.5f, 1f);
                bsTxtRt.anchorMax = new Vector2(0.5f, 1f);
                bsTxtRt.pivot = new Vector2(0.5f, 1f);
                bsTxtRt.anchoredPosition = new Vector2(0f, -51f);
                bsTxtRt.sizeDelta = new Vector2(680f, 21f);

                GlassHudTheme.StyleTmpText(bossStatusText, 15.5f, new Color(1f, 0.78f, 0.28f, 1f), bold: true, addShadow: true);
                bossStatusText.alignment = TextAlignmentOptions.Center;
                bossStatusText.textWrappingMode = TextWrappingModes.NoWrap;
                bossStatusText.raycastTarget = false;
            }
        }

        private void PrepareSliderForFilledVisuals(
            Slider slider,
            Sprite frameSprite,
            bool frameSliced,
            float targetWidth,
            float targetHeight,
            Sprite fillSprite,
            string trailObjectName,
            Sprite trailSprite,
            Vector2 fillAreaOffsetMin,
            Vector2 fillAreaOffsetMax,
            Color frameFallbackColor,
            Color fillFallbackColor,
            Color trailFallbackColor,
            out Image frameImg,
            out Image trailImg,
            out Image fillImg)
        {
            frameImg = null;
            trailImg = null;
            fillImg = null;
            if (slider == null) return;

            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.fillRect = null;

            Transform bgTr = slider.transform.Find("Background");
            if (bgTr != null)
            {
                SetFullStretch(bgTr.GetComponent<RectTransform>());
                frameImg = bgTr.GetComponent<Image>();
                if (frameImg != null)
                {
                    if (frameSliced)
                    {
                        ApplySlicedSpriteWithFallback(
                            frameImg,
                            frameSprite,
                            targetWidth,
                            targetHeight,
                            Color.white,
                            frameFallbackColor);
                    }
                    else
                    {
                        ApplySimpleSpriteWithFallback(
                            frameImg,
                            frameSprite,
                            preserveAspect: false,
                            frameFallbackColor);
                    }
                    frameImg.raycastTarget = false;
                }
            }

            Transform fillAreaTr = slider.transform.Find("Fill Area");
            if (fillAreaTr == null)
            {
                GameObject faObj = new GameObject("Fill Area", typeof(RectTransform));
                faObj.transform.SetParent(slider.transform, false);
                fillAreaTr = faObj.transform;
            }
            SetFullStretch(fillAreaTr.GetComponent<RectTransform>(), fillAreaOffsetMin, fillAreaOffsetMax);

            if (!string.IsNullOrEmpty(trailObjectName))
            {
                Transform existingTrail = fillAreaTr.Find(trailObjectName);
                GameObject trailObj = existingTrail != null
                    ? existingTrail.gameObject
                    : new GameObject(trailObjectName, typeof(RectTransform), typeof(Image));
                if (existingTrail == null)
                {
                    trailObj.transform.SetParent(fillAreaTr, false);
                }

                trailObj.transform.SetAsFirstSibling();
                SetFullStretch(trailObj.GetComponent<RectTransform>());
                trailImg = trailObj.GetComponent<Image>();
                ApplyFilledBarWithFallback(trailImg, trailSprite, trailFallbackColor);
                if (trailSprite == GetFallbackWhiteSprite())
                {
                    trailImg.color = trailFallbackColor;
                }
                trailImg.raycastTarget = false;
            }

            Transform fillTr = fillAreaTr.Find("Fill");
            if (fillTr == null)
            {
                GameObject fObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fObj.transform.SetParent(fillAreaTr, false);
                fillTr = fObj.transform;
            }

            fillTr.SetAsLastSibling();
            SetFullStretch(fillTr.GetComponent<RectTransform>());
            fillImg = fillTr.GetComponent<Image>();
            ApplyFilledBarWithFallback(fillImg, fillSprite, fillFallbackColor);
            fillImg.raycastTarget = false;
        }

        private void RefreshPromptAndPlayerSync()
        {
            BaseCharacter current = (LevelManager.Instance != null && LevelManager.Instance.ActivePlayer != null)
                ? LevelManager.Instance.ActivePlayer
                : FindAnyObjectByType<BaseCharacter>();

            if (current != trackedPlayer)
            {
                trackedPlayer = current;
                if (playerHpSlider != null)
                {
                    playerHpTrail.Reset(playerHpSlider.normalizedValue);
                }
            }

            if (trackedPlayer != null && trackedPlayer.RageManager != null)
            {
                float rageFloat = trackedPlayer.RageManager.CurrentRageFloat;
                bool rageFull = rageFloat >= RageManager.TRANSFORM_COST;
                if (transformPromptObject != null)
                {
                    bool shouldShowTransform = rageFull && !trackedPlayer.IsTransformed;
                    if (transformPromptObject.activeSelf != shouldShowTransform)
                    {
                        transformPromptObject.SetActive(shouldShowTransform);
                    }
                }
                if (specialComboPromptObject != null)
                {
                    bool shouldShowSpecial = rageFull && trackedPlayer.IsTransformed;
                    if (specialComboPromptObject.activeSelf != shouldShowSpecial)
                    {
                        specialComboPromptObject.SetActive(shouldShowSpecial);
                    }
                }
            }
        }

        private void UpdatePlayerHpVisuals(float dt)
        {
            if (playerHpSlider == null) return;

            float norm = Mathf.Clamp01(playerHpSlider.normalizedValue);
            playerHpTrail.Update(norm, dt, PlayerTrailHoldSeconds, PlayerTrailDecaySeconds);

            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = playerHpTrail.CurrentFill;
            }

            if (hpTrailImage != null)
            {
                bool showTrail = playerHpTrail.TrailFill > playerHpTrail.CurrentFill + 0.002f;
                hpTrailImage.enabled = showTrail;
                hpTrailImage.fillAmount = Mathf.Max(playerHpTrail.TrailFill, playerHpTrail.CurrentFill);
            }
        }

        private void UpdateRageVisuals()
        {
            float rageFloat = trackedPlayer != null && trackedPlayer.RageManager != null
                ? trackedPlayer.RageManager.CurrentRageFloat
                : -1f;

            if (rageFloat >= 0f)
            {
                SyncAllRageSegments(rageFloat);
            }
            else if (rageSegmentSliders != null)
            {
                float glowPulse = 0.5f + 0.5f * Mathf.Sin(unscaledPulseClock * 5.2f);
                for (int i = 0; i < 3 && i < rageSegmentSliders.Length; i++)
                {
                    Slider segSlider = rageSegmentSliders[i];
                    if (segSlider == null) continue;
                    ApplySegmentVisual(i, Mathf.Clamp01(segSlider.normalizedValue), glowPulse);
                }
            }
        }

        private void SyncAllRageSegments(float rageFloat)
        {
            float glowPulse = 0.5f + 0.5f * Mathf.Sin(unscaledPulseClock * 5.2f);
            for (int i = 0; i < 3; i++)
            {
                float segNorm;
                if (rageSegmentSliders != null && i < rageSegmentSliders.Length && rageSegmentSliders[i] != null)
                {
                    segNorm = Mathf.Clamp01(rageSegmentSliders[i].normalizedValue);
                }
                else
                {
                    segNorm = Mathf.Clamp01((rageFloat - (i * RageManager.RAGE_PER_SEGMENT)) / RageManager.RAGE_PER_SEGMENT);
                }

                ApplySegmentVisual(i, segNorm, glowPulse);
            }
        }

        private void ApplySegmentVisual(int index, float normalizedFill, float glowPulse)
        {
            if (rageSegmentFills != null && index < rageSegmentFills.Length && rageSegmentFills[index] != null)
            {
                rageSegmentFills[index].fillAmount = normalizedFill;
            }

            if (rageSegmentGlowRims != null && index < rageSegmentGlowRims.Length && rageSegmentGlowRims[index] != null)
            {
                Image rim = rageSegmentGlowRims[index];
                bool isFull = normalizedFill >= 0.995f;
                rim.enabled = isFull;
                if (isFull)
                {
                    rim.color = Color.Lerp(new Color(0.55f, 0.95f, 1f, 0.55f), Color.white, glowPulse);
                }
            }
        }

        private void UpdateBlockBadgeVisuals()
        {
            if (blockBadgeObject == null || blockFillImage == null) return;

            bool isCooldown = trackedPlayer != null && !trackedPlayer.IsBlocking && trackedPlayer.BlockCooldownRemaining > 0.05f;
            if (blockBadgeBg != null && lastBlockBadgeCooldownState != isCooldown)
            {
                lastBlockBadgeCooldownState = isCooldown;
                Sprite readySp = ResolveSprite(shieldPillReadySprite, "07_BattleVitalsHUD/Pill_HUD_ShieldReady_Clean.png");
                Sprite blankSp = ResolveSprite(shieldPillBlankSprite, "07_BattleVitalsHUD/Pill_HUD_ShieldReady_Blank.png");
                if (isCooldown)
                {
                    if (blankSp != null)
                    {
                        GlassHudTheme.ApplySlicedSprite(blockBadgeBg, blankSp, 188f, 36f, new Color(0.60f, 0.68f, 0.74f, 0.92f));
                    }
                    if (blockShieldIcon != null)
                    {
                        blockShieldIcon.enabled = blockShieldIcon.sprite != null;
                        blockShieldIcon.color = new Color(0.55f, 0.72f, 0.65f, 0.55f);
                    }
                }
                else
                {
                    if (readySp != null)
                    {
                        GlassHudTheme.ApplySlicedSprite(blockBadgeBg, readySp, 188f, 36f, Color.white);
                        if (blockShieldIcon != null)
                        {
                            blockShieldIcon.enabled = false;
                        }
                    }
                    else if (blankSp != null)
                    {
                        GlassHudTheme.ApplySlicedSprite(blockBadgeBg, blankSp, 188f, 36f, Color.white);
                        if (blockShieldIcon != null)
                        {
                            blockShieldIcon.enabled = blockShieldIcon.sprite != null;
                            blockShieldIcon.color = Color.white;
                        }
                    }
                }
            }

            if (trackedPlayer != null)
            {
                if (trackedPlayer.IsBlocking)
                {
                    float dur = Mathf.Max(0.01f, trackedPlayer.BlockDuration);
                    blockFillImage.fillAmount = Mathf.Clamp01(trackedPlayer.BlockRemaining / dur);
                    blockFillImage.color = new Color(0.35f, 0.95f, 1f, 0.58f);
                    if (blockStatusText != null)
                    {
                        blockStatusText.text = "[S] ĐANG ĐỠ (-80% DMG)";
                        blockStatusText.color = new Color(0.35f, 0.95f, 1f, 1f);
                    }
                }
                else if (trackedPlayer.BlockCooldownRemaining > 0.05f)
                {
                    float cd = Mathf.Max(0.1f, trackedPlayer.BlockCooldownDuration);
                    blockFillImage.fillAmount = 1f - Mathf.Clamp01(trackedPlayer.BlockCooldownRemaining / cd);
                    blockFillImage.color = new Color(0.65f, 0.80f, 0.78f, 0.24f);
                    if (blockStatusText != null)
                    {
                        blockStatusText.text = $"[S] ĐỠ: {trackedPlayer.BlockCooldownRemaining:0.0}s";
                        blockStatusText.color = new Color(0.85f, 0.88f, 0.92f, 0.90f);
                    }
                }
                else
                {
                    blockFillImage.fillAmount = 1f;
                    blockFillImage.color = new Color(0.30f, 0.92f, 0.58f, 0.34f);
                    if (blockStatusText != null)
                    {
                        blockStatusText.text = "[S] ĐỠ: SẴN SÀNG";
                        blockStatusText.color = new Color(0.72f, 1f, 0.82f, 1f);
                    }
                }
            }
            else
            {
                blockFillImage.fillAmount = 1f;
                blockFillImage.color = new Color(0.30f, 0.92f, 0.58f, 0.34f);
                if (blockStatusText != null)
                {
                    blockStatusText.text = "[S] ĐỠ: SẴN SÀNG";
                    blockStatusText.color = new Color(0.72f, 1f, 0.82f, 1f);
                }
            }
        }

        private void UpdatePromptPulseVisuals()
        {
            float breathe = 0.5f + 0.5f * Mathf.Sin(unscaledPulseClock * 4.5f);
            float pulseScale = Mathf.Lerp(1.0f, 1.055f, breathe);
            float pulseAlpha = Mathf.Lerp(0.80f, 1.0f, breathe);

            ApplySinglePromptPulse(transformPromptObject, transformPromptCanvasGroup, pulseScale, pulseAlpha);
            ApplySinglePromptPulse(specialComboPromptObject, specialComboCanvasGroup, pulseScale, pulseAlpha);
        }

        private static void ApplySinglePromptPulse(
            GameObject promptObj,
            CanvasGroup canvasGroup,
            float pulseScale,
            float pulseAlpha)
        {
            if (promptObj == null) return;

            if (promptObj.activeSelf)
            {
                promptObj.transform.localScale = new Vector3(pulseScale, pulseScale, 1f);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = pulseAlpha;
                }
            }
            else
            {
                promptObj.transform.localScale = Vector3.one;
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }
            }
        }

        private void UpdateBossHpVisuals(float dt)
        {
            if (bossHealthPanel == null || bossSlider == null) return;

            bool isActive = bossHealthPanel.activeSelf;
            if (!isActive)
            {
                wasBossPanelActive = false;
                return;
            }

            float norm = Mathf.Clamp01(bossSlider.normalizedValue);
            if (!wasBossPanelActive)
            {
                wasBossPanelActive = true;
                bossHpTrail.Reset(norm);
            }
            else
            {
                bossHpTrail.Update(norm, dt, BossTrailHoldSeconds, BossTrailDecaySeconds);
            }

            if (bossFillImage != null)
            {
                bossFillImage.fillAmount = bossHpTrail.CurrentFill;
            }

            if (bossTrailImage != null)
            {
                bossTrailImage.fillAmount = Mathf.Max(bossHpTrail.TrailFill, bossHpTrail.CurrentFill);
            }
        }

        private static void ScanAndAttachEnemyWorldBars()
        {
            EnemyBase[] enemies = FindObjectsByType<EnemyBase>();
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyBase enemy = enemies[i];
                if (enemy != null && !enemy.IsDead && enemy.gameObject.activeInHierarchy)
                {
                    EnemyWorldHealthBar.AttachTo(enemy);
                }
            }
        }

        private static bool ApplySlicedSpriteWithFallback(
            Image img,
            Sprite sprite,
            float targetWidth,
            float targetHeight,
            Color tint,
            Color fallbackColor)
        {
            if (img == null) return false;

            if (sprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(img, sprite, targetWidth, targetHeight, tint);
                return true;
            }

            img.sprite = GetFallbackWhiteSprite();
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = fallbackColor;
            return false;
        }

        private static bool ApplySimpleSpriteWithFallback(
            Image img,
            Sprite sprite,
            bool preserveAspect,
            Color fallbackColor)
        {
            if (img == null) return false;

            if (sprite != null)
            {
                GlassHudTheme.ApplySprite(img, sprite, sliced: false, preserveAspect: preserveAspect);
                return true;
            }

            img.sprite = GetFallbackWhiteSprite();
            img.type = Image.Type.Simple;
            img.preserveAspect = preserveAspect;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = fallbackColor;
            return false;
        }

        private static void ApplyFilledBarWithFallback(Image img, Sprite sprite, Color fallbackColor)
        {
            if (img == null) return;

            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
            }
            else
            {
                img.sprite = GetFallbackWhiteSprite();
                img.color = fallbackColor;
            }

            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = 0;
            img.preserveAspect = false;
        }

        private static void SetFullStretch(RectTransform rt, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null) return null;
            if (root.name == targetName) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == targetName) return child;
                Transform nested = FindChildRecursive(child, targetName);
                if (nested != null) return nested;
            }

            return null;
        }

        private static Sprite GetFallbackWhiteSprite()
        {
            if (fallbackWhiteSprite != null) return fallbackWhiteSprite;

            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.DontSave;
            Color32[] pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            fallbackWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            fallbackWhiteSprite.hideFlags = HideFlags.DontSave;
            return fallbackWhiteSprite;
        }
    }
}
