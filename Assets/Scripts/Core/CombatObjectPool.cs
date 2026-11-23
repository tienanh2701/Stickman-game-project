using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Combat;
using StickmanBeatEmUp.Enemies;

namespace StickmanBeatEmUp.Core
{
    /// <summary>
    /// Centralized MonoBehaviour Object Pool coordinator for Projectile, BeamAttack, Explosion,
    /// and EnemyBase instances keyed by Prefab reference (zero obsolete GetInstanceID calls).
    /// Automatically falls back to Instantiate/Destroy for non-pooled objects.
    /// </summary>
    public class CombatObjectPool : MonoBehaviour
    {
        public static CombatObjectPool Instance { get; private set; }

        [Header("Pool Limits")]
        [SerializeField] private int maxProjectilePoolSize = 96;
        [SerializeField] private int maxBeamPoolSize = 24;
        [SerializeField] private int maxExplosionPoolSize = 32;
        [SerializeField] private int maxEnemyPoolSize = 32;

        private readonly Dictionary<GameObject, ObjectPool<Projectile>> projectilePools = new Dictionary<GameObject, ObjectPool<Projectile>>();
        private readonly Dictionary<Projectile, GameObject> projectileInstanceToPrefab = new Dictionary<Projectile, GameObject>();

        private readonly Dictionary<GameObject, ObjectPool<BeamAttack>> beamPools = new Dictionary<GameObject, ObjectPool<BeamAttack>>();
        private readonly Dictionary<BeamAttack, GameObject> beamInstanceToPrefab = new Dictionary<BeamAttack, GameObject>();

        private readonly Dictionary<GameObject, ObjectPool<Explosion>> explosionPools = new Dictionary<GameObject, ObjectPool<Explosion>>();
        private readonly Dictionary<Explosion, GameObject> explosionInstanceToPrefab = new Dictionary<Explosion, GameObject>();

        private readonly Dictionary<GameObject, ObjectPool<EnemyBase>> enemyPools = new Dictionary<GameObject, ObjectPool<EnemyBase>>();
        private readonly Dictionary<EnemyBase, GameObject> enemyInstanceToPrefab = new Dictionary<EnemyBase, GameObject>();

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
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            ClearAllPools();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static CombatObjectPool EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            CombatObjectPool existing = FindAnyObjectByType<CombatObjectPool>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject go = new GameObject("CombatObjectPool");
            return go.AddComponent<CombatObjectPool>();
        }

        public void PrewarmProjectile(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;
            Projectile prefabComp = prefab.GetComponent<Projectile>();
            if (prefabComp == null) return;

            if (!projectilePools.TryGetValue(prefab, out ObjectPool<Projectile> pool))
            {
                pool = new ObjectPool<Projectile>(prefabComp, transform, count, maxProjectilePoolSize);
                projectilePools[prefab] = pool;
                return;
            }

            pool.Prewarm(count);
        }

        public Projectile SpawnProjectile(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                return null;
            }

            Projectile prefabComp = prefab.GetComponent<Projectile>();
            if (prefabComp == null)
            {
                GameObject raw = Instantiate(prefab, position, rotation);
                return raw.GetComponent<Projectile>();
            }

            if (!projectilePools.TryGetValue(prefab, out ObjectPool<Projectile> pool))
            {
                pool = new ObjectPool<Projectile>(prefabComp, transform, 0, maxProjectilePoolSize);
                projectilePools[prefab] = pool;
            }

            Projectile instance = pool.Get(position, rotation);
            projectileInstanceToPrefab[instance] = prefab;
            instance.SetReturnToPoolCallback(DespawnProjectile);
            return instance;
        }

        public void DespawnProjectile(Projectile instance)
        {
            if (instance == null)
            {
                return;
            }

            if (projectileInstanceToPrefab.TryGetValue(instance, out GameObject key) &&
                projectilePools.TryGetValue(key, out ObjectPool<Projectile> pool))
            {
                if (!pool.Release(instance))
                {
                    projectileInstanceToPrefab.Remove(instance);
                }
                return;
            }

            projectileInstanceToPrefab.Remove(instance);
            Destroy(instance.gameObject);
        }

        public void PrewarmBeam(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;
            BeamAttack prefabComp = prefab.GetComponent<BeamAttack>();
            if (prefabComp == null) return;

            if (!beamPools.TryGetValue(prefab, out ObjectPool<BeamAttack> pool))
            {
                pool = new ObjectPool<BeamAttack>(prefabComp, transform, count, maxBeamPoolSize);
                beamPools[prefab] = pool;
                return;
            }

            pool.Prewarm(count);
        }

        public BeamAttack SpawnBeam(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                return null;
            }

            BeamAttack prefabComp = prefab.GetComponent<BeamAttack>();
            if (prefabComp == null)
            {
                GameObject raw = Instantiate(prefab, position, rotation);
                return raw.GetComponent<BeamAttack>();
            }

            if (!beamPools.TryGetValue(prefab, out ObjectPool<BeamAttack> pool))
            {
                pool = new ObjectPool<BeamAttack>(prefabComp, transform, 0, maxBeamPoolSize);
                beamPools[prefab] = pool;
            }

            BeamAttack instance = pool.Get(position, rotation);
            beamInstanceToPrefab[instance] = prefab;
            instance.SetReturnToPoolCallback(DespawnBeam);
            return instance;
        }

        public void DespawnBeam(BeamAttack instance)
        {
            if (instance == null)
            {
                return;
            }

            if (beamInstanceToPrefab.TryGetValue(instance, out GameObject key) &&
                beamPools.TryGetValue(key, out ObjectPool<BeamAttack> pool))
            {
                if (!pool.Release(instance))
                {
                    beamInstanceToPrefab.Remove(instance);
                }
                return;
            }

            beamInstanceToPrefab.Remove(instance);
            Destroy(instance.gameObject);
        }

        public void PrewarmExplosion(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;
            Explosion prefabComp = prefab.GetComponent<Explosion>();
            if (prefabComp == null) return;

            if (!explosionPools.TryGetValue(prefab, out ObjectPool<Explosion> pool))
            {
                pool = new ObjectPool<Explosion>(prefabComp, transform, count, maxExplosionPoolSize);
                explosionPools[prefab] = pool;
                return;
            }

            pool.Prewarm(count);
        }

        public Explosion SpawnExplosion(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                return null;
            }

            Explosion prefabComp = prefab.GetComponent<Explosion>();
            if (prefabComp == null)
            {
                GameObject raw = Instantiate(prefab, position, rotation);
                return raw.GetComponent<Explosion>();
            }

            if (!explosionPools.TryGetValue(prefab, out ObjectPool<Explosion> pool))
            {
                pool = new ObjectPool<Explosion>(prefabComp, transform, 0, maxExplosionPoolSize);
                explosionPools[prefab] = pool;
            }

            Explosion instance = pool.Get(position, rotation);
            explosionInstanceToPrefab[instance] = prefab;
            instance.SetReturnToPoolCallback(DespawnExplosion);
            return instance;
        }

        public void DespawnExplosion(Explosion instance)
        {
            if (instance == null)
            {
                return;
            }

            if (explosionInstanceToPrefab.TryGetValue(instance, out GameObject key) &&
                explosionPools.TryGetValue(key, out ObjectPool<Explosion> pool))
            {
                if (!pool.Release(instance))
                {
                    explosionInstanceToPrefab.Remove(instance);
                }
                return;
            }

            explosionInstanceToPrefab.Remove(instance);
            Destroy(instance.gameObject);
        }

        public void PrewarmEnemy(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;
            EnemyBase prefabComp = prefab.GetComponent<EnemyBase>();
            if (prefabComp == null) return;

            if (!enemyPools.TryGetValue(prefab, out ObjectPool<EnemyBase> pool))
            {
                pool = new ObjectPool<EnemyBase>(prefabComp, transform, count, maxEnemyPoolSize);
                enemyPools[prefab] = pool;
                return;
            }

            pool.Prewarm(count);
        }

        public EnemyBase SpawnEnemy(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                return null;
            }

            EnemyBase prefabComp = prefab.GetComponent<EnemyBase>();
            if (prefabComp == null)
            {
                GameObject raw = Instantiate(prefab, position, rotation);
                return raw.GetComponent<EnemyBase>();
            }

            if (!enemyPools.TryGetValue(prefab, out ObjectPool<EnemyBase> pool))
            {
                pool = new ObjectPool<EnemyBase>(prefabComp, transform, 0, maxEnemyPoolSize);
                enemyPools[prefab] = pool;
            }

            EnemyBase instance = pool.Get(position, rotation);
            enemyInstanceToPrefab[instance] = prefab;
            instance.SetReturnToPoolCallback(DespawnEnemy);
            return instance;
        }

        public void DespawnEnemy(EnemyBase instance)
        {
            if (instance == null)
            {
                return;
            }

            if (enemyInstanceToPrefab.TryGetValue(instance, out GameObject key) &&
                enemyPools.TryGetValue(key, out ObjectPool<EnemyBase> pool))
            {
                if (!pool.Release(instance))
                {
                    enemyInstanceToPrefab.Remove(instance);
                }
                return;
            }

            enemyInstanceToPrefab.Remove(instance);
            Destroy(instance.gameObject);
        }

        public int GetActiveProjectileCount()
        {
            int count = 0;
            foreach (KeyValuePair<GameObject, ObjectPool<Projectile>> kv in projectilePools)
            {
                count += kv.Value.ActiveCount;
            }
            return count;
        }

        public int GetPooledProjectileCount()
        {
            int count = 0;
            foreach (KeyValuePair<GameObject, ObjectPool<Projectile>> kv in projectilePools)
            {
                count += kv.Value.PooledCount;
            }
            return count;
        }

        public int GetActiveEnemyCount()
        {
            int count = 0;
            foreach (KeyValuePair<GameObject, ObjectPool<EnemyBase>> kv in enemyPools)
            {
                count += kv.Value.ActiveCount;
            }
            return count;
        }

        public int GetPooledEnemyCount()
        {
            int count = 0;
            foreach (KeyValuePair<GameObject, ObjectPool<EnemyBase>> kv in enemyPools)
            {
                count += kv.Value.PooledCount;
            }
            return count;
        }

        public void ClearAllPools()
        {
            foreach (KeyValuePair<GameObject, ObjectPool<Projectile>> kv in projectilePools) kv.Value.Clear();
            foreach (KeyValuePair<GameObject, ObjectPool<BeamAttack>> kv in beamPools) kv.Value.Clear();
            foreach (KeyValuePair<GameObject, ObjectPool<Explosion>> kv in explosionPools) kv.Value.Clear();
            foreach (KeyValuePair<GameObject, ObjectPool<EnemyBase>> kv in enemyPools) kv.Value.Clear();

            projectilePools.Clear();
            projectileInstanceToPrefab.Clear();
            beamPools.Clear();
            beamInstanceToPrefab.Clear();
            explosionPools.Clear();
            explosionInstanceToPrefab.Clear();
            enemyPools.Clear();
            enemyInstanceToPrefab.Clear();
        }
    }
}
