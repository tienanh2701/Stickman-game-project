using System;
using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Economy;
using StickmanBeatEmUp.Enemies.Stats;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// OOP abstract base class for World Bosses.
    /// Provides shared Boss vitals, HP/Status events for GameUIManager, Telegraph cues, and Freeze (Diend).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public abstract class BossBase : MonoBehaviour, IDamageable
    {
        protected static readonly int AnimStateHash = Animator.StringToHash("State");

        [Header("Boss Core Setup")]
        [SerializeField] protected EnemyDataSO bossData;
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected Animator animator;
        [SerializeField] protected Hitbox meleeHitbox;
        [SerializeField] protected LayerMask playerLayer;

        [Header("Map Boundaries")]
        public const float GROUND_Y = -2.5f;
        [SerializeField] protected float mapMinX = -22f;
        [SerializeField] protected float mapMaxX = 42f;
        [SerializeField] protected bool lockToGroundY = true;

        public Transform Transform => transform;
        public bool IsDead { get; protected set; }
        public float CurrentHealth { get; protected set; }
        public float MaxHealth => currentMaxHealth > 0f ? currentMaxHealth : BaseMaxHealth * runtimeHealthMult;
        protected float BaseMaxHealth => bossData != null ? bossData.maxHealth : 450f;
        public string BossName => bossData != null ? bossData.enemyName : gameObject.name;

        public virtual bool HasSuperArmor => bossData == null || bossData.hasSuperArmor;
        public virtual bool IsShielded => false;
        public bool IsEndlessEnraged => isEndlessEnraged;
        public bool IsTrainingDummy => isTrainingDummy;

        public event Action<float, float> OnHealthChanged;
        public event Action<string> OnStatusChanged;
        public event Action<BossBase> OnBossDefeated;

        protected Rigidbody2D rb;
        protected Collider2D mainCol;
        protected Transform playerTarget;
        protected Color originalColor = Color.white;
        protected Color baseOriginalColor = Color.white;
        protected Vector3 initialScale = Vector3.one;
        protected float facingDirection = -1f; // Bosses spawn right side facing left initially

        protected bool isActing;
        protected bool isFrozen;
        protected bool isVulnerableStagger;
        protected float damageMultiplier = 1f;
        protected Coroutine actionRoutine;
        protected Coroutine whiteFlashRoutine;

        [Header("Runtime Mode & Endless Modifiers (Per-Instance Only)")]
        protected float runtimeHealthMult = 1f;
        protected float runtimeDamageMult = 1f;
        protected float runtimeMoveSpeedMult = 1f;
        protected float runtimeStaggerMult = 1f;
        protected float runtimeDamageReduction;
        protected float runtimeAttackSpeedMult = 1f;
        protected bool isEndlessEnraged;
        protected bool suppressCoinsAndDeathEvents;
        protected bool isTrainingDummy;
        protected float currentMaxHealth;

        private SpriteRenderer endlessAuraRenderer;
        private static Sprite sharedAuraSprite;

        protected float EffectiveMoveSpeed => (bossData != null ? bossData.moveSpeed : 3.5f) * runtimeMoveSpeedMult;
        protected float EffectiveAttackSpeedMult => Mathf.Max(0.25f, runtimeAttackSpeedMult);
        protected float EffectiveAttackCooldown(float baseCooldown) => baseCooldown / EffectiveAttackSpeedMult;
        protected float EffectiveStaggerDuration(float baseDuration) => Mathf.Max(0.25f, baseDuration * runtimeStaggerMult);
        protected float ScaleOutgoingDamage(float rawDamage) => rawDamage * runtimeDamageMult;

        protected BoxCollider2D cachedLeftWall;
        protected BoxCollider2D cachedRightWall;
        private float nextWallLookupTime;

        private static Material whiteFlashMaterial;
        private Material defaultSharedMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            whiteFlashMaterial = null;
            sharedAuraSprite = null;
        }

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            mainCol = GetComponent<Collider2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (meleeHitbox == null) meleeHitbox = GetComponentInChildren<Hitbox>();
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
                baseOriginalColor = originalColor;
                defaultSharedMaterial = spriteRenderer.sharedMaterial;
            }
            initialScale = transform.localScale;
            if (Mathf.Approximately(initialScale.x, 0f)) initialScale = Vector3.one;

            if (playerLayer.value == 0)
            {
                int pLayer = LayerMask.NameToLayer("Player");
                playerLayer = pLayer >= 0 ? (1 << pLayer) : ~0;
            }
        }

        protected virtual void Start()
        {
            currentMaxHealth = BaseMaxHealth * runtimeHealthMult;
            CurrentHealth = currentMaxHealth;
            NotifyHealthChanged();
            if (lockToGroundY)
            {
                Vector3 p = transform.position;
                p.y = GROUND_Y;
                transform.position = p;
                if (rb != null) rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            }
            FindPlayerTarget();
            FindCameraWalls();
            if (animator != null && !isFrozen)
            {
                animator.speed = runtimeAttackSpeedMult;
            }
        }

        public void SetData(EnemyDataSO data)
        {
            bossData = data;
            if (Application.isPlaying && bossData != null)
            {
                currentMaxHealth = BaseMaxHealth * runtimeHealthMult;
                CurrentHealth = currentMaxHealth;
                NotifyHealthChanged();
            }
        }

        public void SetTrainingDummy(bool dummy)
        {
            isTrainingDummy = dummy;
        }

        public void ApplyEndlessModifiers(
            float hpMult,
            float dmgMult,
            float speedMult,
            float staggerMult,
            float damageReductionPct,
            float attackSpeedMult,
            bool isEnraged)
        {
            float prevMax = Mathf.Max(1f, currentMaxHealth > 0f ? currentMaxHealth : MaxHealth);
            float healthRatio = CurrentHealth > 0f ? Mathf.Clamp01(CurrentHealth / prevMax) : 1f;

            runtimeHealthMult = Mathf.Max(0.1f, hpMult);
            runtimeDamageMult = Mathf.Max(0.1f, dmgMult);
            runtimeMoveSpeedMult = Mathf.Max(0.1f, speedMult);
            runtimeStaggerMult = Mathf.Clamp(staggerMult, 0.1f, 3f);
            runtimeDamageReduction = Mathf.Clamp(damageReductionPct, 0f, 0.85f);
            runtimeAttackSpeedMult = Mathf.Max(0.25f, attackSpeedMult);
            isEndlessEnraged = isEnraged;

            if (animator != null && !isFrozen)
            {
                animator.speed = runtimeAttackSpeedMult;
            }

            currentMaxHealth = BaseMaxHealth * runtimeHealthMult;
            CurrentHealth = currentMaxHealth * healthRatio;
            UpdateEndlessEnragedVisual();
            OnEndlessModifiersApplied();
            NotifyHealthChanged();
        }

        protected virtual void OnEndlessModifiersApplied()
        {
        }

        private void UpdateEndlessEnragedVisual()
        {
            if (isEndlessEnraged)
            {
                originalColor = new Color(
                    Mathf.Min(1f, baseOriginalColor.r * 1.15f + 0.25f),
                    baseOriginalColor.g * 0.55f,
                    baseOriginalColor.b * 0.55f,
                    baseOriginalColor.a);

                if (spriteRenderer != null && !isFrozen && whiteFlashRoutine == null)
                {
                    spriteRenderer.color = originalColor;
                }

                if (endlessAuraRenderer == null)
                {
                    GameObject auraObj = new GameObject("EndlessEnragedAura");
                    auraObj.transform.SetParent(transform, false);
                    auraObj.transform.localPosition = Vector3.zero;
                    auraObj.transform.localScale = new Vector3(1.55f, 1.55f, 1f);
                    endlessAuraRenderer = auraObj.AddComponent<SpriteRenderer>();
                    endlessAuraRenderer.sprite = GetOrCreateAuraSprite();
                    endlessAuraRenderer.color = new Color(1f, 0.12f, 0.12f, 0.48f);
                    if (spriteRenderer != null)
                    {
                        endlessAuraRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                        endlessAuraRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
                    }
                }
                endlessAuraRenderer.enabled = true;
            }
            else
            {
                originalColor = baseOriginalColor;
                if (spriteRenderer != null && !isFrozen && whiteFlashRoutine == null)
                {
                    spriteRenderer.color = originalColor;
                }
                if (endlessAuraRenderer != null)
                {
                    endlessAuraRenderer.enabled = false;
                }
            }
        }

        private static Sprite GetOrCreateAuraSprite()
        {
            if (sharedAuraSprite != null) return sharedAuraSprite;
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float center = (size - 1) * 0.5f;
            float maxR = center;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / maxR;
                    float dy = (y - center) / maxR;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = r >= 1f ? 0f : Mathf.Pow(1f - r, 1.6f) * 0.9f;
                    tex.SetPixel(x, y, new Color(1f, 0.25f, 0.15f, alpha));
                }
            }
            tex.Apply();
            sharedAuraSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return sharedAuraSprite;
        }

        public void DestroySilentlyWithoutReward()
        {
            suppressCoinsAndDeathEvents = true;
            IsDead = true;
            isActing = false;
            StopAllCoroutines();
            whiteFlashRoutine = null;
            actionRoutine = null;
            DeactivateMeleeHitbox();
            if (mainCol != null) mainCol.enabled = false;
            if (endlessAuraRenderer != null) Destroy(endlessAuraRenderer.gameObject);
            Destroy(gameObject);
        }

        protected void FindPlayerTarget()
        {
            BaseCharacter player = FindAnyObjectByType<BaseCharacter>();
            if (player != null) playerTarget = player.transform;
        }

        protected void NotifyHealthChanged() => OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        protected void NotifyStatusChanged(string msg) => OnStatusChanged?.Invoke(msg);

        protected virtual void Update()
        {
            if (isEndlessEnraged && endlessAuraRenderer != null && endlessAuraRenderer.enabled)
            {
                float pulse = 0.88f + 0.18f * Mathf.Sin(Time.time * 8f);
                endlessAuraRenderer.transform.localScale = new Vector3(1.55f * pulse, 1.55f * pulse, 1f);
                endlessAuraRenderer.color = new Color(1f, 0.12f, 0.12f, 0.34f + 0.2f * Mathf.Sin(Time.time * 8f));
            }

            if (IsDead || isFrozen) return;
            if (playerTarget == null)
            {
                FindPlayerTarget();
                if (playerTarget == null) return;
            }
            if (!isActing && !isVulnerableStagger)
            {
                if (!EnemyBase.GlobalEnemyAiEnabled)
                {
                    if (rb != null && (lockToGroundY || Mathf.Abs(rb.linearVelocity.y) < 0.15f))
                    {
                        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    }
                    SetAnimState(0);
                    return;
                }
                TickBossAI();
            }
        }

        protected virtual void LateUpdate()
        {
            ClampWithinMapBounds();
        }

        protected virtual void FindCameraWalls()
        {
            if (Time.time < nextWallLookupTime) return;
            nextWallLookupTime = Time.time + 2.0f;

            if (cachedLeftWall == null)
            {
                GameObject lObj = GameObject.Find("LeftCameraWall");
                if (lObj != null) cachedLeftWall = lObj.GetComponent<BoxCollider2D>();
            }
            if (cachedRightWall == null)
            {
                GameObject rObj = GameObject.Find("RightCameraWall");
                if (rObj != null) cachedRightWall = rObj.GetComponent<BoxCollider2D>();
            }
        }

        protected virtual void ClampWithinMapBounds()
        {
            float minX = mapMinX;
            float maxX = mapMaxX;

            if (Camera.main != null)
            {
                if (cachedLeftWall == null || cachedRightWall == null)
                {
                    FindCameraWalls();
                }

                if (cachedLeftWall != null && cachedRightWall != null &&
                    cachedLeftWall.gameObject.activeInHierarchy && cachedRightWall.gameObject.activeInHierarchy)
                {
                    minX = Mathf.Max(minX, cachedLeftWall.bounds.max.x + 0.45f);
                    maxX = Mathf.Min(maxX, cachedRightWall.bounds.min.x - 0.45f);
                }
            }

            Vector3 pos = transform.position;
            float clampedX = Mathf.Clamp(pos.x, minX, maxX);
            float lockedY = lockToGroundY ? GROUND_Y : pos.y;
            transform.position = new Vector3(clampedX, lockedY, pos.z);

            if (rb != null)
            {
                float vx = rb.linearVelocity.x;
                float vy = lockToGroundY ? 0f : rb.linearVelocity.y;
                if ((clampedX <= minX + 0.001f && vx < 0f) || (clampedX >= maxX - 0.001f && vx > 0f))
                {
                    vx = 0f;
                }
                rb.linearVelocity = new Vector2(vx, vy);
            }
        }

        private static Material GetWhiteFlashMaterial()
        {
            if (whiteFlashMaterial == null)
            {
                Shader shader = Shader.Find("GUI/Text Shader");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    whiteFlashMaterial = new Material(shader);
                }
            }
            return whiteFlashMaterial;
        }

        public void TriggerWhiteFlash()
        {
            if (spriteRenderer == null) return;
            if (whiteFlashRoutine != null)
            {
                StopCoroutine(whiteFlashRoutine);
                if (defaultSharedMaterial != null) spriteRenderer.sharedMaterial = defaultSharedMaterial;
            }
            whiteFlashRoutine = StartCoroutine(WhiteFlashRoutine(0.09f));
        }

        private IEnumerator WhiteFlashRoutine(float duration)
        {
            Material flashMat = GetWhiteFlashMaterial();
            if (flashMat != null)
            {
                spriteRenderer.sharedMaterial = flashMat;
            }
            spriteRenderer.color = Color.white;

            yield return new WaitForSeconds(duration);

            if (defaultSharedMaterial != null)
            {
                spriteRenderer.sharedMaterial = defaultSharedMaterial;
            }
            spriteRenderer.color = originalColor;
            whiteFlashRoutine = null;
        }

        protected abstract void TickBossAI();

        public virtual void TakeDamage(DamageData damageData)
        {
            if (IsDead) return;
            float reducedInput = damageData.damageAmount * (1f - Mathf.Clamp01(runtimeDamageReduction));
            float dmg = reducedInput * damageMultiplier;
            float minHealth = EnemyBase.GlobalEnemyInvincible ? 1f : 0f;
            CurrentHealth = Mathf.Max(minHealth, CurrentHealth - dmg);
            NotifyHealthChanged();
            TriggerWhiteFlash();

            if (!EnemyBase.GlobalEnemyInvincible && CurrentHealth <= 0f)
            {
                StickmanBeatEmUp.Characters.BaseCharacter.NotifyAttackerKill(damageData.attacker, this);
                OnHealthDepleted();
            }
        }

        protected virtual void OnHealthDepleted() => DefeatBoss();

        public virtual void Freeze(float duration)
        {
            if (IsDead) return;
            StopCoroutine(nameof(FreezeRoutine));
            StartCoroutine(FreezeRoutine(duration));
        }

        private IEnumerator FreezeRoutine(float duration)
        {
            isFrozen = true;
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (animator != null) animator.speed = 0f;
            if (spriteRenderer != null)
            {
                if (defaultSharedMaterial != null) spriteRenderer.sharedMaterial = defaultSharedMaterial;
                spriteRenderer.color = new Color(0.55f, 0.85f, 1f, 1f);
            }

            yield return new WaitForSeconds(duration);

            if (animator != null) animator.speed = runtimeAttackSpeedMult;
            if (spriteRenderer != null && !IsDead)
            {
                if (defaultSharedMaterial != null) spriteRenderer.sharedMaterial = defaultSharedMaterial;
                spriteRenderer.color = originalColor;
            }
            isFrozen = false;
        }

        protected IEnumerator PlayTelegraphFlash(float duration, Color? overrideColor = null)
        {
            if (spriteRenderer == null || duration <= 0f) yield break;
            Color flashCol = overrideColor ?? new Color(1f, 0.25f, 0.25f, 1f);
            float elapsed = 0f;
            bool flip = true;
            while (elapsed < duration && !IsDead && !isFrozen)
            {
                spriteRenderer.color = flip ? flashCol : originalColor;
                flip = !flip;
                yield return new WaitForSeconds(0.08f);
                elapsed += 0.08f;
            }
            if (!IsDead && !isFrozen) spriteRenderer.color = originalColor;
        }

        protected virtual int DeathAnimState => 4;

        protected virtual void DefeatBoss()
        {
            if (IsDead) return;
            IsDead = true;
            isActing = false;
            StopAllCoroutines();

            if (whiteFlashRoutine != null)
            {
                StopCoroutine(whiteFlashRoutine);
                whiteFlashRoutine = null;
            }
            if (meleeHitbox != null) meleeHitbox.Deactivate();
            if (animator != null) animator.speed = 1f;
            SetAnimState(DeathAnimState);
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            if (mainCol != null) mainCol.enabled = false;
            if (spriteRenderer != null)
            {
                if (defaultSharedMaterial != null) spriteRenderer.sharedMaterial = defaultSharedMaterial;
                spriteRenderer.color = originalColor;
            }
            if (endlessAuraRenderer != null)
            {
                endlessAuraRenderer.enabled = false;
            }

            if (!suppressCoinsAndDeathEvents && CurrencyManager.Instance != null && !CurrencyManager.Instance.SuppressEnemyRewards)
            {
                CurrencyManager.RewardEnemyKill(CurrencyManager.REWARD_BOSS, transform.position);
            }

            NotifyStatusChanged($"{BossName} DEFEATED!");
            if (!suppressCoinsAndDeathEvents)
            {
                OnBossDefeated?.Invoke(this);
            }
            Destroy(gameObject, 2.2f);
        }

        protected virtual void OnDisable()
        {
            if (spriteRenderer != null && defaultSharedMaterial != null)
            {
                spriteRenderer.sharedMaterial = defaultSharedMaterial;
            }
        }

        protected virtual void FaceTarget()
        {
            if (playerTarget == null) return;
            float dx = playerTarget.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.1f)
            {
                facingDirection = dx >= 0f ? 1f : -1f;
                Vector3 s = initialScale;
                s.x = Mathf.Abs(initialScale.x) * facingDirection;
                transform.localScale = s;
            }
        }

        protected void StopHorizontalMovement()
        {
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        protected void SetAnimState(int state)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetInteger(AnimStateHash, state);
            }
        }

        protected void ActivateMeleeHitbox(float damage, AttackType type, bool breaksArmor = true)
        {
            if (meleeHitbox == null) return;
            Vector2 kb = bossData != null ? bossData.knockbackForce : new Vector2(6f, 3f);
            kb.x = Mathf.Abs(kb.x) * facingDirection;
            float scaledDamage = ScaleOutgoingDamage(damage);
            meleeHitbox.Activate(new DamageData(scaledDamage, kb, type, breaksArmor, gameObject));
        }

        protected void DeactivateMeleeHitbox()
        {
            if (meleeHitbox != null) meleeHitbox.Deactivate();
        }
    }
}
