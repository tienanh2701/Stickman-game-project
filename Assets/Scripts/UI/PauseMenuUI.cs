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

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Quản lý menu Tạm Dừng (Pause) trong trận đấu:
    /// - Kích hoạt qua nút [ || TẠM DỪNG ] trên HUD hoặc phím Escape (InputHandler.OnPauseEvent)
    /// - 3 nút: "TIẾP TỤC", "CHƠI LẠI" (không hoàn lại Q/E), "THOÁT RA MENU"
    /// - Đảm bảo Time.timeScale = 1f khi tiếp tục, chơi lại, thoát hoặc OnDestroy.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
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

            isPaused = paused;
            Time.timeScale = isPaused ? 0f : 1f;

            if (pauseModalPanel != null)
            {
                pauseModalPanel.SetActive(isPaused);
            }
        }

        private void Awake()
        {
            WireButtons();
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
            UnbindInputHandler();
            UnsubscribeLevelEvents();
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            isPaused = false;
            UnbindInputHandler();
            UnsubscribeLevelEvents();
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
    }
}
