using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Enemies;

namespace StickmanBeatEmUp.Level
{
    /// <summary>
    /// Quản lý triệu hồi lính hỗ trợ (1 Melee + 1 Ranged mỗi 18s khi dưới 2 minion)
    /// và 1 Elite (35 HP) khi máu Boss xuống dưới 50% giúp người chơi luôn có nguồn tích Nộ trong trận Boss.
    /// </summary>
    public class BossMinionSpawner : MonoBehaviour
    {
        [Header("Boss Reinforcement Config")]
        [SerializeField] private float spawnInterval = 18f;
        [SerializeField] private int maxActiveMinions = 2;
        [SerializeField] private float halfHpEliteHealth = 35f;

        private LevelManager levelManager;
        private LevelDataSO activeLevelData;
        private BossBase activeBoss;
        private float nextSpawnTime;
        private bool hasSpawnedHalfHpElite;

        private readonly List<EnemyBase> spawnedMinions = new List<EnemyBase>();

        private void Awake()
        {
            levelManager = GetComponent<LevelManager>();
        }

        private void OnEnable()
        {
            if (levelManager == null) levelManager = GetComponent<LevelManager>();
            if (levelManager != null)
            {
                levelManager.OnWorldBossSpawned += HandleBossSpawned;
                levelManager.OnStageCompleted += HandleStageEnded;
                levelManager.OnStageFailed += HandleStageEnded;
            }
        }

        private void OnDisable()
        {
            if (levelManager != null)
            {
                levelManager.OnWorldBossSpawned -= HandleBossSpawned;
                levelManager.OnStageCompleted -= HandleStageEnded;
                levelManager.OnStageFailed -= HandleStageEnded;
            }
            if (activeBoss != null)
            {
                activeBoss.OnBossDefeated -= HandleBossDefeated;
            }
        }

        private void HandleStageEnded()
        {
            if (activeBoss != null)
            {
                activeBoss.OnBossDefeated -= HandleBossDefeated;
                activeBoss = null;
            }
            hasSpawnedHalfHpElite = false;
            CleanupMinions();
        }

        private void HandleBossSpawned(BossBase boss)
        {
            activeLevelData = levelManager != null ? levelManager.CurrentStageData as LevelDataSO : null;
            if (activeLevelData == null && StageManager.Instance != null)
            {
                activeLevelData = StageManager.Instance.ActiveLevel;
            }

            if (activeBoss != null)
            {
                activeBoss.OnBossDefeated -= HandleBossDefeated;
            }

            activeBoss = boss;
            hasSpawnedHalfHpElite = false;

            if (activeBoss != null)
            {
                activeBoss.OnBossDefeated += HandleBossDefeated;
                nextSpawnTime = Time.time + spawnInterval;
            }
        }

        private void HandleBossDefeated(BossBase boss)
        {
            if (activeBoss != null)
            {
                activeBoss.OnBossDefeated -= HandleBossDefeated;
                activeBoss = null;
            }
            CleanupMinions();
        }

        private void Update()
        {
            if (activeBoss == null || activeBoss.IsDead || activeLevelData == null) return;

            spawnedMinions.RemoveAll(m => m == null || m.IsDead);

            // Spawn 1 World Elite (35 HP) một lần duy nhất khi máu Boss < 50%
            if (!hasSpawnedHalfHpElite && activeBoss.MaxHealth > 0f && activeBoss.CurrentHealth < activeBoss.MaxHealth * 0.5f)
            {
                hasSpawnedHalfHpElite = true;
                SpawnReinforcementElite();
            }

            // Cứ mỗi 18s, nếu số minion đang sống < 2 -> Spawn 1 Melee + 1 Ranged Creep
            if (Time.time >= nextSpawnTime)
            {
                nextSpawnTime = Time.time + spawnInterval;
                if (spawnedMinions.Count < maxActiveMinions)
                {
                    SpawnCreepPair();
                }
            }
        }

        private void SpawnCreepPair()
        {
            float baseX = Camera.main != null ? Camera.main.transform.position.x : activeBoss.transform.position.x;

            if (activeLevelData.supportMeleeCreepPrefab != null)
            {
                SpawnMinion(activeLevelData.supportMeleeCreepPrefab, baseX - 4.8f, 0f);
            }
            if (activeLevelData.supportRangedCreepPrefab != null)
            {
                SpawnMinion(activeLevelData.supportRangedCreepPrefab, baseX + 5.2f, 0f);
            }
        }

        private void SpawnReinforcementElite()
        {
            if (activeLevelData.supportElitePrefab == null) return;

            float baseX = Camera.main != null ? Camera.main.transform.position.x : activeBoss.transform.position.x;
            EnemyBase elite = SpawnMinion(activeLevelData.supportElitePrefab, baseX + 4.2f, halfHpEliteHealth);
            if (elite != null && levelManager != null)
            {
                levelManager.NotifySupportEliteSpawned(elite);
            }
        }

        private EnemyBase SpawnMinion(GameObject prefab, float targetX, float hpOverride)
        {
            if (prefab == null) return null;

            float minX = -20f;
            float maxX = 40f;
            if (activeLevelData != null)
            {
                minX = Mathf.Max(-20f, activeLevelData.lockedCameraMinX - 6f);
                maxX = Mathf.Min(40f, activeLevelData.lockedCameraMaxX + 6f);
            }

            Vector3 spawnPos = new Vector3(Mathf.Clamp(targetX, minX, maxX), EnemyBase.GROUND_Y, 0f);
            GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);
            EnemyBase enemy = go.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                if (hpOverride > 0f)
                {
                    enemy.SetMaxHealthOverride(hpOverride);
                }
                spawnedMinions.Add(enemy);
            }
            return enemy;
        }

        private void CleanupMinions()
        {
            for (int i = 0; i < spawnedMinions.Count; i++)
            {
                if (spawnedMinions[i] != null)
                {
                    Destroy(spawnedMinions[i].gameObject);
                }
            }
            spawnedMinions.Clear();
        }
    }
}
