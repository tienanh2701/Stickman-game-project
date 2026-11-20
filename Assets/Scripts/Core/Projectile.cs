using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Core
{
    /// <summary>
    /// Đạn/Chưởng cơ bản cho cả Player (Ki Blast, Gunshot, Thunder Spear) và Ranger Enemy
    /// Hỗ trợ đạn xuyên thấu (Piercing), Tảng Đá Titan nổ diện rộng, tự hủy sau lifetime hoặc va chạm
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour
    {
        [Header("Projectile Stats")]
        [SerializeField] private float speed = 12f;
        [SerializeField] private float lifeTime = 3f;
        [SerializeField] private bool isPiercing = false;
        [SerializeField] private int maxPierceCount = 999;
        [SerializeField] private LayerMask targetLayer;

        [Header("Impact Effect (Optional)")]
        [SerializeField] private GameObject impactVfxPrefab;

        private Rigidbody2D rb;
        private DamageData damageData;
        private int currentPierced = 0;
        private Vector2 moveDirection = Vector2.right;

        private bool isTitanBoulder = false;
        private float boulderExplosionRadius = 7.0f;
        private GameObject boulderExplosionVfx;
        private bool hasExploded = false;
        private bool hasConsumed = false;
        private Coroutine lifeCoroutine;
        private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
        private static Sprite cachedShockwaveRingSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticCache()
        {
            cachedShockwaveRingSprite = null;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            GetComponent<Collider2D>().isTrigger = true;
        }

        public LayerMask TargetLayer => targetLayer;
        public GameObject Attacker => damageData.attacker;

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
            transform.rotation = Quaternion.Euler(0, 0, angle);

            if (rb == null) rb = GetComponent<Rigidbody2D>();
            rb.linearVelocity = moveDirection * speed;

            if (lifeCoroutine != null) StopCoroutine(lifeCoroutine);
            lifeCoroutine = StartCoroutine(LifetimeRoutine());
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
                Destroy(gameObject);
            }
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

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasConsumed || hasExploded || other == null) return;

            if (damageData.attacker != null &&
                (other.gameObject == damageData.attacker || other.transform.IsChildOf(damageData.attacker.transform)))
            {
                return;
            }

            if (isTitanBoulder && IsWallOrBoundary(other))
            {
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

            if (!isPiercing)
            {
                hasConsumed = true;
                if (lifeCoroutine != null)
                {
                    StopCoroutine(lifeCoroutine);
                    lifeCoroutine = null;
                }
                Destroy(gameObject);
            }
            else
            {
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
                        if (lifeCoroutine != null)
                        {
                            StopCoroutine(lifeCoroutine);
                            lifeCoroutine = null;
                        }
                        Destroy(gameObject);
                    }
                }
            }
        }

        private bool IsWallOrBoundary(Collider2D other)
        {
            if (other.isTrigger) return false;
            if (((1 << other.gameObject.layer) & targetLayer.value) != 0) return false;
            if (other.GetComponent<BaseCharacter>() != null || other.GetComponent<IDamageable>() != null) return false;

            string objName = other.gameObject.name;
            if (objName.IndexOf("Ground", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;

            if (objName.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objName.IndexOf("Boundary", System.StringComparison.OrdinalIgnoreCase) >= 0)
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

            Destroy(gameObject);
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
