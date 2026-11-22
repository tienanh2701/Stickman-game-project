using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Characters
{
    /// <summary>
    /// Đồng minh AI "Lord Kiva" được Diend triệu hồi khi biến hình (Transform)
    /// Tự động bám theo Diend, truy lùng kẻ thù và hỗ trợ tấn công
    /// Hỗ trợ chiêu Combo Đặc Biệt: Dark Kiva mở cánh đóng băng (Freeze) toàn bộ địch trên màn hình
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class LordKivaAI : MonoBehaviour
    {
        [Header("Lord Kiva AI Settings")]
        [SerializeField] private float followSpeed = 5.5f;
        [SerializeField] private float attackRange = 1.8f;
        [SerializeField] private float attackCooldown = 1.2f;
        [SerializeField] private float attackDamage = 2f;
        [SerializeField] private float lifetime = 15.0f;
        [SerializeField] private LayerMask enemyLayer;

        [Header("Components & Hitbox")]
        [SerializeField] private Hitbox clawHitbox;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject wingFreezeVfxPrefab;

        private const float KivaScale = 1.35f;
        private static readonly int AnimStateHash = Animator.StringToHash("State");
        private static Sprite cachedCrimsonHaloSprite;

        private Transform playerTarget;
        private Rigidbody2D rb;
        private IDamageable currentEnemyTarget;
        private float nextAttackTime = 0f;
        private bool isPerformingSpecial = false;
        private SpriteRenderer crimsonOutlineRenderer;
        private SpriteRenderer crimsonHaloRenderer;
        private Coroutine lifetimeCoroutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticCache()
        {
            cachedCrimsonHaloSprite = null;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f; // Bay lơ lửng bám theo phong cách Vampire Rider
            transform.localScale = new Vector3(KivaScale, KivaScale, 1.0f);
            if (lifetime <= 0f) lifetime = 15.0f;
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            EnsureCrimsonAuraVisual();
        }

        private void Start()
        {
            if (lifetimeCoroutine == null && lifetime > 0f)
            {
                lifetimeCoroutine = StartCoroutine(LifetimeRoutine(lifetime));
            }
        }

        public void Initialize(Transform player)
        {
            playerTarget = player;
            float facingSign = player != null && player.localScale.x < 0f ? -1f : 1f;
            transform.localScale = new Vector3(KivaScale * facingSign, KivaScale, 1.0f);
            if (player != null)
            {
                transform.position = player.position + new Vector3(-facingSign * 1.5f, 0.25f, 0f);
            }
            EnsureCrimsonAuraVisual();
        }

        public void SetLifetime(float lifetime)
        {
            if (lifetimeCoroutine != null)
            {
                StopCoroutine(lifetimeCoroutine);
                lifetimeCoroutine = null;
            }
            this.lifetime = lifetime;
            if (lifetime > 0f)
            {
                lifetimeCoroutine = StartCoroutine(LifetimeRoutine(lifetime));
            }
        }

        private IEnumerator LifetimeRoutine(float lifetime)
        {
            yield return new WaitForSeconds(lifetime);
            lifetimeCoroutine = null;
            Destroy(gameObject);
        }

        private void EnsureCrimsonAuraVisual()
        {
            int baseOrder = spriteRenderer != null ? spriteRenderer.sortingOrder : 6;
            int baseLayerId = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;

            if (crimsonOutlineRenderer == null)
            {
                Transform existing = transform.Find("CrimsonOutlineVisual");
                if (existing != null)
                {
                    crimsonOutlineRenderer = existing.GetComponent<SpriteRenderer>();
                }
                else
                {
                    GameObject outlineObj = new GameObject("CrimsonOutlineVisual");
                    outlineObj.transform.SetParent(transform, false);
                    outlineObj.transform.localPosition = Vector3.zero;
                    outlineObj.transform.localScale = new Vector3(1.12f, 1.12f, 1f);
                    crimsonOutlineRenderer = outlineObj.AddComponent<SpriteRenderer>();
                }
            }

            if (crimsonOutlineRenderer != null)
            {
                crimsonOutlineRenderer.sortingLayerID = baseLayerId;
                crimsonOutlineRenderer.sortingOrder = baseOrder - 1;
                crimsonOutlineRenderer.color = new Color(1f, 0.12f, 0.12f, 0.6f);
                if (spriteRenderer != null)
                {
                    crimsonOutlineRenderer.sprite = spriteRenderer.sprite;
                    crimsonOutlineRenderer.flipX = spriteRenderer.flipX;
                    crimsonOutlineRenderer.flipY = spriteRenderer.flipY;
                }
            }

            if (crimsonHaloRenderer == null)
            {
                Transform existingHalo = transform.Find("CrimsonAuraHalo");
                if (existingHalo != null)
                {
                    crimsonHaloRenderer = existingHalo.GetComponent<SpriteRenderer>();
                }
                else
                {
                    GameObject haloObj = new GameObject("CrimsonAuraHalo");
                    haloObj.transform.SetParent(transform, false);
                    haloObj.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                    haloObj.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
                    crimsonHaloRenderer = haloObj.AddComponent<SpriteRenderer>();
                }
            }

            if (crimsonHaloRenderer != null)
            {
                crimsonHaloRenderer.sprite = GetOrCreateCrimsonHaloSprite();
                crimsonHaloRenderer.sortingLayerID = baseLayerId;
                crimsonHaloRenderer.sortingOrder = baseOrder - 2;
                crimsonHaloRenderer.color = new Color(1f, 0.12f, 0.12f, 0.5f);
            }
        }

        private static Sprite GetOrCreateCrimsonHaloSprite()
        {
            if (cachedCrimsonHaloSprite != null) return cachedCrimsonHaloSprite;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDist = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = alpha * alpha;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            cachedCrimsonHaloSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return cachedCrimsonHaloSprite;
        }

        private void LateUpdate()
        {
            if (crimsonOutlineRenderer != null && spriteRenderer != null)
            {
                crimsonOutlineRenderer.sprite = spriteRenderer.sprite;
                crimsonOutlineRenderer.flipX = spriteRenderer.flipX;
                crimsonOutlineRenderer.flipY = spriteRenderer.flipY;
                crimsonOutlineRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
            }

            if (crimsonHaloRenderer != null)
            {
                float pulse = 0.45f + 0.15f * Mathf.Sin(Time.time * 5.5f);
                crimsonHaloRenderer.color = new Color(1f, 0.12f, 0.12f, pulse);
            }
        }

        private void Update()
        {
            if (isPerformingSpecial) return;

            FindNearestEnemy();

            if (currentEnemyTarget != null && !currentEnemyTarget.IsDead)
            {
                EngageEnemy();
            }
            else
            {
                FollowPlayer();
            }
        }

        private void FindNearestEnemy()
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 10f, enemyLayer);
            float minDistance = float.MaxValue;
            IDamageable nearest = null;

            foreach (var col in colliders)
            {
                IDamageable dmg = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (dmg != null && !dmg.IsDead && dmg.Transform.GetComponent<BaseCharacter>() == null)
                {
                    float dist = Vector2.Distance(transform.position, dmg.Transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearest = dmg;
                    }
                }
            }

            currentEnemyTarget = nearest;
        }

        private void FollowPlayer()
        {
            if (playerTarget == null) return;

            Vector2 targetPos = (Vector2)playerTarget.position + new Vector2(-1.5f, 0.25f);
            float dist = Vector2.Distance(transform.position, targetPos);

            if (dist > 1.5f)
            {
                Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
                rb.linearVelocity = dir * followSpeed;
                Flip(dir.x);
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
            }
        }

        private void EngageEnemy()
        {
            float dist = Vector2.Distance(transform.position, currentEnemyTarget.Transform.position);
            Vector2 dir = ((Vector2)currentEnemyTarget.Transform.position - (Vector2)transform.position).normalized;
            Flip(dir.x);

            if (dist > attackRange)
            {
                rb.linearVelocity = dir * followSpeed;
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
                if (Time.time >= nextAttackTime)
                {
                    StartCoroutine(PerformClawSlash());
                    nextAttackTime = Time.time + attackCooldown;
                }
            }
        }

        private IEnumerator PerformClawSlash()
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetInteger(AnimStateHash, 1);
            }

            rb.linearVelocity = new Vector2(Mathf.Sign(transform.localScale.x) * 4f, 0);

            DamageData dData = new DamageData(
                attackDamage,
                new Vector2(4f, 2f),
                AttackType.NormalAttack_J,
                true,
                playerTarget != null ? playerTarget.gameObject : gameObject
            );

            if (clawHitbox != null) clawHitbox.Activate(dData);
            yield return new WaitForSeconds(0.2f);
            if (clawHitbox != null) clawHitbox.Deactivate();

            rb.linearVelocity = Vector2.zero;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetInteger(AnimStateHash, 0);
            }
        }

        private void Flip(float dirX)
        {
            if (Mathf.Abs(dirX) > 0.05f)
            {
                transform.localScale = new Vector3(KivaScale * (dirX > 0f ? 1f : -1f), KivaScale, 1.0f);
            }
        }

        /// <summary>
        /// Kích hoạt Combo Đặc Biệt (Dash + I): Lord Kiva mở rộng đôi cánh bóng đêm làm đông cứng (Freeze) toàn bộ kẻ thù trên màn hình!
        /// </summary>
        public void ExecuteDarkKivaWingFreeze(float freezeDuration, float specialDamage = 35f)
        {
            StartCoroutine(WingFreezeRoutine(freezeDuration, specialDamage));
        }

        private IEnumerator WingFreezeRoutine(float freezeDuration, float specialDamage)
        {
            isPerformingSpecial = true;
            rb.linearVelocity = Vector2.zero;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetInteger(AnimStateHash, 2);
            }

            Debug.Log("<color=purple>[DARK KIVA SPECIAL ACTIVE]</color> Kiva mở cánh - Đóng băng toàn bộ kẻ thù!");

            if (wingFreezeVfxPrefab != null)
            {
                GameObject vfx = Instantiate(wingFreezeVfxPrefab, transform.position, Quaternion.identity);
                Destroy(vfx, 1.2f);
            }

            // Quét toàn bộ kẻ thù trên màn hình: gây sát thương TRƯỚC rồi mới đóng băng để TakeDamage không ghi đè trạng thái Freeze!
            Collider2D[] allEnemies = Physics2D.OverlapCircleAll(transform.position, 15f, enemyLayer);
            HashSet<IDamageable> affected = new HashSet<IDamageable>();
            foreach (var col in allEnemies)
            {
                IDamageable dmg = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (dmg != null && !dmg.IsDead && !affected.Contains(dmg) && dmg.Transform.GetComponent<BaseCharacter>() == null)
                {
                    affected.Add(dmg);
                    dmg.TakeDamage(new DamageData(specialDamage, Vector2.zero, AttackType.Combo_DashI, true, playerTarget != null ? playerTarget.gameObject : gameObject));
                    if (!dmg.IsDead)
                    {
                        IFreezable freezable = col.GetComponent<IFreezable>()
                            ?? col.GetComponentInParent<IFreezable>()
                            ?? (dmg.Transform != null ? dmg.Transform.GetComponent<IFreezable>() : null);
                        freezable?.Freeze(freezeDuration);
                    }
                }
            }

            yield return new WaitForSeconds(1.0f);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetInteger(AnimStateHash, 0);
            }
            isPerformingSpecial = false;
        }
    }
}
