using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Economy;
using StickmanBeatEmUp.Items;
using StickmanBeatEmUp.Level;
using StickmanBeatEmUp.Persistence;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Quản lý menu Tạm Dừng (Pause) trong trận đấu dạng Floating Dark Glassmorphism Modal:
    /// - Kích hoạt qua nút [ || TẠM DỪNG ] trên HUD hoặc phím Escape (InputHandler.OnPauseEvent)
    /// - 3 nút pill: "TIẾP TỤC", "CHƠI LẠI" (không hoàn lại Q/E), "VỀ MENU"
    /// - Đảm bảo Time.timeScale = 1f khi tiếp tục, chơi lại, thoát hoặc OnDestroy.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        private const float POP_IN_DURATION = 0.14f;
        private const float POP_IN_START_SCALE = 0.90f;

        [Header("Pause Modal & Buttons")]
        [SerializeField] private GameObject pauseModalPanel;
        [SerializeField] private Button pauseHudButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button exitMenuButton;

        [Header("Scene Navigation")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private InputHandler boundInputHandler;
        private bool isPaused;
        private bool isLevelSubscribed;
        private int lastToggleFrame = -1;
        private float nextInputLookupTime;

        private RectTransform pauseBoxRect;
        private CanvasGroup pauseBoxCanvasGroup;
        private Image pauseBoxImage;
        private Coroutine popInCoroutine;

        private GameObject runtimeUtilityBar;
        private Button runtimeSaveButton;
        private Button runtimeLoadButton;
        private TMP_Text runtimeSaveFeedbackText;

        public bool IsPaused => isPaused;

        public void Configure(
            GameObject modalPanel,
            Button openPauseBtn,
            Button resumeBtn,
            Button retryBtn,
            Button exitMenuBtn)
        {
            pauseModalPanel = modalPanel;
            pauseHudButton = openPauseBtn;
            resumeButton = resumeBtn;
            retryButton = retryBtn;
            exitMenuButton = exitMenuBtn;

            WireButtons();
            ApplyGlassModalTheme();

            if (pauseModalPanel != null)
            {
                pauseModalPanel.SetActive(isPaused);
            }
        }

        public void BindInputHandler(InputHandler inputHandler)
        {
            if (boundInputHandler == inputHandler) return;

            UnbindInputHandler();
            boundInputHandler = inputHandler;
            if (boundInputHandler != null)
            {
                boundInputHandler.OnPauseEvent -= HandlePauseTriggered;
                boundInputHandler.OnPauseEvent += HandlePauseTriggered;
            }
        }

        public void TogglePause()
        {
            SetPaused(!isPaused);
        }

        public void SetPaused(bool paused)
        {
            if (paused)
            {
                GameUIManager gUi = GameUIManager.Instance != null
                    ? GameUIManager.Instance
                    : FindAnyObjectByType<GameUIManager>();
                if (gUi != null && gUi.IsEndScreenOpen)
                {
                    return;
                }
            }

            bool wasPaused = isPaused;
            bool wasPanelActive = pauseModalPanel != null && pauseModalPanel.activeSelf;

            isPaused = paused;
            Time.timeScale = isPaused ? 0f : 1f;

            if (pauseModalPanel != null)
            {
                pauseModalPanel.SetActive(isPaused);
            }

            if (isPaused)
            {
                if (pauseBoxImage == null || pauseBoxImage.sprite == null)
                {
                    ApplyGlassModalTheme();
                }

                EnsureRuntimeUtilityBar();
                RefreshUtilityBarState();

                if (!wasPaused || !wasPanelActive)
                {
                    PlayPauseBoxPopIn();
                }
            }
            else
            {
                StopPauseBoxPopInAndReset();
            }
        }

        public void ApplyGlassModalTheme()
        {
            if (pauseModalPanel == null)
            {
                Transform foundPanel = transform.Find("PauseModalPanel");
                if (foundPanel != null)
                {
                    pauseModalPanel = foundPanel.gameObject;
                }
            }

            if (pauseModalPanel == null) return;

            Image dimImg = pauseModalPanel.GetComponent<Image>();
            if (dimImg != null)
            {
                dimImg.sprite = null;
                dimImg.type = Image.Type.Simple;
                dimImg.color = new Color(0.02f, 0.04f, 0.08f, 0.56f);
                dimImg.raycastTarget = true;
            }

            Transform boxTr = ResolvePauseBoxTransform();
            if (boxTr == null) return;

            pauseBoxRect = boxTr.GetComponent<RectTransform>();
            if (pauseBoxRect != null)
            {
                pauseBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
                pauseBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
                pauseBoxRect.pivot = new Vector2(0.5f, 0.5f);
                pauseBoxRect.anchoredPosition = Vector2.zero;
                pauseBoxRect.sizeDelta = new Vector2(540f, 430f);
            }

            RemoveFlatOutlines(boxTr.gameObject);

            pauseBoxImage = boxTr.GetComponent<Image>();
            if (pauseBoxImage == null)
            {
                pauseBoxImage = boxTr.gameObject.AddComponent<Image>();
            }
            Sprite boxSp = GlassHudTheme.LoadSprite("ModalCard_DarkGlass_Cyan.png");
            if (boxSp != null)
            {
                GlassHudTheme.ApplySlicedSprite(pauseBoxImage, boxSp, 540f, 430f, Color.white);
            }
            else
            {
                pauseBoxImage.color = new Color(0.06f, 0.11f, 0.19f, 0.94f);
            }
            pauseBoxImage.raycastTarget = true;

            pauseBoxCanvasGroup = boxTr.GetComponent<CanvasGroup>();
            if (pauseBoxCanvasGroup == null)
            {
                pauseBoxCanvasGroup = boxTr.gameObject.AddComponent<CanvasGroup>();
            }
            pauseBoxCanvasGroup.interactable = true;
            pauseBoxCanvasGroup.blocksRaycasts = true;

            StylePauseTitle(boxTr);
            EnsurePauseDivider(boxTr);

            StylePauseButton(resumeButton, boxTr, "PillBtn_OrangePrimary.png", "TIẾP TỤC", 38f);
            StylePauseButton(retryButton, boxTr, "PillBtn_GlassNeutral.png", "CHƠI LẠI", -46f);
            StylePauseButton(exitMenuButton, boxTr, "PillBtn_RedDanger.png", "VỀ MENU", -130f);
        }

        private void Awake()
        {
            WireButtons();
            ApplyGlassModalTheme();
            if (pauseModalPanel != null)
            {
                pauseModalPanel.SetActive(false);
            }
            isPaused = false;
        }

        private void OnEnable()
        {
            SubscribeLevelEvents();
            TryBindActiveInputHandler();
        }

        private void Start()
        {
            WireButtons();
            ApplyGlassModalTheme();
            SubscribeLevelEvents();
            TryBindActiveInputHandler();

            if (pauseModalPanel != null)
            {
                pauseModalPanel.SetActive(false);
            }
            isPaused = false;
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (boundInputHandler == null)
            {
                if (Time.unscaledTime >= nextInputLookupTime)
                {
                    nextInputLookupTime = Time.unscaledTime + 0.5f;
                    TryBindActiveInputHandler();
                }

                if (boundInputHandler == null && WasFallbackEscapePressed())
                {
                    HandlePauseTriggered();
                }
            }
        }

        private void OnDisable()
        {
            StopPauseBoxPopInAndReset();
            if (isPaused)
            {
                isPaused = false;
                Time.timeScale = 1f;
                if (pauseModalPanel != null && pauseModalPanel != gameObject)
                {
                    pauseModalPanel.SetActive(false);
                }
            }
            UnbindInputHandler();
            UnsubscribeLevelEvents();
        }

        private void OnDestroy()
        {
            StopPauseBoxPopInAndReset();
            Time.timeScale = 1f;
            isPaused = false;
            UnbindInputHandler();
            UnsubscribeLevelEvents();
        }

        private Transform ResolvePauseBoxTransform()
        {
            if (pauseModalPanel == null) return null;

            Transform byName = pauseModalPanel.transform.Find("PauseBox");
            if (byName != null) return byName;

            if (resumeButton != null && resumeButton.transform.parent != null &&
                resumeButton.transform.parent != pauseModalPanel.transform)
            {
                return resumeButton.transform.parent;
            }

            for (int i = 0; i < pauseModalPanel.transform.childCount; i++)
            {
                Transform child = pauseModalPanel.transform.GetChild(i);
                if (child != null && child.GetComponent<Image>() != null)
                {
                    return child;
                }
            }

            return null;
        }

        private void StylePauseTitle(Transform boxTr)
        {
            Transform titleTr = boxTr.Find("PauseTitle");
            if (titleTr == null && pauseModalPanel != null)
            {
                titleTr = pauseModalPanel.transform.Find("PauseTitle");
            }
            if (titleTr == null)
            {
                TMP_Text[] candidateTexts = boxTr.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < candidateTexts.Length; i++)
                {
                    if (candidateTexts[i] != null &&
                        candidateTexts[i].gameObject.name.IndexOf("Title", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        titleTr = candidateTexts[i].transform;
                        break;
                    }
                }
            }

            if (titleTr == null) return;
            if (titleTr.parent != boxTr)
            {
                titleTr.SetParent(boxTr, false);
            }

            RectTransform titleRt = titleTr.GetComponent<RectTransform>();
            if (titleRt != null)
            {
                titleRt.anchorMin = new Vector2(0.5f, 0.5f);
                titleRt.anchorMax = new Vector2(0.5f, 0.5f);
                titleRt.pivot = new Vector2(0.5f, 0.5f);
                titleRt.anchoredPosition = new Vector2(0f, 148f);
                titleRt.sizeDelta = new Vector2(460f, 64f);
            }

            TMP_Text titleTmp = titleTr.GetComponent<TMP_Text>();
            if (titleTmp != null)
            {
                titleTmp.text = "TẠM DỪNG";
                titleTmp.alignment = TextAlignmentOptions.Center;
                titleTmp.raycastTarget = false;
                GlassHudTheme.StyleTmpText(titleTmp, 44f, new Color(1f, 0.90f, 0.42f, 1f), true, true);
            }
        }

        private static void EnsurePauseDivider(Transform boxTr)
        {
            Transform divTr = boxTr.Find("PauseDivider");
            if (divTr == null)
            {
                GameObject divObj = new GameObject("PauseDivider", typeof(RectTransform), typeof(Image));
                divObj.transform.SetParent(boxTr, false);
                divTr = divObj.transform;
            }

            RectTransform divRt = divTr.GetComponent<RectTransform>();
            if (divRt != null)
            {
                divRt.anchorMin = new Vector2(0.5f, 0.5f);
                divRt.anchorMax = new Vector2(0.5f, 0.5f);
                divRt.pivot = new Vector2(0.5f, 0.5f);
                divRt.anchoredPosition = new Vector2(0f, 98f);
                divRt.sizeDelta = new Vector2(420f, 1.5f);
            }

            Image divImg = divTr.GetComponent<Image>();
            if (divImg != null)
            {
                divImg.sprite = null;
                divImg.type = Image.Type.Simple;
                divImg.color = new Color(0.70f, 0.94f, 1f, 0.25f);
                divImg.raycastTarget = false;
            }
        }

        private static void StylePauseButton(Button btn, Transform boxTr, string spriteFileName, string labelText, float yPos)
        {
            if (btn == null || boxTr == null) return;

            if (btn.transform.parent != boxTr)
            {
                btn.transform.SetParent(boxTr, false);
            }

            RemoveFlatOutlines(btn.gameObject);

            RectTransform rt = btn.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, yPos);
                rt.sizeDelta = new Vector2(360f, 66f);
            }

            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                Sprite btnSp = GlassHudTheme.LoadSprite(spriteFileName);
                if (btnSp != null)
                {
                    GlassHudTheme.ApplySlicedSprite(img, btnSp, 360f, 66f, Color.white);
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
                    labelRt.offsetMin = new Vector2(14f, 0f);
                    labelRt.offsetMax = new Vector2(-14f, 0f);
                }

                GlassHudTheme.StyleTmpText(label, 24f, Color.white, true, true);
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

        private void PlayPauseBoxPopIn()
        {
            if (pauseBoxRect == null) return;

            pauseBoxRect.gameObject.SetActive(true);
            if (popInCoroutine != null)
            {
                StopCoroutine(popInCoroutine);
                popInCoroutine = null;
            }

            if (!Application.isPlaying || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                pauseBoxRect.localScale = Vector3.one;
                if (pauseBoxCanvasGroup != null) pauseBoxCanvasGroup.alpha = 1f;
                return;
            }

            popInCoroutine = StartCoroutine(AnimatePauseBoxPopIn());
        }

        private void StopPauseBoxPopInAndReset()
        {
            if (popInCoroutine != null)
            {
                StopCoroutine(popInCoroutine);
                popInCoroutine = null;
            }

            if (pauseBoxRect != null)
            {
                pauseBoxRect.localScale = Vector3.one;
            }
            if (pauseBoxCanvasGroup != null)
            {
                pauseBoxCanvasGroup.alpha = 1f;
            }
        }

        private IEnumerator AnimatePauseBoxPopIn()
        {
            if (pauseBoxRect == null)
            {
                popInCoroutine = null;
                yield break;
            }

            pauseBoxRect.localScale = new Vector3(POP_IN_START_SCALE, POP_IN_START_SCALE, 1f);
            if (pauseBoxCanvasGroup != null)
            {
                pauseBoxCanvasGroup.alpha = 0f;
            }

            float elapsed = 0f;
            while (elapsed < POP_IN_DURATION)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / POP_IN_DURATION);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float scale = Mathf.Lerp(POP_IN_START_SCALE, 1f, eased);

                if (pauseBoxRect != null)
                {
                    pauseBoxRect.localScale = new Vector3(scale, scale, 1f);
                }
                if (pauseBoxCanvasGroup != null)
                {
                    pauseBoxCanvasGroup.alpha = eased;
                }

                yield return null;
            }

            if (pauseBoxRect != null)
            {
                pauseBoxRect.localScale = Vector3.one;
            }
            if (pauseBoxCanvasGroup != null)
            {
                pauseBoxCanvasGroup.alpha = 1f;
            }

            popInCoroutine = null;
        }

        private void WireButtons()
        {
            if (pauseHudButton != null)
            {
                pauseHudButton.onClick.RemoveAllListeners();
                pauseHudButton.onClick.AddListener(HandlePauseTriggered);
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(() => SetPaused(false));
            }

            if (retryButton != null)
            {
                retryButton.onClick.RemoveAllListeners();
                retryButton.onClick.AddListener(HandleRetryClicked);
            }

            if (exitMenuButton != null)
            {
                exitMenuButton.onClick.RemoveAllListeners();
                exitMenuButton.onClick.AddListener(HandleExitToMenuClicked);
            }
        }

        private void SubscribeLevelEvents()
        {
            if (isLevelSubscribed || LevelManager.Instance == null) return;
            LevelManager.Instance.OnPlayerSpawned -= HandlePlayerSpawned;
            LevelManager.Instance.OnPlayerSpawned += HandlePlayerSpawned;
            isLevelSubscribed = true;
        }

        private void UnsubscribeLevelEvents()
        {
            if (!isLevelSubscribed) return;
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnPlayerSpawned -= HandlePlayerSpawned;
            }
            isLevelSubscribed = false;
        }

        private void HandlePlayerSpawned(BaseCharacter player)
        {
            if (player != null)
            {
                BindInputHandler(player.GetComponent<InputHandler>());
            }
        }

        private void TryBindActiveInputHandler()
        {
            if (boundInputHandler != null) return;

            InputHandler found = null;
            if (LevelManager.Instance != null && LevelManager.Instance.ActivePlayer != null)
            {
                found = LevelManager.Instance.ActivePlayer.GetComponent<InputHandler>();
            }
            if (found == null)
            {
                found = FindAnyObjectByType<InputHandler>();
            }

            if (found != null)
            {
                BindInputHandler(found);
            }
        }

        private void UnbindInputHandler()
        {
            if (boundInputHandler != null)
            {
                boundInputHandler.OnPauseEvent -= HandlePauseTriggered;
                boundInputHandler = null;
            }
        }

        private void HandlePauseTriggered()
        {
            if (lastToggleFrame == Time.frameCount) return;
            lastToggleFrame = Time.frameCount;
            TogglePause();
        }

        private static bool WasFallbackEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private void HandleRetryClicked()
        {
            StopPauseBoxPopInAndReset();
            isPaused = false;
            Time.timeScale = 1f;

            if (pauseModalPanel != null)
            {
                pauseModalPanel.SetActive(false);
            }

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.RetryCurrentStage();
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        private void HandleExitToMenuClicked()
        {
            StopPauseBoxPopInAndReset();
            isPaused = false;
            Time.timeScale = 1f;

            if (pauseModalPanel != null)
            {
                pauseModalPanel.SetActive(false);
            }

            if (CurrencyManager.HasInstance)
            {
                CurrencyManager.Instance.SuppressEnemyRewards = false;
            }
            if (InventoryManager.HasInstance)
            {
                InventoryManager.Instance.IsTrainingFreeUseMode = false;
            }

            if (!string.IsNullOrEmpty(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
            else if (SceneManager.sceneCountInBuildSettings > 0)
            {
                SceneManager.LoadScene(0);
            }
        }

        private void EnsureRuntimeUtilityBar()
        {
            if (!Application.isPlaying || pauseModalPanel == null || runtimeUtilityBar != null) return;

            runtimeUtilityBar = new GameObject("PauseUtilityBar", typeof(RectTransform), typeof(Image));
            runtimeUtilityBar.transform.SetParent(pauseModalPanel.transform, false);

            RectTransform barRt = runtimeUtilityBar.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0.5f, 0.5f);
            barRt.anchorMax = new Vector2(0.5f, 0.5f);
            barRt.pivot = new Vector2(0.5f, 0.5f);
            barRt.anchoredPosition = new Vector2(0f, -262f);
            barRt.sizeDelta = new Vector2(540f, 88f);

            Image barImg = runtimeUtilityBar.GetComponent<Image>();
            barImg.color = new Color(0.05f, 0.09f, 0.16f, 0.88f);
            barImg.raycastTarget = true;

            runtimeSaveButton = CreateRuntimeUtilityButton(runtimeUtilityBar.transform, "PauseSaveMatchButton", "LƯU TRẬN", new Vector2(-175f, 12f), new Vector2(165f, 44f), new Color(0.14f, 0.55f, 0.36f, 1f));
            runtimeLoadButton = CreateRuntimeUtilityButton(runtimeUtilityBar.transform, "PauseLoadMatchButton", "TẢI TRẬN", new Vector2(0f, 12f), new Vector2(165f, 44f), new Color(0.16f, 0.44f, 0.72f, 1f));

            runtimeSaveButton.onClick.AddListener(HandleSaveMatchClicked);
            runtimeLoadButton.onClick.AddListener(HandleLoadMatchClicked);

            GameObject fbObj = new GameObject("SaveFeedbackText", typeof(RectTransform), typeof(TextMeshProUGUI));
            fbObj.transform.SetParent(runtimeUtilityBar.transform, false);
            RectTransform fbRt = fbObj.GetComponent<RectTransform>();
            fbRt.anchorMin = new Vector2(0.5f, 0f);
            fbRt.anchorMax = new Vector2(0.5f, 0f);
            fbRt.pivot = new Vector2(0.5f, 0f);
            fbRt.anchoredPosition = new Vector2(0f, 4f);
            fbRt.sizeDelta = new Vector2(520f, 24f);

            runtimeSaveFeedbackText = fbObj.GetComponent<TextMeshProUGUI>();
            runtimeSaveFeedbackText.alignment = TextAlignmentOptions.Center;
            runtimeSaveFeedbackText.text = string.Empty;
            runtimeSaveFeedbackText.raycastTarget = false;
            GlassHudTheme.StyleTmpText(runtimeSaveFeedbackText, 16f, new Color(0.75f, 0.96f, 1f, 1f), true, true);
        }

        private static Button CreateRuntimeUtilityButton(
            Transform parent,
            string objName,
            string label,
            Vector2 anchoredPos,
            Vector2 size,
            Color tintColor)
        {
            GameObject btnObj = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = btnObj.GetComponent<Image>();
            Sprite pill = GlassHudTheme.LoadSprite("PillBtn_GlassNeutral.png");
            if (pill != null)
            {
                GlassHudTheme.ApplySlicedSprite(img, pill, size.x, size.y, tintColor);
            }
            else
            {
                img.color = tintColor;
            }

            GameObject txtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            TMP_Text tmp = txtObj.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            GlassHudTheme.StyleTmpText(tmp, 18f, Color.white, true, true);

            return btnObj.GetComponent<Button>();
        }

        private void RefreshUtilityBarState()
        {
            if (runtimeLoadButton != null && MidMatchSaveManager.Instance != null)
            {
                runtimeLoadButton.interactable = MidMatchSaveManager.Instance.HasSavedMatch();
            }
        }

        private void HandleSaveMatchClicked()
        {
            MidMatchSaveManager mgr = MidMatchSaveManager.Instance;
            if (mgr == null) return;

            bool ok = mgr.SaveCurrentMatch(out string msg);
            if (runtimeSaveFeedbackText != null)
            {
                runtimeSaveFeedbackText.text = msg;
                runtimeSaveFeedbackText.color = ok
                    ? new Color(0.45f, 1f, 0.62f, 1f)
                    : new Color(1f, 0.42f, 0.42f, 1f);
            }
            RefreshUtilityBarState();
        }

        private void HandleLoadMatchClicked()
        {
            MidMatchSaveManager mgr = MidMatchSaveManager.Instance;
            if (mgr == null) return;

            bool ok = mgr.TryLoadSavedMatch(out string msg);
            if (!ok && runtimeSaveFeedbackText != null)
            {
                runtimeSaveFeedbackText.text = msg;
                runtimeSaveFeedbackText.color = new Color(1f, 0.42f, 0.42f, 1f);
            }
        }
    }
}
