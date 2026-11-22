using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Enemies;

namespace StickmanBeatEmUp.Level
{
    /// <summary>
    /// RageTestSpawner: Sandbox tool for testing player rage system, combos, and transformations.
    /// Provides hotkeys:
    /// - [R]: Instantly fill 30/30 Rage for the active player.
    /// - [T]: Manually spawn a training wave (3 Melee Creeps + 1 Ranged Creep + 1 Elite Enemy).
    /// - [Y]: Wipe all active enemies and enemy projectiles from the arena.
    /// Supports automatic respawn of training dummies (3 Melee + 1 Ranged) when arena is cleared.
    /// </summary>
    public class RageTestSpawner : MonoBehaviour
    {
        [Header("Sandbox Settings")]
        [SerializeField] private bool enableSandbox = false;
        [SerializeField] private float meleeDummyHP = 0f;
        [SerializeField] private float respawnDelay = 2.0f;
        [SerializeField] private Transform sandboxContainer;

        [Header("Enemy Prefab Pools")]
        [SerializeField] private GameObject[] meleeCreepPrefabs;
        [SerializeField] private GameObject[] rangedCreepPrefabs;
        [SerializeField] private GameObject[] elitePrefabs;

        private Coroutine respawnCoroutine;
        private bool manualClearActive = false;
        private bool hasInitializedPack = false;
        private int eliteCycleIndex = 0;

        private void Awake()
        {
            if (enableSandbox)
            {
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.SetSandboxMode(true);
                }
                ApplyDummyHPToPreplacedMelee();
            }
        }

        private void Start()
        {
            if (enableSandbox)
            {
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.SetSandboxMode(true);
                }
                ApplyDummyHPToPreplacedMelee();
            }
        }

        private void ApplyDummyHPToPreplacedMelee()
        {
            if (meleeDummyHP <= 0f) return;

            MeleeCreep[] preplaced = FindObjectsByType<MeleeCreep>();
            if (preplaced != null)
            {
                for (int i = 0; i < preplaced.Length; i++)
                {
                    if (preplaced[i] != null)
                    {
                        preplaced[i].SetMaxHealthOverride(meleeDummyHP);
                    }
                }
            }
        }

        private void Update()
        {
            HandleInput();

            if (!enableSandbox) return;

            if (!hasInitializedPack)
            {
                if (Time.timeSinceLevelLoad > 0.2f)
                {
                    hasInitializedPack = true;
                }
                return;
            }

            if (!manualClearActive && respawnCoroutine == null && CountAliveEnemies() == 0)
            {
                respawnCoroutine = StartCoroutine(AutoRespawnRoutine());
            }
        }

        private void HandleInput()
        {
            bool rDown = false;
            bool tDown = false;
            bool yDown = false;
            bool bDown = false;
            bool nDown = false;

#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                rDown = kb.rKey.wasPressedThisFrame;
                tDown = kb.tKey.wasPressedThisFrame;
                yDown = kb.yKey.wasPressedThisFrame;
                bDown = kb.bKey.wasPressedThisFrame;
                nDown = kb.nKey.wasPressedThisFrame;
            }
#else
            rDown = Input.GetKeyDown(KeyCode.R);
            tDown = Input.GetKeyDown(KeyCode.T);
            yDown = Input.GetKeyDown(KeyCode.Y);
            bDown = Input.GetKeyDown(KeyCode.B);
            nDown = Input.GetKeyDown(KeyCode.N);
#endif

            if (rDown)
            {
                BaseCharacter player = LevelManager.Instance != null ? LevelManager.Instance.ActivePlayer : null;
                if (player == null) player = FindAnyObjectByType<BaseCharacter>();
                if (player != null && player.RageManager != null)
                {
                    player.RageManager.FillMaxRage();
                    Debug.Log("[RageSandbox] [R] Full Rage 30/30");
                }
            }

            if (tDown)
            {
                SpawnManualWave();
            }

            if (yDown)
            {
                ClearAllEnemies();
            }

            if (bDown && LevelManager.Instance != null)
            {
                LevelManager.Instance.LoadPreviousStage();
            }

            if (nDown && LevelManager.Instance != null)
            {
                LevelManager.Instance.LoadNextStage();
            }
        }

        private void SpawnManualWave()
        {
            if (respawnCoroutine != null)
            {
                StopCoroutine(respawnCoroutine);
                respawnCoroutine = null;
            }
            manualClearActive = false;

            BaseCharacter player = LevelManager.Instance != null ? LevelManager.Instance.ActivePlayer : null;
            if (player == null) player = FindAnyObjectByType<BaseCharacter>();

            float playerX = player != null ? player.transform.position.x : 0f;
            float facing = player != null ? (player.transform.localScale.x >= 0f ? 1f : -1f) : 1f;

            float[] meleeOffsets = new float[] { 2.8f, 4.0f, 5.2f };
            if (meleeCreepPrefabs != null && meleeCreepPrefabs.Length > 0)
            {
                for (int i = 0; i < meleeOffsets.Length; i++)
                {
                    float spawnX = Mathf.Clamp(playerX + facing * meleeOffsets[i], -24f, 44f);
                    Vector3 pos = new Vector3(spawnX, StickmanBeatEmUp.Enemies.EnemyBase.GROUND_Y, 0f);
                    GameObject prefab = meleeCreepPrefabs[i % meleeCreepPrefabs.Length];
                    if (prefab != null)
                    {
                        GameObject creep = Instantiate(prefab, pos, Quaternion.identity, sandboxContainer);
                        if (meleeDummyHP > 0f)
                        {
                            creep.GetComponent<EnemyBase>()?.SetMaxHealthOverride(meleeDummyHP);
                        }
                    }
                }
            }

            if (rangedCreepPrefabs != null && rangedCreepPrefabs.Length > 0)
            {
                float rangedX = Mathf.Clamp(playerX + facing * 7.8f, -24f, 44f);
                Vector3 rangedPos = new Vector3(rangedX, StickmanBeatEmUp.Enemies.EnemyBase.GROUND_Y, 0f);
                GameObject rangedPrefab = rangedCreepPrefabs[eliteCycleIndex % rangedCreepPrefabs.Length];
                if (rangedPrefab != null)
                {
                    Instantiate(rangedPrefab, rangedPos, Quaternion.identity, sandboxContainer);
                }
            }

            if (elitePrefabs != null && elitePrefabs.Length > 0)
            {
                float eliteX = Mathf.Clamp(playerX + facing * 6.4f, -24f, 44f);
                Vector3 elitePos = new Vector3(eliteX, StickmanBeatEmUp.Enemies.EnemyBase.GROUND_Y, 0f);
                GameObject elitePrefab = elitePrefabs[eliteCycleIndex % elitePrefabs.Length];
                if (elitePrefab != null)
                {
                    GameObject eliteObj = Instantiate(elitePrefab, elitePos, Quaternion.identity, sandboxContainer);
                    EnemyBase eliteComp = eliteObj.GetComponent<EnemyBase>();
                    if (eliteComp != null)
                    {
                        FindAnyObjectByType<StickmanBeatEmUp.UI.GameUIManager>()?.BindElite(eliteComp);
                    }
                }
            }

            eliteCycleIndex++;
        }

        private void ClearAllEnemies()
        {
            if (respawnCoroutine != null)
            {
                StopCoroutine(respawnCoroutine);
                respawnCoroutine = null;
            }
            manualClearActive = true;

            if (!enableSandbox)
            {
                DamageData lethal = new DamageData(99999f, Vector2.zero, AttackType.Combo_DashI, true, gameObject);

                CrystalCore[] campaignCores = FindObjectsByType<CrystalCore>();
                for (int i = 0; i < campaignCores.Length; i++)
                {
                    if (campaignCores[i] != null && !campaignCores[i].IsDead) campaignCores[i].TakeDamage(lethal);
                }

                BossPartHitbox[] parts = FindObjectsByType<BossPartHitbox>();
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] != null && !parts[i].IsDead) parts[i].TakeDamage(lethal);
                }

                BossBase[] campaignBosses = FindObjectsByType<BossBase>();
                for (int i = 0; i < campaignBosses.Length; i++)
                {
                    if (campaignBosses[i] != null && !campaignBosses[i].IsDead)
                    {
                        campaignBosses[i].TakeDamage(lethal);
                        if (campaignBosses[i] != null && !campaignBosses[i].IsDead) campaignBosses[i].TakeDamage(lethal);
                    }
                }

                EnemyBase[] campaignEnemies = FindObjectsByType<EnemyBase>();
                for (int i = 0; i < campaignEnemies.Length; i++)
                {
                    if (campaignEnemies[i] != null && !campaignEnemies[i].IsDead) campaignEnemies[i].TakeDamage(lethal);
                }

                BaseEnemy[] campaignBase = FindObjectsByType<BaseEnemy>();
                for (int i = 0; i < campaignBase.Length; i++)
                {
                    if (campaignBase[i] != null && !campaignBase[i].IsDead) campaignBase[i].TakeDamage(lethal);
                }
            }
            else
            {
                EnemyBase[] enemyBases = FindObjectsByType<EnemyBase>();
                for (int i = 0; i < enemyBases.Length; i++)
                {
                    if (enemyBases[i] != null) Destroy(enemyBases[i].gameObject);
                }

                BossBase[] bossBases = FindObjectsByType<BossBase>();
                for (int i = 0; i < bossBases.Length; i++)
                {
                    if (bossBases[i] != null) Destroy(bossBases[i].gameObject);
                }

                BaseEnemy[] baseEnemies = FindObjectsByType<BaseEnemy>();
                for (int i = 0; i < baseEnemies.Length; i++)
                {
                    if (baseEnemies[i] != null) Destroy(baseEnemies[i].gameObject);
                }

                CrystalCore[] cores = FindObjectsByType<CrystalCore>();
                for (int i = 0; i < cores.Length; i++)
                {
                    if (cores[i] != null) Destroy(cores[i].gameObject);
                }

                TetherCableHitbox[] cables = FindObjectsByType<TetherCableHitbox>();
                for (int i = 0; i < cables.Length; i++)
                {
                    if (cables[i] != null) Destroy(cables[i].gameObject);
                }
            }

            Projectile[] projs = FindObjectsByType<Projectile>();
            int playerLayer = LayerMask.NameToLayer("Player");
            for (int i = 0; i < projs.Length; i++)
            {
                Projectile p = projs[i];
                if (p == null) continue;

                bool isEnemy = false;
                if (playerLayer >= 0 && ((p.TargetLayer.value & (1 << playerLayer)) != 0))
                {
                    isEnemy = true;
                }
                else if (p.Attacker != null && (p.Attacker.GetComponent<EnemyBase>() != null || p.Attacker.GetComponent<BaseEnemy>() != null || p.Attacker.GetComponent<BossBase>() != null))
                {
                    isEnemy = true;
                }

                if (isEnemy)
                {
                    Destroy(p.gameObject);
                }
            }
        }

        private IEnumerator AutoRespawnRoutine()
        {
            yield return new WaitForSeconds(respawnDelay);

            if (!manualClearActive && CountAliveEnemies() == 0)
            {
                SpawnAutoPack();
            }

            respawnCoroutine = null;
        }

        private void SpawnAutoPack()
        {
            BaseCharacter player = LevelManager.Instance != null ? LevelManager.Instance.ActivePlayer : null;
            if (player == null) player = FindAnyObjectByType<BaseCharacter>();

            float playerX = player != null ? player.transform.position.x : 0f;
            float facing = player != null ? (player.transform.localScale.x >= 0f ? 1f : -1f) : 1f;

            // Spawn 3 MeleeCreep (balanced EnemyDataSO HP unless overridden)
            float[] meleeOffsets = new float[] { 2.8f, 4.2f, 5.6f };
            if (meleeCreepPrefabs != null && meleeCreepPrefabs.Length > 0)
            {
                for (int i = 0; i < meleeOffsets.Length; i++)
                {
                    float spawnX = Mathf.Clamp(playerX + facing * meleeOffsets[i], -24f, 44f);
                    Vector3 pos = new Vector3(spawnX, StickmanBeatEmUp.Enemies.EnemyBase.GROUND_Y, 0f);
                    GameObject prefab = meleeCreepPrefabs[i % meleeCreepPrefabs.Length];
                    if (prefab != null)
                    {
                        GameObject creep = Instantiate(prefab, pos, Quaternion.identity, sandboxContainer);
                        if (meleeDummyHP > 0f)
                        {
                            creep.GetComponent<EnemyBase>()?.SetMaxHealthOverride(meleeDummyHP);
                        }
                    }
                }
            }

            // Spawn 1 RangedCreep
            float[] rangeOffsets = new float[] { 7.8f };
            if (rangedCreepPrefabs != null && rangedCreepPrefabs.Length > 0)
            {
                for (int i = 0; i < rangeOffsets.Length; i++)
                {
                    float spawnX = Mathf.Clamp(playerX + facing * rangeOffsets[i], -24f, 44f);
                    Vector3 pos = new Vector3(spawnX, StickmanBeatEmUp.Enemies.EnemyBase.GROUND_Y, 0f);
                    GameObject prefab = rangedCreepPrefabs[i % rangedCreepPrefabs.Length];
                    if (prefab != null)
                    {
                        Instantiate(prefab, pos, Quaternion.identity, sandboxContainer);
                    }
                }
            }
        }

        private int CountAliveEnemies()
        {
            int count = 0;
            EnemyBase[] eb = FindObjectsByType<EnemyBase>();
            for (int i = 0; i < eb.Length; i++)
            {
                if (eb[i] != null && !eb[i].IsDead) count++;
            }

            BossBase[] bb = FindObjectsByType<BossBase>();
            for (int i = 0; i < bb.Length; i++)
            {
                if (bb[i] != null && !bb[i].IsDead) count++;
            }

            BaseEnemy[] be = FindObjectsByType<BaseEnemy>();
            for (int i = 0; i < be.Length; i++)
            {
                if (be[i] != null && !be[i].IsDead) count++;
            }

            return count;
        }
    }
}
