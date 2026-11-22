using System;
using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Economy;
using StickmanBeatEmUp.Enemies.Stats;
using StickmanBeatEmUp.UI;
using StickmanGame.VFX;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// OOP abstract base class for all non-boss enemies (Creeps and Elites).
    /// Provides HP management, telegraph red-flash cue, Super Armor vs Stagger, Freeze (Diend), and trimmed Animator state syncing.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public abstract class EnemyBase : MonoBehaviour, IDamageable, ICombatTargetProfile, IFreezable
    {
        protected static readonly int AnimStateHash = Animator.StringToHash("State");

        [Header("Data & References")]
        [SerializeField] protected EnemyDataSO enemyData;
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected Animator animator;
        [SerializeField] protected Hitbox meleeHitbox;
        [SerializeField] protected LayerMask playerLayer;

        [Header("Map Boundaries")]
        public const float GROUND_Y = -2.5f;
        [SerializeField] protected float mapMinX = -22f;
        [SerializeField] protected float mapMaxX = 42f;
        [SerializeField] protected bool lockToGroundY = true;

        [Header("Sandbox / Custom Override")]
        [SerializeField] protected float maxHealthOverride = 0f;

        [Header("Combat & Stagger")]
        [SerializeField] protected float hurtDuration = 0.25f;

        public Transform Transform => transform;
        public bool IsDead { get; protected set; }
        public float CurrentHealth { get; protected set; }
        public float MaxHealth => (maxHealthOverride > 0f ? maxHealthOverride : (enemyData != null ? enemyData.maxHealth : 50f)) * runtimeHealthMult;
        public EnemyDataSO Data => enemyData;

        public float EffectiveAttackDamage => (enemyData != null ? enemyData.attackDamage : 10f) * runtimeDamageMult;
        public float EffectiveHeavyDamage => (enemyData != null ? enemyData.heavyAttackDamage : 20f) * runtimeDamageMult;
        public float EffectiveAttackCooldown => (enemyData != null ? enemyData.attackCooldown : 1.5f) / Mathf.Max(0.1f, runtimeAttackSpeedMult);
        public float EffectiveMoveSpeed => (enemyData != null ? enemyData.moveSpeed : 3.5f) * runtimeMoveSpeedMult;
        public float HurtDuration { get => hurtDuration; set => hurtDuration = Mathf.Max(0.05f, value); }
        public float BaseHurtDuration => baseHurtDuration;
        public bool IsEndlessEnraged => isEndlessEnraged;
        public bool ignoreNormalAttackStun
        {
            get => isEndlessEnraged;
            set
            {
                isEndlessEnraged = value;
                ApplyEnragedVisuals(value);
            }
        }
        public bool IsTrainingDummy => isTrainingDummy;

        public virtual bool HasSuperArmor => (enemyData != null && enemyData.hasSuperArmor) || isSuperArmorActive;
        public virtual bool IsShielded => isShielded;
        public virtual bool IsEliteOrBoss => enemyData != null && (enemyData.isElite || enemyData.isBoss);

        public event Action<float, float> OnHealthChanged;
        public event Action<EnemyBase> OnDied;

        public void SetTrainingDummy(bool value) => isTrainingDummy = value;

        public void SetMaxHealthOverride(float hp)
        {
            maxHealthOverride = hp;
            CurrentHealth = MaxHealth;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        // Per-instance runtime scaling & mode flags (never mutate EnemyDataSO assets)
        protected float runtimeHealthMult = 1f;
        protected float runtimeDamageMult = 1f;
        protected float runtimeMoveSpeedMult = 1f;
        protected float runtimeStaggerMult = 1f;
        protected float runtimeDamageReduction = 0f;
        protected float runtimeAttackSpeedMult = 1f;
        protected bool isEndlessEnraged = false;
        protected bool suppressCoinsAndDeathEvents = false;
        protected bool isTrainingDummy = false;
        private float baseHurtDuration = 0.25f;

        protected Rigidbody2D rb;
        protected Collider2D col;
        protected Transform playerTarget;
        protected Color originalColor = Color.white;
        private Color baseOriginalColor = Color.white;
        protected Vector3 initialScale = Vector3.one;
        protected float facingDirection = 1f;

        protected bool isActing;
        protected bool isStaggered;
        protected bool isFrozen;
        protected bool isSuperArmorActive;
        protected bool isShielded;
        protected float nextAttackTime;
        protected Coroutine telegraphRoutine;
        protected Coroutine actionRoutine;
        protected Coroutine staggerRoutine;
        protected Coroutine whiteFlashRoutine;

        protected BoxCollider2D cachedLeftWall;
        protected BoxCollider2D cachedRightWall;
        private float nextWallLookupTime;

        private static Material whiteFlashMaterial;
        private static Sprite cachedEnragedAuraSprite;
        private Material defaultSharedMaterial;
        private SpriteRenderer enragedAuraRenderer;
        private static readonly Color EnragedBodyTint = new Color(1f, 0.42f, 0.35f, 1f);

        public static bool GlobalEnemyAiEnabled { get; set; } = true;
        public static bool GlobalEnemyInvincible { get; set; } = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEnemyBaseStatics()
        {
            whiteFlashMaterial = null;
            cachedEnragedAuraSprite = null;
            GlobalEnemyAiEnabled = true;
            GlobalEnemyInvincible = false;
        }

        protected virtual void Awake()
        {
            baseHurtDuration = Mathf.Max(0.05f, hurtDuration);
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
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
            CurrentHealth = MaxHealth;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
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
            SetAnimState(0);
        }

        public void SetData(EnemyDataSO data)
        {
            enemyData = data;
            if (Application.isPlaying && enemyData != null)
            {
                CurrentHealth = MaxHealth;
                OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
            }
        }

        /// <summary>
        /// Applies Endless wave modifiers to this runtime enemy instance without mutating EnemyDataSO.
        /// </summary>
        public virtual void ApplyEndlessModifiers(
            float hpMult,
            float dmgMult,
            float speedMult,
            float staggerMult,
            float damageReductionPct,
            float attackSpeedMult,
            bool isEnraged)
        {
            runtimeHealthMult = Mathf.Max(0.1f, hpMult);
            runtimeDamageMult = Mathf.Max(0.1f, dmgMult);
            runtimeMoveSpeedMult = Mathf.Max(0.1f, speedMult);
            runtimeStaggerMult = Mathf.Max(0.1f, staggerMult);
            hurtDuration = Mathf.Max(0.05f, baseHurtDuration * runtimeStaggerMult);
            runtimeDamageReduction = Mathf.Clamp(damageReductionPct, 0f, 0.9f);
            runtimeAttackSpeedMult = Mathf.Max(0.1f, attackSpeedMult);
            isEndlessEnraged = isEnraged;

            if (animator != null && !isFrozen)
            {
                animator.speed = runtimeAttackSpeedMult;
            }

            CurrentHealth = MaxHealth;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
            ApplyEnragedVisuals(isEndlessEnraged);
        }

        private void ApplyEnragedVisuals(bool enraged)
        {
            originalColor = enraged ? EnragedBodyTint : baseOriginalColor;
            if (spriteRenderer != null && !isFrozen && whiteFlashRoutine == null)
            {
                spriteRenderer.color = originalColor;
            }

            if (enraged)
            {
                EnsureEnragedAura();
                if (enragedAuraRenderer != null)
                {
                    enragedAuraRenderer.enabled = true;
                }
            }
            else if (enragedAuraRenderer != null)
            {
                enragedAuraRenderer.enabled = false;
            }
        }

        private void EnsureEnragedAura()
        {
            if (enragedAuraRenderer != null) return;

            Transform existing = transform.Find("EndlessEnragedAura");
            GameObject auraObj;
            if (existing != null)
            {
                auraObj = existing.gameObject;
                enragedAuraRenderer = auraObj.GetComponent<SpriteRenderer>();
            }
            else
            {
                auraObj = new GameObject("EndlessEnragedAura");
                auraObj.transform.SetParent(transform, false);
                auraObj.transform.localPosition = new Vector3(0f, 0.85f, 0f);
                auraObj.transform.localScale = new Vector3(1.85f, 1.85f, 1f);
                enragedAuraRenderer = auraObj.AddComponent<SpriteRenderer>();
            }

            if (enragedAuraRenderer != null)
            {
                if (enragedAuraRenderer.sprite == null)
                {
                    enragedAuraRenderer.sprite = GetOrCreateEnragedAuraSprite();
                }
                enragedAuraRenderer.color = new Color(1f, 0.16f, 0.12f, 0.58f);
                if (spriteRenderer != null)
                {
                    enragedAuraRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                    enragedAuraRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
                }
                else
                {
                    enragedAuraRenderer.sortingOrder = 3;
                }
            }
        }

        private static Sprite GetOrCreateEnragedAuraSprite()
        {
            if (cachedEnragedAuraSprite != null) return cachedEnragedAuraSprite;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxR = size * 0.48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxR;
                    float alpha = 0f;
                    if (dist < 1f)
                    {
                        float rim = Mathf.Clamp01(1f - Mathf.Abs(dist - 0.62f) * 2.1f);
                        float core = Mathf.Clamp01((1f - dist) * 0.65f);
                        alpha = Mathf.Clamp01(Mathf.Max(rim * 0.9f, core * 0.55f));
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            cachedEnragedAuraSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 32f);
            return cachedEnragedAuraSprite;
        }

        private void UpdateEnragedAuraPulse()
        {
            if (!isEndlessEnraged || enragedAuraRenderer == null || !enragedAuraRenderer.enabled) return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6.5f);
            float s = Mathf.Lerp(1.65f, 2.15f, pulse);
            enragedAuraRenderer.transform.localScale = new Vector3(s, s, 1f);
            Color c = new Color(1f, Mathf.Lerp(0.12f, 0.28f, pulse), 0.1f, Mathf.Lerp(0.38f, 0.72f, pulse));
            enragedAuraRenderer.color = c;
        }

        protected void FindPlayerTarget()
        {
            BaseCharacter player = FindAnyObjectByType<BaseCharacter>();
            if (player != null)
            {
                playerTarget = player.transform;
                return;
            }
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) playerTarget = pObj.transform;
        }

        protected virtual void Update()
        {
            if (IsDead || isFrozen) return;

            UpdateEnragedAuraPulse();

            if (runtimeAttackSpeedMult > 1f && !isActing && nextAttackTime > Time.time)
            {
                nextAttackTime -= Time.deltaTime * (runtimeAttackSpeedMult - 1f);
            }

            if (playerTarget == null)
            {
                FindPlayerTarget();
                if (playerTarget == null) return;
            }

            if (isStaggered || isActing) return;

            if (!GlobalEnemyAiEnabled)
            {
                if (rb != null && (lockToGroundY || Mathf.Abs(rb.linearVelocity.y) < 0.15f))
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
                SetAnimState(0);
                return;
            }

            TickBehavior();
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

        /// <summary>Subclasses implement specific movement / attack decision loop.</summary>
        protected abstract void TickBehavior();

        protected virtual void SpawnHitSpark(DamageData damageData)
        {
            Vector3 hitPos = HitSparkManager.ResolveHitPoint(transform, col, spriteRenderer, damageData.attacker);
            HitSparkManager.Spawn(hitPos);
        }

        public virtual void TakeDamage(DamageData damageData)
        {
            if (IsDead) return;
            Vector3 hitPos = HitSparkManager.ResolveHitPoint(transform, col, spriteRenderer, damageData.attacker);
            SpawnHitSpark(damageData);

            float prevHealth = CurrentHealth;
            float reducedDamage = damageData.damageAmount * Mathf.Clamp01(1f - runtimeDamageReduction);
            float minHealth = GlobalEnemyInvincible ? 1f : 0f;
            CurrentHealth = Mathf.Max(minHealth, CurrentHealth - reducedDamage);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            Vector3 popupWorldPos = hitPos + Vector3.up * 0.35f;
            if (IsShielded)
            {
                DamagePopupManager.SpawnBlocked(popupWorldPos);
            }
            else if (GlobalEnemyInvincible)
            {
                float dealt = Mathf.Max(0f, prevHealth - CurrentHealth);
                if (dealt > 0.001f)
                {
                    DamagePopupManager.SpawnDamage(
                        popupWorldPos,
                        dealt,
                        damageData.attackType,
                        damageData.breaksSuperArmor || dealt >= 24f);
                }
            }
            else if (reducedDamage > 0f)
            {
                DamagePopupManager.SpawnDamage(
                    popupWorldPos,
                    reducedDamage,
                    damageData.attackType,
                    damageData.breaksSuperArmor || reducedDamage >= 24f);
            }

            if (!GlobalEnemyInvincible && CurrentHealth <= 0f)
            {
                StickmanBeatEmUp.Characters.BaseCharacter.NotifyAttackerKill(damageData.attacker, this);
                Die();
                return;
            }

            bool immuneToLightJ = isEndlessEnraged &&
                                  damageData.attackType == AttackType.NormalAttack_J &&
                                  !damageData.breaksSuperArmor;

            if (HasSuperArmor || IsShielded || immuneToLightJ)
            {
                // Do NOT interrupt current attack or stagger!
                TriggerWhiteFlash();
                return;
            }

            // Otherwise (no SuperArmor and not Shielded):
            // EVERY Player hit forces HurtState (hurtDuration hit-stun, interrupting current enemy attack):
            InterruptActions();
            TriggerWhiteFlash();
            ApplyKnockback(damageData.knockbackForce * 0.35f);

            float staggerDuration = Mathf.Max(0.05f, hurtDuration);
            if (staggerRoutine != null)
            {
                StopCoroutine(staggerRoutine);
            }
            staggerRoutine = StartCoroutine(StaggerRoutine(staggerDuration));
        }

        protected virtual void ApplyKnockback(Vector2 force)
        {
            if (rb == null) return;
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(force, ForceMode2D.Impulse);
        }

        protected void InterruptActions()
        {
            isSuperArmorActive = false;
            if (actionRoutine != null) { StopCoroutine(actionRoutine); actionRoutine = null; }
            if (telegraphRoutine != null) { StopCoroutine(telegraphRoutine); telegraphRoutine = null; }
            if (staggerRoutine != null) { StopCoroutine(staggerRoutine); staggerRoutine = null; isStaggered = false; }
            if (whiteFlashRoutine != null) { StopCoroutine(whiteFlashRoutine); whiteFlashRoutine = null; }
            if (meleeHitbox != null) meleeHitbox.Deactivate();
            if (spriteRenderer != null)
            {
                if (defaultSharedMaterial != null) spriteRenderer.sharedMaterial = defaultSharedMaterial;
                if (!isFrozen) spriteRenderer.color = originalColor;
            }
            isActing = false;
        }

        protected IEnumerator StaggerRoutine(float duration)
        {
            isStaggered = true;
            SetAnimState(GetHitAnimState());
            yield return new WaitForSeconds(duration);
            if (!IsDead && !isFrozen)
            {
                isStaggered = false;
                SetAnimState(0);
            }
            staggerRoutine = null;
        }

        public virtual void Freeze(float duration)
        {
            if (IsDead) return;
            StopCoroutine(nameof(FreezeRoutine));
            StartCoroutine(FreezeRoutine(duration));
        }

        private IEnumerator FreezeRoutine(float duration)
        {
            InterruptActions();
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
            const float pulseStep = 0.08f;
            bool tint = true;

            while (elapsed < duration && !IsDead && !isFrozen)
            {
                spriteRenderer.color = tint ? flashCol : originalColor;
                tint = !tint;
                yield return new WaitForSeconds(pulseStep);
                elapsed += pulseStep;
            }

            if (!IsDead && !isFrozen) spriteRenderer.color = originalColor;
        }

        protected virtual int GetHitAnimState() => 3;
        protected virtual int GetDeathAnimState() => 4;

        protected virtual int GetKillCoinReward()
        {
            if (enemyData != null && !string.IsNullOrEmpty(enemyData.enemyName) &&
                enemyData.enemyName.IndexOf("Saibaman", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CurrencyManager.REWARD_SAIBAMAN;
            }

            if (enemyData != null && enemyData.isBoss)
            {
                return CurrencyManager.REWARD_BOSS;
            }

            if (enemyData != null && enemyData.isElite)
            {
                return CurrencyManager.REWARD_ELITE;
            }

            return CurrencyManager.REWARD_NORMAL_CREEP;
        }

        /// <summary>
        /// Immediately destroys this enemy instance without awarding coins or firing death/victory events.
        /// Used for silent Training Clear-All and Endless cleanup.
        /// </summary>
        public virtual void DestroySilentlyWithoutReward()
        {
            suppressCoinsAndDeathEvents = true;
            IsDead = true;
            InterruptActions();
            StopAllCoroutines();
            DeactivateMeleeHitbox();
            if (enragedAuraRenderer != null) enragedAuraRenderer.enabled = false;
            if (col != null) col.enabled = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            Destroy(gameObject);
        }

        protected virtual void Die()
        {
            if (IsDead) return;
            IsDead = true;
            InterruptActions();
            StopAllCoroutines();

            if (enragedAuraRenderer != null) enragedAuraRenderer.enabled = false;
            if (animator != null) animator.speed = 1f;
            SetAnimState(GetDeathAnimState());
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            if (col != null) col.enabled = false;
            if (spriteRenderer != null)
            {
                if (defaultSharedMaterial != null) spriteRenderer.sharedMaterial = defaultSharedMaterial;
                spriteRenderer.color = originalColor;
            }

            if (!suppressCoinsAndDeathEvents)
            {
                CurrencyManager cm = CurrencyManager.Instance;
                if (cm != null && !cm.SuppressEnemyRewards)
                {
                    int reward = GetKillCoinReward();
                    CurrencyManager.RewardEnemyKill(reward, transform.position);
                }

                OnDied?.Invoke(this);
            }

            Destroy(gameObject, 1.35f);
        }

        protected virtual void OnDisable()
        {
            if (spriteRenderer != null && defaultSharedMaterial != null)
            {
                spriteRenderer.sharedMaterial = defaultSharedMaterial;
            }
        }

        protected void FaceTarget()
        {
            if (playerTarget == null) return;
            float dx = playerTarget.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.08f)
            {
                facingDirection = dx >= 0f ? 1f : -1f;
                Vector3 s = initialScale;
                s.x = Mathf.Abs(initialScale.x) * facingDirection;
                transform.localScale = s;
            }
        }

        protected void MoveHorizontally(float dirX, float speed)
        {
            if (rb == null) return;
            float effectiveSpeed = speed * runtimeMoveSpeedMult;
            rb.linearVelocity = new Vector2(dirX * effectiveSpeed, rb.linearVelocity.y);
            if (Mathf.Abs(dirX) > 0.05f)
            {
                facingDirection = dirX >= 0f ? 1f : -1f;
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

        protected void ActivateMeleeHitbox(float damage, AttackType type, bool breaksArmor = false)
        {
            if (meleeHitbox == null) return;
            Vector2 kb = enemyData != null ? enemyData.knockbackForce : new Vector2(3.5f, 1.5f);
            kb.x = Mathf.Abs(kb.x) * facingDirection;
            float scaledDamage = damage * runtimeDamageMult;
            meleeHitbox.Activate(new DamageData(scaledDamage, kb, type, breaksArmor, gameObject));
        }

        protected void DeactivateMeleeHitbox()
        {
            if (meleeHitbox != null) meleeHitbox.Deactivate();
        }
    }
}
