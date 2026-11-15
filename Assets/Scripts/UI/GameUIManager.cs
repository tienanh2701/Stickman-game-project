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

        private TMP_FontAsset runtimeTmpFont;
        private BaseCharacter activePlayer;
        private BossController activeBoss;
        private BossBase activeWorldBoss;
        private EnemyBase activeElite;
        private GameMode currentGameMode = GameMode.Campaign;
        private int latestEndlessWave = 1;

        public bool IsEndScreenOpen =>
            (victoryPanel != null && victoryPanel.activeSelf) ||
            (defeatPanel != null && defeatPanel.activeSelf);

        public GameMode CurrentGameMode => currentGameMode;
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
            }
            UpdateShieldBar(0f, 0f);
        }

        public void ConfigureModeHud(GameMode mode)
        {
            currentGameMode = mode;

            if (endlessWaveBanner != null)
            {
                endlessWaveBanner.SetActive(mode == GameMode.Endless);
            }

            if (mode == GameMode.Endless)
            {
                if (victoryNextButton != null) victoryNextButton.gameObject.SetActive(false);
                if (endlessBannerText != null)
                {
                    endlessBannerText.text = $"CHẾ ĐỘ VÔ TẬN - WAVE {Mathf.Max(1, latestEndlessWave)}";
                }
                if (waveText != null)
                {
                    waveText.text = $"VÔ TẬN | WAVE {Mathf.Max(1, latestEndlessWave)}";
                }
            }
            else if (mode == GameMode.Training)
            {
                if (victoryNextButton != null) victoryNextButton.gameObject.SetActive(false);
                if (waveText != null)
                {
                    waveText.text = "LUYỆN TẬP | TỰ DO (Q/E MIỄN PHÍ)";
                }
                if (enemiesRemainingText != null)
                {
                    enemiesRemainingText.text = "Hình nhân tập luyện bất tử";
                }
            }
        }

        public void UpdateEndlessWave(int wave, int enemiesRemaining = -1)
        {
            latestEndlessWave = Mathf.Max(1, wave);
            if (waveText != null)
            {
                waveText.text = $"VÔ TẬN | WAVE {latestEndlessWave}";
            }
            if (endlessWaveBanner != null)
            {
                endlessWaveBanner.SetActive(true);
            }
            if (endlessBannerText != null)
            {
                endlessBannerText.text = $"WAVE {latestEndlessWave}";
            }
            if (enemiesRemaining >= 0)
            {
                UpdateEnemiesRemaining(enemiesRemaining);
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
            ApplyVietnameseTmpFont();
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
                Font sourceFont = vietnameseFont != null
                    ? vietnameseFont
                    : Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 32);
                if (sourceFont == null) return;

                runtimeTmpFont = TMP_FontAsset.CreateFontAsset(sourceFont);
                if (runtimeTmpFont == null) return;
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

            WireNavigationButtons();

            if (bossHealthPanel != null) bossHealthPanel.SetActive(false);
            if (transformReadyPrompt != null) transformReadyPrompt.SetActive(false);
            if (specialComboReadyPrompt != null) specialComboReadyPrompt.SetActive(false);
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (defeatPanel != null) defeatPanel.SetActive(false);
            UpdateShieldBar(0f, 0f);
            UpdateWaveDisplay(0, 0);

            GameMode selectedMode = StageManager.LoadSelectedGameMode();
            ConfigureModeHud(selectedMode);
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
            if (Instance == this)
            {
                Instance = null;
            }
            if (runtimeTmpFont != null)
            {
                Destroy(runtimeTmpFont);
                runtimeTmpFont = null;
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
                playerHealthText.text = $"MÁU: {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
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
            activeBoss = boss;
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

            boss.OnHealthChangedEvent += (hp, max) =>
            {
                if (bossHealthSlider != null) bossHealthSlider.value = hp;
            };

            boss.OnDeathEvent += (b) =>
            {
                if (activeBoss == b) activeBoss = null;
                if (bossHealthPanel != null) bossHealthPanel.SetActive(false);
            };
        }

        private string GetStageCodePrefix()
        {
            StageDataSO st = LevelManager.Instance != null ? LevelManager.Instance.CurrentStageData : null;
            if (st == null) return "MÀN";
            string tag = st.stageNumber == 4 ? " [BOSS]" : (st.stageNumber == 3 ? " [TINH ANH]" : "");
            return $"TG{(int)st.worldType + 1}-M{st.stageNumber}{tag}";
        }

        private void UpdateWaveDisplay(int currentWave, int totalWaves)
        {
            if (currentGameMode == GameMode.Endless)
            {
                if (currentWave > 0)
                {
                    UpdateEndlessWave(currentWave);
                }
                else if (waveText != null)
                {
                    waveText.text = "VÔ TẬN | CHUẨN BỊ";
                }
                return;
            }

            if (currentGameMode == GameMode.Training)
            {
                if (waveText != null)
                {
                    waveText.text = "LUYỆN TẬP | TỰ DO (Q/E MIỄN PHÍ)";
                }
                return;
            }

            if (waveText == null) return;
            StageDataSO st = LevelManager.Instance != null ? LevelManager.Instance.CurrentStageData : null;
            string prefix = GetStageCodePrefix();
            if (currentWave <= 0)
            {
                waveText.text = $"{prefix} | SẴN SÀNG";
            }
            else if (st != null && st.isBossStage && st.waves != null && currentWave > st.waves.Count)
            {
                waveText.text = $"{prefix} | ĐẠI CHIẾN BOSS";
            }
            else
            {
                waveText.text = $"{prefix} | ĐỢT: {currentWave} / {totalWaves}";
            }
        }

        private void UpdateEnemiesRemaining(int count)
        {
            if (currentGameMode == GameMode.Training) return;

            if (enemiesRemainingText != null)
            {
                enemiesRemainingText.text = $"Quái còn lại: {count}";
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
            if (defeatTitleText != null)
            {
                if (currentGameMode == GameMode.Endless)
                {
                    defeatTitleText.text = $"KẾT THÚC CHẾ ĐỘ VÔ TẬN - ĐẠT WAVE {Mathf.Max(1, latestEndlessWave)}";
                }
                else
                {
                    defeatTitleText.text = "THẤT BẠI!";
                }
            }

            if (defeatPanel != null) defeatPanel.SetActive(true);
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
