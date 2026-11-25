using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Rules.Combat;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Economy;
using StickmanBeatEmUp.Enemies;
using StickmanBeatEmUp.Environment;
using StickmanBeatEmUp.GameModes;
using StickmanBeatEmUp.Items;

namespace StickmanBeatEmUp.Level
{
    /// <summary>
    /// LevelManager: Quản lý toàn bộ tiến trình màn chơi (Stage Flow):
    /// - Quản lý 3 Thế giới x 4 Màn (Tổng 12 Màn)
    /// - Màn 4 của mỗi thế giới có Boss cuối 2 Phase
    /// - Bắt đầu đi tới tọa độ X -> Khóa Camera Bounds -> Triệu hồi quái theo từng Wave
    /// - Quét sạch quái -> Mở khóa màn tiếp theo (Lưu PlayerPrefs), có thể chọn chơi lại màn cũ bất kỳ lúc nào
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Stage Configuration")]
        [SerializeField] private StageDataSO currentStageData;
        [SerializeField] private List<StageDataSO> allStages = new List<StageDataSO>();

        [Header("Scene References")]
        [SerializeField] private CameraFollowBeatEmUp cameraFollow;
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private GameObject[] characterPrefabs; // [0] Goku, [1] Eren, [2] Diend

        [Header("Game Mode Managers (Endless & Training)")]
        [SerializeField] private EndlessModeManager endlessModeManager;
        [SerializeField] private TrainingModeManager trainingModeManager;

        [Header("State")]
        [SerializeField] private bool sandboxMode = false;
        [SerializeField] private bool isCombatLocked = false;
        [SerializeField] private int currentWaveIndex = 0;
        [SerializeField] private int remainingEnemiesInWave = 0;

        private BaseCharacter activePlayer;
        private List<BaseEnemy> activeEnemies = new List<BaseEnemy>();
        private GameMode currentGameMode = GameMode.Campaign;
        private bool isRespawningTrainingPlayer;
        private GameBalanceConfigDTO runtimeBalanceConfig;
        private const string UNLOCKED_STAGE_KEY = "Stickman_UnlockedStage";
        private const string SELECTED_STAGE_INDEX_KEY = "SelectedStageIndex";

        public BaseCharacter ActivePlayer => activePlayer;
        public StageDataSO CurrentStageData => currentStageData;
        public IReadOnlyList<StageDataSO> AllStages => allStages;
        public GameMode CurrentGameMode => currentGameMode;
        public bool IsSandboxMode => sandboxMode;
        public void SetSandboxMode(bool value) { sandboxMode = value; }

        // Events
        public event Action<BaseCharacter> OnPlayerSpawned;
        public event Action<EnemyBase> OnEliteSpawned;
        public event Action<BaseEnemy> OnBossSpawned;
        public event Action<BossBase> OnWorldBossSpawned;
        public event Action<int, int> OnWaveChanged; // (currentWave, totalWaves)
        public event Action<int> OnEnemiesRemainingChanged;
        public event Action OnStageCompleted;
        public event Action OnStageFailed;

        public void SetCurrentStage(StageDataSO stage)
        {
            currentStageData = stage;
        }

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
            else
            {
                Destroy(gameObject);
                return;
            }

            currentGameMode = StageManager.LoadSelectedGameMode();
            StageManager.CurrentGameMode = currentGameMode;

            if (currentGameMode == GameMode.Campaign)
            {
                int selectedStageIdx = PlayerPrefs.GetInt(SELECTED_STAGE_INDEX_KEY, -1);
                if (allStages != null && allStages.Count > 0)
                {
                    if (selectedStageIdx >= 0 && selectedStageIdx < allStages.Count && allStages[selectedStageIdx] != null)
                    {
                        currentStageData = allStages[selectedStageIdx];
                    }
                    else if (currentStageData == null)
                    {
                        currentStageData = allStages[0];
                    }
                }
            }
            else
            {
                currentStageData = null;
            }

            ApplyModeGlobalFlags();
            LoadRuntimeBalanceConfigIfPresent();
        }

        private void LoadRuntimeBalanceConfigIfPresent()
        {
            runtimeBalanceConfig = null;
            string configPath = Path.Combine(Application.streamingAssetsPath, "Config", "game_balance.json");
            if (!File.Exists(configPath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(configPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                GameBalanceConfigDTO parsed;
                try
                {
                    parsed = JsonUtility.FromJson<GameBalanceConfigDTO>(json);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidGameConfigException("game_balance.json", $"Malformed JSON syntax: {ex.Message}", ex);
                }

                if (parsed != null)
                {
                    parsed.Validate();
                    runtimeBalanceConfig = parsed;
                }
            }
            catch (StickmanGameException ex)
            {
                Debug.LogWarning($"[LevelManager] External game_balance.json invalid ({ex.ErrorCode}): {ex.Message}");
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[LevelManager] Cannot read game_balance.json: {ex.Message}");
            }
        }

        private void ResolveCurrentStageMultipliers(out float hpMultiplier, out float damageMultiplier)
        {
            hpMultiplier = 1f;
            damageMultiplier = 1f;

            if (currentStageData is LevelDataSO levelData)
            {
                hpMultiplier = levelData.enemyHpMultiplier > 0f ? levelData.enemyHpMultiplier : 1f;
                damageMultiplier = levelData.enemyDamageMultiplier > 0f ? levelData.enemyDamageMultiplier : 1f;
            }

            if (runtimeBalanceConfig != null && currentStageData != null)
            {
                int worldIdx = ((int)currentStageData.worldType) + 1;
                int stageIdx = Mathf.Clamp(currentStageData.stageNumber, 1, 4);
                LevelBalanceDTO dto = WaveCompositionCalculator.ResolveCampaignLevelBalance(worldIdx, stageIdx, runtimeBalanceConfig);
                hpMultiplier = dto.enemyHpMultiplier;
                damageMultiplier = dto.enemyDamageMultiplier;
            }
        }

        private void OnDestroy()
        {
            if (activePlayer != null)
            {
                activePlayer.OnStateChanged -= HandlePlayerStateChanged;
            }
            ResetModeGlobalFlags();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void ApplyModeGlobalFlags()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.IsTrainingFreeUseMode = (currentGameMode == GameMode.Training);
            }
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.SuppressEnemyRewards = (currentGameMode == GameMode.Training);
            }
        }

        private static void ResetModeGlobalFlags()
        {
            CurrencyManager cm = CurrencyManager.HasInstance ? CurrencyManager.Instance : null;
            if (cm != null)
            {
                cm.SuppressEnemyRewards = false;
            }
            InventoryManager inv = InventoryManager.HasInstance ? InventoryManager.Instance : null;
            if (inv != null)
            {
                inv.IsTrainingFreeUseMode = false;
            }
        }

        private void Start()
        {
            Time.timeScale = 1f;
            currentGameMode = StageManager.LoadSelectedGameMode();
            StageManager.CurrentGameMode = currentGameMode;
            ApplyModeGlobalFlags();

            if (cameraFollow == null)
            {
                cameraFollow = FindAnyObjectByType<CameraFollowBeatEmUp>();
            }

            RageTestSpawner[] rageSpawners = FindObjectsByType<RageTestSpawner>();
            if (currentGameMode != GameMode.Campaign && !sandboxMode)
            {
                for (int i = 0; i < rageSpawners.Length; i++)
                {
                    RageTestSpawner rageSpawner = rageSpawners[i];
                    if (rageSpawner == null) continue;
                    rageSpawner.enabled = false;

                    // Never deactivate a GameObject that hosts core managers (LevelManager, Endless, Training, StageManager).
                    bool hostsCoreManager =
                        rageSpawner.gameObject == gameObject ||
                        rageSpawner.GetComponent<LevelManager>() != null ||
                        rageSpawner.GetComponent<EndlessModeManager>() != null ||
                        rageSpawner.GetComponent<TrainingModeManager>() != null ||
                        rageSpawner.GetComponent<StageManager>() != null;
                    if (!hostsCoreManager)
                    {
                        rageSpawner.gameObject.SetActive(false);
                    }
                }
            }

            SpawnPlayer();
            if (cameraFollow != null && activePlayer != null)
            {
                cameraFollow.SetTarget(activePlayer.transform);
            }
            if (activePlayer != null)
            {
                OnPlayerSpawned?.Invoke(activePlayer);
            }

            CampaignHazardSpawner hazardSpawner = FindAnyObjectByType<CampaignHazardSpawner>();
            if (hazardSpawner != null)
            {
                hazardSpawner.SetGameMode(currentGameMode);
            }

            if (currentGameMode == GameMode.Endless)
            {
                StartEndlessMode(hazardSpawner);
            }
            else if (currentGameMode == GameMode.Training)
            {
                StartTrainingMode(hazardSpawner);
            }
            else
            {
                if (!sandboxMode && currentStageData != null)
                {
                    StartCoroutine(AutoStartStageCombatRoutine());
                }
            }
        }

        private void StartEndlessMode(CampaignHazardSpawner hazardSpawner)
        {
            const float arenaMinX = -22f;
            const float arenaMaxX = 42f;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            StageManager stageMgr = StageManager.Instance != null
                ? StageManager.Instance
                : (GetComponent<StageManager>() ?? FindAnyObjectByType<StageManager>());
            if (stageMgr != null)
            {
                if (!stageMgr.gameObject.activeSelf) stageMgr.gameObject.SetActive(true);
                stageMgr.enabled = true;
                stageMgr.ApplySelectedLevel();
            }

            hazardSpawner?.ClearAllHazards();
            ConfigureFlatOpenArena(arenaMinX, arenaMaxX);

            if (endlessModeManager == null)
            {
                endlessModeManager = GetComponent<EndlessModeManager>()
                    ?? FindAnyObjectByType<EndlessModeManager>()
                    ?? gameObject.AddComponent<EndlessModeManager>();
            }
            if (!endlessModeManager.gameObject.activeSelf)
            {
                endlessModeManager.gameObject.SetActive(true);
            }
            endlessModeManager.enabled = true;
            endlessModeManager.SetBalanceConfig(runtimeBalanceConfig);
            endlessModeManager.SetArenaBounds(arenaMinX, arenaMaxX);
            endlessModeManager.BeginEndless(this, activePlayer);
        }

        private void StartTrainingMode(CampaignHazardSpawner hazardSpawner)
        {
            const float arenaMinX = -22f;
            const float arenaMaxX = 42f;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            StageManager stageMgr = StageManager.Instance != null
                ? StageManager.Instance
                : (GetComponent<StageManager>() ?? FindAnyObjectByType<StageManager>());
            if (stageMgr != null)
            {
                if (!stageMgr.gameObject.activeSelf) stageMgr.gameObject.SetActive(true);
                stageMgr.enabled = true;
                stageMgr.ApplySelectedLevel();
            }

            hazardSpawner?.ClearAllHazards();
            ConfigureFlatOpenArena(arenaMinX, arenaMaxX);

            if (trainingModeManager == null)
            {
                trainingModeManager = GetComponent<TrainingModeManager>()
                    ?? FindAnyObjectByType<TrainingModeManager>()
                    ?? gameObject.AddComponent<TrainingModeManager>();
            }
            if (!trainingModeManager.gameObject.activeSelf)
            {
                trainingModeManager.gameObject.SetActive(true);
            }
            trainingModeManager.enabled = true;
            trainingModeManager.BeginTraining(this, activePlayer);
        }

        private IEnumerator AutoStartStageCombatRoutine()
        {
            yield return new WaitForSeconds(0.25f);
            if (currentGameMode == GameMode.Campaign && !sandboxMode && !isCombatLocked && currentStageData != null)
            {
                TriggerCombatLock();
            }
        }

        private void ConfigureFlatOpenArena(float arenaMinX, float arenaMaxX)
        {
            isCombatLocked = false;

            GameObject leftBoundary = GameObject.Find("LeftMapBoundary");
            if (leftBoundary != null)
            {
                Vector3 lp = leftBoundary.transform.position;
                lp.x = arenaMinX - 0.5f;
                leftBoundary.transform.position = lp;
            }

            GameObject rightBoundary = GameObject.Find("RightMapBoundary");
            if (rightBoundary != null)
            {
                Vector3 rp = rightBoundary.transform.position;
                rp.x = arenaMaxX + 0.5f;
                rightBoundary.transform.position = rp;
            }

            if (activePlayer != null)
            {
                Vector3 p = activePlayer.transform.position;
                p.x = Mathf.Clamp(p.x, arenaMinX + 1.0f, arenaMaxX - 1.0f);
                p.y = EnemyBase.GROUND_Y;
                activePlayer.transform.position = p;
            }

            if (cameraFollow != null)
            {
                cameraFollow.FollowWithinMapBounds(arenaMinX, arenaMaxX);
            }
        }

        private void SpawnPlayer(float? overrideX = null)
        {
            BaseCharacter existingPlayer = FindAnyObjectByType<BaseCharacter>();

            // Mặc định chọn nhân vật 0 (Goku) hoặc theo lựa chọn của người chơi
            int chosenCharIndex = PlayerPrefs.GetInt("SelectedCharacter", 0);
            if (characterPrefabs != null && characterPrefabs.Length > 0)
            {
                chosenCharIndex = Mathf.Clamp(chosenCharIndex, 0, characterPrefabs.Length - 1);
                if (characterPrefabs[chosenCharIndex] != null)
                {
                    if (existingPlayer != null)
                    {
                        existingPlayer.OnStateChanged -= HandlePlayerStateChanged;
                        Destroy(existingPlayer.gameObject);
                    }
                    float spawnX = overrideX ?? (playerSpawnPoint != null ? playerSpawnPoint.position.x : 0f);
                    if (currentGameMode != GameMode.Campaign)
                    {
                        spawnX = Mathf.Clamp(spawnX, -21f, 41f);
                    }
                    Vector3 spawnPos = new Vector3(spawnX, StickmanBeatEmUp.Enemies.EnemyBase.GROUND_Y, 0f);
                    GameObject pObj = Instantiate(characterPrefabs[chosenCharIndex], spawnPos, Quaternion.identity);
                    activePlayer = pObj.GetComponent<BaseCharacter>();
                }
                else
                {
                    activePlayer = existingPlayer;
                    if (activePlayer != null)
                    {
                        Vector3 p = activePlayer.transform.position;
                        p.y = StickmanBeatEmUp.Enemies.EnemyBase.GROUND_Y;
                        activePlayer.transform.position = p;
                    }
                }
            }
            else
            {
                activePlayer = existingPlayer;
            }

            if (activePlayer != null)
            {
                PlayerItemController.EnsureAttached(activePlayer);
                activePlayer.OnStateChanged -= HandlePlayerStateChanged;
                activePlayer.OnStateChanged += HandlePlayerStateChanged;
            }
        }

        public void RespawnTrainingPlayer()
        {
            if (currentGameMode != GameMode.Training || isRespawningTrainingPlayer) return;
            isRespawningTrainingPlayer = true;
            try
            {
                if (activePlayer == null)
                {
                    SpawnPlayer(0f);
                    if (cameraFollow != null && activePlayer != null)
                    {
                        cameraFollow.SetTarget(activePlayer.transform);
                    }
                    if (activePlayer != null)
                    {
                        OnPlayerSpawned?.Invoke(activePlayer);
                        trainingModeManager?.SetActivePlayer(activePlayer);
                    }
                    return;
                }

                if (activePlayer.IsDead)
                {
                    activePlayer.SendMessage("RespawnPlayerFullState", SendMessageOptions.DontRequireReceiver);
                    if (!activePlayer.IsDead)
                    {
                        OnPlayerSpawned?.Invoke(activePlayer);
                        trainingModeManager?.SetActivePlayer(activePlayer);
                    }
                }
            }
            finally
            {
                isRespawningTrainingPlayer = false;
            }
        }

        private void HandlePlayerStateChanged(CharacterState state)
        {
            if (state == CharacterState.Dead)
            {
                if (currentGameMode == GameMode.Training)
                {
                    RespawnTrainingPlayer();
                    return;
                }
                OnStageFailed?.Invoke();
            }
        }

        private void Update()
        {
            if (currentGameMode != GameMode.Campaign || sandboxMode || isCombatLocked || currentStageData == null || activePlayer == null) return;

            // Kiểm tra Player đi qua tọa độ kích hoạt khóa màn (Lock Trigger X)
            if (activePlayer.transform.position.x >= currentStageData.lockTriggerX)
            {
                TriggerCombatLock();
            }
        }

        /// <summary>
        /// Kích hoạt Khóa Camera và Bắt đầu Spawn quái theo Wave
        /// </summary>
        public void TriggerCombatLock()
        {
            if (currentGameMode != GameMode.Campaign || isCombatLocked || currentStageData == null) return;
            isCombatLocked = true;

            Debug.Log($"<color=yellow>[BEAT 'EM UP ARENA LOCKED]</color> Khóa Camera tại X={currentStageData.lockedCameraMinX} -> {currentStageData.lockedCameraMaxX}");

            if (cameraFollow != null)
            {
                cameraFollow.LockCamera(currentStageData.lockedCameraMinX, currentStageData.lockedCameraMaxX);
            }

            CampaignHazardSpawner hazardSpawner = FindAnyObjectByType<CampaignHazardSpawner>();
            if (hazardSpawner != null)
            {
                float camHalfWidth = Camera.main != null ? Camera.main.orthographicSize * Camera.main.aspect : 8.88f;
                float arenaMinX = currentStageData.lockedCameraMinX - camHalfWidth + 0.5f;
                float arenaMaxX = currentStageData.lockedCameraMaxX + camHalfWidth - 0.5f;
                hazardSpawner.SpawnHazardsForStage(currentStageData.worldType, currentStageData.stageNumber, arenaMinX, arenaMaxX);
            }

            currentWaveIndex = 0;
            StartCoroutine(StartNextWaveRoutine());
        }

        private IEnumerator StartNextWaveRoutine()
        {
            if (currentWaveIndex < currentStageData.waves.Count)
            {
                WaveData wave = currentStageData.waves[currentWaveIndex];
                OnWaveChanged?.Invoke(currentWaveIndex + 1, currentStageData.waves.Count + (currentStageData.isBossStage ? 1 : 0));

                yield return new WaitForSeconds(wave.delayBeforeWave);

                remainingEnemiesInWave = wave.enemies.Count;
                OnEnemiesRemainingChanged?.Invoke(remainingEnemiesInWave);

                foreach (var entry in wave.enemies)
                {
                    if (entry.spawnDelay > 0) yield return new WaitForSeconds(entry.spawnDelay);

                    float refX = cameraFollow != null ? cameraFollow.transform.position.x : (activePlayer != null ? activePlayer.transform.position.x : 0f);
                    float minSpawnX = Mathf.Max(-20f, currentStageData.lockedCameraMinX - 6.5f);
                    float maxSpawnX = Mathf.Min(40f, currentStageData.lockedCameraMaxX + 6.5f);
                    Vector3 spawnPos = new Vector3(Mathf.Clamp(refX + entry.spawnOffset.x, minSpawnX, maxSpawnX), EnemyBase.GROUND_Y + entry.spawnOffset.y, 0f);

                    if (entry.enemyPrefab != null)
                    {
                        GameObject enemyObj = Instantiate(entry.enemyPrefab, spawnPos, Quaternion.identity);
                        EnemyBase newEnemy = enemyObj.GetComponent<EnemyBase>();
                        if (newEnemy != null)
                        {
                            ResolveCurrentStageMultipliers(out float stageHpMult, out float stageDmgMult);
                            newEnemy.ApplyCampaignScaling(stageHpMult, stageDmgMult);
                            if (entry.maxHealthOverride > 0f)
                            {
                                newEnemy.SetMaxHealthOverride(entry.maxHealthOverride);
                            }
                            newEnemy.OnDied += HandleEnemyBaseDefeated;
                            if (newEnemy is EliteEnemy || (newEnemy.Data != null && newEnemy.Data.isElite))
                            {
                                OnEliteSpawned?.Invoke(newEnemy);
                            }
                            continue;
                        }

                        BossBase waveBoss = enemyObj.GetComponent<BossBase>();
                        if (waveBoss != null)
                        {
                            ResolveCurrentStageMultipliers(out float waveBossHpMult, out float waveBossDmgMult);
                            waveBoss.ApplyCampaignScaling(waveBossHpMult, waveBossDmgMult);
                            waveBoss.OnBossDefeated += HandleBossBaseDefeated;
                            continue;
                        }

                        BaseEnemy enemy = enemyObj.GetComponent<BaseEnemy>();
                        if (enemy != null)
                        {
                            activeEnemies.Add(enemy);
                            enemy.OnDeathEvent += HandleEnemyDefeated;
                        }
                    }
                }
            }
            else if (currentStageData.isBossStage)
            {
                // Sau khi hết các wave lính thường ở màn 4 -> Triệu hồi BOSS!
                yield return StartCoroutine(SpawnBossRoutine());
            }
            else
            {
                CompleteStage();
            }
        }

        private IEnumerator SpawnBossRoutine()
        {
            Debug.Log("<color=red><b>[WARNING: BOSS INCOMING!]</b></color>");
            OnWaveChanged?.Invoke(currentStageData.waves.Count + 1, currentStageData.waves.Count + 1);

            yield return new WaitForSeconds(0.65f);

            if (currentStageData.bossPrefab != null)
            {
                Vector3 spawnPos = currentStageData.bossSpawnPosition;
                spawnPos.y = BossBase.GROUND_Y;
                float camHalfWidth = Camera.main != null ? Camera.main.orthographicSize * Camera.main.aspect : 8f;
                float arenaLeft = currentStageData.lockedCameraMinX - camHalfWidth + 1.5f;
                float arenaRight = currentStageData.lockedCameraMaxX + camHalfWidth - 1.5f;
                if (arenaRight > arenaLeft)
                {
                    spawnPos.x = Mathf.Clamp(spawnPos.x, arenaLeft, arenaRight);
                }

                GameObject bossObj = Instantiate(currentStageData.bossPrefab, spawnPos, Quaternion.identity);
                BossBase newBoss = bossObj.GetComponent<BossBase>();
                if (newBoss != null)
                {
                    ResolveCurrentStageMultipliers(out float bossHpMult, out float bossDmgMult);
                    newBoss.ApplyCampaignScaling(bossHpMult, bossDmgMult);
                    remainingEnemiesInWave = 1;
                    OnEnemiesRemainingChanged?.Invoke(remainingEnemiesInWave);
                    newBoss.OnBossDefeated += HandleBossBaseDefeated;
                    OnWorldBossSpawned?.Invoke(newBoss);
                    yield break;
                }

                BaseEnemy boss = bossObj.GetComponent<BaseEnemy>();
                if (boss != null)
                {
                    remainingEnemiesInWave = 1;
                    OnEnemiesRemainingChanged?.Invoke(remainingEnemiesInWave);
                    activeEnemies.Add(boss);
                    boss.OnDeathEvent += HandleEnemyDefeated;
                    OnBossSpawned?.Invoke(boss);
                }
            }
        }

        private void HandleEnemyBaseDefeated(EnemyBase enemy)
        {
            if (enemy != null) enemy.OnDied -= HandleEnemyBaseDefeated;
            DecrementWaveEnemyCount();
        }

        private void HandleBossBaseDefeated(BossBase boss)
        {
            if (boss != null && !boss.IsDead) return;
            if (boss != null) boss.OnBossDefeated -= HandleBossBaseDefeated;
            DecrementWaveEnemyCount();
        }

        private void HandleEnemyDefeated(BaseEnemy enemy)
        {
            if (activeEnemies.Contains(enemy))
            {
                activeEnemies.Remove(enemy);
            }
            DecrementWaveEnemyCount();
        }

        private void DecrementWaveEnemyCount()
        {
            remainingEnemiesInWave--;
            remainingEnemiesInWave = Mathf.Max(0, remainingEnemiesInWave);
            OnEnemiesRemainingChanged?.Invoke(remainingEnemiesInWave);

            if (remainingEnemiesInWave <= 0)
            {
                currentWaveIndex++;
                if (currentWaveIndex < currentStageData.waves.Count || (currentStageData.isBossStage && currentWaveIndex == currentStageData.waves.Count))
                {
                    StartCoroutine(StartNextWaveRoutine());
                }
                else
                {
                    CompleteStage();
                }
            }
        }

        private void CompleteStage()
        {
            Debug.Log($"<color=green><b>[STAGE CLEAR!]</b></color> Chúc mừng bạn đã hoàn thành {currentStageData.stageName}!");

            FindAnyObjectByType<CampaignHazardSpawner>()?.ClearAllHazards();

            if (cameraFollow != null)
            {
                cameraFollow.UnlockCamera();
            }

            // Tính số thứ tự màn toàn cục (1..12)
            int currentGlobalStageIndex = ((int)currentStageData.worldType * 4) + currentStageData.stageNumber;
            int unlockedStage = PlayerPrefs.GetInt(UNLOCKED_STAGE_KEY, 1);

            if (currentGlobalStageIndex >= unlockedStage)
            {
                PlayerPrefs.SetInt(UNLOCKED_STAGE_KEY, currentGlobalStageIndex + 1);
                PlayerPrefs.Save();
                Debug.Log($"<color=cyan>[PROGRESS SAVED]</color> Mở khóa Màn {currentGlobalStageIndex + 1}!");
            }

            OnStageCompleted?.Invoke();
        }

        /// <summary>
        /// Cho phép chọn và chơi lại màn bất kỳ đã mở khóa
        /// </summary>
        public bool CanPlayStage(int worldIndex, int stageNum)
        {
            int targetGlobalIndex = (worldIndex * 4) + stageNum;
            int unlockedStage = PlayerPrefs.GetInt(UNLOCKED_STAGE_KEY, 1);
            return targetGlobalIndex <= unlockedStage;
        }

        public void LoadStage(StageDataSO stageData)
        {
            if (stageData != null && allStages != null)
            {
                int idx = allStages.IndexOf(stageData);
                if (idx >= 0)
                {
                    PlayerPrefs.SetInt(SELECTED_STAGE_INDEX_KEY, idx);
                    PlayerPrefs.Save();
                }
            }
            Time.timeScale = 1f;
            currentStageData = stageData;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        public void RetryCurrentStage()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        public void LoadStageByIndex(int stageIndex)
        {
            Time.timeScale = 1f;
            if (allStages == null || allStages.Count == 0)
            {
                RetryCurrentStage();
                return;
            }

            int wrappedIdx = ((stageIndex % allStages.Count) + allStages.Count) % allStages.Count;
            PlayerPrefs.SetInt(SELECTED_STAGE_INDEX_KEY, wrappedIdx);
            int unlockedStage = PlayerPrefs.GetInt(UNLOCKED_STAGE_KEY, 1);
            if (unlockedStage < wrappedIdx + 1)
            {
                PlayerPrefs.SetInt(UNLOCKED_STAGE_KEY, wrappedIdx + 1);
            }
            PlayerPrefs.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        public void LoadNextStage()
        {
            if (allStages == null || allStages.Count == 0)
            {
                RetryCurrentStage();
                return;
            }

            int currentIdx = currentStageData != null ? allStages.IndexOf(currentStageData) : PlayerPrefs.GetInt(SELECTED_STAGE_INDEX_KEY, 0);
            if (currentIdx < 0) currentIdx = PlayerPrefs.GetInt(SELECTED_STAGE_INDEX_KEY, 0);
            LoadStageByIndex((currentIdx + 1) % allStages.Count);
        }

        public void LoadPreviousStage()
        {
            if (allStages == null || allStages.Count == 0)
            {
                RetryCurrentStage();
                return;
            }

            int currentIdx = currentStageData != null ? allStages.IndexOf(currentStageData) : PlayerPrefs.GetInt(SELECTED_STAGE_INDEX_KEY, 0);
            if (currentIdx < 0) currentIdx = PlayerPrefs.GetInt(SELECTED_STAGE_INDEX_KEY, 0);
            LoadStageByIndex((currentIdx - 1 + allStages.Count) % allStages.Count);
        }

        public void ConfigureModeManagers(EndlessModeManager endless, TrainingModeManager training)
        {
            if (endless != null) endlessModeManager = endless;
            if (training != null) trainingModeManager = training;
        }

        public void NotifySupportEliteSpawned(EnemyBase elite)
        {
            if (elite != null) OnEliteSpawned?.Invoke(elite);
        }

        public void NotifyWorldBossSpawned(BossBase boss)
        {
            if (boss != null) OnWorldBossSpawned?.Invoke(boss);
        }

        public void NotifyBossSpawned(BaseEnemy boss)
        {
            if (boss != null) OnBossSpawned?.Invoke(boss);
        }

        public void NotifyWaveChanged(int wave, int totalWaves)
        {
            OnWaveChanged?.Invoke(wave, totalWaves);
        }

        public void NotifyEnemiesRemainingChanged(int remaining)
        {
            OnEnemiesRemainingChanged?.Invoke(remaining);
        }

        public void ReturnToCharacterSelect()
        {
            Time.timeScale = 1f;
            ResetModeGlobalFlags();

            if (Application.CanStreamedLevelBeLoaded("MainMenu"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
            else if (Application.CanStreamedLevelBeLoaded("CharacterSelectScene"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("CharacterSelectScene");
            }
            else if (UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings > 1)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            }
            else
            {
                RetryCurrentStage();
            }
        }
    }
}
