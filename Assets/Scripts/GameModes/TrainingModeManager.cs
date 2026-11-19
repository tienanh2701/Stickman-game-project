using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Combat;
using StickmanBeatEmUp.Enemies;
using StickmanBeatEmUp.Items;
using StickmanBeatEmUp.Level;
using StickmanBeatEmUp.UI;

namespace StickmanBeatEmUp.GameModes
{
    [Serializable]
    public class TrainingUnitEntry
    {
        public string displayName;
        public GameObject prefab;
    }

    /// <summary>
    /// Training Mode sandbox controller:
    /// - Full AoT World 1 map span [-22 .. 42], groundY = -2.5 with unlocked player-following camera
    /// - Top-Dock Sandbox Controls: Player Invincible, Auto-Max Rage, Enemy AI Toggle, Enemy Invincible
    /// - Free Q/E consumable item usage & non-overlapping in-front-of-player enemy/elite/boss spawner
    /// - Instant Clear All cleanup without awarding Xu coins
    /// </summary>
    public class TrainingModeManager : MonoBehaviour
    {
        public static TrainingModeManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private const float MinSpawnSeparation = 1.35f;
        public const int MaxSpawnedTrainingUnits = 30;
        private const float SpawnScrollStepNormalized = 0.35f;
        private const float SpawnScrollLerpDuration = 0.18f;

        private static readonly Color SpawnCardTextColor = new Color(0.933f, 0.965f, 1f, 1f);
        private static readonly Color ArrowKeycapTextColor = new Color(0.75f, 0.96f, 1f, 1f);
        private static readonly Color ToggleOnTextColor = Color.white;
        private static readonly Color ToggleOffTextColor = new Color(0.80f, 0.87f, 0.96f, 1f);

        [Header("Arena Bounds")]
        [SerializeField] private float arenaMinX = -22f;
        [SerializeField] private float arenaMaxX = 42f;
        [SerializeField] private float groundY = -2.5f;

        [Header("Training Unit Roster (12 Units Across 3 Worlds)")]
        [SerializeField] private TrainingUnitEntry[] unitRoster;

        [Header("Training Toggles")]
        [SerializeField] private bool playerInvincible = false;
        [SerializeField] private bool autoHpEnabled = false;
        [SerializeField] private bool autoRageEnabled = true;
        [SerializeField] private bool enableEnemyAI = true;
        [SerializeField] private bool enemyInvincible = false;
        [SerializeField] private float maintenanceInterval = 0.25f;

        [Header("Training UI References")]
        [SerializeField] private GameObject trainingPanel;
        [SerializeField] private Button autoHpButton;
        [SerializeField] private TMP_Text autoHpLabel;
        [SerializeField] private Button playerInvincibleButton;
        [SerializeField] private TMP_Text playerInvincibleLabel;
        [SerializeField] private Button autoRageButton;
        [SerializeField] private TMP_Text autoRageLabel;
        [SerializeField] private Button enemyAiButton;
        [SerializeField] private TMP_Text enemyAiLabel;
        [SerializeField] private Button enemyInvincibleButton;
        [SerializeField] private TMP_Text enemyInvincibleLabel;
        [SerializeField] private Button clearAllButton;
        [SerializeField] private Button[] spawnButtons;
        [SerializeField] private TMP_Text[] spawnButtonLabels;
        [SerializeField] private Image[] spawnIcons;

        [Header("09_TrainingSpawnerDock Sprites (Standalone Safety)")]
        [SerializeField] private Sprite dockFrameSprite;
        [SerializeField] private Sprite toggleOffSprite;
        [SerializeField] private Sprite toggleOnSprite;
        [SerializeField] private Sprite blueLedOrbSprite;
        [SerializeField] private Sprite plasmaDividerSprite;
        [SerializeField] private Sprite spawnCardSprite;
        [SerializeField] private Sprite clearAllSprite;
        [SerializeField] private Sprite arrowButtonSprite;
        [SerializeField] private Sprite clearTrashIconSprite;

        private LevelManager levelManager;
        private BaseCharacter activePlayer;
        private bool isActive;
        private float nextMaintenanceTime;
        private readonly List<GameObject> spawnedUnits = new List<GameObject>();
        private Button scrollLeftButton;
        private Button scrollRightButton;
        private Coroutine spawnScrollLerpCoroutine;
        private float spawnScrollTargetNormalized;

        public float ArenaMinX => arenaMinX;
        public float ArenaMaxX => arenaMaxX;
        public float GroundY => groundY;
        public bool IsActive => isActive;
        public bool AutoHpEnabled => playerInvincible || autoHpEnabled;
        public bool AutoRageEnabled => autoRageEnabled;
        public bool PlayerInvincible => playerInvincible;
        public bool AutoMaxRage => autoRageEnabled;
        public bool EnableEnemyAI => enableEnemyAI;
        public bool EnemyInvincible => enemyInvincible;

        private Button EffectivePlayerInvincibleButton => playerInvincibleButton != null ? playerInvincibleButton : autoHpButton;
        private TMP_Text EffectivePlayerInvincibleLabel => playerInvincibleLabel != null ? playerInvincibleLabel : autoHpLabel;

        public void ConfigureUnitRoster(TrainingUnitEntry[] entries)
        {
            if (entries != null && entries.Length > 0)
            {
                unitRoster = entries;
                BindSpawnButtons();
            }
        }

        public void ConfigureUI(
            GameObject panel,
            Button autoHpBtn,
            TMP_Text autoHpTxt,
            Button autoRageBtn,
            TMP_Text autoRageTxt,
            Button clearBtn,
            Button[] spawnBtns,
            TMP_Text[] spawnLbls)
        {
            trainingPanel = panel;
            autoHpButton = autoHpBtn;
            autoHpLabel = autoHpTxt;
            playerInvincibleButton = autoHpBtn;
            playerInvincibleLabel = autoHpTxt;
            autoRageButton = autoRageBtn;
            autoRageLabel = autoRageTxt;
            clearAllButton = clearBtn;
            spawnButtons = spawnBtns;
            spawnButtonLabels = spawnLbls;

            CacheDockSpritesIfMissing();
            DiscoverMissingTopBarRefs();
            EnsureSpawnScrollBarConfigured();
            ApplySleekGlassTrainingTheme();

            if (!isActive && trainingPanel != null)
            {
                trainingPanel.SetActive(false);
            }
            else if (isActive)
            {
                WireUiControls();
            }
        }

        public void ConfigureTopBarUI(
            GameObject panel,
            Button playerInvincibleBtn,
            TMP_Text playerInvincibleTxt,
            Button autoMaxRageBtn,
            TMP_Text autoMaxRageTxt,
            Button enemyAiBtn,
            TMP_Text enemyAiTxt,
            Button enemyInvincibleBtn,
            TMP_Text enemyInvincibleTxt,
            Button clearBtn,
            Button[] spawnBtns,
            TMP_Text[] spawnLbls,
            Image[] spawnIconImgs)
        {
            trainingPanel = panel;
            playerInvincibleButton = playerInvincibleBtn;
            playerInvincibleLabel = playerInvincibleTxt;
            autoHpButton = playerInvincibleBtn;
            autoHpLabel = playerInvincibleTxt;
            autoRageButton = autoMaxRageBtn;
            autoRageLabel = autoMaxRageTxt;
            enemyAiButton = enemyAiBtn;
            enemyAiLabel = enemyAiTxt;
            enemyInvincibleButton = enemyInvincibleBtn;
            enemyInvincibleLabel = enemyInvincibleTxt;
            clearAllButton = clearBtn;
            spawnButtons = spawnBtns;
            spawnButtonLabels = spawnLbls;
            spawnIcons = spawnIconImgs;

            CacheDockSpritesIfMissing();
            DiscoverMissingTopBarRefs();
            EnsureSpawnScrollBarConfigured();
            ApplySleekGlassTrainingTheme();

            if (!isActive && trainingPanel != null)
            {
                trainingPanel.SetActive(false);
            }
            else if (isActive)
            {
                WireUiControls();
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this && !Instance.isActive)
            {
                Instance = this;
            }

            if (trainingPanel != null && StageManager.LoadSelectedGameMode() != GameMode.Training)
            {
                trainingPanel.SetActive(false);
            }
        }

        private void Start()
        {
            CacheDockSpritesIfMissing();
            DiscoverMissingTopBarRefs();
            EnsureSpawnScrollBarConfigured();
            ApplySleekGlassTrainingTheme();
        }

        private void CacheDockSpritesIfMissing()
        {
            if (dockFrameSprite == null) dockFrameSprite = GlassHudTheme.LoadSprite("09_TrainingSpawnerDock/Frame_Training_MainDock_Clean.png");
            if (toggleOffSprite == null) toggleOffSprite = GlassHudTheme.LoadSprite("09_TrainingSpawnerDock/Toggle_Training_OptionPill_Blank.png");
            if (toggleOnSprite == null) toggleOnSprite = GlassHudTheme.LoadSprite("09_TrainingSpawnerDock/Toggle_Training_OptionPill_BlueLED_Clean.png");
            if (blueLedOrbSprite == null) blueLedOrbSprite = GlassHudTheme.LoadSprite("09_TrainingSpawnerDock/Icon_Training_BlueLEDOrb.png");
            if (plasmaDividerSprite == null) plasmaDividerSprite = GlassHudTheme.LoadSprite("09_TrainingSpawnerDock/Divider_Training_PlasmaTube_Violet.png");
            if (spawnCardSprite == null) spawnCardSprite = GlassHudTheme.LoadSprite("09_TrainingSpawnerDock/Slot_Training_EnemySpawnCard_Clean.png");
            if (clearAllSprite == null) clearAllSprite = GlassHudTheme.LoadSprite("09_TrainingSpawnerDock/Btn_Training_ClearAll_RedRing_Clean.png");
            if (arrowButtonSprite == null) arrowButtonSprite = GlassHudTheme.LoadSprite("Keycap_Badge_Dark.png");
            if (clearTrashIconSprite == null) clearTrashIconSprite = GlassHudTheme.LoadSprite("Icon_Trash_Red.png");
        }

        public void BeginTraining(LevelManager manager, BaseCharacter player)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            enabled = true;
            Instance = this;

            if (spawnScrollLerpCoroutine != null)
            {
                StopCoroutine(spawnScrollLerpCoroutine);
                spawnScrollLerpCoroutine = null;
            }
            spawnScrollTargetNormalized = 0f;

            levelManager = manager;
            isActive = true;

            playerInvincible = false;
            autoHpEnabled = false;
            autoRageEnabled = true;
            enableEnemyAI = true;
            enemyInvincible = false;
            EnemyBase.GlobalEnemyAiEnabled = true;
            EnemyBase.GlobalEnemyInvincible = false;

            SetActivePlayer(player);

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.IsTrainingFreeUseMode = true;
            }

            EnsureRosterPopulated();
            EnsureTrainingPanelExists();
            CacheDockSpritesIfMissing();
            DiscoverMissingTopBarRefs();

            if (trainingPanel != null)
            {
                trainingPanel.SetActive(true);
                EnsureBossPanelClearsTrainingDock();
            }
            EnsureSpawnScrollBarConfigured();
            ApplySleekGlassTrainingTheme();
            WireUiControls();

            ScrollRect spawnScroll = trainingPanel != null
                ? trainingPanel.GetComponentInChildren<ScrollRect>(true)
                : null;
            if (spawnScroll != null)
            {
                spawnScroll.horizontalNormalizedPosition = 0f;
            }

            GameUIManager.Instance?.ConfigureModeHud(GameMode.Training);
            levelManager?.NotifyWaveChanged(0, 0);

            if (autoRageEnabled && activePlayer != null && activePlayer.RageManagerRef != null)
            {
                activePlayer.RageManagerRef.FillMaxRage();
            }
            ApplyTrainingPlayerBuffs();
        }

        public void SetActivePlayer(BaseCharacter player)
        {
            activePlayer = player;
            if (isActive)
            {
                if (activePlayer != null)
                {
                    activePlayer.TrainingInvincible = playerInvincible;
                }
                ConfigureTrainingMapAndCamera();
                ApplyTrainingPlayerBuffs();
            }
        }

        private void ConfigureTrainingMapAndCamera()
        {
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

            CameraFollowBeatEmUp camFollow = CameraFollowBeatEmUp.Instance != null
                ? CameraFollowBeatEmUp.Instance
                : FindAnyObjectByType<CameraFollowBeatEmUp>();

            if (camFollow != null)
            {
                if (activePlayer != null)
                {
                    camFollow.SetTarget(activePlayer.transform);
                }
                camFollow.FollowWithinMapBounds(arenaMinX, arenaMaxX);
            }
        }

        public void StopTraining()
        {
            isActive = false;
            if (trainingPanel != null)
            {
                trainingPanel.SetActive(false);
            }
            if (InventoryManager.HasInstance)
            {
                InventoryManager.Instance.IsTrainingFreeUseMode = false;
            }
            ResetSandboxOverrides();
        }

        private void OnDisable()
        {
            if (spawnScrollLerpCoroutine != null)
            {
                StopCoroutine(spawnScrollLerpCoroutine);
                spawnScrollLerpCoroutine = null;
            }
            ResetSandboxOverrides();
        }

        private void OnDestroy()
        {
            if (isActive && InventoryManager.HasInstance)
            {
                InventoryManager.Instance.IsTrainingFreeUseMode = false;
            }
            ResetSandboxOverrides();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void ResetSandboxOverrides()
        {
            EnemyBase.GlobalEnemyAiEnabled = true;
            EnemyBase.GlobalEnemyInvincible = false;
            if (activePlayer != null)
            {
                activePlayer.TrainingInvincible = false;
            }
        }

        public void OnPlayerFatalDamageInTraining(BaseCharacter player)
        {
            if (player == null) return;

            if (activePlayer == null)
            {
                activePlayer = player;
            }

            player.RespawnPlayerFullState();
            player.TrainingInvincible = playerInvincible;

            if (autoRageEnabled && player.RageManagerRef != null)
            {
                player.RageManagerRef.FillMaxRage();
            }
        }

        private void Update()
        {
            if (!isActive) return;

            if (activePlayer != null && (activePlayer.IsDead || activePlayer.CurrentHealth <= 0f))
            {
                OnPlayerFatalDamageInTraining(activePlayer);
            }

            if (autoRageEnabled && activePlayer != null && !activePlayer.IsDead &&
                activePlayer.RageManagerRef != null && activePlayer.RageManagerRef.CurrentRageFloat < RageManager.MAX_RAGE)
            {
                activePlayer.RageManagerRef.AddRageFloat(5f * Time.deltaTime);
            }

            if (Time.time >= nextMaintenanceTime)
            {
                nextMaintenanceTime = Time.time + Mathf.Max(0.05f, maintenanceInterval);
                ApplyTrainingPlayerBuffs();
            }
        }

        private void ApplyTrainingPlayerBuffs()
        {
            if (!isActive || activePlayer == null) return;

            activePlayer.TrainingInvincible = playerInvincible;

            if (activePlayer.IsDead || activePlayer.CurrentHealth <= 0f)
            {
                OnPlayerFatalDamageInTraining(activePlayer);
                return;
            }

            if (autoRageEnabled && activePlayer.RageManagerRef != null && activePlayer.RageManagerRef.CurrentRage < RageManager.MAX_RAGE)
            {
                activePlayer.RageManagerRef.FillMaxRage();
            }
        }

        public void TogglePlayerInvincible()
        {
            playerInvincible = !playerInvincible;
            autoHpEnabled = playerInvincible;
            if (activePlayer != null)
            {
                activePlayer.TrainingInvincible = playerInvincible;
            }
            RefreshToggleLabels();
        }

        public void ToggleAutoHp() => TogglePlayerInvincible();

        public void ToggleAutoRage()
        {
            autoRageEnabled = !autoRageEnabled;
            RefreshToggleLabels();
            if (autoRageEnabled && activePlayer != null && activePlayer.RageManagerRef != null)
            {
                activePlayer.RageManagerRef.FillMaxRage();
            }
        }

        public void ToggleEnemyAI()
        {
            enableEnemyAI = !enableEnemyAI;
            EnemyBase.GlobalEnemyAiEnabled = enableEnemyAI;
            RefreshToggleLabels();
        }

        public void ToggleEnemyInvincible()
        {
            enemyInvincible = !enemyInvincible;
            EnemyBase.GlobalEnemyInvincible = enemyInvincible;
            RefreshToggleLabels();
        }

        public void SpawnUnitByIndex(int index)
        {
            if (!isActive || unitRoster == null || index < 0 || index >= unitRoster.Length) return;
            TrainingUnitEntry entry = unitRoster[index];
            if (entry != null && entry.prefab != null)
            {
                SpawnUnitNoOverlap(entry.prefab);
            }
        }

        public GameObject SpawnUnitNoOverlap(GameObject prefab)
        {
            if (prefab == null) return null;

            PruneDeadSpawnedUnits();
            if (spawnedUnits.Count >= MaxSpawnedTrainingUnits)
            {
                Debug.LogWarning($"[TrainingModeManager] Đã đạt giới hạn tối đa {MaxSpawnedTrainingUnits} mục tiêu tập luyện.");
                return null;
            }

            List<float> occupiedXs = CollectAliveEnemyXs();
            float playerX = activePlayer != null ? activePlayer.transform.position.x : 0f;
            int dir = (activePlayer != null && activePlayer.FacingDirection < 0) ? -1 : 1;
            bool isTetherBoss = prefab.GetComponent<TetherBossController>() != null;

            float minAllowedX = arenaMinX + 1.5f;
            float maxAllowedX = isTetherBoss ? Mathf.Max(minAllowedX, arenaMaxX - 5.5f) : arenaMaxX - 1.5f;
            if (maxAllowedX < minAllowedX) maxAllowedX = minAllowedX;

            float chosenX = playerX;
            bool foundSlot = false;

            // 1. Scan IN FRONT of Player: playerX + dir * (3.2f + step)
            for (float step = 0f; ; step += 1.35f)
            {
                float candidateX = playerX + dir * (3.2f + step);
                if (candidateX < minAllowedX || candidateX > maxAllowedX) break;
                if (IsCandidatePositionValid(candidateX, playerX, occupiedXs))
                {
                    chosenX = candidateX;
                    foundSlot = true;
                    break;
                }
            }

            // 2. Fallback BEHIND Player if near wall or front is occupied: playerX - dir * (3.2f + step)
            if (!foundSlot)
            {
                for (float step = 0f; ; step += 1.35f)
                {
                    float candidateX = playerX - dir * (3.2f + step);
                    if (candidateX < minAllowedX || candidateX > maxAllowedX) break;
                    if (IsCandidatePositionValid(candidateX, playerX, occupiedXs))
                    {
                        chosenX = candidateX;
                        foundSlot = true;
                        break;
                    }
                }
            }

            // 3. Closer gap scan (in front then behind) still respecting MinSpawnSeparation >= 1.3f
            if (!foundSlot)
            {
                for (float offset = 1.5f; offset < 3.2f; offset += 0.55f)
                {
                    float frontCand = Mathf.Clamp(playerX + dir * offset, minAllowedX, maxAllowedX);
                    if (IsCandidatePositionValid(frontCand, playerX, occupiedXs))
                    {
                        chosenX = frontCand;
                        foundSlot = true;
                        break;
                    }
                    float backCand = Mathf.Clamp(playerX - dir * offset, minAllowedX, maxAllowedX);
                    if (IsCandidatePositionValid(backCand, playerX, occupiedXs))
                    {
                        chosenX = backCand;
                        foundSlot = true;
                        break;
                    }
                }
            }

            // 4. Arena-wide fallback if densely packed: maximize clearance from enemies & player
            if (!foundSlot)
            {
                float bestDist = -1f;
                chosenX = Mathf.Clamp(playerX + dir * 3.2f, minAllowedX, maxAllowedX);
                for (float x = minAllowedX; x <= maxAllowedX + 0.001f; x += 0.65f)
                {
                    if (Mathf.Abs(x - playerX) < MinSpawnSeparation) continue;
                    float nearest = Mathf.Abs(x - playerX);
                    for (int i = 0; i < occupiedXs.Count; i++)
                    {
                        float d = Mathf.Abs(x - occupiedXs[i]);
                        if (d < nearest) nearest = d;
                    }
                    if (nearest > bestDist)
                    {
                        bestDist = nearest;
                        chosenX = x;
                    }
                }
            }

            chosenX = Mathf.Clamp(chosenX, minAllowedX, maxAllowedX);

            GameObject obj = Instantiate(prefab, new Vector3(chosenX, groundY, 0f), Quaternion.identity);
            spawnedUnits.Add(obj);
            EnsureBossPanelClearsTrainingDock();

            BossBase boss = obj.GetComponent<BossBase>();
            if (boss != null)
            {
                boss.SetTrainingDummy(true);
                levelManager?.NotifyWorldBossSpawned(boss);
                return obj;
            }

            EnemyBase enemy = obj.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.SetTrainingDummy(true);
                if (enemy is EliteEnemy || (enemy.Data != null && enemy.Data.isElite))
                {
                    if (levelManager != null)
                    {
                        levelManager.NotifySupportEliteSpawned(enemy);
                    }
                    else
                    {
                        GameUIManager.Instance?.BindElite(enemy);
                    }
                }
                return obj;
            }

            BaseEnemy legacy = obj.GetComponent<BaseEnemy>();
            if (legacy != null && legacy is BossController)
            {
                levelManager?.NotifyBossSpawned(legacy);
            }

            return obj;
        }

        private void PruneDeadSpawnedUnits()
        {
            for (int i = spawnedUnits.Count - 1; i >= 0; i--)
            {
                GameObject unit = spawnedUnits[i];
                if (unit == null)
                {
                    spawnedUnits.RemoveAt(i);
                    continue;
                }

                EnemyBase eb = unit.GetComponent<EnemyBase>();
                if (eb != null && eb.IsDead)
                {
                    spawnedUnits.RemoveAt(i);
                    continue;
                }

                BossBase bb = unit.GetComponent<BossBase>();
                if (bb != null && bb.IsDead)
                {
                    spawnedUnits.RemoveAt(i);
                    continue;
                }

                BaseEnemy le = unit.GetComponent<BaseEnemy>();
                if (le != null && le.IsDead)
                {
                    spawnedUnits.RemoveAt(i);
                }
            }
        }

        private static bool IsCandidatePositionValid(float candidateX, float playerX, List<float> occupiedXs)
        {
            if (Mathf.Abs(candidateX - playerX) < MinSpawnSeparation) return false;
            for (int i = 0; i < occupiedXs.Count; i++)
            {
                if (Mathf.Abs(candidateX - occupiedXs[i]) < MinSpawnSeparation) return false;
            }
            return true;
        }

        private static List<float> CollectAliveEnemyXs()
        {
            List<float> xs = new List<float>();

            EnemyBase[] enemyBases = FindObjectsByType<EnemyBase>();
            for (int i = 0; i < enemyBases.Length; i++)
            {
                if (enemyBases[i] != null && !enemyBases[i].IsDead)
                {
                    xs.Add(enemyBases[i].transform.position.x);
                }
            }

            BossBase[] bosses = FindObjectsByType<BossBase>();
            for (int i = 0; i < bosses.Length; i++)
            {
                if (bosses[i] != null && !bosses[i].IsDead)
                {
                    xs.Add(bosses[i].transform.position.x);
                }
            }

            BaseEnemy[] legacy = FindObjectsByType<BaseEnemy>();
            for (int i = 0; i < legacy.Length; i++)
            {
                if (legacy[i] != null && !legacy[i].IsDead)
                {
                    xs.Add(legacy[i].transform.position.x);
                }
            }

            return xs;
        }

        public void ClearAllEnemies()
        {
            EnemyBase[] enemyBases = FindObjectsByType<EnemyBase>();
            for (int i = 0; i < enemyBases.Length; i++)
            {
                if (enemyBases[i] != null) enemyBases[i].DestroySilentlyWithoutReward();
            }

            BossBase[] bosses = FindObjectsByType<BossBase>();
            for (int i = 0; i < bosses.Length; i++)
            {
                if (bosses[i] != null) bosses[i].DestroySilentlyWithoutReward();
            }

            BaseEnemy[] legacy = FindObjectsByType<BaseEnemy>();
            for (int i = 0; i < legacy.Length; i++)
            {
                if (legacy[i] != null) legacy[i].DestroySilentlyWithoutReward();
            }

            Projectile[] projectiles = FindObjectsByType<Projectile>();
            for (int i = 0; i < projectiles.Length; i++)
            {
                if (projectiles[i] != null) Destroy(projectiles[i].gameObject);
            }

            Shockwave[] shockwaves = FindObjectsByType<Shockwave>();
            for (int i = 0; i < shockwaves.Length; i++)
            {
                if (shockwaves[i] != null) Destroy(shockwaves[i].gameObject);
            }

            spawnedUnits.Clear();
        }

        private void DiscoverMissingTopBarRefs()
        {
            if (trainingPanel == null) return;

            if (playerInvincibleButton == null)
            {
                Transform t = FindChildRecursive(trainingPanel.transform, "PlayerInvincibleButton");
                if (t != null)
                {
                    playerInvincibleButton = t.GetComponent<Button>();
                    playerInvincibleLabel = t.GetComponentInChildren<TMP_Text>(true);
                }
            }
            if (playerInvincibleLabel == null && EffectivePlayerInvincibleButton != null)
            {
                playerInvincibleLabel = EffectivePlayerInvincibleButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (autoRageButton == null)
            {
                Transform t = FindChildRecursive(trainingPanel.transform, "AutoMaxRageButton")
                    ?? FindChildRecursive(trainingPanel.transform, "AutoRageButton");
                if (t != null)
                {
                    autoRageButton = t.GetComponent<Button>();
                    autoRageLabel = t.GetComponentInChildren<TMP_Text>(true);
                }
            }
            if (autoRageLabel == null && autoRageButton != null)
            {
                autoRageLabel = autoRageButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (enemyAiButton == null)
            {
                Transform t = FindChildRecursive(trainingPanel.transform, "EnemyAiButton");
                if (t != null)
                {
                    enemyAiButton = t.GetComponent<Button>();
                    enemyAiLabel = t.GetComponentInChildren<TMP_Text>(true);
                }
            }
            if (enemyAiLabel == null && enemyAiButton != null)
            {
                enemyAiLabel = enemyAiButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (enemyInvincibleButton == null)
            {
                Transform t = FindChildRecursive(trainingPanel.transform, "EnemyInvincibleButton");
                if (t != null)
                {
                    enemyInvincibleButton = t.GetComponent<Button>();
                    enemyInvincibleLabel = t.GetComponentInChildren<TMP_Text>(true);
                }
            }
            if (enemyInvincibleLabel == null && enemyInvincibleButton != null)
            {
                enemyInvincibleLabel = enemyInvincibleButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (clearAllButton == null)
            {
                Transform t = FindChildRecursive(trainingPanel.transform, "ClearAllEnemiesButton");
                if (t != null)
                {
                    clearAllButton = t.GetComponent<Button>();
                }
            }

            if (scrollLeftButton == null)
            {
                Transform t = FindChildRecursive(trainingPanel.transform, "ScrollLeftButton");
                if (t != null)
                {
                    scrollLeftButton = t.GetComponent<Button>();
                }
            }

            if (scrollRightButton == null)
            {
                Transform t = FindChildRecursive(trainingPanel.transform, "ScrollRightButton");
                if (t != null)
                {
                    scrollRightButton = t.GetComponent<Button>();
                }
            }

            if (spawnButtons != null && (spawnIcons == null || spawnIcons.Length != spawnButtons.Length))
            {
                spawnIcons = new Image[spawnButtons.Length];
                for (int i = 0; i < spawnButtons.Length; i++)
                {
                    if (spawnButtons[i] == null) continue;
                    Transform iconTr = spawnButtons[i].transform.Find("Icon");
                    if (iconTr != null)
                    {
                        spawnIcons[i] = iconTr.GetComponent<Image>();
                    }
                }
            }
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == targetName) return child;
                Transform found = FindChildRecursive(child, targetName);
                if (found != null) return found;
            }
            return null;
        }

        private void WireUiControls()
        {
            Button invBtn = EffectivePlayerInvincibleButton;
            if (invBtn != null)
            {
                invBtn.onClick.RemoveAllListeners();
                invBtn.onClick.AddListener(TogglePlayerInvincible);
            }
            if (autoRageButton != null)
            {
                autoRageButton.onClick.RemoveAllListeners();
                autoRageButton.onClick.AddListener(ToggleAutoRage);
            }
            if (enemyAiButton != null)
            {
                enemyAiButton.onClick.RemoveAllListeners();
                enemyAiButton.onClick.AddListener(ToggleEnemyAI);
            }
            if (enemyInvincibleButton != null)
            {
                enemyInvincibleButton.onClick.RemoveAllListeners();
                enemyInvincibleButton.onClick.AddListener(ToggleEnemyInvincible);
            }
            if (clearAllButton != null)
            {
                clearAllButton.onClick.RemoveAllListeners();
                clearAllButton.onClick.AddListener(ClearAllEnemies);
            }
            WireCarouselArrowButtons();
            BindSpawnButtons();
            RefreshToggleLabels();
        }

        private void BindSpawnButtons()
        {
            if (spawnButtons == null) return;
            for (int i = 0; i < spawnButtons.Length; i++)
            {
                Button btn = spawnButtons[i];
                if (btn == null) continue;

                btn.onClick.RemoveAllListeners();
                bool hasEntry = unitRoster != null && i < unitRoster.Length && unitRoster[i] != null && unitRoster[i].prefab != null;
                btn.gameObject.SetActive(hasEntry);
                if (!hasEntry) continue;

                int capturedIndex = i;
                btn.onClick.AddListener(() => SpawnUnitByIndex(capturedIndex));

                if (spawnButtonLabels != null && i < spawnButtonLabels.Length && spawnButtonLabels[i] != null)
                {
                    string rawName = !string.IsNullOrEmpty(unitRoster[i].displayName)
                        ? unitRoster[i].displayName
                        : unitRoster[i].prefab.name;
                    spawnButtonLabels[i].text = FormatConciseSpawnTag(rawName, unitRoster[i].prefab);
                }

                Image iconImg = (spawnIcons != null && i < spawnIcons.Length) ? spawnIcons[i] : null;
                if (iconImg == null)
                {
                    Transform iconTr = btn.transform.Find("Icon");
                    if (iconTr != null) iconImg = iconTr.GetComponent<Image>();
                }
                if (iconImg != null)
                {
                    if (iconImg.sprite == null)
                    {
                        SpriteRenderer sr = unitRoster[i].prefab.GetComponent<SpriteRenderer>()
                            ?? unitRoster[i].prefab.GetComponentInChildren<SpriteRenderer>(true);
                        if (sr != null && sr.sprite != null)
                        {
                            iconImg.sprite = sr.sprite;
                        }
                    }
                    iconImg.preserveAspect = true;
                    iconImg.enabled = iconImg.sprite != null;
                }
            }

            EnsureSpawnScrollBarConfigured();
            ApplySleekGlassTrainingTheme();
        }

        private static string FormatConciseSpawnTag(string fullName, GameObject prefab)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return prefab != null ? prefab.name : "Unit";
            }

            string tag = fullName.Trim();
            if (tag.StartsWith("TG1:", StringComparison.OrdinalIgnoreCase) ||
                tag.StartsWith("TG2:", StringComparison.OrdinalIgnoreCase) ||
                tag.StartsWith("TG3:", StringComparison.OrdinalIgnoreCase))
            {
                tag = tag.Substring(4).Trim();
            }

            bool isBoss = tag.IndexOf("[BOSS]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          tag.IndexOf("(Boss)", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          (prefab != null && prefab.GetComponent<BossBase>() != null);
            bool isElite = !isBoss && (tag.IndexOf("[ELITE]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       tag.IndexOf("(Elite)", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       (prefab != null && prefab.GetComponent<EliteEnemy>() != null));

            tag = tag.Replace("[BOSS]", string.Empty)
                     .Replace("[ELITE]", string.Empty)
                     .Replace("(Boss)", string.Empty)
                     .Replace("(Elite)", string.Empty)
                     .Replace("Lính ", string.Empty)
                     .Replace(" Boss", string.Empty)
                     .Replace(" Titan", string.Empty)
                     .Trim();

            if (tag.Equals("War Hammer", StringComparison.OrdinalIgnoreCase)) tag = "WarHam";
            else if (tag.Equals("Shadow Moon", StringComparison.OrdinalIgnoreCase)) tag = "Shadow";
            else if (tag.Equals("Pure", StringComparison.OrdinalIgnoreCase)) tag = "Pure Titan";
            else if (tag.Equals("Beast", StringComparison.OrdinalIgnoreCase)) tag = "Beast";
            else if (tag.Equals("Female", StringComparison.OrdinalIgnoreCase)) tag = "Female";

            if (isBoss && !tag.EndsWith("(B)", StringComparison.OrdinalIgnoreCase)) tag += " (B)";
            else if (isElite && !tag.EndsWith("(E)", StringComparison.OrdinalIgnoreCase)) tag += " (E)";

            return tag;
        }

        private void RefreshToggleLabels()
        {
            StyleToggleButton(
                EffectivePlayerInvincibleButton,
                EffectivePlayerInvincibleLabel,
                playerInvincible ? "BẤT TỬ: BẬT" : "BẤT TỬ: TẮT",
                playerInvincible);

            StyleToggleButton(
                autoRageButton,
                autoRageLabel,
                autoRageEnabled ? "MAX NỘ: BẬT" : "MAX NỘ: TẮT",
                autoRageEnabled);

            StyleToggleButton(
                enemyAiButton,
                enemyAiLabel,
                enableEnemyAI ? "AI QUÁI: BẬT" : "AI QUÁI: TẮT",
                enableEnemyAI);

            StyleToggleButton(
                enemyInvincibleButton,
                enemyInvincibleLabel,
                enemyInvincible ? "QUÁI BẤT TỬ: BẬT" : "QUÁI BẤT TỬ: TẮT",
                enemyInvincible);
        }

        private void EnsureRosterPopulated()
        {
            if (unitRoster != null && unitRoster.Length > 0)
            {
                for (int i = 0; i < unitRoster.Length; i++)
                {
                    if (unitRoster[i] != null && unitRoster[i].prefab != null) return;
                }
            }

            List<TrainingUnitEntry> harvested = new List<TrainingUnitEntry>();
            List<GameObject> seen = new List<GameObject>();

            IEnumerable<StageDataSO> stageSource = levelManager != null ? levelManager.AllStages : null;
            if (stageSource != null)
            {
                foreach (StageDataSO stage in stageSource)
                {
                    HarvestStageUnits(stage, seen, harvested);
                }
            }
            else if (StageManager.Instance != null && StageManager.Instance.AllLevels != null)
            {
                foreach (LevelDataSO lvl in StageManager.Instance.AllLevels)
                {
                    HarvestStageUnits(lvl, seen, harvested);
                }
            }

            if (harvested.Count > 0)
            {
                unitRoster = harvested.ToArray();
            }
        }

        private static void HarvestStageUnits(StageDataSO stage, List<GameObject> seen, List<TrainingUnitEntry> outList)
        {
            if (stage == null) return;

            if (stage.waves != null)
            {
                foreach (WaveData w in stage.waves)
                {
                    if (w?.enemies == null) continue;
                    foreach (EnemySpawnEntry e in w.enemies)
                    {
                        AddUniqueRosterUnit(e.enemyPrefab, seen, outList);
                    }
                }
            }
            if (stage is LevelDataSO level)
            {
                AddUniqueRosterUnit(level.supportMeleeCreepPrefab, seen, outList);
                AddUniqueRosterUnit(level.supportRangedCreepPrefab, seen, outList);
                AddUniqueRosterUnit(level.supportElitePrefab, seen, outList);
            }
            AddUniqueRosterUnit(stage.bossPrefab, seen, outList);
        }

        private static void AddUniqueRosterUnit(GameObject prefab, List<GameObject> seen, List<TrainingUnitEntry> outList)
        {
            if (prefab == null || seen.Contains(prefab)) return;
            seen.Add(prefab);
            string display = prefab.name.Replace("_", " ");
            EnemyBase eb = prefab.GetComponent<EnemyBase>();
            BossBase bb = prefab.GetComponent<BossBase>();
            if (bb != null) display = "[BOSS] " + bb.BossName;
            else if (eb != null && eb.Data != null && !string.IsNullOrEmpty(eb.Data.enemyName))
            {
                display = (eb.Data.isElite ? "[ELITE] " : string.Empty) + eb.Data.enemyName;
            }
            outList.Add(new TrainingUnitEntry { displayName = display, prefab = prefab });
        }

        public void EnsureSpawnScrollBarConfigured()
        {
            if (trainingPanel == null) return;

            ScrollRect sr = trainingPanel.GetComponentInChildren<ScrollRect>(true);
            if (sr == null) return;

            sr.horizontal = true;
            sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.elasticity = 0.08f;
            sr.inertia = true;
            sr.decelerationRate = 0.135f;
            sr.scrollSensitivity = 28f;

            Image scrollImg = sr.GetComponent<Image>();
            if (scrollImg == null)
            {
                scrollImg = sr.gameObject.AddComponent<Image>();
            }
            scrollImg.sprite = null;
            scrollImg.color = new Color(0f, 0f, 0f, 0.01f);
            scrollImg.raycastTarget = true;

            RectTransform vpRt = sr.viewport;
            if (vpRt == null)
            {
                Transform vpTf = FindChildRecursive(sr.transform, "Viewport");
                if (vpTf != null) vpRt = vpTf.GetComponent<RectTransform>();
            }
            if (vpRt != null)
            {
                sr.viewport = vpRt;
                vpRt.anchorMin = Vector2.zero;
                vpRt.anchorMax = Vector2.one;
                vpRt.pivot = new Vector2(0.5f, 0.5f);
                vpRt.offsetMin = new Vector2(2f, 1f);
                vpRt.offsetMax = new Vector2(-2f, -1f);

                Image vpImg = vpRt.GetComponent<Image>();
                if (vpImg == null)
                {
                    vpImg = vpRt.gameObject.AddComponent<Image>();
                }
                vpImg.sprite = null;
                vpImg.color = new Color(0f, 0f, 0f, 0.01f);
                vpImg.raycastTarget = true;
                if (vpRt.GetComponent<RectMask2D>() == null)
                {
                    vpRt.gameObject.AddComponent<RectMask2D>();
                }
            }

            RectTransform contentRt = sr.content;
            if (contentRt == null)
            {
                Transform contentTf = FindChildRecursive(sr.transform, "Content");
                if (contentTf != null) contentRt = contentTf.GetComponent<RectTransform>();
            }
            if (contentRt != null)
            {
                sr.content = contentRt;
                contentRt.anchorMin = new Vector2(0f, 0f);
                contentRt.anchorMax = new Vector2(0f, 1f);
                contentRt.pivot = new Vector2(0f, 0.5f);
                contentRt.anchoredPosition = Vector2.zero;
                contentRt.sizeDelta = Vector2.zero;

                HorizontalLayoutGroup hlg = contentRt.GetComponent<HorizontalLayoutGroup>();
                if (hlg == null)
                {
                    hlg = contentRt.gameObject.AddComponent<HorizontalLayoutGroup>();
                }
                hlg.padding = new RectOffset(4, 6, 2, 2);
                hlg.spacing = 6f;
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;

                ContentSizeFitter csf = contentRt.GetComponent<ContentSizeFitter>();
                if (csf == null)
                {
                    csf = contentRt.gameObject.AddComponent<ContentSizeFitter>();
                }
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

                for (int i = 0; i < contentRt.childCount; i++)
                {
                    Transform child = contentRt.GetChild(i);
                    if (child != null && child.TryGetComponent(out Button childBtn))
                    {
                        ConfigureSpawnButtonForScroll(childBtn, sr);
                    }
                }
            }

            if (spawnButtons != null)
            {
                for (int i = 0; i < spawnButtons.Length; i++)
                {
                    if (spawnButtons[i] != null)
                    {
                        ConfigureSpawnButtonForScroll(spawnButtons[i], sr);
                    }
                }
            }

            if (contentRt != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
            }
        }

        private static void ConfigureSpawnButtonForScroll(Button btn, ScrollRect scrollRect)
        {
            if (btn == null) return;

            LayoutElement le = btn.GetComponent<LayoutElement>();
            if (le == null)
            {
                le = btn.gameObject.AddComponent<LayoutElement>();
            }
            le.minWidth = 64f;
            le.preferredWidth = 64f;
            le.minHeight = 74f;
            le.preferredHeight = 74f;
            le.flexibleWidth = 0f;

            ScrollEventForwarder fwd = btn.GetComponent<ScrollEventForwarder>();
            if (fwd == null)
            {
                fwd = btn.gameObject.AddComponent<ScrollEventForwarder>();
            }
            fwd.Initialize(scrollRect);
        }

        private void EnsureBossPanelClearsTrainingDock()
        {
            Canvas canvas = trainingPanel != null
                ? trainingPanel.GetComponentInParent<Canvas>()
                : (GameUIManager.Instance != null ? GameUIManager.Instance.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>());
            if (canvas == null) return;

            Transform bossPanelTr = canvas.transform.Find("BossHealthPanel");
            if (bossPanelTr != null)
            {
                RectTransform bossRt = bossPanelTr.GetComponent<RectTransform>();
                if (bossRt != null && bossRt.anchoredPosition.y > -114f)
                {
                    bossRt.anchoredPosition = new Vector2(bossRt.anchoredPosition.x, -114f);
                }
            }
        }

        /// <summary>
        /// Upgrades either a pre-serialized TrainingTopBar or runtime fallback hierarchy
        /// to the unified 09_TrainingSpawnerDock frame, 2x2 LED pill toggles,
        /// violet plasma dividers, flanked spawn carousel (&lt; / &gt;), and circular Clear button.
        /// </summary>
        public void ApplySleekGlassTrainingTheme()
        {
            if (trainingPanel == null) return;

            CacheDockSpritesIfMissing();
            DiscoverMissingTopBarRefs();
            if (isActive)
            {
                EnsureBossPanelClearsTrainingDock();
            }

            RectTransform panelRt = trainingPanel.GetComponent<RectTransform>();
            if (panelRt != null)
            {
                panelRt.anchorMin = new Vector2(0.5f, 1f);
                panelRt.anchorMax = new Vector2(0.5f, 1f);
                panelRt.pivot = new Vector2(0.5f, 1f);
                panelRt.anchoredPosition = new Vector2(-12f, -8f);
                panelRt.sizeDelta = new Vector2(968f, 98f);
            }

            Image panelImg = GetOrAddImage(trainingPanel);
            Sprite dockSp = dockFrameSprite != null
                ? dockFrameSprite
                : GlassHudTheme.LoadSprite("09_TrainingSpawnerDock/Frame_Training_MainDock_Clean.png");
            if (dockSp != null)
            {
                GlassHudTheme.ApplySlicedSprite(panelImg, dockSp, 968f, 98f, Color.white);
            }
            else
            {
                GlassHudTheme.ApplySprite(panelImg, "TrainingDock_GlassPanel.png", default, true, false);
                panelImg.color = panelImg.sprite != null
                    ? Color.white
                    : new Color(0.06f, 0.09f, 0.15f, 0.92f);
            }

            Transform group1 = FindChildRecursive(trainingPanel.transform, "Group1_PlayerControls");
            ConfigureGroupContainer(group1, new Vector2(14f, 0f), new Vector2(164f, 78f));

            Transform group2 = FindChildRecursive(trainingPanel.transform, "Group2_EnemyControls");
            ConfigureGroupContainer(group2, new Vector2(182f, 0f), new Vector2(164f, 78f));

            Sprite divSp = plasmaDividerSprite != null
                ? plasmaDividerSprite
                : GlassHudTheme.LoadSprite("09_TrainingSpawnerDock/Divider_Training_PlasmaTube_Violet.png");
            EnsurePlasmaDivider(trainingPanel.transform, "Divider_Left", new Vector2(349f, 0f), new Vector2(16f, 78f), divSp);

            Transform group3 = FindChildRecursive(trainingPanel.transform, "Group3_SpawnAndClear");
            ConfigureGroupContainer(group3, new Vector2(368f, 0f), new Vector2(588f, 80f));

            ConfigureToggleLayout(EffectivePlayerInvincibleButton, new Vector2(0f, 20f));
            ConfigureToggleLayout(autoRageButton, new Vector2(0f, -20f));
            ConfigureToggleLayout(enemyAiButton, new Vector2(0f, 20f));
            ConfigureToggleLayout(enemyInvincibleButton, new Vector2(0f, -20f));
            RefreshToggleLabels();

            ScrollRect sr = trainingPanel.GetComponentInChildren<ScrollRect>(true);
            if (sr != null)
            {
                RectTransform scrollRt = sr.GetComponent<RectTransform>();
                if (scrollRt != null)
                {
                    scrollRt.anchorMin = new Vector2(0f, 0.5f);
                    scrollRt.anchorMax = new Vector2(0f, 0.5f);
                    scrollRt.pivot = new Vector2(0f, 0.5f);
                    scrollRt.anchoredPosition = new Vector2(29f, 0f);
                    scrollRt.sizeDelta = new Vector2(432f, 78f);
                }

                Image scrollImg = GetOrAddImage(sr.gameObject);
                scrollImg.sprite = null;
                scrollImg.color = new Color(0f, 0f, 0f, 0.01f);
                scrollImg.raycastTarget = true;

                Transform arrowParent = group3 != null ? group3 : sr.transform.parent;
                TMP_FontAsset sharedFont = FindCanvasTmpFont(trainingPanel.GetComponentInParent<Canvas>());

                scrollLeftButton = EnsureCarouselArrowButton(
                    arrowParent,
                    scrollLeftButton,
                    "ScrollLeftButton",
                    "<",
                    new Vector2(13f, 0f),
                    sharedFont);

                scrollRightButton = EnsureCarouselArrowButton(
                    arrowParent,
                    scrollRightButton,
                    "ScrollRightButton",
                    ">",
                    new Vector2(478f, 0f),
                    sharedFont);

                if (group3 != null)
                {
                    EnsurePlasmaDivider(group3, "Divider_Right", new Vector2(493f, 0f), new Vector2(16f, 78f), divSp);
                }

                if (Application.isPlaying)
                {
                    WireCarouselArrowButtons();
                }
            }

            StyleAllSpawnCards(sr);
            StyleClearAllButton();

            if (sr != null && sr.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(sr.content);
            }
        }

        private static void EnsurePlasmaDivider(Transform parent, string name, Vector2 anchoredPos, Vector2 size, Sprite divSprite)
        {
            if (parent == null) return;

            Transform existing = parent.Find(name);
            GameObject divObj = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(Image));
            if (existing == null)
            {
                divObj.transform.SetParent(parent, false);
            }

            RectTransform rt = divObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = GetOrAddImage(divObj);
            img.raycastTarget = false;
            if (divSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(img, divSprite, size.x, size.y, Color.white);
                img.enabled = true;
            }
            else
            {
                img.color = new Color(0.55f, 0.30f, 0.92f, 0.75f);
            }
        }

        private static Image GetOrAddImage(GameObject go)
        {
            if (go == null) return null;
            Image img = go.GetComponent<Image>();
            return img != null ? img : go.AddComponent<Image>();
        }

        private static void ConfigureGroupContainer(Transform groupTr, Vector2 anchoredPos, Vector2 size)
        {
            if (groupTr == null) return;

            RectTransform rt = groupTr.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = anchoredPos;
                rt.sizeDelta = size;
            }

            if (groupTr.TryGetComponent(out Image bg))
            {
                bg.sprite = null;
                bg.color = Color.clear;
                bg.raycastTarget = false;
            }
        }

        private static void ConfigureToggleLayout(Button btn, Vector2 anchoredPos)
        {
            if (btn == null) return;
            RectTransform rt = btn.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = anchoredPos;
                rt.sizeDelta = new Vector2(164f, 36f);
            }
        }

        private void StyleToggleButton(Button btn, TMP_Text lbl, string labelText, bool isOn)
        {
            if (btn != null)
            {
                Image btnImg = btn.image != null ? btn.image : btn.GetComponent<Image>();
                if (btnImg != null)
                {
                    Sprite targetSp = isOn
                        ? (toggleOnSprite != null ? toggleOnSprite : GlassHudTheme.LoadSprite("09_TrainingSpawnerDock/Toggle_Training_OptionPill_BlueLED_Clean.png"))
                        : (toggleOffSprite != null ? toggleOffSprite : GlassHudTheme.LoadSprite("09_TrainingSpawnerDock/Toggle_Training_OptionPill_Blank.png"));

                    if (targetSp != null)
                    {
                        GlassHudTheme.ApplySlicedSprite(btnImg, targetSp, 164f, 36f, Color.white);
                    }
                    else
                    {
                        GlassHudTheme.ApplySprite(btnImg, isOn ? "TogglePill_On_Green.png" : "TogglePill_Off_DarkGlass.png", default, true, false);
                        btnImg.color = btnImg.sprite != null
                            ? Color.white
                            : (isOn ? new Color(0.16f, 0.60f, 0.34f, 0.96f) : new Color(0.14f, 0.18f, 0.25f, 0.95f));
                    }
                    btn.targetGraphic = btnImg;
                }

                Transform existingOrb = btn.transform.Find("LedOrb");
                GameObject orbObj = existingOrb != null
                    ? existingOrb.gameObject
                    : new GameObject("LedOrb", typeof(RectTransform), typeof(Image));
                if (existingOrb == null)
                {
                    orbObj.transform.SetParent(btn.transform, false);
                }

                RectTransform orbRt = orbObj.GetComponent<RectTransform>();
                orbRt.anchorMin = new Vector2(0f, 0.5f);
                orbRt.anchorMax = new Vector2(0f, 0.5f);
                orbRt.pivot = new Vector2(0.5f, 0.5f);
                orbRt.anchoredPosition = new Vector2(14f, 0f);
                orbRt.sizeDelta = new Vector2(20f, 20f);

                Image orbImg = GetOrAddImage(orbObj);
                orbImg.raycastTarget = false;
                if (isOn)
                {
                    // Toggle_Training_OptionPill_BlueLED_Clean already contains the lit orb in its L=130 bezel
                    orbImg.enabled = false;
                }
                else
                {
                    Sprite orbSp = blueLedOrbSprite != null
                        ? blueLedOrbSprite
                        : GlassHudTheme.LoadSprite("09_TrainingSpawnerDock/Icon_Training_BlueLEDOrb.png");
                    if (orbSp != null)
                    {
                        GlassHudTheme.ApplySlicedSprite(orbImg, orbSp, 20f, 20f, new Color(0.45f, 0.52f, 0.62f, 0.45f));
                        orbImg.enabled = true;
                    }
                    else
                    {
                        orbImg.enabled = false;
                    }
                }
            }

            if (lbl != null)
            {
                lbl.text = labelText;
                lbl.transform.SetAsLastSibling();
                RectTransform lblRt = lbl.GetComponent<RectTransform>();
                if (lblRt != null)
                {
                    lblRt.anchorMin = Vector2.zero;
                    lblRt.anchorMax = Vector2.one;
                    lblRt.pivot = new Vector2(0.5f, 0.5f);
                    lblRt.offsetMin = new Vector2(26f, 0f);
                    lblRt.offsetMax = new Vector2(-6f, 0f);
                }

                lbl.alignment = TextAlignmentOptions.MidlineLeft;
                lbl.textWrappingMode = TextWrappingModes.NoWrap;
                lbl.overflowMode = TextOverflowModes.Ellipsis;
                lbl.raycastTarget = false;

                Color textColor = isOn ? ToggleOnTextColor : ToggleOffTextColor;
                GlassHudTheme.StyleTmpText(lbl, 11.5f, textColor, true, true);
                lbl.enableAutoSizing = true;
                lbl.fontSizeMin = 9.5f;
                lbl.fontSizeMax = 11.5f;
            }
        }

        private void StyleClearAllButton()
        {
            if (clearAllButton == null) return;

            RectTransform clearRt = clearAllButton.GetComponent<RectTransform>();
            if (clearRt != null)
            {
                clearRt.anchorMin = new Vector2(1f, 0.5f);
                clearRt.anchorMax = new Vector2(1f, 0.5f);
                clearRt.pivot = new Vector2(0.5f, 0.5f);
                clearRt.anchoredPosition = new Vector2(-39f, 0f);
                clearRt.sizeDelta = new Vector2(76f, 76f);
            }

            Image clearImg = clearAllButton.image != null
                ? clearAllButton.image
                : GetOrAddImage(clearAllButton.gameObject);
            Sprite clearSp = clearAllSprite != null
                ? clearAllSprite
                : GlassHudTheme.LoadSprite("09_TrainingSpawnerDock/Btn_Training_ClearAll_RedRing_Clean.png");
            if (clearSp != null)
            {
                GlassHudTheme.ApplySlicedSprite(clearImg, clearSp, 76f, 76f, Color.white);
            }
            else
            {
                GlassHudTheme.ApplySprite(clearImg, "PillBtn_RedDanger.png", default, true, false);
                clearImg.color = clearImg.sprite != null
                    ? Color.white
                    : new Color(0.72f, 0.16f, 0.20f, 0.96f);
            }
            clearAllButton.targetGraphic = clearImg;

            Transform trashTr = clearAllButton.transform.Find("TrashIcon");
            GameObject trashObj;
            if (trashTr != null)
            {
                trashObj = trashTr.gameObject;
            }
            else
            {
                trashObj = new GameObject("TrashIcon", typeof(RectTransform), typeof(Image));
                trashObj.transform.SetParent(clearAllButton.transform, false);
            }

            RectTransform trashRt = trashObj.GetComponent<RectTransform>();
            trashRt.anchorMin = new Vector2(0.5f, 1f);
            trashRt.anchorMax = new Vector2(0.5f, 1f);
            trashRt.pivot = new Vector2(0.5f, 1f);
            trashRt.anchoredPosition = new Vector2(0f, -14f);
            trashRt.sizeDelta = new Vector2(18f, 18f);

            Image trashImg = GetOrAddImage(trashObj);
            Sprite trashSp = clearTrashIconSprite != null
                ? clearTrashIconSprite
                : GlassHudTheme.LoadSprite("Icon_Trash_Red.png");
            if (trashSp != null)
            {
                GlassHudTheme.ApplySprite(trashImg, trashSp, false, true);
            }
            else
            {
                GlassHudTheme.ApplySprite(trashImg, "Icon_Trash_Red.png", default, false, true);
            }
            trashImg.color = Color.white;
            trashImg.preserveAspect = true;
            trashImg.raycastTarget = false;
            trashImg.enabled = trashImg.sprite != null;

            Transform lblTr = clearAllButton.transform.Find("Label");
            TMP_Text clearLbl = lblTr != null
                ? lblTr.GetComponent<TMP_Text>()
                : clearAllButton.GetComponentInChildren<TMP_Text>(true);

            if (clearLbl == null)
            {
                TMP_FontAsset sharedFont = FindCanvasTmpFont(clearAllButton.GetComponentInParent<Canvas>());
                GameObject lblObj = CreateTmpTextObj("Label", clearAllButton.transform, "XÓA\nQUÁI", 10f, Color.white, sharedFont);
                clearLbl = lblObj.GetComponent<TMP_Text>();
            }

            RectTransform lblRt = clearLbl.GetComponent<RectTransform>();
            if (lblRt != null)
            {
                lblRt.anchorMin = Vector2.zero;
                lblRt.anchorMax = Vector2.one;
                lblRt.pivot = new Vector2(0.5f, 0.5f);
                lblRt.offsetMin = new Vector2(10f, 10f);
                lblRt.offsetMax = new Vector2(-10f, -32f);
            }

            clearLbl.text = "XÓA\nQUÁI";
            clearLbl.alignment = TextAlignmentOptions.Center;
            clearLbl.textWrappingMode = TextWrappingModes.NoWrap;
            clearLbl.raycastTarget = false;
            GlassHudTheme.StyleTmpText(clearLbl, 10f, Color.white, true, true);
            clearLbl.enableAutoSizing = true;
            clearLbl.fontSizeMin = 8f;
            clearLbl.fontSizeMax = 10.5f;
        }

        private Button EnsureCarouselArrowButton(
            Transform parent,
            Button existing,
            string objName,
            string symbol,
            Vector2 anchoredPos,
            TMP_FontAsset fallbackFont)
        {
            if (parent == null) return existing;

            Button btn = existing;
            if (btn == null)
            {
                Transform existingTr = parent.Find(objName);
                if (existingTr != null)
                {
                    btn = existingTr.GetComponent<Button>();
                }
            }

            GameObject btnObj;
            if (btn == null)
            {
                btnObj = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(Button));
                btnObj.transform.SetParent(parent, false);
                btn = btnObj.GetComponent<Button>();
            }
            else
            {
                btnObj = btn.gameObject;
                if (btnObj.transform.parent != parent)
                {
                    btnObj.transform.SetParent(parent, false);
                }
            }

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(26f, 70f);

            Image bg = GetOrAddImage(btnObj);
            Sprite arrowSp = arrowButtonSprite != null
                ? arrowButtonSprite
                : GlassHudTheme.LoadSprite("Keycap_Badge_Dark.png");
            if (arrowSp != null)
            {
                GlassHudTheme.ApplySlicedSprite(bg, arrowSp, 26f, 70f, Color.white);
            }
            else
            {
                bg.color = new Color(0.11f, 0.16f, 0.24f, 0.94f);
            }
            bg.raycastTarget = true;
            btn.targetGraphic = bg;

            Transform lblTr = btnObj.transform.Find("Label");
            TMP_Text lbl = lblTr != null
                ? lblTr.GetComponent<TMP_Text>()
                : btnObj.GetComponentInChildren<TMP_Text>(true);
            if (lbl == null)
            {
                GameObject lblObj = CreateTmpTextObj("Label", btnObj.transform, symbol, 16f, ArrowKeycapTextColor, fallbackFont);
                lbl = lblObj.GetComponent<TMP_Text>();
            }

            RectTransform lblRt = lbl.GetComponent<RectTransform>();
            if (lblRt != null)
            {
                lblRt.anchorMin = Vector2.zero;
                lblRt.anchorMax = Vector2.one;
                lblRt.pivot = new Vector2(0.5f, 0.5f);
                lblRt.offsetMin = Vector2.zero;
                lblRt.offsetMax = Vector2.zero;
            }

            if (lbl.font == null && fallbackFont != null)
            {
                lbl.font = fallbackFont;
            }
            lbl.text = symbol;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.raycastTarget = false;
            GlassHudTheme.StyleTmpText(lbl, 16f, ArrowKeycapTextColor, true, true);

            return btn;
        }

        private void WireCarouselArrowButtons()
        {
            if (scrollLeftButton != null)
            {
                scrollLeftButton.onClick.RemoveAllListeners();
                scrollLeftButton.onClick.AddListener(() => NudgeSpawnCarousel(-SpawnScrollStepNormalized));
            }
            if (scrollRightButton != null)
            {
                scrollRightButton.onClick.RemoveAllListeners();
                scrollRightButton.onClick.AddListener(() => NudgeSpawnCarousel(SpawnScrollStepNormalized));
            }
        }

        private void NudgeSpawnCarousel(float normalizedDelta)
        {
            if (trainingPanel == null) return;
            ScrollRect sr = trainingPanel.GetComponentInChildren<ScrollRect>(true);
            if (sr == null) return;

            float basePos = spawnScrollLerpCoroutine != null
                ? spawnScrollTargetNormalized
                : sr.horizontalNormalizedPosition;

            spawnScrollTargetNormalized = Mathf.Clamp01(basePos + normalizedDelta);

            if (spawnScrollLerpCoroutine != null)
            {
                StopCoroutine(spawnScrollLerpCoroutine);
                spawnScrollLerpCoroutine = null;
            }

            if (!gameObject.activeInHierarchy || !sr.gameObject.activeInHierarchy)
            {
                sr.horizontalNormalizedPosition = spawnScrollTargetNormalized;
                return;
            }

            spawnScrollLerpCoroutine = StartCoroutine(SmoothLerpSpawnScrollCoroutine(sr, spawnScrollTargetNormalized));
        }

        private IEnumerator SmoothLerpSpawnScrollCoroutine(ScrollRect sr, float targetNormalized)
        {
            float startNormalized = sr.horizontalNormalizedPosition;
            float elapsed = 0f;
            sr.StopMovement();

            while (elapsed < SpawnScrollLerpDuration)
            {
                if (sr == null)
                {
                    spawnScrollLerpCoroutine = null;
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / SpawnScrollLerpDuration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                sr.velocity = Vector2.zero;
                sr.horizontalNormalizedPosition = Mathf.Lerp(startNormalized, targetNormalized, eased);
                yield return null;
            }

            if (sr != null)
            {
                sr.velocity = Vector2.zero;
                sr.horizontalNormalizedPosition = targetNormalized;
            }
            spawnScrollLerpCoroutine = null;
        }

        private void StyleAllSpawnCards(ScrollRect sr)
        {
            Transform contentTr = sr != null ? sr.content : null;
            if (contentTr != null)
            {
                for (int i = 0; i < contentTr.childCount; i++)
                {
                    Transform child = contentTr.GetChild(i);
                    if (child != null && child.TryGetComponent(out Button childBtn))
                    {
                        StyleSpawnCardButton(childBtn);
                    }
                }
            }

            if (spawnButtons != null)
            {
                for (int i = 0; i < spawnButtons.Length; i++)
                {
                    Button btn = spawnButtons[i];
                    if (btn == null) continue;
                    if (contentTr != null && btn.transform.parent == contentTr) continue;
                    StyleSpawnCardButton(btn);
                }
            }
        }

        private void StyleSpawnCardButton(Button btn)
        {
            if (btn == null) return;

            RectTransform btnRt = btn.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                btnRt.pivot = new Vector2(0.5f, 0.5f);
                btnRt.sizeDelta = new Vector2(64f, 74f);
            }

            LayoutElement le;
            if (!btn.TryGetComponent(out le))
            {
                le = btn.gameObject.AddComponent<LayoutElement>();
            }
            le.minWidth = 64f;
            le.preferredWidth = 64f;
            le.minHeight = 74f;
            le.preferredHeight = 74f;
            le.flexibleWidth = 0f;

            Image bg = btn.image != null ? btn.image : GetOrAddImage(btn.gameObject);
            Sprite cardSp = spawnCardSprite != null
                ? spawnCardSprite
                : GlassHudTheme.LoadSprite("09_TrainingSpawnerDock/Slot_Training_EnemySpawnCard_Clean.png");
            if (cardSp != null)
            {
                GlassHudTheme.ApplySlicedSprite(bg, cardSp, 64f, 74f, Color.white);
            }
            else
            {
                GlassHudTheme.ApplySprite(bg, "MiniGlassCard_SpawnSlot.png", default, true, false);
                bg.color = bg.sprite != null
                    ? Color.white
                    : new Color(0.14f, 0.20f, 0.30f, 0.94f);
            }
            btn.targetGraphic = bg;

            Transform iconTr = btn.transform.Find("Icon");
            if (iconTr != null && iconTr.TryGetComponent(out Image iconImg))
            {
                RectTransform iconRt = iconTr.GetComponent<RectTransform>();
                if (iconRt != null)
                {
                    iconRt.anchorMin = Vector2.zero;
                    iconRt.anchorMax = Vector2.one;
                    iconRt.pivot = new Vector2(0.5f, 0.5f);
                    iconRt.offsetMin = new Vector2(7f, 20f);
                    iconRt.offsetMax = new Vector2(-7f, -6f);
                }
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                iconImg.color = Color.white;
            }

            Transform lblTr = btn.transform.Find("Label");
            TMP_Text lblTmp = lblTr != null
                ? lblTr.GetComponent<TMP_Text>()
                : btn.GetComponentInChildren<TMP_Text>(true);
            if (lblTmp != null)
            {
                RectTransform lblRt = lblTmp.GetComponent<RectTransform>();
                if (lblRt != null)
                {
                    lblRt.anchorMin = new Vector2(0f, 0f);
                    lblRt.anchorMax = new Vector2(1f, 0f);
                    lblRt.pivot = new Vector2(0.5f, 0f);
                    lblRt.anchoredPosition = new Vector2(0f, 3f);
                    lblRt.sizeDelta = new Vector2(-10f, 15f);
                }
                lblTmp.alignment = TextAlignmentOptions.Center;
                lblTmp.textWrappingMode = TextWrappingModes.NoWrap;
                lblTmp.overflowMode = TextOverflowModes.Ellipsis;
                lblTmp.raycastTarget = false;
                GlassHudTheme.StyleTmpText(lblTmp, 9.5f, SpawnCardTextColor, true, true);
                lblTmp.enableAutoSizing = true;
                lblTmp.fontSizeMin = 7.5f;
                lblTmp.fontSizeMax = 10f;
            }
        }

        private void EnsureTrainingPanelExists()
        {
            if (trainingPanel != null) return;
            BuildRuntimeTopBarFallback();
        }

        private void BuildRuntimeTopBarFallback()
        {
            if (trainingPanel != null) return;

            Canvas canvas = GameUIManager.Instance != null
                ? GameUIManager.Instance.GetComponentInParent<Canvas>()
                : FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            Transform bossPanelTr = canvas.transform.Find("BossHealthPanel");
            if (bossPanelTr != null)
            {
                RectTransform bossRt = bossPanelTr.GetComponent<RectTransform>();
                if (bossRt != null && bossRt.anchoredPosition.y > -114f)
                {
                    bossRt.anchoredPosition = new Vector2(bossRt.anchoredPosition.x, -114f);
                }
            }

            TMP_FontAsset sharedFont = FindCanvasTmpFont(canvas);

            trainingPanel = new GameObject("TrainingTopBar", typeof(RectTransform), typeof(Image));
            trainingPanel.transform.SetParent(canvas.transform, false);

            int modalSiblingIdx = canvas.transform.childCount;
            string[] modalNames = { "VictoryPanel", "DefeatPanel", "PauseModalPanel" };
            for (int m = 0; m < modalNames.Length; m++)
            {
                Transform modalTr = canvas.transform.Find(modalNames[m]);
                if (modalTr != null) modalSiblingIdx = Mathf.Min(modalSiblingIdx, modalTr.GetSiblingIndex());
            }
            if (modalSiblingIdx < canvas.transform.childCount)
            {
                trainingPanel.transform.SetSiblingIndex(modalSiblingIdx);
            }
            RectTransform panelRt = trainingPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 1f);
            panelRt.anchorMax = new Vector2(0.5f, 1f);
            panelRt.pivot = new Vector2(0.5f, 1f);
            panelRt.anchoredPosition = new Vector2(-12f, -8f);
            panelRt.sizeDelta = new Vector2(968f, 98f);
            Image panelImg = trainingPanel.GetComponent<Image>();
            panelImg.color = Color.white;

            // Group 1: Player Controls
            GameObject group1 = CreateUiContainer("Group1_PlayerControls", trainingPanel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(164f, 78f), Color.clear);
            playerInvincibleButton = CreateActionButton("PlayerInvincibleButton", group1.transform, "BẤT TỬ: TẮT", new Vector2(0f, 20f), new Vector2(164f, 36f), Color.white, 11.5f, sharedFont, out playerInvincibleLabel);
            autoHpButton = playerInvincibleButton;
            autoHpLabel = playerInvincibleLabel;
            autoRageButton = CreateActionButton("AutoMaxRageButton", group1.transform, "MAX NỘ: BẬT", new Vector2(0f, -20f), new Vector2(164f, 36f), Color.white, 11.5f, sharedFont, out autoRageLabel);

            // Group 2: Enemy Controls
            GameObject group2 = CreateUiContainer("Group2_EnemyControls", trainingPanel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(182f, 0f), new Vector2(164f, 78f), Color.clear);
            enemyAiButton = CreateActionButton("EnemyAiButton", group2.transform, "AI QUÁI: BẬT", new Vector2(0f, 20f), new Vector2(164f, 36f), Color.white, 11.5f, sharedFont, out enemyAiLabel);
            enemyInvincibleButton = CreateActionButton("EnemyInvincibleButton", group2.transform, "QUÁI BẤT TỬ: TẮT", new Vector2(0f, -20f), new Vector2(164f, 36f), Color.white, 11.5f, sharedFont, out enemyInvincibleLabel);

            // Group 3: Spawn Carousel (< / Scroll / >) + Clear
            GameObject group3 = CreateUiContainer("Group3_SpawnAndClear", trainingPanel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(368f, 0f), new Vector2(588f, 80f), Color.clear);

            scrollLeftButton = EnsureCarouselArrowButton(group3.transform, null, "ScrollLeftButton", "<", new Vector2(13f, 0f), sharedFont);

            GameObject scrollObj = CreateUiContainer("SpawnListScroll", group3.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(29f, 0f), new Vector2(432f, 78f), new Color(0f, 0f, 0f, 0.01f));
            scrollObj.GetComponent<Image>().raycastTarget = true;
            ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
            sr.horizontal = true;
            sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.elasticity = 0.08f;
            sr.inertia = true;
            sr.decelerationRate = 0.135f;
            sr.scrollSensitivity = 28f;

            GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportObj.transform.SetParent(scrollObj.transform, false);
            RectTransform vpRt = viewportObj.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = new Vector2(2f, 1f);
            vpRt.offsetMax = new Vector2(-2f, -1f);
            Image vpImg = viewportObj.GetComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.01f);
            vpImg.raycastTarget = true;
            sr.viewport = vpRt;

            int count = unitRoster != null && unitRoster.Length > 0 ? unitRoster.Length : 12;
            GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            contentObj.transform.SetParent(viewportObj.transform, false);
            RectTransform contentRt = contentObj.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(0f, 1f);
            contentRt.pivot = new Vector2(0f, 0.5f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;
            HorizontalLayoutGroup hlg = contentObj.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(4, 6, 2, 2);
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            ContentSizeFitter csf = contentObj.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            sr.content = contentRt;

            spawnButtons = new Button[count];
            spawnButtonLabels = new TMP_Text[count];
            spawnIcons = new Image[count];
            for (int i = 0; i < count; i++)
            {
                string labelText = (unitRoster != null && i < unitRoster.Length && unitRoster[i] != null)
                    ? FormatConciseSpawnTag(unitRoster[i].displayName, unitRoster[i].prefab)
                    : $"Unit {i + 1}";
                Sprite iconSprite = null;
                if (unitRoster != null && i < unitRoster.Length && unitRoster[i] != null && unitRoster[i].prefab != null)
                {
                    SpriteRenderer unitSr = unitRoster[i].prefab.GetComponent<SpriteRenderer>()
                        ?? unitRoster[i].prefab.GetComponentInChildren<SpriteRenderer>(true);
                    if (unitSr != null) iconSprite = unitSr.sprite;
                }

                Button btn = CreateScrollSpawnIconButton(
                    $"SpawnUnit_{i}",
                    contentObj.transform,
                    sr,
                    labelText,
                    iconSprite,
                    sharedFont,
                    out TMP_Text btnLbl,
                    out Image btnIcon);
                spawnButtons[i] = btn;
                spawnButtonLabels[i] = btnLbl;
                spawnIcons[i] = btnIcon;
            }

            scrollRightButton = EnsureCarouselArrowButton(group3.transform, null, "ScrollRightButton", ">", new Vector2(478f, 0f), sharedFont);

            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
            sr.horizontalNormalizedPosition = 0f;

            clearAllButton = CreateActionButton("ClearAllEnemiesButton", group3.transform, "XÓA\nQUÁI", new Vector2(-39f, 0f), new Vector2(76f, 76f), Color.white, 10f, sharedFont, out _);
            RectTransform clearRt = clearAllButton.GetComponent<RectTransform>();
            clearRt.anchorMin = new Vector2(1f, 0.5f);
            clearRt.anchorMax = new Vector2(1f, 0.5f);
            clearRt.pivot = new Vector2(0.5f, 0.5f);
            clearRt.anchoredPosition = new Vector2(-39f, 0f);

            ApplySleekGlassTrainingTheme();
        }

        private static TMP_FontAsset FindCanvasTmpFont(Canvas canvas)
        {
            if (canvas != null)
            {
                TextMeshProUGUI[] texts = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i] != null && texts[i].font != null) return texts[i].font;
                }
            }
            return GlassHudTheme.ResolveSharedTmpFont(canvas != null ? canvas.transform : null);
        }

        private static GameObject CreateUiContainer(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, Color bgColor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = go.GetComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = bgColor.a > 0f;
            return go;
        }

        private static GameObject CreateTmpTextObj(string name, Transform parent, string text, float fontSize, Color color, TMP_FontAsset font)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return go;
        }

        private static Button CreateActionButton(
            string name,
            Transform parent,
            string text,
            Vector2 anchoredPos,
            Vector2 size,
            Color bgColor,
            float fontSize,
            TMP_FontAsset font,
            out TMP_Text labelOut)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            btnObj.GetComponent<Image>().color = bgColor;
            Button btn = btnObj.GetComponent<Button>();

            GameObject txtObj = CreateTmpTextObj("Label", btnObj.transform, text, fontSize, Color.white, font);
            RectTransform txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(4f, 2f);
            txtRt.offsetMax = new Vector2(-4f, -2f);
            labelOut = txtObj.GetComponent<TextMeshProUGUI>();
            return btn;
        }

        private static Button CreateScrollSpawnIconButton(
            string name,
            Transform parent,
            ScrollRect scrollRect,
            string text,
            Sprite iconSprite,
            TMP_FontAsset font,
            out TMP_Text labelOut,
            out Image iconOut)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button), typeof(ScrollEventForwarder));
            btnObj.transform.SetParent(parent, false);
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(64f, 74f);

            LayoutElement le = btnObj.GetComponent<LayoutElement>();
            le.minWidth = 64f;
            le.preferredWidth = 64f;
            le.minHeight = 74f;
            le.preferredHeight = 74f;
            le.flexibleWidth = 0f;

            Image bg = btnObj.GetComponent<Image>();
            bg.color = Color.white;
            Button btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = bg;
            btnObj.GetComponent<ScrollEventForwarder>().Initialize(scrollRect);

            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(btnObj.transform, false);
            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(7f, 20f);
            iconRt.offsetMax = new Vector2(-7f, -6f);
            iconOut = iconObj.GetComponent<Image>();
            iconOut.sprite = iconSprite;
            iconOut.preserveAspect = true;
            iconOut.raycastTarget = false;
            iconOut.enabled = iconSprite != null;

            GameObject txtObj = CreateTmpTextObj("Label", btnObj.transform, text, 9.5f, SpawnCardTextColor, font);
            RectTransform txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0f, 0f);
            txtRt.anchorMax = new Vector2(1f, 0f);
            txtRt.pivot = new Vector2(0.5f, 0f);
            txtRt.anchoredPosition = new Vector2(0f, 3f);
            txtRt.sizeDelta = new Vector2(-10f, 15f);
            TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            labelOut = tmp;
            return btn;
        }
    }
}
