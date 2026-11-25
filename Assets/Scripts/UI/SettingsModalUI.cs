using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif
using StickmanBeatEmUp.Audio;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.CorePure.Settings;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Shared Dark Glassmorphism Settings modal built at runtime for Main Menu and Pause Menu.
    /// Provides Audio, Display/Performance, Gameplay/HUD toggles, and conflict-free Key Rebinding.
    /// </summary>
    public class SettingsModalUI : MonoBehaviour
    {
        private const string ModalPanelName = "SettingsModalPanel";
        private const float ModalWidth = 980f;
        private const float ModalHeight = 680f;
        private const float PopInDuration = 0.14f;
        private const float PopInStartScale = 0.92f;

        private static readonly Color CyanAccent = new Color(0.133f, 0.827f, 0.933f, 1f);
        private static readonly Color GoldAccent = new Color(1f, 0.88f, 0.36f, 1f);
        private static readonly Color SoftTextColor = new Color(0.86f, 0.91f, 0.97f, 1f);
        private static readonly Color SuccessColor = new Color(0.24f, 0.88f, 0.62f, 1f);
        private static readonly Color ConflictColor = new Color(1f, 0.42f, 0.48f, 1f);
        private static readonly Color NeutralFeedbackColor = new Color(0.75f, 0.84f, 0.94f, 1f);
        private static readonly Color SliderTrackColor = new Color(0.08f, 0.12f, 0.22f, 0.95f);

        private struct KeybindRowWidgets
        {
            public GameInputAction Action;
            public UnityEngine.UI.Button KeyButton;
            public UnityEngine.UI.Image KeyButtonBg;
            public TMP_Text KeyButtonText;
        }

        public static int LastConsumedEscapeFrame { get; private set; } = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            LastConsumedEscapeFrame = -1;
        }

        private GameObject modalRoot;
        private RectTransform boxRect;
        private CanvasGroup boxCanvasGroup;
        private TMP_FontAsset uiFont;

        // Tabs
        private int activeTabIndex;
        private readonly UnityEngine.UI.Button[] tabButtons = new UnityEngine.UI.Button[3];
        private readonly UnityEngine.UI.Image[] tabImages = new UnityEngine.UI.Image[3];
        private readonly GameObject[] tabPanels = new GameObject[3];

        // Tab 0: Audio & Display
        private UnityEngine.UI.Slider masterSlider;
        private TMP_Text masterValueText;
        private UnityEngine.UI.Button masterMuteBtn;
        private UnityEngine.UI.Image masterMuteImg;
        private TMP_Text masterMuteText;

        private UnityEngine.UI.Slider bgmSlider;
        private TMP_Text bgmValueText;
        private UnityEngine.UI.Button bgmMuteBtn;
        private UnityEngine.UI.Image bgmMuteImg;
        private TMP_Text bgmMuteText;

        private UnityEngine.UI.Slider sfxSlider;
        private TMP_Text sfxValueText;
        private UnityEngine.UI.Button sfxMuteBtn;
        private UnityEngine.UI.Image sfxMuteImg;
        private TMP_Text sfxMuteText;

        private UnityEngine.UI.Button fullscreenBtn;
        private UnityEngine.UI.Image fullscreenImg;
        private TMP_Text fullscreenText;
        private UnityEngine.UI.Button resolutionBtn;
        private TMP_Text resolutionText;
        private UnityEngine.UI.Button vsyncBtn;
        private UnityEngine.UI.Image vsyncImg;
        private TMP_Text vsyncText;
        private UnityEngine.UI.Button fpsBtn;
        private TMP_Text fpsText;

        // Tab 1: Gameplay & HUD
        private UnityEngine.UI.Button shakeToggleBtn;
        private UnityEngine.UI.Image shakeToggleImg;
        private TMP_Text shakeToggleText;
        private UnityEngine.UI.Slider shakeSlider;
        private TMP_Text shakeValueText;

        private UnityEngine.UI.Button damagePopupBtn;
        private UnityEngine.UI.Image damagePopupImg;
        private TMP_Text damagePopupText;

        private UnityEngine.UI.Button hitSparkBtn;
        private UnityEngine.UI.Image hitSparkImg;
        private TMP_Text hitSparkText;

        private UnityEngine.UI.Button controlsHintBtn;
        private UnityEngine.UI.Image controlsHintImg;
        private TMP_Text controlsHintText;

        private UnityEngine.UI.Slider doubleTapSlider;
        private TMP_Text doubleTapValueText;

        // Tab 2: Controls
        private UnityEngine.UI.Button presetDefaultBtn;
        private UnityEngine.UI.Button presetArrowsBtn;
        private TMP_Text presetStatusText;
        private UnityEngine.UI.Button swapPolicyBtn;
        private UnityEngine.UI.Image swapPolicyImg;
        private TMP_Text swapPolicyText;
        private TMP_Text keybindFeedbackText;
        private UnityEngine.UI.ScrollRect keybindScrollRect;
        private readonly List<KeybindRowWidgets> keybindRows = new List<KeybindRowWidgets>(KeyBindingMap.ActionCount);

        // Cached theme sprites
        private Sprite modalBgSprite;
        private Sprite neutralPillSprite;
        private Sprite primaryPillSprite;
        private Sprite dangerPillSprite;
        private Sprite greenPillSprite;
        private Sprite toggleOnSprite;
        private Sprite toggleOffSprite;
        private Sprite keycapSprite;

        private Action onClosedCallback;
        private Coroutine popInCoroutine;
        private GameInputAction? capturingAction;
        private float nextSfxPreviewTime = -10f;
        private bool isBuilt;
        private bool isRefreshingUi;

        public bool IsOpen => modalRoot != null && modalRoot.activeSelf;

        public static SettingsModalUI GetOrCreate(Transform canvasRoot)
        {
            if (canvasRoot == null) return null;

            Transform existing = canvasRoot.Find(ModalPanelName);
            SettingsModalUI modal = existing != null ? existing.GetComponent<SettingsModalUI>() : null;
            if (modal == null)
            {
                GameObject panelObj = existing != null
                    ? existing.gameObject
                    : new GameObject(
                        ModalPanelName,
                        typeof(RectTransform),
                        typeof(UnityEngine.UI.Image),
                        typeof(SettingsModalUI));
                if (existing == null)
                {
                    panelObj.transform.SetParent(canvasRoot, false);
                }

                modal = panelObj.GetComponent<SettingsModalUI>();
                if (modal == null)
                {
                    modal = panelObj.AddComponent<SettingsModalUI>();
                }
            }

            modal.EnsureBuilt();
            return modal;
        }

        public static SettingsModalUI OpenForCanvas(Transform canvasRoot, Action onClosed = null)
        {
            SettingsModalUI modal = GetOrCreate(canvasRoot);
            if (modal != null)
            {
                modal.Open(onClosed);
            }
            return modal;
        }

        public void Open(Action onClosed = null)
        {
            EnsureBuilt();
            onClosedCallback = onClosed;
            CancelKeyCaptureInternal("Bấm vào một nút phím rồi nhấn phím mới (ESC để hủy).");

            if (transform.parent != null)
            {
                transform.SetAsLastSibling();
            }

            modalRoot.SetActive(true);
            RefreshAllUI();
            PlayPopIn();
        }

        public void Close()
        {
            CancelKeyCaptureInternal(null, refreshUi: false);
            StopPopInAndReset();

            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }

            Action cb = onClosedCallback;
            onClosedCallback = null;
            cb?.Invoke();
        }

        private void OnDisable()
        {
            CancelKeyCaptureInternal(null, refreshUi: false);
            StopPopInAndReset();
        }

        private void OnDestroy()
        {
            CancelKeyCaptureInternal(null, refreshUi: false);
            StopPopInAndReset();
        }

        private void Update()
        {
            if (!IsOpen) return;

            if (capturingAction.HasValue)
            {
                if (WasKeyPressedThisFrame(KeyCode.Escape))
                {
                    LastConsumedEscapeFrame = Time.frameCount;
                    CancelKeyCaptureInternal("Đã hủy đổi phím.");
                    return;
                }

                IReadOnlyList<int> supported = KeyBindingMap.SupportedKeyCodes;
                for (int i = 0; i < supported.Count; i++)
                {
                    KeyCode candidate = (KeyCode)supported[i];
                    if (candidate == KeyCode.Escape) continue;

                    if (WasKeyPressedThisFrame(candidate))
                    {
                        LastConsumedEscapeFrame = Time.frameCount;
                        CompleteKeyCapture(candidate);
                        return;
                    }
                }
                return;
            }

            if (WasKeyPressedThisFrame(KeyCode.Escape))
            {
                LastConsumedEscapeFrame = Time.frameCount;
                Close();
            }
        }

        private void EnsureBuilt()
        {
            if (isBuilt && boxRect != null) return;

            modalRoot = gameObject;
            uiFont = GlassHudTheme.ResolveSharedTmpFont(this);
            LoadThemeSprites();

            RectTransform rootRt = modalRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            UnityEngine.UI.Image dimBg = modalRoot.GetComponent<UnityEngine.UI.Image>();
            if (dimBg == null) dimBg = modalRoot.AddComponent<UnityEngine.UI.Image>();
            dimBg.sprite = null;
            dimBg.color = new Color(0.02f, 0.04f, 0.08f, 0.72f);
            dimBg.raycastTarget = true;

            ClearChildrenExcept(modalRoot.transform);

            GameObject boxObj = CreateRect("SettingsBox", modalRoot.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(ModalWidth, ModalHeight));
            boxRect = boxObj.GetComponent<RectTransform>();

            UnityEngine.UI.Image boxImg = boxObj.AddComponent<UnityEngine.UI.Image>();
            if (modalBgSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(boxImg, modalBgSprite, ModalWidth, ModalHeight, Color.white);
            }
            else
            {
                boxImg.color = new Color(0.06f, 0.10f, 0.18f, 0.96f);
            }
            boxImg.raycastTarget = true;

            boxCanvasGroup = boxObj.AddComponent<CanvasGroup>();
            boxCanvasGroup.interactable = true;
            boxCanvasGroup.blocksRaycasts = true;

            CreateText("SettingsTitle", boxObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 298f), new Vector2(520f, 46f),
                "CÀI ĐẶT", 34f, GoldAccent, TextAlignmentOptions.Center, bold: true);

            BuildTabHeader(boxObj.transform);
            CreateDivider("TopDivider", boxObj.transform, new Vector2(0f, 206f), new Vector2(900f, 1.5f));

            GameObject bodyObj = CreateRect("TabBody", boxObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -18f), new Vector2(900f, 432f));

            BuildAudioDisplayTab(bodyObj.transform);
            BuildGameplayTab(bodyObj.transform);
            BuildControlsTab(bodyObj.transform);

            CreateDivider("BottomDivider", boxObj.transform, new Vector2(0f, -244f), new Vector2(900f, 1.5f));
            BuildFooter(boxObj.transform);

            modalRoot.SetActive(false);
            isBuilt = true;
            SwitchTab(0);
        }

        private void LoadThemeSprites()
        {
            modalBgSprite = GlassHudTheme.LoadSprite("ModalCard_DarkGlass_Cyan.png");
            neutralPillSprite = GlassHudTheme.LoadSprite("PillBtn_GlassNeutral.png");
            primaryPillSprite = GlassHudTheme.LoadSprite("PillBtn_OrangePrimary.png");
            dangerPillSprite = GlassHudTheme.LoadSprite("PillBtn_RedDanger.png");
            greenPillSprite = GlassHudTheme.LoadSprite("PillBtn_GreenConfirm.png");
            toggleOnSprite = GlassHudTheme.LoadSprite("TogglePill_On_Green.png");
            toggleOffSprite = GlassHudTheme.LoadSprite("TogglePill_Off_DarkGlass.png");
            keycapSprite = GlassHudTheme.LoadSprite("Keycap_Badge_Dark.png");
        }

        private void BuildTabHeader(Transform boxTr)
        {
            GameObject headerObj = CreateRect("TabBar", boxTr,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 240f), new Vector2(900f, 48f));
            UnityEngine.UI.HorizontalLayoutGroup hlg = headerObj.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            string[] titles = { "ÂM THANH & HIỂN THỊ", "TRẢI NGHIỆM", "ĐIỀU KHIỂN" };
            for (int i = 0; i < titles.Length; i++)
            {
                int tabIndex = i;
                UnityEngine.UI.Button btn = CreateButton(
                    $"TabButton_{i}",
                    headerObj.transform,
                    Vector2.zero,
                    new Vector2(280f, 46f),
                    titles[i],
                    18f,
                    Color.white,
                    neutralPillSprite,
                    new Color(0.14f, 0.22f, 0.34f, 0.92f),
                    out UnityEngine.UI.Image bg,
                    out _);
                btn.onClick.AddListener(() => SwitchTab(tabIndex));
                tabButtons[i] = btn;
                tabImages[i] = bg;
            }
        }

        private void BuildAudioDisplayTab(Transform bodyTr)
        {
            GameObject tabObj = CreateStretchRect("Tab_AudioDisplay", bodyTr);
            tabPanels[0] = tabObj;

            UnityEngine.UI.VerticalLayoutGroup vlg = tabObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            CreateAudioSliderRow(tabObj.transform, "Âm lượng tổng (Master)",
                out masterSlider, out masterValueText, out masterMuteBtn, out masterMuteImg, out masterMuteText,
                val => GameSettingsManager.Instance.SetMasterVolume(val),
                () => GameSettingsManager.Instance.SetMasterMuted(!GameSettingsManager.Instance.MasterMuted),
                includeTestButton: false);

            CreateAudioSliderRow(tabObj.transform, "Nhạc nền (BGM)",
                out bgmSlider, out bgmValueText, out bgmMuteBtn, out bgmMuteImg, out bgmMuteText,
                val => GameSettingsManager.Instance.SetBgmVolume(val),
                () => GameSettingsManager.Instance.SetBgmMuted(!GameSettingsManager.Instance.BgmMuted),
                includeTestButton: false);

            CreateAudioSliderRow(tabObj.transform, "Hiệu ứng chiến đấu (SFX)",
                out sfxSlider, out sfxValueText, out sfxMuteBtn, out sfxMuteImg, out sfxMuteText,
                val =>
                {
                    GameSettingsManager.Instance.SetSfxVolume(val);
                    PlayDelayedSliderPreview();
                },
                () =>
                {
                    GameSettingsManager.Instance.SetSfxMuted(!GameSettingsManager.Instance.SfxMuted);
                    if (!GameSettingsManager.Instance.SfxMuted) AudioManager.Instance?.PlayTestSfx();
                },
                includeTestButton: true);

            GameObject spacer = new GameObject("AudioDisplayDivider", typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement));
            spacer.transform.SetParent(tabObj.transform, false);
            UnityEngine.UI.LayoutElement spacerLe = spacer.GetComponent<UnityEngine.UI.LayoutElement>();
            spacerLe.preferredHeight = 8f;
            spacerLe.minHeight = 8f;

            CreateSingleButtonRow(tabObj.transform, "Chế độ hiển thị", 260f,
                out fullscreenBtn, out fullscreenImg, out fullscreenText,
                () =>
                {
                    GameSettingsManager.Instance.SetFullscreen(!GameSettingsManager.Instance.Fullscreen);
                    RefreshAllUI();
                });

            CreateSingleButtonRow(tabObj.transform, "Độ phân giải cửa sổ", 260f,
                out resolutionBtn, out _, out resolutionText,
                () =>
                {
                    int next = (GameSettingsManager.Instance.ResolutionIndex + 1) % 3;
                    GameSettingsManager.Instance.SetResolutionIndex(next);
                    RefreshAllUI();
                });

            CreateDualActionRow(tabObj.transform, "Đồng bộ khung hình (VSync / FPS)",
                out vsyncBtn, out vsyncImg, out vsyncText,
                () =>
                {
                    GameSettingsManager.Instance.SetVSyncEnabled(!GameSettingsManager.Instance.VSyncEnabled);
                    RefreshAllUI();
                },
                out fpsBtn, out fpsText,
                () =>
                {
                    int cur = GameSettingsManager.Instance.TargetFps;
                    int next = cur == 60 ? 120 : (cur == 120 ? -1 : 60);
                    GameSettingsManager.Instance.SetTargetFps(next);
                    RefreshAllUI();
                });
        }

        private void BuildGameplayTab(Transform bodyTr)
        {
            GameObject tabObj = CreateStretchRect("Tab_Gameplay", bodyTr);
            tabPanels[1] = tabObj;

            UnityEngine.UI.VerticalLayoutGroup vlg = tabObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.padding = new RectOffset(8, 8, 12, 8);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            CreateShakeRow(tabObj.transform);

            CreateSingleButtonRow(tabObj.transform, "Hiện số sát thương nổi", 180f,
                out damagePopupBtn, out damagePopupImg, out damagePopupText,
                () =>
                {
                    GameSettingsManager.Instance.SetShowDamagePopups(!GameSettingsManager.Instance.ShowDamagePopups);
                    RefreshAllUI();
                });

            CreateSingleButtonRow(tabObj.transform, "Hiệu ứng tia đánh trúng", 180f,
                out hitSparkBtn, out hitSparkImg, out hitSparkText,
                () =>
                {
                    GameSettingsManager.Instance.SetShowHitSparks(!GameSettingsManager.Instance.ShowHitSparks);
                    RefreshAllUI();
                });

            CreateSingleButtonRow(tabObj.transform, "Hiện thanh gợi ý phím trên HUD", 180f,
                out controlsHintBtn, out controlsHintImg, out controlsHintText,
                () =>
                {
                    GameSettingsManager.Instance.SetShowControlsHint(!GameSettingsManager.Instance.ShowControlsHint);
                    RefreshAllUI();
                });

            CreateDoubleTapRow(tabObj.transform);
        }

        private void BuildControlsTab(Transform bodyTr)
        {
            GameObject tabObj = CreateStretchRect("Tab_Controls", bodyTr);
            tabPanels[2] = tabObj;

            // 1. Preset bar
            GameObject presetBar = CreateRect("PresetBar", tabObj.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -4f), new Vector2(888f, 44f));
            UnityEngine.UI.HorizontalLayoutGroup presetHlg = presetBar.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            presetHlg.spacing = 10f;
            presetHlg.childAlignment = TextAnchor.MiddleLeft;
            presetHlg.childControlWidth = false;
            presetHlg.childControlHeight = true;
            presetHlg.childForceExpandWidth = false;

            TMP_Text presetLbl = CreateText("PresetLabel", presetBar.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(106f, 40f),
                "Bộ phím:", 18f, SoftTextColor, TextAlignmentOptions.MidlineLeft, bold: true);
            EnsureLayoutElement(presetLbl.gameObject, 106f, 40f);

            presetDefaultBtn = CreateButton("PresetDefaultBtn", presetBar.transform, Vector2.zero, new Vector2(264f, 42f),
                "MẶC ĐỊNH (WASD + JUIO)", 15f, Color.white, neutralPillSprite, new Color(0.16f, 0.25f, 0.38f, 0.95f),
                out _, out _);
            EnsureLayoutElement(presetDefaultBtn.gameObject, 264f, 42f);
            presetDefaultBtn.onClick.AddListener(() =>
            {
                CancelKeyCaptureInternal(null);
                GameSettingsManager.Instance.ApplyControlPreset(ControlPresetId.DefaultWasd);
                SetKeybindFeedback("Đã chuyển sang bộ phím Mặc định (A/D + J/U/I/O).", SuccessColor);
                RefreshAllUI();
            });

            presetArrowsBtn = CreateButton("PresetArrowsBtn", presetBar.transform, Vector2.zero, new Vector2(210f, 42f),
                "MŨI TÊN + ZXCV", 15f, Color.white, neutralPillSprite, new Color(0.16f, 0.25f, 0.38f, 0.95f),
                out _, out _);
            EnsureLayoutElement(presetArrowsBtn.gameObject, 210f, 42f);
            presetArrowsBtn.onClick.AddListener(() =>
            {
                CancelKeyCaptureInternal(null);
                GameSettingsManager.Instance.ApplyControlPreset(ControlPresetId.ArrowKeysZx);
                SetKeybindFeedback("Đã chuyển sang bộ phím Mũi tên + Z/X/C/V.", SuccessColor);
                RefreshAllUI();
            });

            presetStatusText = CreateText("PresetStatusText", presetBar.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(270f, 40f),
                "Đang dùng: Mặc định", 16f, GoldAccent, TextAlignmentOptions.MidlineRight, bold: true);
            EnsureLayoutElement(presetStatusText.gameObject, 270f, 40f);

            // 2. Conflict Policy & Feedback bar
            GameObject policyBar = CreateRect("ConflictPolicyBar", tabObj.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -54f), new Vector2(888f, 42f));
            UnityEngine.UI.HorizontalLayoutGroup policyHlg = policyBar.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            policyHlg.spacing = 12f;
            policyHlg.childAlignment = TextAnchor.MiddleLeft;
            policyHlg.childControlWidth = false;
            policyHlg.childControlHeight = true;

            swapPolicyBtn = CreateButton("ConflictPolicyBtn", policyBar.transform, Vector2.zero, new Vector2(318f, 40f),
                "Trùng phím: CHẶN TRÙNG", 15f, Color.white, toggleOffSprite ?? neutralPillSprite, new Color(0.18f, 0.22f, 0.30f, 0.95f),
                out swapPolicyImg, out swapPolicyText);
            EnsureLayoutElement(swapPolicyBtn.gameObject, 318f, 40f);
            swapPolicyBtn.onClick.AddListener(() =>
            {
                GameSettingsManager.Instance.SetAutoSwapOnConflict(!GameSettingsManager.Instance.AutoSwapOnConflict);
                RefreshAllUI();
            });

            keybindFeedbackText = CreateText("KeybindFeedbackText", policyBar.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(556f, 40f),
                "Bấm vào một nút phím rồi nhấn phím mới (ESC để hủy).", 15f, NeutralFeedbackColor, TextAlignmentOptions.MidlineLeft, bold: true);
            keybindFeedbackText.textWrappingMode = TextWrappingModes.Normal;
            keybindFeedbackText.enableAutoSizing = true;
            keybindFeedbackText.fontSizeMin = 12f;
            keybindFeedbackText.fontSizeMax = 15.5f;
            EnsureLayoutElement(keybindFeedbackText.gameObject, 556f, 40f);

            // 3. ScrollView for 12 Keybind Rows (strictly follows scrollview-setup hierarchy)
            GameObject scrollObj = CreateRect("KeybindsScrollView", tabObj.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            RectTransform scrollRt = scrollObj.GetComponent<RectTransform>();
            scrollRt.offsetMin = new Vector2(6f, 2f);
            scrollRt.offsetMax = new Vector2(-6f, -104f);

            UnityEngine.UI.Image scrollBg = scrollObj.AddComponent<UnityEngine.UI.Image>();
            scrollBg.color = new Color(0.04f, 0.07f, 0.13f, 0.72f);

            keybindScrollRect = scrollObj.AddComponent<UnityEngine.UI.ScrollRect>();
            keybindScrollRect.horizontal = false;
            keybindScrollRect.vertical = true;
            keybindScrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            keybindScrollRect.scrollSensitivity = 32f;

            GameObject viewportObj = CreateStretchRect("Viewport", scrollObj.transform);
            RectTransform vpRt = viewportObj.GetComponent<RectTransform>();
            vpRt.offsetMin = new Vector2(4f, 4f);
            vpRt.offsetMax = new Vector2(-4f, -4f);
            UnityEngine.UI.Image vpImg = viewportObj.AddComponent<UnityEngine.UI.Image>();
            vpImg.color = Color.white;
            UnityEngine.UI.Mask vpMask = viewportObj.AddComponent<UnityEngine.UI.Mask>();
            vpMask.showMaskGraphic = false;

            GameObject contentObj = CreateRect("Content", viewportObj.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 0f));
            UnityEngine.UI.VerticalLayoutGroup contentVlg = contentObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            contentVlg.spacing = 6f;
            contentVlg.padding = new RectOffset(8, 8, 6, 8);
            contentVlg.childAlignment = TextAnchor.UpperCenter;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = false;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;

            UnityEngine.UI.ContentSizeFitter fitter = contentObj.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            keybindScrollRect.viewport = vpRt;
            keybindScrollRect.content = contentObj.GetComponent<RectTransform>();

            ClearChildrenExcept(contentObj.transform);
            keybindRows.Clear();

            for (int i = 0; i < KeyBindingMap.ActionCount; i++)
            {
                GameInputAction action = (GameInputAction)i;
                GameObject rowObj = CreateRowContainer($"KeyRow_{action}", contentObj.transform, 44f);
                UnityEngine.UI.HorizontalLayoutGroup rowHlg = rowObj.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                rowHlg.spacing = 12f;
                rowHlg.padding = new RectOffset(14, 14, 4, 4);
                rowHlg.childAlignment = TextAnchor.MiddleLeft;
                rowHlg.childControlWidth = false;
                rowHlg.childControlHeight = true;

                TMP_Text actionLbl = CreateText("ActionLabel", rowObj.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(580f, 36f),
                    GameSettingsManager.GetActionDisplayName(action), 18f, Color.white, TextAlignmentOptions.MidlineLeft, bold: true);
                EnsureLayoutElement(actionLbl.gameObject, 580f, 36f, flexibleWidth: 1f);

                bool isPause = action == GameInputAction.Pause;
                UnityEngine.UI.Button keyBtn = CreateButton("KeyBindButton", rowObj.transform, Vector2.zero, new Vector2(210f, 36f),
                    "A", 16f, isPause ? SoftTextColor : GoldAccent, keycapSprite ?? neutralPillSprite, new Color(0.14f, 0.20f, 0.30f, 0.95f),
                    out UnityEngine.UI.Image keyBg, out TMP_Text keyLbl);
                EnsureLayoutElement(keyBtn.gameObject, 210f, 36f);

                ScrollEventForwarder forwarder = keyBtn.gameObject.AddComponent<ScrollEventForwarder>();
                forwarder.Initialize(keybindScrollRect);

                if (isPause)
                {
                    keyBtn.interactable = false;
                }
                else
                {
                    keyBtn.onClick.AddListener(() => BeginKeyCapture(action));
                }

                keybindRows.Add(new KeybindRowWidgets
                {
                    Action = action,
                    KeyButton = keyBtn,
                    KeyButtonBg = keyBg,
                    KeyButtonText = keyLbl
                });
            }
        }

        private void BuildFooter(Transform boxTr)
        {
            UnityEngine.UI.Button resetBtn = CreateButton("ResetDefaultsButton", boxTr,
                new Vector2(-210f, -286f), new Vector2(290f, 52f),
                "KHÔI PHỤC MẶC ĐỊNH", 18f, Color.white, dangerPillSprite, new Color(0.62f, 0.16f, 0.22f, 0.95f),
                out _, out _);
            resetBtn.onClick.AddListener(() =>
            {
                CancelKeyCaptureInternal(null);
                GameSettingsManager.Instance.ResetToDefaults();
                RefreshAllUI();
                SetKeybindFeedback("Đã khôi phục cài đặt mặc định (giữ nguyên tài khoản & xu).", SuccessColor);
            });

            UnityEngine.UI.Button closeBtn = CreateButton("CloseSettingsButton", boxTr,
                new Vector2(240f, -286f), new Vector2(230f, 52f),
                "ĐÓNG", 20f, Color.white, primaryPillSprite, new Color(0.86f, 0.48f, 0.10f, 0.96f),
                out _, out _);
            closeBtn.onClick.AddListener(Close);
        }

        private void SwitchTab(int index)
        {
            activeTabIndex = Mathf.Clamp(index, 0, tabPanels.Length - 1);
            if (activeTabIndex != 2)
            {
                CancelKeyCaptureInternal(null);
            }

            for (int i = 0; i < tabPanels.Length; i++)
            {
                if (tabPanels[i] != null)
                {
                    tabPanels[i].SetActive(i == activeTabIndex);
                }

                if (tabImages[i] != null)
                {
                    Sprite targetSprite = i == activeTabIndex ? (primaryPillSprite ?? neutralPillSprite) : neutralPillSprite;
                    if (targetSprite != null)
                    {
                        GlassHudTheme.ApplySlicedSprite(tabImages[i], targetSprite, 280f, 46f,
                            i == activeTabIndex ? Color.white : new Color(0.82f, 0.88f, 0.96f, 0.85f));
                    }
                    else
                    {
                        tabImages[i].color = i == activeTabIndex
                            ? new Color(0.92f, 0.56f, 0.14f, 0.95f)
                            : new Color(0.14f, 0.22f, 0.34f, 0.85f);
                    }
                }
            }
        }

        private void RefreshAllUI()
        {
            if (!isBuilt || !Application.isPlaying || !gameObject.scene.isLoaded) return;
            GameSettingsManager s = GameSettingsManager.Instance;
            if (s == null) return;

            isRefreshingUi = true;
            try
            {
                // Tab 0: Audio
                if (masterSlider != null) masterSlider.value = s.MasterVolume;
                if (masterValueText != null) masterValueText.text = $"{Mathf.RoundToInt(s.MasterVolume * 100f)}%";
                StyleMuteToggle(masterMuteBtn, masterMuteImg, masterMuteText, !s.MasterMuted);

                if (bgmSlider != null) bgmSlider.value = s.BgmVolume;
                if (bgmValueText != null) bgmValueText.text = $"{Mathf.RoundToInt(s.BgmVolume * 100f)}%";
                StyleMuteToggle(bgmMuteBtn, bgmMuteImg, bgmMuteText, !s.BgmMuted);

                if (sfxSlider != null) sfxSlider.value = s.SfxVolume;
                if (sfxValueText != null) sfxValueText.text = $"{Mathf.RoundToInt(s.SfxVolume * 100f)}%";
                StyleMuteToggle(sfxMuteBtn, sfxMuteImg, sfxMuteText, !s.SfxMuted);

                // Tab 0: Display
                StyleBooleanToggle(fullscreenBtn, fullscreenImg, fullscreenText, s.Fullscreen, "TOÀN MÀN HÌNH", "CỬA SỔ", 260f, 40f);
                if (resolutionText != null)
                {
                    SettingsDataDTO.GetResolutionSize(s.ResolutionIndex, out int w, out int h);
                    resolutionText.text = $"{w} x {h}";
                }
                StyleBooleanToggle(vsyncBtn, vsyncImg, vsyncText, s.VSyncEnabled, "VSync: BẬT", "VSync: TẮT", 170f, 40f);
                if (fpsText != null)
                {
                    fpsText.text = s.VSyncEnabled
                        ? "FPS: Theo VSync"
                        : (s.TargetFps < 0 ? "FPS: Mở rộng" : $"FPS: {s.TargetFps}");
                }
                if (fpsBtn != null)
                {
                    fpsBtn.interactable = !s.VSyncEnabled;
                }

                // Tab 1: Gameplay & HUD
                StyleMuteToggle(shakeToggleBtn, shakeToggleImg, shakeToggleText, s.ScreenShakeEnabled);
                if (shakeSlider != null)
                {
                    shakeSlider.value = s.ScreenShakeIntensity;
                    shakeSlider.interactable = s.ScreenShakeEnabled;
                }
                if (shakeValueText != null)
                {
                    shakeValueText.text = s.ScreenShakeEnabled ? $"{Mathf.RoundToInt(s.ScreenShakeIntensity * 100f)}%" : "TẮT";
                }

                StyleMuteToggle(damagePopupBtn, damagePopupImg, damagePopupText, s.ShowDamagePopups);
                StyleMuteToggle(hitSparkBtn, hitSparkImg, hitSparkText, s.ShowHitSparks);
                StyleMuteToggle(controlsHintBtn, controlsHintImg, controlsHintText, s.ShowControlsHint);

                if (doubleTapSlider != null) doubleTapSlider.value = s.DoubleTapThreshold;
                if (doubleTapValueText != null) doubleTapValueText.text = $"{s.DoubleTapThreshold:0.00}s";

                // Tab 2: Controls
                if (presetStatusText != null)
                {
                    string presetName = s.ControlPreset == ControlPresetId.DefaultWasd
                        ? "Mặc định"
                        : (s.ControlPreset == ControlPresetId.ArrowKeysZx ? "Mũi tên" : "Tùy chỉnh");
                    presetStatusText.text = $"Đang dùng: {presetName}";
                }

                StyleBooleanToggle(
                    swapPolicyBtn,
                    swapPolicyImg,
                    swapPolicyText,
                    s.AutoSwapOnConflict,
                    "Trùng phím: TỰ HOÁN ĐỔI",
                    "Trùng phím: CHẶN TRÙNG",
                    318f,
                    40f);

                for (int i = 0; i < keybindRows.Count; i++)
                {
                    KeybindRowWidgets row = keybindRows[i];
                    if (row.KeyButtonText == null) continue;

                    bool isWaiting = capturingAction.HasValue && capturingAction.Value == row.Action;
                    if (isWaiting)
                    {
                        row.KeyButtonText.text = "[ NHẤN PHÍM... ]";
                        row.KeyButtonText.color = CyanAccent;
                    }
                    else if (row.Action == GameInputAction.Pause)
                    {
                        row.KeyButtonText.text = "[ ESC - Cố định ]";
                        row.KeyButtonText.color = SoftTextColor;
                    }
                    else
                    {
                        string lbl = GameSettingsManager.FormatKeyLabel(s.GetKeyCode(row.Action));
                        row.KeyButtonText.text = $"[ {lbl} ]";
                        row.KeyButtonText.color = GoldAccent;
                    }
                }
            }
            finally
            {
                isRefreshingUi = false;
            }
        }

        private void BeginKeyCapture(GameInputAction action)
        {
            if (action == GameInputAction.Pause)
            {
                SetKeybindFeedback("Phím Tạm dừng được khóa cố định ở ESC.", ConflictColor);
                AudioManager.Instance?.PlayUiSfx(UiSfxType.Error, 0.90f);
                return;
            }

            capturingAction = action;
            GameSettingsManager.IsCapturingKeyRebind = true;
            EventSystem.current?.SetSelectedGameObject(null);
            SetKeybindFeedback($"Đang chờ phím mới cho [{GameSettingsManager.GetActionDisplayName(action)}] (nhấn ESC để hủy)...", CyanAccent);
            RefreshAllUI();
        }

        private void CompleteKeyCapture(KeyCode newKey)
        {
            if (!capturingAction.HasValue) return;
            GameInputAction target = capturingAction.Value;
            capturingAction = null;
            GameSettingsManager.IsCapturingKeyRebind = false;
            EventSystem.current?.SetSelectedGameObject(null);

            KeyBindChangeResult res = GameSettingsManager.Instance.TryRebindKey(target, newKey);
            string actionName = GameSettingsManager.GetActionDisplayName(target);
            string newKeyLabel = GameSettingsManager.FormatKeyLabel(newKey);
            string prevKeyLabel = GameSettingsManager.FormatKeyLabel((KeyCode)res.PreviousKeyCode);
            string conflictName = res.ConflictAction.HasValue
                ? GameSettingsManager.GetActionDisplayName(res.ConflictAction.Value)
                : string.Empty;

            switch (res.Status)
            {
                case KeyBindChangeStatus.Applied:
                    SetKeybindFeedback($"Đã gán [{newKeyLabel}] cho [{actionName}].", SuccessColor);
                    AudioManager.Instance?.PlayUiSfx(UiSfxType.Confirm, 0.85f);
                    break;
                case KeyBindChangeStatus.Swapped:
                    SetKeybindFeedback($"Đã hoán đổi [{newKeyLabel}] <-> [{prevKeyLabel}] với [{conflictName}].", CyanAccent);
                    AudioManager.Instance?.PlayUiSfx(UiSfxType.Confirm, 0.85f);
                    break;
                case KeyBindChangeStatus.Unchanged:
                    SetKeybindFeedback($"[{actionName}] giữ nguyên phím [{newKeyLabel}].", NeutralFeedbackColor);
                    break;
                case KeyBindChangeStatus.ConflictRejected:
                    SetKeybindFeedback($"Trùng phím! [{newKeyLabel}] đang dùng cho [{conflictName}] — không thể gán 2 nút vào 1 phím.", ConflictColor);
                    AudioManager.Instance?.PlayUiSfx(UiSfxType.Error, 0.90f);
                    break;
                case KeyBindChangeStatus.ReservedKeyRejected:
                    SetKeybindFeedback($"Phím [{newKeyLabel}] được giữ cho Tạm dừng, không thể gán cho [{actionName}].", ConflictColor);
                    AudioManager.Instance?.PlayUiSfx(UiSfxType.Error, 0.90f);
                    break;
                case KeyBindChangeStatus.ReservedActionLocked:
                    SetKeybindFeedback("Phím Tạm dừng được khóa cố định ở ESC.", ConflictColor);
                    AudioManager.Instance?.PlayUiSfx(UiSfxType.Error, 0.90f);
                    break;
                default:
                    SetKeybindFeedback($"Phím [{newKeyLabel}] không được hỗ trợ.", ConflictColor);
                    AudioManager.Instance?.PlayUiSfx(UiSfxType.Error, 0.90f);
                    break;
            }

            RefreshAllUI();
        }

        private void CancelKeyCaptureInternal(string feedbackMessage, bool refreshUi = true)
        {
            capturingAction = null;
            GameSettingsManager.IsCapturingKeyRebind = false;
            EventSystem.current?.SetSelectedGameObject(null);
            if (!string.IsNullOrEmpty(feedbackMessage))
            {
                SetKeybindFeedback(feedbackMessage, NeutralFeedbackColor);
            }
            if (refreshUi && isBuilt && IsOpen && gameObject.activeInHierarchy && gameObject.scene.isLoaded)
            {
                RefreshAllUI();
            }
        }

        private void SetKeybindFeedback(string text, Color color)
        {
            if (keybindFeedbackText != null)
            {
                keybindFeedbackText.text = text;
                keybindFeedbackText.color = color;
            }
        }

        private void PlayDelayedSliderPreview()
        {
            if (isRefreshingUi) return;
            if (Time.unscaledTime < nextSfxPreviewTime) return;
            nextSfxPreviewTime = Time.unscaledTime + 0.25f;
            AudioManager.Instance?.PlayTestSfx();
        }

        // ==================== UI Builders ====================

        private void CreateAudioSliderRow(
            Transform parent,
            string title,
            out UnityEngine.UI.Slider slider,
            out TMP_Text valueText,
            out UnityEngine.UI.Button muteBtn,
            out UnityEngine.UI.Image muteImg,
            out TMP_Text muteText,
            Action<float> onSliderChanged,
            Action onMuteClicked,
            bool includeTestButton)
        {
            GameObject rowObj = CreateRowContainer($"Row_{title}", parent, 52f);
            UnityEngine.UI.HorizontalLayoutGroup hlg = rowObj.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.padding = new RectOffset(16, 16, 6, 6);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            TMP_Text lbl = CreateText("Label", rowObj.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(270f, 38f),
                title, 18f, Color.white, TextAlignmentOptions.MidlineLeft, bold: true);
            EnsureLayoutElement(lbl.gameObject, 270f, 38f);

            float sliderW = includeTestButton ? 310f : 390f;
            slider = CreateSlider("Slider", rowObj.transform, sliderW, 24f, 0f, 1f);
            UnityEngine.UI.Slider capturedSlider = slider;
            slider.onValueChanged.AddListener(v =>
            {
                if (isRefreshingUi) return;
                onSliderChanged?.Invoke(v);
                RefreshAllUI();
            });
            // Keep warning free reference usage
            _ = capturedSlider;

            valueText = CreateText("ValueText", rowObj.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(64f, 38f),
                "100%", 17f, GoldAccent, TextAlignmentOptions.Center, bold: true);
            EnsureLayoutElement(valueText.gameObject, 64f, 38f);

            muteBtn = CreateButton("MuteButton", rowObj.transform, Vector2.zero, new Vector2(96f, 36f),
                "BẬT", 16f, Color.white, toggleOnSprite ?? greenPillSprite, new Color(0.16f, 0.52f, 0.34f, 0.95f),
                out muteImg, out muteText);
            EnsureLayoutElement(muteBtn.gameObject, 96f, 36f);
            muteBtn.onClick.AddListener(() =>
            {
                onMuteClicked?.Invoke();
                RefreshAllUI();
            });

            if (includeTestButton)
            {
                UnityEngine.UI.Button testBtn = CreateButton("TestSfxButton", rowObj.transform, Vector2.zero, new Vector2(68f, 36f),
                    "THỬ", 15f, Color.white, neutralPillSprite, new Color(0.20f, 0.34f, 0.54f, 0.95f),
                    out _, out _);
                EnsureLayoutElement(testBtn.gameObject, 68f, 36f);
                testBtn.onClick.AddListener(() => AudioManager.Instance?.PlayTestSfx());
            }
        }

        private void CreateSingleButtonRow(
            Transform parent,
            string title,
            float buttonWidth,
            out UnityEngine.UI.Button btn,
            out UnityEngine.UI.Image img,
            out TMP_Text btnText,
            Action onClick)
        {
            GameObject rowObj = CreateRowContainer($"Row_{title}", parent, 52f);
            UnityEngine.UI.HorizontalLayoutGroup hlg = rowObj.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.padding = new RectOffset(16, 16, 6, 6);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            TMP_Text lbl = CreateText("Label", rowObj.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(520f, 38f),
                title, 18f, Color.white, TextAlignmentOptions.MidlineLeft, bold: true);
            EnsureLayoutElement(lbl.gameObject, 520f, 38f, flexibleWidth: 1f);

            btn = CreateButton("ActionButton", rowObj.transform, Vector2.zero, new Vector2(buttonWidth, 38f),
                "BẬT", 16f, Color.white, neutralPillSprite, new Color(0.16f, 0.28f, 0.42f, 0.95f),
                out img, out btnText);
            EnsureLayoutElement(btn.gameObject, buttonWidth, 38f);
            btn.onClick.AddListener(() => onClick?.Invoke());
        }

        private void CreateDualActionRow(
            Transform parent,
            string title,
            out UnityEngine.UI.Button leftBtn,
            out UnityEngine.UI.Image leftImg,
            out TMP_Text leftText,
            Action onLeftClick,
            out UnityEngine.UI.Button rightBtn,
            out TMP_Text rightText,
            Action onRightClick)
        {
            GameObject rowObj = CreateRowContainer($"Row_{title}", parent, 52f);
            UnityEngine.UI.HorizontalLayoutGroup hlg = rowObj.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.padding = new RectOffset(16, 16, 6, 6);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            TMP_Text lbl = CreateText("Label", rowObj.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(440f, 38f),
                title, 18f, Color.white, TextAlignmentOptions.MidlineLeft, bold: true);
            EnsureLayoutElement(lbl.gameObject, 440f, 38f, flexibleWidth: 1f);

            leftBtn = CreateButton("VSyncButton", rowObj.transform, Vector2.zero, new Vector2(170f, 38f),
                "VSync: BẬT", 15f, Color.white, toggleOnSprite ?? greenPillSprite, new Color(0.16f, 0.52f, 0.34f, 0.95f),
                out leftImg, out leftText);
            EnsureLayoutElement(leftBtn.gameObject, 170f, 38f);
            leftBtn.onClick.AddListener(() => onLeftClick?.Invoke());

            rightBtn = CreateButton("FpsButton", rowObj.transform, Vector2.zero, new Vector2(190f, 38f),
                "FPS: 60", 15f, Color.white, neutralPillSprite, new Color(0.16f, 0.28f, 0.42f, 0.95f),
                out _, out rightText);
            EnsureLayoutElement(rightBtn.gameObject, 190f, 38f);
            rightBtn.onClick.AddListener(() => onRightClick?.Invoke());
        }

        private void CreateShakeRow(Transform parent)
        {
            GameObject rowObj = CreateRowContainer("Row_ScreenShake", parent, 54f);
            UnityEngine.UI.HorizontalLayoutGroup hlg = rowObj.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.padding = new RectOffset(16, 16, 6, 6);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            TMP_Text lbl = CreateText("Label", rowObj.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(270f, 38f),
                "Rung màn hình", 18f, Color.white, TextAlignmentOptions.MidlineLeft, bold: true);
            EnsureLayoutElement(lbl.gameObject, 270f, 38f);

            shakeSlider = CreateSlider("ShakeSlider", rowObj.transform, 390f, 24f, 0f, 1f);
            shakeSlider.onValueChanged.AddListener(v =>
            {
                if (isRefreshingUi) return;
                GameSettingsManager.Instance.SetScreenShakeIntensity(v);
                RefreshAllUI();
            });

            shakeValueText = CreateText("ShakeValueText", rowObj.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(64f, 38f),
                "100%", 17f, GoldAccent, TextAlignmentOptions.Center, bold: true);
            EnsureLayoutElement(shakeValueText.gameObject, 64f, 38f);

            shakeToggleBtn = CreateButton("ShakeToggleBtn", rowObj.transform, Vector2.zero, new Vector2(96f, 36f),
                "BẬT", 16f, Color.white, toggleOnSprite ?? greenPillSprite, new Color(0.16f, 0.52f, 0.34f, 0.95f),
                out shakeToggleImg, out shakeToggleText);
            EnsureLayoutElement(shakeToggleBtn.gameObject, 96f, 36f);
            shakeToggleBtn.onClick.AddListener(() =>
            {
                GameSettingsManager.Instance.SetScreenShakeEnabled(!GameSettingsManager.Instance.ScreenShakeEnabled);
                RefreshAllUI();
            });
        }

        private void CreateDoubleTapRow(Transform parent)
        {
            GameObject rowObj = CreateRowContainer("Row_DoubleTap", parent, 54f);
            UnityEngine.UI.HorizontalLayoutGroup hlg = rowObj.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.padding = new RectOffset(16, 16, 6, 6);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            TMP_Text lbl = CreateText("Label", rowObj.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(320f, 38f),
                "Độ nhạy Double-Tap lướt", 18f, Color.white, TextAlignmentOptions.MidlineLeft, bold: true);
            EnsureLayoutElement(lbl.gameObject, 320f, 38f);

            doubleTapSlider = CreateSlider(
                "DoubleTapSlider",
                rowObj.transform,
                420f,
                24f,
                SettingsDataDTO.MinDoubleTapThreshold,
                SettingsDataDTO.MaxDoubleTapThreshold);
            doubleTapSlider.onValueChanged.AddListener(v =>
            {
                if (isRefreshingUi) return;
                GameSettingsManager.Instance.SetDoubleTapThreshold(v);
                RefreshAllUI();
            });

            doubleTapValueText = CreateText("DoubleTapValueText", rowObj.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(88f, 38f),
                "0.25s", 17f, GoldAccent, TextAlignmentOptions.Center, bold: true);
            EnsureLayoutElement(doubleTapValueText.gameObject, 88f, 38f);
        }

        private GameObject CreateRowContainer(string name, Transform parent, float height)
        {
            GameObject rowObj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
            rowObj.transform.SetParent(parent, false);
            RectTransform rt = rowObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, height);

            UnityEngine.UI.Image bg = rowObj.GetComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.08f, 0.13f, 0.22f, 0.72f);
            bg.raycastTarget = false;

            UnityEngine.UI.LayoutElement le = rowObj.GetComponent<UnityEngine.UI.LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return rowObj;
        }

        private UnityEngine.UI.Slider CreateSlider(string name, Transform parent, float width, float height, float minVal, float maxVal)
        {
            GameObject sliderObj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Slider), typeof(UnityEngine.UI.LayoutElement));
            sliderObj.transform.SetParent(parent, false);
            RectTransform sliderRt = sliderObj.GetComponent<RectTransform>();
            sliderRt.sizeDelta = new Vector2(width, height);
            EnsureLayoutElement(sliderObj, width, height);

            GameObject bgObj = CreateStretchRect("Background", sliderObj.transform);
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.25f);
            bgRt.anchorMax = new Vector2(1f, 0.75f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            UnityEngine.UI.Image bgImg = bgObj.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = SliderTrackColor;

            GameObject fillAreaObj = CreateStretchRect("Fill Area", sliderObj.transform);
            RectTransform fillAreaRt = fillAreaObj.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRt.offsetMin = new Vector2(2f, 0f);
            fillAreaRt.offsetMax = new Vector2(-10f, 0f);

            GameObject fillObj = CreateStretchRect("Fill", fillAreaObj.transform);
            UnityEngine.UI.Image fillImg = fillObj.AddComponent<UnityEngine.UI.Image>();
            fillImg.color = CyanAccent;
            fillImg.raycastTarget = false;

            GameObject handleAreaObj = CreateStretchRect("Handle Slide Area", sliderObj.transform);
            RectTransform handleAreaRt = handleAreaObj.GetComponent<RectTransform>();
            handleAreaRt.offsetMin = new Vector2(8f, 0f);
            handleAreaRt.offsetMax = new Vector2(-8f, 0f);

            GameObject handleObj = CreateRect("Handle", handleAreaObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(20f, 24f));
            UnityEngine.UI.Image handleImg = handleObj.AddComponent<UnityEngine.UI.Image>();
            if (keycapSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(handleImg, keycapSprite, 20f, 24f, GoldAccent);
            }
            else
            {
                handleImg.color = GoldAccent;
            }

            UnityEngine.UI.Slider slider = sliderObj.GetComponent<UnityEngine.UI.Slider>();
            slider.minValue = minVal;
            slider.maxValue = maxVal;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.fillRect = fillObj.GetComponent<RectTransform>();
            slider.handleRect = handleObj.GetComponent<RectTransform>();
            slider.targetGraphic = handleImg;
            slider.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            return slider;
        }

        private UnityEngine.UI.Button CreateButton(
            string name,
            Transform parent,
            Vector2 anchoredPos,
            Vector2 size,
            string label,
            float fontSize,
            Color textColor,
            Sprite bgSprite,
            Color fallbackBgColor,
            out UnityEngine.UI.Image bgImage,
            out TMP_Text labelTmp)
        {
            GameObject btnObj = CreateRect(name, parent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                anchoredPos, size);

            bgImage = btnObj.AddComponent<UnityEngine.UI.Image>();
            if (bgSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(bgImage, bgSprite, size.x, size.y, Color.white);
            }
            else
            {
                bgImage.color = fallbackBgColor;
            }
            bgImage.raycastTarget = true;

            UnityEngine.UI.Button btn = btnObj.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bgImage;
            btn.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };

            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.92f, 0.97f, 1f, 1f);
            cb.pressedColor = new Color(0.76f, 0.82f, 0.90f, 1f);
            cb.disabledColor = new Color(0.55f, 0.58f, 0.64f, 0.72f);
            btn.colors = cb;

            GameObject lblObj = CreateStretchRect("Label", btnObj.transform);
            RectTransform lblRt = lblObj.GetComponent<RectTransform>();
            lblRt.offsetMin = new Vector2(8f, 2f);
            lblRt.offsetMax = new Vector2(-8f, -2f);

            labelTmp = lblObj.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.textWrappingMode = TextWrappingModes.NoWrap;
            labelTmp.enableAutoSizing = true;
            labelTmp.fontSizeMin = 11f;
            labelTmp.fontSizeMax = fontSize;
            labelTmp.raycastTarget = false;
            if (uiFont != null) labelTmp.font = uiFont;
            GlassHudTheme.StyleTmpText(labelTmp, fontSize, textColor, bold: true, addShadow: true);

            return btn;
        }

        private TMP_Text CreateText(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 size,
            string content,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            bool bold)
        {
            GameObject txtObj = CreateRect(name, parent, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), anchoredPos, size);
            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            if (uiFont != null) tmp.font = uiFont;
            GlassHudTheme.StyleTmpText(tmp, fontSize, color, bold, addShadow: true);
            return tmp;
        }

        private void StyleMuteToggle(UnityEngine.UI.Button btn, UnityEngine.UI.Image img, TMP_Text txt, bool isEnabled)
        {
            StyleBooleanToggle(btn, img, txt, isEnabled, "BẬT", "TẮT", 96f, 36f);
        }

        private void StyleBooleanToggle(
            UnityEngine.UI.Button btn,
            UnityEngine.UI.Image img,
            TMP_Text txt,
            bool isOn,
            string onLabel,
            string offLabel,
            float width,
            float height)
        {
            if (txt != null)
            {
                txt.text = isOn ? onLabel : offLabel;
                txt.color = isOn ? Color.white : new Color(0.84f, 0.88f, 0.94f, 1f);
            }

            if (img != null)
            {
                Sprite target = isOn ? (toggleOnSprite ?? greenPillSprite) : (toggleOffSprite ?? neutralPillSprite);
                if (target != null)
                {
                    GlassHudTheme.ApplySlicedSprite(img, target, width, height, Color.white);
                }
                else
                {
                    img.color = isOn
                        ? new Color(0.16f, 0.54f, 0.36f, 0.95f)
                        : new Color(0.18f, 0.22f, 0.30f, 0.95f);
                }
            }
        }

        private static void CreateDivider(string name, Transform parent, Vector2 anchoredPos, Vector2 size)
        {
            GameObject obj = CreateRect(name, parent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                anchoredPos, size);
            UnityEngine.UI.Image img = obj.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.70f, 0.94f, 1f, 0.24f);
            img.raycastTarget = false;
        }

        private static GameObject CreateStretchRect(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return obj;
        }

        private static GameObject CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return obj;
        }

        private static void EnsureLayoutElement(GameObject go, float preferredW, float preferredH, float flexibleWidth = -1f)
        {
            UnityEngine.UI.LayoutElement le = go.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le == null) le = go.AddComponent<UnityEngine.UI.LayoutElement>();
            le.preferredWidth = preferredW;
            le.minWidth = preferredW;
            le.preferredHeight = preferredH;
            le.minHeight = preferredH;
            if (flexibleWidth >= 0f)
            {
                le.flexibleWidth = flexibleWidth;
            }
        }

        private static void ClearChildrenExcept(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child == null) continue;
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        // ==================== Pop-In Animation (Unscaled Time) ====================

        private void PlayPopIn()
        {
            if (boxRect == null) return;
            StopPopInAndReset();

            if (!Application.isPlaying || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                boxRect.localScale = Vector3.one;
                if (boxCanvasGroup != null) boxCanvasGroup.alpha = 1f;
                return;
            }

            popInCoroutine = StartCoroutine(AnimatePopIn());
        }

        private void StopPopInAndReset()
        {
            if (popInCoroutine != null)
            {
                StopCoroutine(popInCoroutine);
                popInCoroutine = null;
            }
            if (boxRect != null) boxRect.localScale = Vector3.one;
            if (boxCanvasGroup != null) boxCanvasGroup.alpha = 1f;
        }

        private IEnumerator AnimatePopIn()
        {
            if (boxRect == null)
            {
                popInCoroutine = null;
                yield break;
            }

            boxRect.localScale = new Vector3(PopInStartScale, PopInStartScale, 1f);
            if (boxCanvasGroup != null) boxCanvasGroup.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < PopInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / PopInDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float scale = Mathf.Lerp(PopInStartScale, 1f, eased);
                if (boxRect != null) boxRect.localScale = new Vector3(scale, scale, 1f);
                if (boxCanvasGroup != null) boxCanvasGroup.alpha = eased;
                yield return null;
            }

            if (boxRect != null) boxRect.localScale = Vector3.one;
            if (boxCanvasGroup != null) boxCanvasGroup.alpha = 1f;
            popInCoroutine = null;
        }

        // ==================== Key Input Polling ====================

        private static bool WasKeyPressedThisFrame(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            KeyControl ctrl = GetKeyControl(kb, keyCode);
            return ctrl != null && ctrl.wasPressedThisFrame;
#else
            return Input.GetKeyDown(keyCode);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static KeyControl GetKeyControl(Keyboard kb, KeyCode keyCode)
        {
            return keyCode switch
            {
                KeyCode.A => kb.aKey,
                KeyCode.B => kb.bKey,
                KeyCode.C => kb.cKey,
                KeyCode.D => kb.dKey,
                KeyCode.E => kb.eKey,
                KeyCode.F => kb.fKey,
                KeyCode.G => kb.gKey,
                KeyCode.H => kb.hKey,
                KeyCode.I => kb.iKey,
                KeyCode.J => kb.jKey,
                KeyCode.K => kb.kKey,
                KeyCode.L => kb.lKey,
                KeyCode.M => kb.mKey,
                KeyCode.N => kb.nKey,
                KeyCode.O => kb.oKey,
                KeyCode.P => kb.pKey,
                KeyCode.Q => kb.qKey,
                KeyCode.R => kb.rKey,
                KeyCode.S => kb.sKey,
                KeyCode.T => kb.tKey,
                KeyCode.U => kb.uKey,
                KeyCode.V => kb.vKey,
                KeyCode.W => kb.wKey,
                KeyCode.X => kb.xKey,
                KeyCode.Y => kb.yKey,
                KeyCode.Z => kb.zKey,
                KeyCode.Alpha0 => kb.digit0Key,
                KeyCode.Alpha1 => kb.digit1Key,
                KeyCode.Alpha2 => kb.digit2Key,
                KeyCode.Alpha3 => kb.digit3Key,
                KeyCode.Alpha4 => kb.digit4Key,
                KeyCode.Alpha5 => kb.digit5Key,
                KeyCode.Alpha6 => kb.digit6Key,
                KeyCode.Alpha7 => kb.digit7Key,
                KeyCode.Alpha8 => kb.digit8Key,
                KeyCode.Alpha9 => kb.digit9Key,
                KeyCode.Space => kb.spaceKey,
                KeyCode.Tab => kb.tabKey,
                KeyCode.Return => kb.enterKey,
                KeyCode.Escape => kb.escapeKey,
                KeyCode.UpArrow => kb.upArrowKey,
                KeyCode.DownArrow => kb.downArrowKey,
                KeyCode.RightArrow => kb.rightArrowKey,
                KeyCode.LeftArrow => kb.leftArrowKey,
                KeyCode.LeftShift => kb.leftShiftKey,
                KeyCode.RightShift => kb.rightShiftKey,
                KeyCode.LeftControl => kb.leftCtrlKey,
                KeyCode.RightControl => kb.rightCtrlKey,
                _ => null
            };
        }
#endif
    }
}
