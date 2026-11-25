using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;
using StickmanBeatEmUp.CorePure.Patterns.Factories;
using StickmanBeatEmUp.CorePure.Rules.Combat;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Enemies;
using StickmanBeatEmUp.Enemies.Stats;
using StickmanBeatEmUp.Level;
using StickmanBeatEmUp.UI;

namespace StickmanBeatEmUp.GameModes
{
    /// <summary>
    /// Endless Mode loop manager:
    /// - Full Attack on Titan arena [-22 .. 42], groundY = -2.5
    /// - Categorized 3-World pools (Melee Creeps, Ranged Creeps, Elites, Bosses) backed by EndlessEnemyCatalogSO & Zero-Null fallback
    /// - Wave progression with 3s rest +10 HP between cleared waves
    /// - Per-instance enemy scaling via WorldEnemyFactory (IEnemyFactory) & CombatObjectPool recycling
    /// - Escalating Creeps, Elites (Wave 3+ every 2 waves), and World Bosses every 5 waves (Wave 5, 10, 15...)
    /// </summary>
    public class EndlessModeManager : MonoBehaviour
    {
        [Header("Arena Bounds")]
        [SerializeField] private float arenaMinX = -22f;
        [SerializeField] private float arenaMaxX = 42f;
        [SerializeField] private float groundY = -2.5f;

        [Header("Wave Timing & Recovery")]
        [SerializeField] private float restDuration = 3.0f;
        [SerializeField] private float waveHealAmount = 10f;
        [SerializeField] private float spawnStaggerDelay = 0.16f;

        [Header("Catalog Registry (Zero Hardcoded Paths)")]
        [SerializeField] private EndlessEnemyCatalogSO enemyCatalog;
        [SerializeField] private string catalogResourcePath = "EndlessPrefabs/EndlessEnemyCatalog";

        [Header("Object Pooling")]
        [SerializeField] private bool useCombatObjectPool = true;

        [Header("Endless Categorized Pools")]
        [SerializeField] private List<GameObject> meleeCreeps = new List<GameObject>();
        [SerializeField] private List<GameObject> rangedCreeps = new List<GameObject>();
        [SerializeField] private List<GameObject> elites = new List<GameObject>();
        [SerializeField] private List<GameObject> bosses = new List<GameObject>();

        [Header("Endless Prefab Pools (3 Worlds - Legacy Backwards Compatibility)")]
        [SerializeField] private GameObject[] creepPrefabs;
        [SerializeField] private GameObject[] elitePrefabs;
        [SerializeField] private GameObject[] bossPrefabs;

        private LevelManager levelManager;
        private BaseCharacter activePlayer;
        private Coroutine waveRoutine;
        private bool isRunning;
        private int currentWave;
        private int pendingSpawnCount;
        private int spawnSideToggle;
        private bool hasExplicitCategorizedConfig;

        private readonly List<GameObject> activeEnemies = new List<GameObject>();
        private readonly WorldEnemyFactory defaultEnemyFactory = new WorldEnemyFactory();
        private IEnemyFactory enemyFactory;
        private GameBalanceConfigDTO cachedBalanceConfig;

        private GameObject restBannerObj;
        private TextMeshProUGUI restBannerText;

        public int CurrentWave => currentWave;
        public bool IsRunning => isRunning;
        public float ArenaMinX => arenaMinX;
        public float ArenaMaxX => arenaMaxX;
        public float GroundY => groundY;
        public IEnemyFactory EnemyFactory => enemyFactory ?? defaultEnemyFactory;

        public void SetEnemyFactory(IEnemyFactory factory)
        {
            enemyFactory = factory ?? defaultEnemyFactory;
        }

        public void SetBalanceConfig(GameBalanceConfigDTO config)
        {
            cachedBalanceConfig = config;
            if (config != null && enemyFactory == null)
            {
                enemyFactory = new WorldEnemyFactory(config);
            }
        }

        public void SetEnemyCatalog(EndlessEnemyCatalogSO catalog)
        {
            enemyCatalog = catalog;
        }

        public void SetArenaBounds(float minX, float maxX)
        {
            if (maxX > minX)
            {
                arenaMinX = minX;
                arenaMaxX = maxX;
            }
        }

        public void ConfigurePrefabPools(GameObject[] creeps, GameObject[] elitesArray, GameObject[] bossesArray)
        {
            GameObject[] validCreeps = FilterValidPrefabs(creeps);
            GameObject[] validElites = FilterValidPrefabs(elitesArray);
            GameObject[] validBosses = FilterValidPrefabs(bossesArray);

            if (validCreeps.Length > 0) creepPrefabs = validCreeps;
            if (validElites.Length > 0) elitePrefabs = validElites;
            if (validBosses.Length > 0) bossPrefabs = validBosses;

            if (validCreeps.Length > 0)
            {
                meleeCreeps.Clear();
                rangedCreeps.Clear();
                for (int i = 0; i < validCreeps.Length; i++)
                {
                    ClassifyAndAddCategorizedPrefab(validCreeps[i], meleeCreeps, rangedCreeps, elites, bosses);
                }
            }
            if (validElites.Length > 0)
            {
                elites.Clear();
                for (int i = 0; i < validElites.Length; i++)
                {
                    ClassifyAndAddCategorizedPrefab(validElites[i], meleeCreeps, rangedCreeps, elites, bosses);
                }
            }
            if (validBosses.Length > 0)
            {
                bosses.Clear();
                for (int i = 0; i < validBosses.Length; i++)
                {
                    ClassifyAndAddCategorizedPrefab(validBosses[i], meleeCreeps, rangedCreeps, elites, bosses);
                }
            }
        }

        public void ConfigureCategorizedPrefabPools(
            GameObject[] melee,
            GameObject[] ranged,
            GameObject[] eliteList,
            GameObject[] bossList)
        {
            hasExplicitCategorizedConfig = true;
            meleeCreeps.Clear();
            rangedCreeps.Clear();
            elites.Clear();
            bosses.Clear();

            AddUniqueValid(meleeCreeps, melee);
            AddUniqueValid(rangedCreeps, ranged);
            AddUniqueValid(elites, eliteList);
            AddUniqueValid(bosses, bossList);

            SyncLegacyArraysFromCategorized();
        }

        private void Start()
        {
            if (StageManager.LoadSelectedGameMode() == GameMode.Endless && !isRunning)
            {
                StartCoroutine(AutoStartSafetyRoutine());
            }
        }

        private IEnumerator AutoStartSafetyRoutine()
        {
            yield return null;
            if (!isRunning && StageManager.LoadSelectedGameMode() == GameMode.Endless)
            {
                BeginEndless(LevelManager.Instance, ResolvePlayer(null));
            }
        }

        public void BeginEndless(BaseCharacter player)
        {
            BeginEndless(levelManager != null ? levelManager : LevelManager.Instance, player);
        }

        public void BeginEndless(LevelManager manager, BaseCharacter player)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            enabled = true;

            levelManager = manager != null ? manager : (levelManager != null ? levelManager : LevelManager.Instance);
            BaseCharacter resolvedPlayer = ResolvePlayer(player);
            SetActivePlayer(resolvedPlayer);

            // Upgrade legacy serialized [-11.5, 11.5] scene bounds to the full Attack on Titan arena.
            if (Mathf.Approximately(arenaMinX, -11.5f) && Mathf.Approximately(arenaMaxX, 11.5f))
            {
                arenaMinX = -22f;
                arenaMaxX = 42f;
            }

            StageManager stageManager = StageManager.Instance != null
                ? StageManager.Instance
                : (GetComponent<StageManager>() ?? FindAnyObjectByType<StageManager>());
            if (stageManager != null && StageManager.LoadSelectedGameMode() == GameMode.Endless)
            {
                stageManager.ApplySelectedLevel();
            }

            AutoLoadPrefabsZeroNullFallback();
            EnsureRestBannerCreated();

            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
                waveRoutine = null;
            }
            ClearActiveEnemiesSilently();

            isRunning = true;
            currentWave = 1;
            pendingSpawnCount = 0;
            spawnSideToggle = 0;
            SetRestBannerVisible(false, string.Empty);

            GameUIManager.Instance?.ConfigureModeHud(GameMode.Endless);
            waveRoutine = StartCoroutine(RunEndlessWaveLoop(1));
        }

        public void StartNextWave()
        {
            if (levelManager == null) levelManager = LevelManager.Instance;
            if (activePlayer == null)
            {
                SetActivePlayer(ResolvePlayer(null));
            }
            if (activePlayer == null || activePlayer.IsDead)
            {
                return;
            }

            AutoLoadPrefabsZeroNullFallback();
            EnsureRestBannerCreated();

            isRunning = true;
            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
                waveRoutine = null;
            }
            SetRestBannerVisible(false, string.Empty);

            int nextWave = Mathf.Max(1, currentWave + 1);
            waveRoutine = StartCoroutine(RunEndlessWaveLoop(nextWave));
        }

        public void SetActivePlayer(BaseCharacter player)
        {
            if (activePlayer != null)
            {
                activePlayer.OnStateChanged -= HandlePlayerStateChanged;
            }
            activePlayer = player;
            if (activePlayer != null)
            {
                activePlayer.OnStateChanged += HandlePlayerStateChanged;
            }
        }

        public void StopEndless()
        {
            isRunning = false;
            pendingSpawnCount = 0;
            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
                waveRoutine = null;
            }
            SetRestBannerVisible(false, string.Empty);
        }

        public void ApplyEndlessScaling(EnemyBase enemy, int wave)
        {
            if (enemy == null) return;
            bool isCreep = !enemy.IsEliteOrBoss;
            ApplyEndlessScaling(enemy, wave, isCreep);
        }

        public void ApplyEndlessScaling(EnemyBase enemy, int wave, bool isCreep)
        {
            if (enemy == null) return;

            int w = Mathf.Max(1, wave);
            ComputeWaveScaling(
                w,
                out float hpMult,
                out float dmgMult,
                out float moveSpeedMult,
                out float staggerMult,
                out float damageReduction,
                out float attackSpeedMult);

            EnemyBlueprint blueprint = TryCreateEndlessBlueprint(enemy.gameObject, enemy.Data, null, w);
            if (blueprint != null)
            {
                if (blueprint.healthMultiplier > 0f) hpMult = blueprint.healthMultiplier;
                if (blueprint.damageMultiplier > 0f) dmgMult = blueprint.damageMultiplier;
                if (!string.IsNullOrWhiteSpace(blueprint.strategyId))
                {
                    enemy.SetStrategyById(blueprint.strategyId);
                }
            }

            bool isEnraged = isCreep && WaveCompositionCalculator.CalculateEnemyScaling(w, cachedBalanceConfig).EnragedChance > 0f && UnityEngine.Random.value < WaveCompositionCalculator.CalculateEnemyScaling(w, cachedBalanceConfig).EnragedChance;

            enemy.ApplyEndlessModifiers(
                hpMult,
                dmgMult,
                moveSpeedMult,
                staggerMult,
                damageReduction,
                attackSpeedMult,
                isEnraged);
        }

        public void ApplyEndlessScaling(BossBase boss, int wave)
        {
            if (boss == null) return;

            int w = Mathf.Max(1, wave);
            ComputeBossWaveScaling(
                w,
                out float bossHpMult,
                out float bossDmgMult,
                out float bossSpeedMult,
                out float bossStaggerMult,
                out float bossDrPct,
                out float bossAtkSpd);

            EnemyBlueprint blueprint = TryCreateEndlessBlueprint(boss.gameObject, null, boss.BossName, w);
            if (blueprint != null)
            {
                if (blueprint.healthMultiplier > 0f) bossHpMult = blueprint.healthMultiplier;
                if (blueprint.damageMultiplier > 0f) bossDmgMult = blueprint.damageMultiplier;
                if (!string.IsNullOrWhiteSpace(blueprint.strategyId))
                {
                    boss.SetStrategyById(blueprint.strategyId);
                }
            }

            boss.ApplyEndlessModifiers(
                bossHpMult,
                bossDmgMult,
                bossSpeedMult,
                bossStaggerMult,
                bossDrPct,
                bossAtkSpd,
                false);
        }

        private EnemyBlueprint TryCreateEndlessBlueprint(GameObject sourceObj, EnemyDataSO data, string fallbackName, int wave)
        {
            string canonicalId = null;
            if (data != null && !string.IsNullOrWhiteSpace(data.enemyName))
            {
                canonicalId = defaultEnemyFactory.ResolveCanonicalEnemyId(data.enemyName);
            }
            if (string.IsNullOrEmpty(canonicalId) && !string.IsNullOrWhiteSpace(fallbackName))
            {
                canonicalId = defaultEnemyFactory.ResolveCanonicalEnemyId(fallbackName);
            }
            if (string.IsNullOrEmpty(canonicalId) && sourceObj != null)
            {
                canonicalId = defaultEnemyFactory.ResolveCanonicalEnemyId(sourceObj.name);
            }

            if (string.IsNullOrEmpty(canonicalId))
            {
                return null;
            }

            IEnemyFactory factory = EnemyFactory;
            if (factory == null || !factory.CanCreate(canonicalId))
            {
                return null;
            }

            int worldIndex = ResolveWorldIndex(data, canonicalId);
            return factory.CreateEnemyBlueprint(canonicalId, worldIndex, Mathf.Max(1, wave), true);
        }

        private static int ResolveWorldIndex(EnemyDataSO data, string canonicalId)
        {
            if (data != null)
            {
                switch (data.worldTheme)
                {
                    case WorldType.World1_RuinedCity: return 1;
                    case WorldType.World2_Wasteland: return 2;
                    case WorldType.World3_NeoCity: return 3;
                }
            }

            if (!string.IsNullOrEmpty(canonicalId))
            {
                if (canonicalId.StartsWith("w2_", StringComparison.OrdinalIgnoreCase)) return 2;
                if (canonicalId.StartsWith("w3_", StringComparison.OrdinalIgnoreCase)) return 3;
            }

            return 1;
        }

        private void ComputeWaveScaling(
            int wave,
            out float hpMult,
            out float dmgMult,
            out float moveSpeedMult,
            out float staggerMult,
            out float damageReduction,
            out float attackSpeedMult)
        {
            WaveScalingResult s = WaveCompositionCalculator.CalculateEnemyScaling(wave, cachedBalanceConfig);
            hpMult = s.HealthMultiplier;
            dmgMult = s.DamageMultiplier;
            moveSpeedMult = s.MoveSpeedMultiplier;
            staggerMult = s.StaggerMultiplier;
            damageReduction = s.DamageReduction;
            attackSpeedMult = s.AttackSpeedMultiplier;
        }

        private void ComputeBossWaveScaling(
            int wave,
            out float bossHpMult,
            out float bossDmgMult,
            out float bossSpeedMult,
            out float bossStaggerMult,
            out float bossDrPct,
            out float bossAtkSpd)
        {
            WaveScalingResult s = WaveCompositionCalculator.CalculateBossScaling(wave, cachedBalanceConfig);
            bossHpMult = s.HealthMultiplier;
            bossDmgMult = s.DamageMultiplier;
            bossSpeedMult = s.MoveSpeedMultiplier;
            bossStaggerMult = s.StaggerMultiplier;
            bossDrPct = s.DamageReduction;
            bossAtkSpd = s.AttackSpeedMultiplier;
        }

        private BaseCharacter ResolvePlayer(BaseCharacter candidate)
        {
            if (candidate != null) return candidate;
            if (levelManager != null && levelManager.ActivePlayer != null) return levelManager.ActivePlayer;
            return FindAnyObjectByType<BaseCharacter>();
        }

        private void OnDestroy()
        {
            SetActivePlayer(null);
            StopEndless();
        }

        private void HandlePlayerStateChanged(CharacterState state)
        {
            if (state == CharacterState.Dead)
            {
                HandlePlayerDied();
            }
        }

        private void HandlePlayerDied()
        {
            StopEndless();

            GameUIManager ui = GameUIManager.Instance;
            if (ui != null)
            {
                ui.HideEndlessWaveBanner();
                ui.UnbindBoss();
            }
        }

        private void CalculateWaveComposition(int wave, out int creepCount, out int eliteCount, out int bossCount)
        {
            WaveCompositionResult comp = WaveCompositionCalculator.CalculateComposition(
                wave,
                hasBossPool: bosses.Count > 0,
                hasElitePool: true,
                hasCreepPool: true);
            creepCount = comp.CreepCount;
            eliteCount = comp.EliteCount;
            bossCount = comp.BossCount;
        }

        private IEnumerator RunEndlessWaveLoop(int startWave = 1)
        {
            currentWave = Mathf.Max(1, startWave);

            while (isRunning && activePlayer != null && !activePlayer.IsDead)
            {
                int wave = currentWave;
                CalculateWaveComposition(wave, out int creepCount, out int eliteCount, out int bossCount);

                bool hasAnyCreep = meleeCreeps.Count > 0 || rangedCreeps.Count > 0 || elites.Count > 0 || bosses.Count > 0;
                int actualCreepCount = hasAnyCreep ? creepCount : 0;
                int actualEliteCount = elites.Count > 0 ? eliteCount : 0;
                int totalExpected = actualCreepCount + actualEliteCount + bossCount;

                if (totalExpected <= 0)
                {
                    waveRoutine = null;
                    yield break;
                }

                pendingSpawnCount = totalExpected;
                spawnSideToggle = 0;
                UpdateRemainingHudCount();

                // 1. Spawn Creeps (split evenly between meleeCreeps & rangedCreeps, alternating camera edges)
                for (int i = 0; i < actualCreepCount; i++)
                {
                    if (!isRunning || activePlayer == null || activePlayer.IsDead)
                    {
                        waveRoutine = null;
                        yield break;
                    }

                    GameObject prefab = PickCreepPrefabForIndex(i);
                    if (prefab != null)
                    {
                        SpawnEndlessEnemy(prefab, wave, false);
                    }
                    else
                    {
                        pendingSpawnCount = Mathf.Max(0, pendingSpawnCount - 1);
                        UpdateRemainingHudCount();
                    }

                    if (spawnStaggerDelay > 0f) yield return new WaitForSeconds(spawnStaggerDelay);
                }

                // 2. Spawn Elites (Wave 3+, every 2 waves: 3, 5, 7, 9, ...)
                for (int i = 0; i < actualEliteCount; i++)
                {
                    if (!isRunning || activePlayer == null || activePlayer.IsDead)
                    {
                        waveRoutine = null;
                        yield break;
                    }

                    GameObject prefab = PickRandomValidPrefab(elites);
                    if (prefab != null)
                    {
                        SpawnEndlessEnemy(prefab, wave, false);
                    }
                    else
                    {
                        pendingSpawnCount = Mathf.Max(0, pendingSpawnCount - 1);
                        UpdateRemainingHudCount();
                    }

                    if (spawnStaggerDelay > 0f) yield return new WaitForSeconds(spawnStaggerDelay);
                }

                // 3. Spawn World Boss (Every 5 waves: 5, 10, 15, 20...)
                if (bossCount > 0)
                {
                    if (!isRunning || activePlayer == null || activePlayer.IsDead)
                    {
                        waveRoutine = null;
                        yield break;
                    }

                    GameObject bossPrefab = PickRandomValidPrefab(bosses);
                    if (bossPrefab != null)
                    {
                        SpawnEndlessEnemy(bossPrefab, wave, true);
                    }
                    else
                    {
                        pendingSpawnCount = Mathf.Max(0, pendingSpawnCount - 1);
                        UpdateRemainingHudCount();
                    }
                }

                pendingSpawnCount = 0;
                UpdateRemainingHudCount();

                // 4. Wait until all enemies of the current wave are defeated (polling/pruning every 0.15s)
                while (isRunning && activePlayer != null && !activePlayer.IsDead)
                {
                    PruneDestroyedTrackedEnemies();
                    if (activeEnemies.Count == 0)
                    {
                        break;
                    }
                    yield return new WaitForSeconds(0.15f);
                }

                if (!isRunning || activePlayer == null || activePlayer.IsDead)
                {
                    waveRoutine = null;
                    yield break;
                }

                // 5. Wave Clear Recovery: +10 HP & 3.0s rest countdown banner
                activePlayer.Heal(waveHealAmount);

                float remainingRest = Mathf.Max(0.1f, restDuration);
                while (remainingRest > 0f && isRunning && activePlayer != null && !activePlayer.IsDead)
                {
                    string countdownMsg = $"WAVE {wave} HOÀN THÀNH! +{Mathf.RoundToInt(waveHealAmount)} HP | WAVE {wave + 1} SAU {remainingRest:0.0}s";
                    SetRestBannerVisible(true, countdownMsg);
                    yield return null;
                    remainingRest -= Time.deltaTime;
                }
                SetRestBannerVisible(false, string.Empty);

                if (!isRunning || activePlayer == null || activePlayer.IsDead)
                {
                    waveRoutine = null;
                    yield break;
                }

                currentWave++;
            }

            waveRoutine = null;
        }

        private GameObject PickCreepPrefabForIndex(int index)
        {
            bool preferMelee = (index % 2) == 0;
            if (preferMelee)
            {
                if (meleeCreeps.Count > 0) return PickRandomValidPrefab(meleeCreeps);
                if (rangedCreeps.Count > 0) return PickRandomValidPrefab(rangedCreeps);
            }
            else
            {
                if (rangedCreeps.Count > 0) return PickRandomValidPrefab(rangedCreeps);
                if (meleeCreeps.Count > 0) return PickRandomValidPrefab(meleeCreeps);
            }

            if (elites.Count > 0) return PickRandomValidPrefab(elites);
            if (bosses.Count > 0) return PickRandomValidPrefab(bosses);
            return null;
        }

        private void SpawnEndlessEnemy(GameObject prefab, int wave, bool isBossSpawn)
        {
            if (prefab == null)
            {
                pendingSpawnCount = Mathf.Max(0, pendingSpawnCount - 1);
                UpdateRemainingHudCount();
                return;
            }

            bool isTetherBoss = isBossSpawn && prefab.GetComponent<TetherBossController>() != null;
            Vector3 spawnPos = ComputeCameraEdgeSpawnPosition(isTetherBoss);

            GameObject obj;
            if (useCombatObjectPool && !isBossSpawn && prefab.GetComponent<EnemyBase>() != null)
            {
                CombatObjectPool pool = CombatObjectPool.EnsureInstance();
                EnemyBase pooledEnemy = pool != null ? pool.SpawnEnemy(prefab, spawnPos, Quaternion.identity) : null;
                obj = pooledEnemy != null ? pooledEnemy.gameObject : Instantiate(prefab, spawnPos, Quaternion.identity);
            }
            else
            {
                obj = Instantiate(prefab, spawnPos, Quaternion.identity);
            }

            pendingSpawnCount = Mathf.Max(0, pendingSpawnCount - 1);

            BossBase boss = obj.GetComponent<BossBase>();
            if (boss != null)
            {
                ApplyEndlessScaling(boss, wave);
                activeEnemies.Add(obj);
                boss.OnBossDefeated += HandleBossDefeated;
                levelManager?.NotifyWorldBossSpawned(boss);
                UpdateRemainingHudCount();
                return;
            }

            EnemyBase enemy = obj.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                ApplyEndlessScaling(enemy, wave);
                activeEnemies.Add(obj);
                enemy.OnDied += HandleEnemyBaseDefeated;
                if (enemy.IsEliteOrBoss)
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
                UpdateRemainingHudCount();
                return;
            }

            BaseEnemy legacy = obj.GetComponent<BaseEnemy>();
            if (legacy != null)
            {
                activeEnemies.Add(obj);
                legacy.OnDeathEvent += HandleLegacyEnemyDefeated;
                if (obj.GetComponent<BossController>() != null)
                {
                    levelManager?.NotifyBossSpawned(legacy);
                }
                UpdateRemainingHudCount();
                return;
            }

            Destroy(obj);
            UpdateRemainingHudCount();
        }

        private Vector3 ComputeCameraEdgeSpawnPosition(bool isTetherBoss)
        {
            float clampMin = arenaMinX + 1.5f;
            float clampMax = isTetherBoss ? Mathf.Min(arenaMaxX - 1.5f, arenaMaxX - 6.0f) : arenaMaxX - 1.5f;
            if (clampMax < clampMin) clampMax = clampMin;

            Camera cam = Camera.main;
            float camX = cam != null
                ? cam.transform.position.x
                : (activePlayer != null ? activePlayer.transform.position.x : 0f);
            float camHalfW = cam != null ? cam.orthographicSize * cam.aspect : 8.88f;

            float sideSign = (spawnSideToggle++ % 2 == 0) ? -1f : 1f;
            float edgeOffset = camHalfW + UnityEngine.Random.Range(0.5f, 1.8f);
            float rawX = camX + sideSign * edgeOffset;

            float clampedX = Mathf.Clamp(rawX, clampMin, clampMax);
            if (activePlayer != null && Mathf.Abs(clampedX - activePlayer.transform.position.x) < 2.2f)
            {
                float oppositeX = Mathf.Clamp(camX - sideSign * edgeOffset, clampMin, clampMax);
                if (Mathf.Abs(oppositeX - activePlayer.transform.position.x) > Mathf.Abs(clampedX - activePlayer.transform.position.x))
                {
                    clampedX = oppositeX;
                }
            }

            return new Vector3(clampedX, groundY, 0f);
        }

        private void HandleEnemyBaseDefeated(EnemyBase enemy)
        {
            if (enemy != null)
            {
                enemy.OnDied -= HandleEnemyBaseDefeated;
                activeEnemies.Remove(enemy.gameObject);
            }
            UpdateRemainingHudCount();
        }

        private void HandleBossDefeated(BossBase boss)
        {
            if (boss != null)
            {
                boss.OnBossDefeated -= HandleBossDefeated;
                activeEnemies.Remove(boss.gameObject);
            }
            UpdateRemainingHudCount();
        }

        private void HandleLegacyEnemyDefeated(BaseEnemy enemy)
        {
            if (enemy != null)
            {
                enemy.OnDeathEvent -= HandleLegacyEnemyDefeated;
                activeEnemies.Remove(enemy.gameObject);
            }
            UpdateRemainingHudCount();
        }

        private void UpdateRemainingHudCount()
        {
            if (!isRunning || (activePlayer != null && activePlayer.IsDead)) return;
            int remaining = activeEnemies.Count + Mathf.Max(0, pendingSpawnCount);
            SyncHudWaveAndCount(currentWave, remaining);
        }

        private void PruneDestroyedTrackedEnemies()
        {
            int removed = 0;
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                GameObject go = activeEnemies[i];
                if (go == null)
                {
                    activeEnemies.RemoveAt(i);
                    removed++;
                    continue;
                }

                EnemyBase eb = go.GetComponent<EnemyBase>();
                if (eb != null && (eb.IsDead || !go.activeInHierarchy))
                {
                    eb.OnDied -= HandleEnemyBaseDefeated;
                    activeEnemies.RemoveAt(i);
                    removed++;
                    continue;
                }

                BossBase bb = go.GetComponent<BossBase>();
                if (bb != null && (bb.IsDead || !go.activeInHierarchy))
                {
                    bb.OnBossDefeated -= HandleBossDefeated;
                    activeEnemies.RemoveAt(i);
                    removed++;
                    continue;
                }

                BaseEnemy be = go.GetComponent<BaseEnemy>();
                if (be != null && (be.IsDead || !go.activeInHierarchy))
                {
                    be.OnDeathEvent -= HandleLegacyEnemyDefeated;
                    activeEnemies.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                UpdateRemainingHudCount();
            }
        }

        private void ClearActiveEnemiesSilently()
        {
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                GameObject go = activeEnemies[i];
                if (go == null) continue;

                EnemyBase eb = go.GetComponent<EnemyBase>();
                if (eb != null)
                {
                    eb.OnDied -= HandleEnemyBaseDefeated;
                    eb.DestroySilentlyWithoutReward();
                    continue;
                }

                BossBase bb = go.GetComponent<BossBase>();
                if (bb != null)
                {
                    bb.OnBossDefeated -= HandleBossDefeated;
                    bb.DestroySilentlyWithoutReward();
                    continue;
                }

                BaseEnemy be = go.GetComponent<BaseEnemy>();
                if (be != null)
                {
                    be.OnDeathEvent -= HandleLegacyEnemyDefeated;
                    be.DestroySilentlyWithoutReward();
                    continue;
                }

                Destroy(go);
            }

            activeEnemies.Clear();
            pendingSpawnCount = 0;
        }

        private void SyncHudWaveAndCount(int wave, int remaining)
        {
            levelManager?.NotifyWaveChanged(wave, wave);
            levelManager?.NotifyEnemiesRemainingChanged(remaining);
            GameUIManager.Instance?.UpdateEndlessWave(wave, remaining);
        }

        public void AutoLoadPrefabsZeroNullFallback()
        {
            // 1. Merge & classify any valid prefabs from categorized lists + legacy serialized arrays
            List<GameObject> snapshot = new List<GameObject>();
            CollectNonNullUnique(snapshot, meleeCreeps);
            CollectNonNullUnique(snapshot, rangedCreeps);
            CollectNonNullUnique(snapshot, elites);
            CollectNonNullUnique(snapshot, bosses);
            if (!hasExplicitCategorizedConfig)
            {
                CollectNonNullUnique(snapshot, creepPrefabs);
                CollectNonNullUnique(snapshot, elitePrefabs);
                CollectNonNullUnique(snapshot, bossPrefabs);
            }

            meleeCreeps.Clear();
            rangedCreeps.Clear();
            elites.Clear();
            bosses.Clear();

            for (int i = 0; i < snapshot.Count; i++)
            {
                ClassifyAndAddCategorizedPrefab(snapshot[i], meleeCreeps, rangedCreeps, elites, bosses);
            }

            // 2. Supplement from serialized EndlessEnemyCatalogSO or Resources catalog (works in Editor and Builds)
            if (AnyCategoryEmpty())
            {
                EndlessEnemyCatalogSO resolvedCatalog = enemyCatalog != null
                    ? enemyCatalog
                    : (!string.IsNullOrEmpty(catalogResourcePath)
                        ? Resources.Load<EndlessEnemyCatalogSO>(catalogResourcePath)
                        : null);
                if (resolvedCatalog != null && resolvedCatalog.HasAnyPrefabs)
                {
                    if (meleeCreeps.Count == 0) AddUniqueValid(meleeCreeps, resolvedCatalog.meleeCreeps);
                    if (rangedCreeps.Count == 0) AddUniqueValid(rangedCreeps, resolvedCatalog.rangedCreeps);
                    if (elites.Count == 0) AddUniqueValid(elites, resolvedCatalog.elites);
                    if (bosses.Count == 0) AddUniqueValid(bosses, resolvedCatalog.bosses);
                }
            }

            // 3. Scan LevelManager.AllStages & StageManager.AllLevels across all 12 levels (3 Worlds)
            if (AnyCategoryEmpty() || !hasExplicitCategorizedConfig)
            {
                List<GameObject> harvestedMelee = new List<GameObject>();
                List<GameObject> harvestedRanged = new List<GameObject>();
                List<GameObject> harvestedElites = new List<GameObject>();
                List<GameObject> harvestedBosses = new List<GameObject>();

                IEnumerable<StageDataSO> stageSource = levelManager != null ? levelManager.AllStages : null;
                if (stageSource != null)
                {
                    foreach (StageDataSO stage in stageSource)
                    {
                        CollectPrefabsFromStage(stage, harvestedMelee, harvestedRanged, harvestedElites, harvestedBosses);
                    }
                }

                StageManager stageManager = StageManager.Instance != null
                    ? StageManager.Instance
                    : (GetComponent<StageManager>() ?? FindAnyObjectByType<StageManager>());
                if (stageManager != null && stageManager.AllLevels != null)
                {
                    foreach (LevelDataSO lvl in stageManager.AllLevels)
                    {
                        CollectPrefabsFromLevel(lvl, harvestedMelee, harvestedRanged, harvestedElites, harvestedBosses);
                    }
                }

                if (meleeCreeps.Count == 0) AddUniqueValid(meleeCreeps, harvestedMelee);
                if (rangedCreeps.Count == 0) AddUniqueValid(rangedCreeps, harvestedRanged);
                if (elites.Count == 0) AddUniqueValid(elites, harvestedElites);
                if (bosses.Count == 0) AddUniqueValid(bosses, harvestedBosses);
            }

            // 4. Load from Resources/EndlessPrefabs if present at runtime
            if (AnyCategoryEmpty())
            {
                GameObject[] resourcePrefabs = Resources.LoadAll<GameObject>("EndlessPrefabs");
                if (resourcePrefabs != null && resourcePrefabs.Length > 0)
                {
                    List<GameObject> resMelee = new List<GameObject>();
                    List<GameObject> resRanged = new List<GameObject>();
                    List<GameObject> resElites = new List<GameObject>();
                    List<GameObject> resBosses = new List<GameObject>();

                    for (int i = 0; i < resourcePrefabs.Length; i++)
                    {
                        ClassifyAndAddCategorizedPrefab(resourcePrefabs[i], resMelee, resRanged, resElites, resBosses);
                    }

                    if (meleeCreeps.Count == 0) AddUniqueValid(meleeCreeps, resMelee);
                    if (rangedCreeps.Count == 0) AddUniqueValid(rangedCreeps, resRanged);
                    if (elites.Count == 0) AddUniqueValid(elites, resElites);
                    if (bosses.Count == 0) AddUniqueValid(bosses, resBosses);
                }
            }

            // 5. Final creep pool cross-fallback so wave spawning never stalls even in stripped test scenes
            if (meleeCreeps.Count == 0 && rangedCreeps.Count > 0) AddUniqueValid(meleeCreeps, rangedCreeps);
            if (rangedCreeps.Count == 0 && meleeCreeps.Count > 0) AddUniqueValid(rangedCreeps, meleeCreeps);

            SyncLegacyArraysFromCategorized();
        }

        private bool AnyCategoryEmpty()
        {
            return meleeCreeps.Count == 0 || rangedCreeps.Count == 0 || elites.Count == 0 || bosses.Count == 0;
        }

        private void SyncLegacyArraysFromCategorized()
        {
            List<GameObject> combinedCreeps = new List<GameObject>(meleeCreeps.Count + rangedCreeps.Count);
            AddUniqueValid(combinedCreeps, meleeCreeps);
            AddUniqueValid(combinedCreeps, rangedCreeps);
            creepPrefabs = combinedCreeps.ToArray();
            elitePrefabs = elites.ToArray();
            bossPrefabs = bosses.ToArray();
        }

        private static void CollectNonNullUnique(List<GameObject> target, IList<GameObject> source)
        {
            if (source == null) return;
            for (int i = 0; i < source.Count; i++)
            {
                GameObject item = source[i];
                if (item != null && !target.Contains(item))
                {
                    target.Add(item);
                }
            }
        }

        private static void AddUniqueValid(List<GameObject> target, IList<GameObject> source)
        {
            if (source == null) return;
            for (int i = 0; i < source.Count; i++)
            {
                GameObject item = source[i];
                if (item != null && !target.Contains(item))
                {
                    target.Add(item);
                }
            }
        }

        private static GameObject[] FilterValidPrefabs(GameObject[] source)
        {
            if (source == null || source.Length == 0) return Array.Empty<GameObject>();

            List<GameObject> valid = new List<GameObject>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null && !valid.Contains(source[i]))
                {
                    valid.Add(source[i]);
                }
            }
            return valid.ToArray();
        }

        private static void CollectPrefabsFromStage(
            StageDataSO stage,
            List<GameObject> melee,
            List<GameObject> ranged,
            List<GameObject> eliteList,
            List<GameObject> bossList)
        {
            if (stage == null) return;

            if (stage.waves != null)
            {
                foreach (WaveData w in stage.waves)
                {
                    if (w?.enemies == null) continue;
                    foreach (EnemySpawnEntry e in w.enemies)
                    {
                        ClassifyAndAddCategorizedPrefab(e.enemyPrefab, melee, ranged, eliteList, bossList);
                    }
                }
            }

            ClassifyAndAddCategorizedPrefab(stage.bossPrefab, melee, ranged, eliteList, bossList);
        }

        private static void CollectPrefabsFromLevel(
            LevelDataSO levelData,
            List<GameObject> melee,
            List<GameObject> ranged,
            List<GameObject> eliteList,
            List<GameObject> bossList)
        {
            if (levelData == null) return;

            CollectPrefabsFromStage(levelData, melee, ranged, eliteList, bossList);
            ClassifyAndAddCategorizedPrefab(levelData.supportMeleeCreepPrefab, melee, ranged, eliteList, bossList);
            ClassifyAndAddCategorizedPrefab(levelData.supportRangedCreepPrefab, melee, ranged, eliteList, bossList);
            ClassifyAndAddCategorizedPrefab(levelData.supportElitePrefab, melee, ranged, eliteList, bossList);
        }

        private static void ClassifyAndAddCategorizedPrefab(
            GameObject prefab,
            List<GameObject> melee,
            List<GameObject> ranged,
            List<GameObject> eliteList,
            List<GameObject> bossList)
        {
            if (prefab == null) return;

            if (prefab.GetComponent<BossBase>() != null || prefab.GetComponent<BossController>() != null)
            {
                if (!bossList.Contains(prefab)) bossList.Add(prefab);
                return;
            }

            EnemyBase eb = prefab.GetComponent<EnemyBase>();
            if (eb != null)
            {
                if (eb.IsEliteOrBoss)
                {
                    if (!eliteList.Contains(prefab)) eliteList.Add(prefab);
                    return;
                }

                if (prefab.GetComponent<RangedCreep>() != null || (eb.Data != null && eb.Data.attackRange >= 3.5f))
                {
                    if (!ranged.Contains(prefab)) ranged.Add(prefab);
                    return;
                }

                if (!melee.Contains(prefab)) melee.Add(prefab);
                return;
            }

            BaseEnemy be = prefab.GetComponent<BaseEnemy>();
            if (be != null)
            {
                if (prefab.GetComponent<EliteBruiserEnemy>() != null)
                {
                    if (!eliteList.Contains(prefab)) eliteList.Add(prefab);
                }
                else if (prefab.GetComponent<RangerEnemy>() != null)
                {
                    if (!ranged.Contains(prefab)) ranged.Add(prefab);
                }
                else
                {
                    if (!melee.Contains(prefab)) melee.Add(prefab);
                }
            }
        }

        private static GameObject PickRandomValidPrefab(IList<GameObject> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            int attempts = pool.Count * 2;
            for (int i = 0; i < attempts; i++)
            {
                GameObject pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                if (pick != null) return pick;
            }
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null) return pool[i];
            }
            return null;
        }

        private void EnsureRestBannerCreated()
        {
            if (restBannerObj != null) return;
            Canvas canvas = GameUIManager.Instance != null
                ? GameUIManager.Instance.GetComponentInParent<Canvas>()
                : FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            restBannerObj = new GameObject("EndlessRestCountdownBanner", typeof(RectTransform), typeof(Image));
            restBannerObj.transform.SetParent(canvas.transform, false);
            Transform victoryTr = canvas.transform.Find("VictoryPanel");
            if (victoryTr != null)
            {
                restBannerObj.transform.SetSiblingIndex(Mathf.Max(0, victoryTr.GetSiblingIndex()));
            }
            RectTransform rt = restBannerObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -92f);
            rt.sizeDelta = new Vector2(620f, 38f);

            Image bg = restBannerObj.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.14f, 0.22f, 0.88f);
            bg.raycastTarget = false;
            GlassHudTheme.ApplySprite(bg, "StatusPill_Emerald.png", default, true, false);

            GameObject txtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(restBannerObj.transform, false);
            RectTransform trt = txtObj.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(18f, 2f);
            trt.offsetMax = new Vector2(-18f, -2f);

            restBannerText = txtObj.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI[] existingTexts = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < existingTexts.Length; i++)
            {
                if (existingTexts[i] != null && existingTexts[i].font != null)
                {
                    restBannerText.font = existingTexts[i].font;
                    break;
                }
            }
            restBannerText.alignment = TextAlignmentOptions.Center;
            restBannerText.raycastTarget = false;
            GlassHudTheme.StyleTmpText(restBannerText, 17f, new Color(0.55f, 1f, 0.72f, 1f), true, true);

            restBannerObj.SetActive(false);
        }

        private void SetRestBannerVisible(bool visible, string message)
        {
            if (restBannerObj != null)
            {
                restBannerObj.SetActive(visible);
            }
            if (restBannerText != null)
            {
                restBannerText.text = message;
            }
        }
    }
}
