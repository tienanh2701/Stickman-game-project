using System;
using System.Collections.Generic;
using UnityEngine;

namespace StickmanBeatEmUp.Core
{
    /// <summary>
    /// Optional lifecycle contract for combat MonoBehaviours managed by <see cref="ObjectPool{T}"/>.
    /// </summary>
    public interface IPoolableCombatObject
    {
        void OnSpawnedFromPool();
        void OnReturnedToPool();
    }

    /// <summary>
    /// Generic reusable Unity Component Object Pool with O(1) rent/release, prewarm,
    /// active/pooled telemetry counts, and max capacity guard to eliminate GC spikes during combat.
    /// </summary>
    public sealed class ObjectPool<T> where T : Component
    {
        private readonly T prefab;
        private readonly Transform poolRoot;
        private readonly Stack<T> inactive = new Stack<T>();
        private readonly HashSet<T> activeInstances = new HashSet<T>();
        private readonly Action<T> onGet;
        private readonly Action<T> onRelease;
        private readonly int maxPoolSize;

        public int ActiveCount => activeInstances.Count;
        public int InactiveCount => inactive.Count;
        public int PooledCount => inactive.Count;
        public int MaxCapacity => maxPoolSize;
        public int TotalCreated { get; private set; }

        public ObjectPool(
            T prefab,
            Transform poolRoot = null,
            int initialCapacity = 0,
            int maxPoolSize = 128,
            Action<T> onGet = null,
            Action<T> onRelease = null)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            this.prefab = prefab;
            this.poolRoot = poolRoot;
            this.maxPoolSize = Mathf.Max(1, maxPoolSize);
            this.onGet = onGet;
            this.onRelease = onRelease;

            Prewarm(initialCapacity);
        }

        public void Prewarm(int count)
        {
            int target = Mathf.Clamp(count, 0, maxPoolSize);
            while (inactive.Count < target)
            {
                T created = CreateInstance();
                created.gameObject.SetActive(false);
                inactive.Push(created);
            }
        }

        public T Rent(Vector3 position, Quaternion rotation)
        {
            return Get(position, rotation);
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            T instance = null;
            while (inactive.Count > 0 && instance == null)
            {
                instance = inactive.Pop();
            }

            if (instance == null)
            {
                instance = CreateInstance();
            }

            Transform tr = instance.transform;
            tr.SetParent(null, false);
            tr.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);

            activeInstances.Add(instance);
            if (instance is IPoolableCombatObject poolable)
            {
                poolable.OnSpawnedFromPool();
            }

            onGet?.Invoke(instance);
            return instance;
        }

        /// <summary>
        /// Returns an active instance to the pool, or destroys it when max capacity is reached.
        /// Returns true if the instance was retained in the pool, false if ignored or destroyed.
        /// </summary>
        public bool Release(T instance)
        {
            if (instance == null)
            {
                return false;
            }

            if (!activeInstances.Remove(instance))
            {
                return false;
            }

            if (instance is IPoolableCombatObject poolable)
            {
                poolable.OnReturnedToPool();
            }

            onRelease?.Invoke(instance);

            if (inactive.Count >= maxPoolSize)
            {
                UnityEngine.Object.Destroy(instance.gameObject);
                return false;
            }

            instance.gameObject.SetActive(false);
            if (poolRoot != null)
            {
                instance.transform.SetParent(poolRoot, false);
            }

            inactive.Push(instance);
            return true;
        }

        public void Clear()
        {
            while (inactive.Count > 0)
            {
                T item = inactive.Pop();
                if (item != null)
                {
                    UnityEngine.Object.Destroy(item.gameObject);
                }
            }

            activeInstances.Clear();
        }

        private T CreateInstance()
        {
            T created = UnityEngine.Object.Instantiate(prefab, poolRoot);
            TotalCreated++;
            return created;
        }
    }
}
