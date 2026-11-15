using System;
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

        private LevelManager levelManager;
        private BaseCharacter activePlayer;
        private bool isActive;
        private float nextMaintenanceTime;
        private readonly List<GameObject> spawnedUnits = new List<GameObject>();

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

            DiscoverMissingTopBarRefs();

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

        public void BeginTraining(LevelManager manager, BaseCharacter player)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            enabled = true;
            Instance = this;

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
            DiscoverMissingTopBarRefs();

            if (trainingPanel != null)
            {
                trainingPanel.SetActive(true);
            }
            WireUiControls();

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

            if (enemyAiButton == null)
            {
                Transform t = FindChildRecursive(trainingPanel.transform, "EnemyAiButton");
                if (t != null)
                {
                    enemyAiButton = t.GetComponent<Button>();
                    enemyAiLabel = t.GetComponentInChildren<TMP_Text>(true);
                }
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
            TMP_Text invLbl = EffectivePlayerInvincibleLabel;
            Button invBtn = EffectivePlayerInvincibleButton;
            if (invLbl != null)
            {
                invLbl.text = playerInvincible ? "BẤT TỬ PLAYER: BẬT" : "BẤT TỬ PLAYER: TẮT";
            }
            if (invBtn != null && invBtn.image != null)
            {
                invBtn.image.color = playerInvincible
                    ? new Color(0.16f, 0.62f, 0.32f, 0.96f)
                    : new Color(0.28f, 0.31f, 0.37f, 0.95f);
            }

            if (autoRageLabel != null)
            {
                autoRageLabel.text = autoRageEnabled ? "MAX NỘ: BẬT" : "MAX NỘ: TẮT";
            }
            if (autoRageButton != null && autoRageButton.image != null)
            {
                autoRageButton.image.color = autoRageEnabled
                    ? new Color(0.82f, 0.46f, 0.10f, 0.96f)
                    : new Color(0.28f, 0.31f, 0.37f, 0.95f);
            }

            if (enemyAiLabel != null)
            {
                enemyAiLabel.text = enableEnemyAI ? "AI QUÁI: BẬT" : "AI QUÁI: TẮT";
            }
            if (enemyAiButton != null && enemyAiButton.image != null)
            {
                enemyAiButton.image.color = enableEnemyAI
                    ? new Color(0.16f, 0.58f, 0.34f, 0.96f)
                    : new Color(0.68f, 0.20f, 0.22f, 0.96f);
            }

            if (enemyInvincibleLabel != null)
            {
                enemyInvincibleLabel.text = enemyInvincible ? "BẤT TỬ QUÁI: BẬT" : "BẤT TỬ QUÁI: TẮT";
            }
            if (enemyInvincibleButton != null && enemyInvincibleButton.image != null)
            {
                enemyInvincibleButton.image.color = enemyInvincible
                    ? new Color(0.78f, 0.48f, 0.12f, 0.96f)
                    : new Color(0.28f, 0.31f, 0.37f, 0.95f);
            }
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

        private void EnsureTrainingPanelExists()
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
                if (bossRt != null && bossRt.anchoredPosition.y > -110f)
                {
                    bossRt.anchoredPosition = new Vector2(bossRt.anchoredPosition.x, -112f);
                }
            }

            TMP_FontAsset sharedFont = FindCanvasTmpFont(canvas);

            trainingPanel = new GameObject("TrainingTopBar", typeof(RectTransform), typeof(Image));
            trainingPanel.transform.SetParent(canvas.transform, false);
            RectTransform panelRt = trainingPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 1f);
            panelRt.anchorMax = new Vector2(0.5f, 1f);
            panelRt.pivot = new Vector2(0.5f, 1f);
            panelRt.anchoredPosition = new Vector2(34f, -6f);
            panelRt.sizeDelta = new Vector2(1016f, 88f);
            trainingPanel.GetComponent<Image>().color = new Color(0.06f, 0.09f, 0.15f, 0.92f);

            // Group 1: Player Controls
            GameObject group1 = CreateUiContainer("Group1_PlayerControls", trainingPanel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(184f, 76f), new Color(0.11f, 0.15f, 0.22f, 0.92f));
            playerInvincibleButton = CreateActionButton("PlayerInvincibleButton", group1.transform, "BẤT TỬ PLAYER: TẮT", new Vector2(0f, 18f), new Vector2(172f, 32f), new Color(0.28f, 0.31f, 0.37f, 0.95f), 11f, sharedFont, out playerInvincibleLabel);
            autoHpButton = playerInvincibleButton;
            autoHpLabel = playerInvincibleLabel;
            autoRageButton = CreateActionButton("AutoMaxRageButton", group1.transform, "MAX NỘ: BẬT", new Vector2(0f, -18f), new Vector2(172f, 32f), new Color(0.82f, 0.46f, 0.10f, 0.96f), 11f, sharedFont, out autoRageLabel);

            // Group 2: Enemy Controls
            GameObject group2 = CreateUiContainer("Group2_EnemyControls", trainingPanel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(200f, 0f), new Vector2(176f, 76f), new Color(0.11f, 0.15f, 0.22f, 0.92f));
            enemyAiButton = CreateActionButton("EnemyAiButton", group2.transform, "AI QUÁI: BẬT", new Vector2(0f, 18f), new Vector2(164f, 32f), new Color(0.16f, 0.58f, 0.34f, 0.96f), 11f, sharedFont, out enemyAiLabel);
            enemyInvincibleButton = CreateActionButton("EnemyInvincibleButton", group2.transform, "BẤT TỬ QUÁI: TẮT", new Vector2(0f, -18f), new Vector2(164f, 32f), new Color(0.28f, 0.31f, 0.37f, 0.95f), 11f, sharedFont, out enemyInvincibleLabel);

            // Group 3: Spawn Icon Bar + Clear
            GameObject group3 = CreateUiContainer("Group3_SpawnAndClear", trainingPanel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(384f, 0f), new Vector2(624f, 76f), new Color(0.09f, 0.12f, 0.19f, 0.90f));

            GameObject scrollObj = CreateUiContainer("SpawnListScroll", group3.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(504f, 70f), new Color(0.05f, 0.07f, 0.12f, 0.85f));
            ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
            sr.horizontal = true;
            sr.vertical = false;
            sr.scrollSensitivity = 28f;
            sr.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportObj.transform.SetParent(scrollObj.transform, false);
            RectTransform vpRt = viewportObj.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = new Vector2(4f, 3f);
            vpRt.offsetMax = new Vector2(-4f, -3f);
            sr.viewport = vpRt;

            int count = unitRoster != null && unitRoster.Length > 0 ? unitRoster.Length : 12;
            const float tileWidth = 64f;
            const float tileStep = 68f;
            GameObject contentObj = new GameObject("Content", typeof(RectTransform));
            contentObj.transform.SetParent(viewportObj.transform, false);
            RectTransform contentRt = contentObj.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(0f, 1f);
            contentRt.pivot = new Vector2(0f, 0.5f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(count * tileStep + 4f, 0f);
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
                    4f + i * tileStep,
                    tileWidth,
                    labelText,
                    iconSprite,
                    sharedFont,
                    out TMP_Text btnLbl,
                    out Image btnIcon);
                spawnButtons[i] = btn;
                spawnButtonLabels[i] = btnLbl;
                spawnIcons[i] = btnIcon;
            }

            clearAllButton = CreateActionButton("ClearAllEnemiesButton", group3.transform, "XÓA SẠCH\nQUÁI", new Vector2(-58f, 0f), new Vector2(108f, 68f), new Color(0.72f, 0.16f, 0.20f, 0.96f), 12f, sharedFont, out _);
            RectTransform clearRt = clearAllButton.GetComponent<RectTransform>();
            clearRt.anchorMin = new Vector2(1f, 0.5f);
            clearRt.anchorMax = new Vector2(1f, 0.5f);
            clearRt.pivot = new Vector2(1f, 0.5f);
            clearRt.anchoredPosition = new Vector2(-4f, 0f);
        }

        private static TMP_FontAsset FindCanvasTmpFont(Canvas canvas)
        {
            if (canvas == null) return null;
            TextMeshProUGUI[] texts = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].font != null) return texts[i].font;
            }
            return null;
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
            go.GetComponent<Image>().color = bgColor;
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
            float xPos,
            float width,
            string text,
            Sprite iconSprite,
            TMP_FontAsset font,
            out TMP_Text labelOut,
            out Image iconOut)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(xPos, 0f);
            rt.sizeDelta = new Vector2(width, 60f);

            Image bg = btnObj.GetComponent<Image>();
            bg.color = new Color(0.18f, 0.25f, 0.38f, 0.96f);
            Button btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = bg;

            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(btnObj.transform, false);
            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(6f, 18f);
            iconRt.offsetMax = new Vector2(-6f, -3f);
            iconOut = iconObj.GetComponent<Image>();
            iconOut.sprite = iconSprite;
            iconOut.preserveAspect = true;
            iconOut.raycastTarget = false;
            iconOut.enabled = iconSprite != null;

            GameObject txtObj = CreateTmpTextObj("Label", btnObj.transform, text, 9.5f, Color.white, font);
            RectTransform txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0f, 0f);
            txtRt.anchorMax = new Vector2(1f, 0f);
            txtRt.pivot = new Vector2(0.5f, 0f);
            txtRt.anchoredPosition = new Vector2(0f, 2f);
            txtRt.sizeDelta = new Vector2(-4f, 16f);
            TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            labelOut = tmp;
            return btn;
        }
    }
}
