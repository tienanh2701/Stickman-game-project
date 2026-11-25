using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Audio;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Core
{
    /// <summary>
    /// Đạn/Chưởng cơ bản cho cả Player (Ki Blast, Gunshot, Thunder Spear) và Ranger Enemy.
    /// Hỗ trợ đạn xuyên thấu (Piercing), Tảng Đá Titan nổ diện rộng, Object Pool và
    /// quét chống tunneling giữa vị trí vật lý khung trước và hiện tại bằng Physics2D.CircleCast.
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour, IPoolableCombatObject
    {
        [Header("Projectile Stats")]
        [SerializeField] private float speed = 12f;
        [SerializeField] private float lifeTime = 3f;
        [SerializeField] private bool isPiercing = false;
        [SerializeField] private int maxPierceCount = 999;
        [SerializeField] private LayerMask targetLayer;

        [Header("Anti-Tunneling Sweep")]
        [SerializeField] private float minSweepRadius = 0.14f;
        [SerializeField] private float maxSweepRadius = 0.65f;

        [Header("Impact Effect (Optional)")]
        [SerializeField] private GameObject impactVfxPrefab;

        private Rigidbody2D rb;
        private Collider2D projCollider;
        private DamageData damageData;
        private int currentPierced = 0;
        private Vector2 moveDirection = Vector2.right;

        private float defaultSpeed = 12f;
        private bool defaultIsPiercing = false;
        private int defaultMaxPierceCount = 999;

        private bool isTitanBoulder = false;
        private float boulderExplosionRadius = 7.0f;
        private GameObject boulderExplosionVfx;
        private bool hasExploded = false;
        private bool hasConsumed = false;
        private Coroutine lifeCoroutine;

        private Vector2 previousPosition;
        private bool hasPreviousPosition;
        private float sweepRadius = 0.22f;
        private Action<Projectile> returnToPoolCallback;

        private readonly RaycastHit2D[] sweepHitsBuffer = new RaycastHit2D[16];
        private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
        private static Sprite cachedShockwaveRingSprite;

        private string impactSfxResourceName;
        private float impactSfxVolumeScale = 1f;
        private float impactSfxMinInterval = 0.05f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticCache()
        {
            cachedShockwaveRingSprite = null;
        }

        private void Awake()
        {
            defaultSpeed = speed;
            defaultIsPiercing = isPiercing;
            defaultMaxPierceCount = maxPierceCount;

            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            projCollider = GetComponent<Collider2D>();
            projCollider.isTrigger = true;
            sweepRadius = ResolveSweepRadius();
        }

        public LayerMask TargetLayer => targetLayer;
        public GameObject Attacker => damageData.attacker;

        public void SetReturnToPoolCallback(Action<Projectile> callback)
        {
            returnToPoolCallback = callback;
        }

        public void SetPoolReturnAction(Action<Projectile> callback)
        {
            returnToPoolCallback = callback;
        }

        public void OnSpawnedFromPool()
        {
            if (lifeCoroutine != null)
            {
                StopCoroutine(lifeCoroutine);
                lifeCoroutine = null;
            }

            speed = defaultSpeed;
            isPiercing = defaultIsPiercing;
            maxPierceCount = defaultMaxPierceCount;
            currentPierced = 0;
            hasConsumed = false;
            hasExploded = false;
            isTitanBoulder = false;
            boulderExplosionRadius = 7.0f;
            boulderExplosionVfx = null;
            hasPreviousPosition = false;
            previousPosition = transform.position;
            hitTargets.Clear();
            impactSfxResourceName = null;
            impactSfxVolumeScale = 1f;
            impactSfxMinInterval = 0.05f;

            if (projCollider == null) projCollider = GetComponent<Collider2D>();
            if (projCollider != null) projCollider.enabled = true;
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = transform.position;
                rb.linearVelocity = Vector2.zero;
            }
        }

        public void OnReturnedToPool()
        {
            if (lifeCoroutine != null)
            {
                StopCoroutine(lifeCoroutine);
                lifeCoroutine = null;
            }

            if (rb != null) rb.linearVelocity = Vector2.zero;
            hasConsumed = true;
            hasPreviousPosition = false;
            hitTargets.Clear();
            impactSfxResourceName = null;
            impactSfxVolumeScale = 1f;
            impactSfxMinInterval = 0.05f;
            if (projCollider != null) projCollider.enabled = false;
        }

        public void SetImpactSfx(string resourceSubPath, float volumeScale = 1f, float minInterval = 0.05f)
        {
            impactSfxResourceName = resourceSubPath;
            impactSfxVolumeScale = volumeScale;
            impactSfxMinInterval = minInterval;
        }

        private void PlayImpactSfx()
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(impactSfxResourceName)) return;
            AudioManager.Instance?.PlaySfxByResourceName(impactSfxResourceName, impactSfxVolumeScale, impactSfxMinInterval);
        }

        public void SetSpeed(float newSpeed)
        {
            speed = newSpeed;
            if (rb != null && rb.linearVelocity.sqrMagnitude > 0.001f)
            {
                rb.linearVelocity = moveDirection * speed;
            }
        }

        public void ConfigureTitanBoulder(float explosionRadius, GameObject explosionVfx)
        {
            isTitanBoulder = true;
            isPiercing = true;
            boulderExplosionRadius = explosionRadius > 0f ? explosionRadius : 7.0f;
            boulderExplosionVfx = explosionVfx;
            hasExploded = false;
        }

        public void Initialize(DamageData damage, Vector2 direction, LayerMask targetMask, bool piercing = false)
        {
            damageData = damage;
            moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            targetLayer = targetMask;
            isPiercing = isTitanBoulder || piercing;
            currentPierced = 0;
            hasConsumed = false;
            hasExploded = false;
            hitTargets.Clear();

            // Xoay đầu đạn theo hướng bay
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (projCollider == null) projCollider = GetComponent<Collider2D>();
            if (projCollider != null) projCollider.enabled = true;
            sweepRadius = ResolveSweepRadius();

            Vector2 startPos = transform.position;
            rb.position = startPos;
            rb.linearVelocity = moveDirection * speed;
            previousPosition = startPos;
            hasPreviousPosition = true;

            if (lifeCoroutine != null) StopCoroutine(lifeCoroutine);
            lifeCoroutine = StartCoroutine(LifetimeRoutine());
        }

        private float ResolveSweepRadius()
        {
            if (TryGetComponent<CircleCollider2D>(out CircleCollider2D circle))
            {
                float maxScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
                return Mathf.Clamp(circle.radius * maxScale, minSweepRadius, maxSweepRadius);
            }

            if (projCollider != null)
            {
                Vector3 ext = projCollider.bounds.extents;
                float minExtent = Mathf.Min(ext.x, ext.y);
                if (minExtent > 0.001f)
                {
                    return Mathf.Clamp(minExtent, minSweepRadius, maxSweepRadius);
                }
            }

            return Mathf.Clamp(0.22f, minSweepRadius, maxSweepRadius);
        }

        private IEnumerator LifetimeRoutine()
        {
            yield return new WaitForSeconds(lifeTime);
            lifeCoroutine = null;

            if (isTitanBoulder)
            {
                TriggerBoulderExplosion();
            }
            else if (!hasConsumed)
            {
                hasConsumed = true;
                ReleaseOrDestroy();
            }
        }

        private void FixedUpdate()
        {
            if (hasConsumed || hasExploded) return;

            Vector2 currentPos = transform.position;
            if (!hasPreviousPosition)
            {
                previousPosition = currentPos;
                hasPreviousPosition = true;
                return;
            }

            SweepForTunneling(previousPosition, currentPos);
            previousPosition = currentPos;
        }

        private void Update()
        {
            if (isTitanBoulder && !hasExploded)
            {
                if (transform.position.x <= -22f || transform.position.x >= 42f)
                {
                    TriggerBoulderExplosion();
                }
            }
        }

        private void SweepForTunneling(Vector2 fromPos, Vector2 toPos)
        {
            Vector2 delta = toPos - fromPos;
            float distance = delta.magnitude;
            if (distance <= 0.0005f)
            {
                return;
            }

            int sweepMask = isTitanBoulder ? ~0 : targetLayer.value;
            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = true;
            filter.useLayerMask = true;
            filter.SetLayerMask(sweepMask);

            int hitCount = Physics2D.CircleCast(fromPos, sweepRadius, delta / distance, filter, sweepHitsBuffer, distance);
            if (hitCount <= 0)
            {
                return;
            }

            // Sắp xếp tăng dần theo distance (Insertion Sort tại chỗ, không cấp phát rác)
            for (int i = 1; i < hitCount; i++)
            {
                RaycastHit2D key = sweepHitsBuffer[i];
                int j = i - 1;
                while (j >= 0 && sweepHitsBuffer[j].distance > key.distance)
                {
                    sweepHitsBuffer[j + 1] = sweepHitsBuffer[j];
                    j--;
                }
                sweepHitsBuffer[j + 1] = key;
            }

            for (int i = 0; i < hitCount; i++)
            {
                if (hasConsumed || hasExploded)
                {
                    break;
                }

                Collider2D hitCol = sweepHitsBuffer[i].collider;
                if (hitCol == null || hitCol == projCollider)
                {
                    continue;
                }

                Vector2? impactPos = sweepHitsBuffer[i].distance > 0.001f
                    ? (Vector2?)sweepHitsBuffer[i].centroid
                    : null;
                ProcessColliderHit(hitCol, impactPos);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            ProcessColliderHit(other, null);
        }

        private void ProcessColliderHit(Collider2D other, Vector2? sweptImpactPosition)
        {
            if (hasConsumed || hasExploded || other == null) return;

            if (damageData.attacker != null &&
                (other.gameObject == damageData.attacker || other.transform.IsChildOf(damageData.attacker.transform)))
            {
                return;
            }

            if (isTitanBoulder && IsWallOrBoundary(other))
            {
                if (sweptImpactPosition.HasValue)
                {
                    transform.position = new Vector3(sweptImpactPosition.Value.x, sweptImpactPosition.Value.y, transform.position.z);
                }
                TriggerBoulderExplosion();
                return;
            }

            // Kiểm tra Layer mục tiêu
            if (((1 << other.gameObject.layer) & targetLayer.value) == 0) return;

            IDamageable target = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead || hitTargets.Contains(target)) return;

            if (damageData.attacker != null && target.Transform.gameObject == damageData.attacker)
            {
                return;
            }

            if (isTitanBoulder && IsEliteOrBoss(other, target))
            {
                if (sweptImpactPosition.HasValue)
                {
                    transform.position = new Vector3(sweptImpactPosition.Value.x, sweptImpactPosition.Value.y, transform.position.z);
                }
                TriggerBoulderExplosion();
                return;
            }

            hitTargets.Add(target);

            // Áp dụng hướng knockback theo hướng bay của đạn
            DamageData deliveredDamage = damageData;
            float dirSign = Mathf.Abs(moveDirection.x) > 0.01f ? Mathf.Sign(moveDirection.x) : 1f;
            deliveredDamage.knockbackForce = new Vector2(
                dirSign * Mathf.Abs(damageData.knockbackForce.x),
                damageData.knockbackForce.y
            );

            target.TakeDamage(deliveredDamage);

            BaseCharacter.NotifyAttackerHit(damageData.attacker, damageData.attackType, target);

            SpawnImpactVfx();
            if (!isTitanBoulder)
            {
                PlayImpactSfx();
            }

            if (!isPiercing)
            {
                hasConsumed = true;
                ReleaseOrDestroy();
                return;
            }

            currentPierced++;
            if (currentPierced >= maxPierceCount)
            {
                if (isTitanBoulder)
                {
                    TriggerBoulderExplosion();
                }
                else
                {
                    hasConsumed = true;
                    ReleaseOrDestroy();
                }
            }
        }

        private void ReleaseOrDestroy()
        {
            if (lifeCoroutine != null)
            {
                StopCoroutine(lifeCoroutine);
                lifeCoroutine = null;
            }

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            if (returnToPoolCallback != null)
            {
                returnToPoolCallback(this);
                return;
            }

            Destroy(gameObject);
        }

        private bool IsWallOrBoundary(Collider2D other)
        {
            if (other.isTrigger) return false;
            if (((1 << other.gameObject.layer) & targetLayer.value) != 0) return false;
            if (other.GetComponent<BaseCharacter>() != null || other.GetComponent<IDamageable>() != null) return false;

            string objName = other.gameObject.name;
            if (objName.IndexOf("Ground", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            if (objName.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                objName.IndexOf("Boundary", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return other.bounds.size.y > other.bounds.size.x * 1.5f;
        }

        private static bool IsEliteOrBoss(Collider2D other, IDamageable target)
        {
            if (target != null && target.Transform != null)
            {
                ICombatTargetProfile profile = target.Transform.GetComponent<ICombatTargetProfile>()
                    ?? target.Transform.GetComponentInParent<ICombatTargetProfile>();
                if (profile != null && profile.IsEliteOrBoss)
                {
                    return true;
                }
            }

            if (other != null)
            {
                ICombatTargetProfile otherProfile = other.GetComponent<ICombatTargetProfile>()
                    ?? other.GetComponentInParent<ICombatTargetProfile>();
                if (otherProfile != null && otherProfile.IsEliteOrBoss)
                {
                    return true;
                }
            }

            return false;
        }

        public void TriggerBoulderExplosion()
        {
            if (hasExploded) return;
            hasExploded = true;
            hasConsumed = true;

            if (lifeCoroutine != null)
            {
                StopCoroutine(lifeCoroutine);
                lifeCoroutine = null;
            }

            if (rb != null) rb.linearVelocity = Vector2.zero;

            SpawnBoulderExplosionVisual();
            PlayImpactSfx();
            CameraFollowBeatEmUp.Instance?.Shake(0.24f, 0.22f);

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, boulderExplosionRadius, targetLayer);
            HashSet<IDamageable> damaged = new HashSet<IDamageable>();
            if (hits != null)
            {
                foreach (Collider2D hitCol in hits)
                {
                    IDamageable target = hitCol.GetComponent<IDamageable>() ?? hitCol.GetComponentInParent<IDamageable>();
                    if (target == null || target.IsDead || damaged.Contains(target)) continue;
                    if (damageData.attacker != null && target.Transform.gameObject == damageData.attacker) continue;

                    damaged.Add(target);

                    float dirX = target.Transform.position.x >= transform.position.x ? 1f : -1f;
                    DamageData aoeData = damageData;
                    aoeData.knockbackForce = new Vector2(dirX * Mathf.Abs(damageData.knockbackForce.x), Mathf.Abs(damageData.knockbackForce.y));

                    target.TakeDamage(aoeData);
                    BaseCharacter.NotifyAttackerHit(damageData.attacker, damageData.attackType, target);
                }
            }

            ReleaseOrDestroy();
        }

        private void SpawnBoulderExplosionVisual()
        {
            GameObject prefab = boulderExplosionVfx != null ? boulderExplosionVfx : impactVfxPrefab;
            float explosionDiameter = Mathf.Max(1f, boulderExplosionRadius * 2f);
            SpriteRenderer mySr = GetComponent<SpriteRenderer>();
            int baseSortingLayer = mySr != null ? mySr.sortingLayerID : 0;
            int baseSortingOrder = mySr != null ? mySr.sortingOrder : 6;

            // Sóng xung kích vòng tròn khớp chính xác đường kính vùng nổ (boulderExplosionRadius * 2f)
            GameObject ringObj = new GameObject("BoulderExplosionShockwaveRing");
            ringObj.transform.position = transform.position;
            ringObj.transform.localScale = new Vector3(explosionDiameter, explosionDiameter, 1f);
            SpriteRenderer ringSr = ringObj.AddComponent<SpriteRenderer>();
            ringSr.sprite = GetOrCreateShockwaveRingSprite();
            ringSr.color = new Color(1f, 0.58f, 0.18f, 0.85f);
            ringSr.sortingLayerID = baseSortingLayer;
            ringSr.sortingOrder = baseSortingOrder + 2;
            Destroy(ringObj, 0.35f);

            if (prefab != null)
            {
                GameObject vfx = Instantiate(prefab, transform.position, Quaternion.identity);
                Combat.Explosion exp = vfx.GetComponent<Combat.Explosion>();
                if (exp != null)
                {
                    Destroy(exp);
                }
                SpriteRenderer vfxSr = vfx.GetComponentInChildren<SpriteRenderer>();
                float spriteExtent = vfxSr != null && vfxSr.sprite != null
                    ? Mathf.Max(0.5f, Mathf.Max(vfxSr.sprite.bounds.size.x, vfxSr.sprite.bounds.size.y))
                    : 1f;
                vfx.transform.localScale = Vector3.one * (explosionDiameter / spriteExtent);
                Destroy(vfx, 0.45f);
                return;
            }

            if (mySr != null && mySr.sprite != null)
            {
                float spriteExtent = Mathf.Max(0.5f, Mathf.Max(mySr.sprite.bounds.size.x, mySr.sprite.bounds.size.y));
                GameObject burst = new GameObject("BoulderExplosionBurst");
                burst.transform.position = transform.position;
                burst.transform.localScale = Vector3.one * (explosionDiameter / spriteExtent);
                SpriteRenderer sr = burst.AddComponent<SpriteRenderer>();
                sr.sprite = mySr.sprite;
                sr.color = new Color(1f, 0.6f, 0.25f, 0.8f);
                sr.sortingLayerID = baseSortingLayer;
                sr.sortingOrder = baseSortingOrder + 1;
                Destroy(burst, 0.35f);
            }
        }

        private static Sprite GetOrCreateShockwaveRingSprite()
        {
            if (cachedShockwaveRingSprite != null) return cachedShockwaveRingSprite;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDist = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normDist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float alpha = 0f;
                    if (normDist <= 1f)
                    {
                        float ringAlpha = Mathf.Clamp01(1f - Mathf.Abs(normDist - 0.88f) / 0.14f);
                        float fillAlpha = normDist < 0.88f ? 0.18f * (normDist / 0.88f) : 0f;
                        alpha = Mathf.Max(ringAlpha, fillAlpha);
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            cachedShockwaveRingSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return cachedShockwaveRingSprite;
        }

        private void SpawnImpactVfx()
        {
            if (impactVfxPrefab != null)
            {
                Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);
            }
        }
    }
}
