using System;
using System.Collections;
using System.IO;
using UnityEngine;
using StickmanBeatEmUp.Audio;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.CorePure.Entities.Combat;
using StickmanBeatEmUp.CorePure.Interfaces;
using StickmanBeatEmUp.CorePure.Rules.Combat;
using StickmanBeatEmUp.Characters.Stats;
using StickmanBeatEmUp.GameModes;
using StickmanBeatEmUp.Level;
using StickmanBeatEmUp.UI;
using StickmanGame.VFX;

namespace StickmanBeatEmUp.Characters
{
    /// <summary>
    /// Lớp cơ sở trừu tượng (Abstract Base Class) cho toàn bộ nhân vật người chơi (Goku, Eren, Diend).
    /// Triển khai FSM, Hệ thống Di chuyển, Dash, Combo, Hitbox, Tích Nộ, và Nhận Sát Thương.
    /// Áp dụng mẫu thiết kế Template Method & State Machine.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(RageManager))]
    [RequireComponent(typeof(InputHandler))]
    public abstract class BaseCharacter : MonoBehaviour, IDamageable
    {
        [Header("Character Stats Config (ScriptableObject)")]
        [SerializeField] protected CharacterStatsSO stats;

        [Header("Ground Check References")]
        [SerializeField] protected Transform groundCheckPoint;
        [SerializeField] protected float groundCheckRadius = 0.2f;
        [SerializeField] protected LayerMask groundLayer;

        [Header("Combat Hitboxes & Spawn Points")]
        [Tooltip("Hitbox cho đòn đánh cận chiến thường")]
        [SerializeField] protected Hitbox meleeHitbox;
        [Tooltip("Điểm xuất phát cho đạn/kỹ năng chưởng")]
        [SerializeField] protected Transform projectileSpawnPoint;
        [Tooltip("Layer kẻ thù nhận sát thương")]
        [SerializeField] protected LayerMask enemyLayerMask;

        [Header("Map Boundaries")]
        public const float GROUND_Y = -2.5f;
        [SerializeField] protected float mapMinX = -22f;
        [SerializeField] protected float mapMaxX = 42f;
        [SerializeField] protected float mapMaxY = 4.8f;

        protected BoxCollider2D cachedLeftWall;
        protected BoxCollider2D cachedRightWall;
        private float nextWallLookupTime;

        [Header("Visual & Audio")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected Animator animator;
        [SerializeField] protected TrailRenderer dashTrail;
        [SerializeField] protected GameObject transformAuraEffect;

        [Header("Block Mechanic (Key S)")]
        [SerializeField] protected SpriteRenderer blockShieldVisual;
        [SerializeField] protected float blockDuration = 0.75f;
        [SerializeField] protected float blockCooldown = 0f;
        [SerializeField] protected float blockDamageMultiplier = 0.2f;

        [Header("Hurt & Stun Immunity")]
        [SerializeField] protected float hurtDuration = 0.15f;
        [SerializeField] protected float stunImmunityDuration = 0.4f;

        private static readonly int AnimStateHash = Animator.StringToHash("State");
        private static readonly int AnimTransformedHash = Animator.StringToHash("IsTransformed");
        private static readonly int AnimSkillIndexHash = Animator.StringToHash("SkillIndex");

        // Core Components
        protected Rigidbody2D rb;
        protected Collider2D col;
        protected InputHandler inputHandler;
        protected RageManager rageManager;
        private IInputProvider inputProvider;
        private IInputProvider subscribedInputProvider;
        private bool isInputSubscribed = false;
        private IStatSource statSource = BaseStatSource.Default;

        // FSM & Combat State
        protected CharacterState currentState = CharacterState.Idle;
        protected float currentHealth;
        protected bool isGrounded;
        protected int facingDirection = 1; // 1: Phải, -1: Trái
        protected bool isTransformed = false;
        protected bool isInvulnerable = false;
        protected float lastDashEndTime = -10f;
        protected float defaultGravityScale = 4.0f;
        protected bool isGravityOverriddenBySkill = false;

        // Block State
        protected float lastBlockTime = -10f;
        protected float blockTimer = 0f;
        private Coroutine blockFlashCoroutine;
        private Color blockShieldBaseColor = new Color(0.85f, 0.98f, 1f, 0.72f);

        // Item Buffs & Environmental Hazard Modifiers
        protected float spinalFluidTimer = 0f;
        protected float virtualShieldHp = 0f;
        protected float maxVirtualShieldHp = 0f;
        protected float spinalReflectDamage = 0f;

        protected float ultraWaterTimer = 0f;
        protected float ultraWaterDamageMultiplier = 1f;
        protected float ultraWaterSpeedMultiplier = 1f;
        protected float ultraWaterRagePerSec = 0f;

        protected float hazardSlowTimer = 0f;
        protected float hazardSpeedMultiplier = 1f;
        protected float hazardGravityTimer = 0f;
        protected float hazardGravityMultiplier = 1f;

        private Coroutine hurtCoroutine;
        private Coroutine timedTransformCoroutine;
        private Coroutine trainingRespawnBlinkCoroutine;
        private float timedTransformEndTime = -1f;
        private bool isTrainingRespawnInvulnerable = false;
        private bool isReflectingDamage = false;
        private float stunImmunityUntilTime = -999f;

        private static Sprite cachedBlockShieldSprite;

        // Properties công khai
        public Transform Transform => transform;
        public bool IsDead => currentState == CharacterState.Dead;
        public CharacterState CurrentState => currentState;
        public bool IsTransformed => isTransformed;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => stats != null ? stats.maxHealth : 100f;
        public RageManager RageManager => rageManager;
        public RageManager RageManagerRef => rageManager;
        public IInputProvider InputProvider => inputProvider ?? inputHandler;
        public IStatSource StatSource => statSource ?? BaseStatSource.Default;

        public void SetInputProvider(IInputProvider provider)
        {
            IInputProvider nextProvider = provider ?? inputHandler;
            if (ReferenceEquals(inputProvider, nextProvider)) return;

            bool wasSubscribed = isInputSubscribed;
            if (wasSubscribed)
            {
                UnsubscribeInputProvider();
            }

            inputProvider = nextProvider;

            if (wasSubscribed)
            {
                SubscribeInputProvider();
            }
        }

        public void SetStatSource(IStatSource source)
        {
            statSource = source ?? BaseStatSource.Default;
        }

        public void ResetStatSource()
        {
            statSource = BaseStatSource.Default;
        }

        public bool IsBlocking => currentState == CharacterState.Block && blockTimer > 0f;
        public float BlockCooldownRemaining => Mathf.Max(0f, (lastBlockTime + blockCooldown) - Time.time);
        public float BlockCooldownDuration => blockCooldown;
        public float BlockRemaining => currentState == CharacterState.Block ? Mathf.Max(0f, blockTimer) : 0f;
        public float BlockDuration => blockDuration;
        public bool TrainingInvincible { get; set; }
        public int FacingDirection => facingDirection;

        public float OutgoingDamageMultiplier => StatSource.AttackDamageMultiplier * (ultraWaterTimer > 0f ? ultraWaterDamageMultiplier : 1f);
        public float GetModifiedDamage(float rawDamage) => DamageCalculator.CalculateOutgoingDamage(rawDamage, StatSource, ultraWaterTimer > 0f ? ultraWaterDamageMultiplier : 1f);
        public bool HasSpinalFluidBuff => spinalFluidTimer > 0f && virtualShieldHp > 0f;
        public bool HasUltraWaterBuff => ultraWaterTimer > 0f;
        public float VirtualShieldHP => virtualShieldHp;
        protected virtual float HitRageAmount => 1.5f;

        // Events
        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action<float, float> OnShieldChanged; // (current, max)
        public event Action<CharacterState> OnStateChanged;
        public event Action<bool> OnTransformedChanged;
        public event Action<AttackType, IDamageable> OnEnemyHit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBaseCharacterStatics()
        {
            cachedBlockShieldSprite = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ConfigureLayerCollisions()
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");

            int p = playerLayer >= 0 ? playerLayer : 7;
            int e = enemyLayer >= 0 ? enemyLayer : 8;

            Physics2D.IgnoreLayerCollision(p, e, true);
            Physics2D.IgnoreLayerCollision(e, e, true);

            if (p != 7 || e != 8)
            {
                Physics2D.IgnoreLayerCollision(7, 8, true);
                Physics2D.IgnoreLayerCollision(8, 8, true);
            }
        }

        protected virtual void Awake()
        {
            blockDuration = 0.75f;
            blockCooldown = 0f;
            if (hurtDuration <= 0f) hurtDuration = 0.15f;
            if (stats != null && stats.hurtDuration > 0f)
            {
                hurtDuration = Mathf.Min(hurtDuration, stats.hurtDuration);
            }
            hurtDuration = Mathf.Min(hurtDuration, 0.15f);
            if (stunImmunityDuration < 0f) stunImmunityDuration = 0.4f;
            if (mapMaxY < 4.8f) mapMaxY = 4.8f;

            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            inputHandler = GetComponent<InputHandler>();
            if (inputProvider == null)
            {
                inputProvider = inputHandler;
            }
            rageManager = GetComponent<RageManager>();

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (rb != null)
            {
                rb.gravityScale = 4.0f;
                defaultGravityScale = rb.gravityScale;
            }

            currentHealth = stats != null ? stats.maxHealth : 100f;

            if (dashTrail != null)
            {
                dashTrail.emitting = false;
            }

            if (transformAuraEffect != null)
            {
                transformAuraEffect.SetActive(false);
            }

            if (blockShieldVisual == null)
            {
                Transform shieldTr = transform.Find("BlockShieldVisual");
                if (shieldTr != null)
                {
                    blockShieldVisual = shieldTr.GetComponent<SpriteRenderer>();
                }
            }

            if (blockShieldVisual != null)
            {
                ConfigureBlockShieldVisual();
                blockShieldVisual.gameObject.SetActive(false);
            }
        }

        private void ConfigureBlockShieldVisual()
        {
            if (blockShieldVisual == null) return;

            if (blockShieldVisual.sprite == null || blockShieldVisual.sprite.bounds.size.x < 0.5f)
            {
                Sprite loaded = LoadBlockShieldSprite();
                if (loaded != null)
                {
                    blockShieldVisual.sprite = loaded;
                }
            }

            Vector3 center = (spriteRenderer != null && spriteRenderer.sprite != null)
                ? spriteRenderer.sprite.bounds.center
                : new Vector3(0f, 0.85f, 0f);
            if (spriteRenderer != null && spriteRenderer.sprite != null && spriteRenderer.transform != transform)
            {
                center = transform.InverseTransformPoint(spriteRenderer.transform.TransformPoint(center));
            }

            blockShieldVisual.transform.localPosition = new Vector3(center.x, center.y, 0f);
            blockShieldVisual.transform.localScale = new Vector3(1.7f, 1.7f, 1f);
            blockShieldBaseColor = new Color(0.85f, 0.98f, 1f, 0.72f);
            blockShieldVisual.color = blockShieldBaseColor;
        }

        private static Sprite LoadBlockShieldSprite()
        {
            if (cachedBlockShieldSprite != null) return cachedBlockShieldSprite;

#if UNITY_EDITOR
            Sprite editorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Combat/FX_BlockShield_Cyan.png");
            if (editorSprite != null)
            {
                cachedBlockShieldSprite = editorSprite;
                return cachedBlockShieldSprite;
            }
#endif

            string[] diskCandidates =
            {
                Path.Combine(Application.dataPath, "Sprites/Combat/FX_BlockShield_Cyan.png"),
                Path.Combine(Application.streamingAssetsPath, "Sprites/Combat/FX_BlockShield_Cyan.png")
            };

            for (int i = 0; i < diskCandidates.Length; i++)
            {
                if (!File.Exists(diskCandidates[i])) continue;
                byte[] bytes = File.ReadAllBytes(diskCandidates[i]);
                if (bytes == null || bytes.Length == 0) continue;

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes, false))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    cachedBlockShieldSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 160f);
                    cachedBlockShieldSprite.name = "FX_BlockShield_Cyan_Runtime";
                    return cachedBlockShieldSprite;
                }
                Destroy(tex);
            }

            const int size = 128;
            Texture2D fallbackTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            fallbackTex.filterMode = FilterMode.Bilinear;
            fallbackTex.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist >= 1f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    float rim = Mathf.Clamp01(1f - Mathf.Abs(dist - 0.86f) * 8f);
                    float fill = Mathf.Clamp01(0.28f * (1f - dist * 0.4f));
                    float alpha = Mathf.Clamp01(Mathf.Max(rim * 0.92f, fill));
                    pixels[y * size + x] = new Color(0.75f, 0.96f, 1f, alpha);
                }
            }
            fallbackTex.SetPixels(pixels);
            fallbackTex.Apply();
            cachedBlockShieldSprite = Sprite.Create(fallbackTex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 80f);
            cachedBlockShieldSprite.name = "FX_BlockShield_Cyan_Fallback";
            return cachedBlockShieldSprite;
        }

        protected virtual void Start()
        {
            if (currentHealth <= 0f)
            {
                currentHealth = stats != null ? stats.maxHealth : 100f;
            }
            OnHealthChanged?.Invoke(currentHealth, stats != null ? stats.maxHealth : 100f);

            // Đăng ký sự kiện từ IInputProvider (mặc định là InputHandler)
            SubscribeInputProvider();

            ChangeState(CharacterState.Idle);
            FindCameraWalls();
        }

        private void SubscribeInputProvider()
        {
            IInputProvider provider = InputProvider;
            if (provider == null) return;
            if (isInputSubscribed && ReferenceEquals(subscribedInputProvider, provider)) return;

            UnsubscribeInputProvider();
            provider.OnMoveEvent += HandleMoveInput;
            provider.OnJumpEvent += HandleJumpInput;
            provider.OnDashEvent += HandleDashInput;
            provider.OnAttackEvent += HandleAttackInput;
            provider.OnSkill1Event += HandleSkill1Input;
            provider.OnSkill2Event += HandleSkill2Input;
            provider.OnTransformEvent += HandleTransformInput;
            provider.OnBlockEvent += HandleBlockInput;
            subscribedInputProvider = provider;
            isInputSubscribed = true;
        }

        private void UnsubscribeInputProvider()
        {
            if (subscribedInputProvider != null)
            {
                subscribedInputProvider.OnMoveEvent -= HandleMoveInput;
                subscribedInputProvider.OnJumpEvent -= HandleJumpInput;
                subscribedInputProvider.OnDashEvent -= HandleDashInput;
                subscribedInputProvider.OnAttackEvent -= HandleAttackInput;
                subscribedInputProvider.OnSkill1Event -= HandleSkill1Input;
                subscribedInputProvider.OnSkill2Event -= HandleSkill2Input;
                subscribedInputProvider.OnTransformEvent -= HandleTransformInput;
                subscribedInputProvider.OnBlockEvent -= HandleBlockInput;
                subscribedInputProvider = null;
            }
            isInputSubscribed = false;
        }

        protected virtual void OnDestroy()
        {
            UnsubscribeInputProvider();
        }

        protected virtual void Update()
        {
            CheckGrounded();
            UpdateBuffAndHazardTimers();
            UpdateFSM();
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
            float clampedY = Mathf.Min(pos.y, mapMaxY);
            transform.position = new Vector3(clampedX, clampedY, pos.z);

            if (rb != null)
            {
                float vx = rb.linearVelocity.x;
                float vy = rb.linearVelocity.y;
                if ((clampedX <= minX + 0.001f && vx < 0f) || (clampedX >= maxX - 0.001f && vx > 0f))
                {
                    vx = 0f;
                }
                if (clampedY >= mapMaxY - 0.001f && vy > 0f)
                {
                    vy = 0f;
                }
                rb.linearVelocity = new Vector2(vx, vy);
            }
        }

        #region Physics & Ground Check
        protected virtual void CheckGrounded()
        {
            Vector2 checkPos = groundCheckPoint != null ? (Vector2)groundCheckPoint.position : (Vector2)transform.position + Vector2.up * 0.02f;
            isGrounded = Physics2D.OverlapCircle(checkPos, groundCheckRadius, groundLayer);
        }
        #endregion

        #region FSM Management
        protected static CombatActorState ToCombatActorState(CharacterState state)
        {
            switch (state)
            {
                case CharacterState.Idle: return CombatActorState.Idle;
                case CharacterState.Run: return CombatActorState.Run;
                case CharacterState.Dash: return CombatActorState.Dash;
                case CharacterState.Attack: return CombatActorState.Attack;
                case CharacterState.Skill: return CombatActorState.Skill;
                case CharacterState.Transform: return CombatActorState.Transform;
                case CharacterState.Hurt: return CombatActorState.Hurt;
                case CharacterState.Dead: return CombatActorState.Dead;
                case CharacterState.Block: return CombatActorState.Block;
                default: return CombatActorState.Idle;
            }
        }

        protected static CombatAttackKind ToCombatAttackKind(AttackType attackType)
        {
            switch (attackType)
            {
                case AttackType.NormalAttack_J: return CombatAttackKind.NormalAttackJ;
                case AttackType.Skill_U: return CombatAttackKind.SkillU;
                case AttackType.Skill_I: return CombatAttackKind.SkillI;
                case AttackType.Combo_DashJ: return CombatAttackKind.ComboDashJ;
                case AttackType.Combo_DashI: return CombatAttackKind.ComboDashI;
                case AttackType.TransformBurst: return CombatAttackKind.TransformBurst;
                case AttackType.EnemyAttack: return CombatAttackKind.EnemyAttack;
                case AttackType.EnemyHeavyAttack: return CombatAttackKind.EnemyHeavyAttack;
                default: return CombatAttackKind.EnemyAttack;
            }
        }

        public virtual void ChangeState(CharacterState newState)
        {
            if (!CombatRuleGuard.CanTransition(ToCombatActorState(currentState), ToCombatActorState(newState)))
            {
                return;
            }

            ExitCurrentState(currentState);
            currentState = newState;
            EnterNewState(newState);

            OnStateChanged?.Invoke(newState);
        }

        protected void SetActionAnimation(CharacterState state, int skillIndex = 0)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetBool(AnimTransformedHash, isTransformed);
                animator.SetInteger(AnimSkillIndexHash, skillIndex);
                animator.SetInteger(AnimStateHash, (int)state);
            }

            ChangeState(state);

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetInteger(AnimSkillIndexHash, skillIndex);
            }
        }

        protected void SetTransformedState(bool transformed)
        {
            if (!transformed && timedTransformCoroutine != null)
            {
                StopCoroutine(timedTransformCoroutine);
                timedTransformCoroutine = null;
                timedTransformEndTime = -1f;
            }
            else if (!transformed)
            {
                timedTransformEndTime = -1f;
            }

            isTransformed = transformed;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetBool(AnimTransformedHash, isTransformed);
            }
            OnTransformedChanged?.Invoke(isTransformed);
        }

        protected void SetTransformedState(bool transformed, float duration)
        {
            if (timedTransformCoroutine != null)
            {
                StopCoroutine(timedTransformCoroutine);
                timedTransformCoroutine = null;
            }
            timedTransformEndTime = -1f;

            SetTransformedState(transformed);

            if (transformed && duration > 0f)
            {
                timedTransformEndTime = Time.time + duration;
                timedTransformCoroutine = StartCoroutine(TimedTransformRoutine(duration));
            }
        }

        private IEnumerator TimedTransformRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            timedTransformCoroutine = null;
            timedTransformEndTime = -1f;

            if (!IsDead && isTransformed)
            {
                OnTimedTransformExpired();
                SetTransformedState(false);
            }
        }

        protected virtual void OnTimedTransformExpired()
        {
            if (transformAuraEffect != null)
            {
                transformAuraEffect.SetActive(false);
            }
        }

        public virtual void AnimEvent_HitboxOn()
        {
            if (meleeHitbox != null)
            {
                Collider2D hbCol = meleeHitbox.GetComponent<Collider2D>();
                if (hbCol != null) hbCol.enabled = true;
            }
        }

        public virtual void AnimEvent_HitboxOff()
        {
            if (meleeHitbox != null)
            {
                meleeHitbox.Deactivate();
            }
        }

        protected virtual void EnterNewState(CharacterState state)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetInteger(AnimStateHash, (int)state);
                animator.SetBool(AnimTransformedHash, isTransformed);
                if (state == CharacterState.Idle || state == CharacterState.Run ||
                    state == CharacterState.Dash || state == CharacterState.Hurt ||
                    state == CharacterState.Dead || state == CharacterState.Block)
                {
                    animator.SetInteger(AnimSkillIndexHash, 0);
                }
            }

            switch (state)
            {
                case CharacterState.Idle:
                    if (rb != null)
                    {
                        ApplyEffectiveGravity();
                        if (isGrounded) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    }
                    break;
                case CharacterState.Run:
                    break;
                case CharacterState.Dash:
                    break;
                case CharacterState.Attack:
                    break;
                case CharacterState.Skill:
                    break;
                case CharacterState.Transform:
                    break;
                case CharacterState.Block:
                    if (rb != null)
                    {
                        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    }
                    if (blockShieldVisual != null)
                    {
                        ConfigureBlockShieldVisual();
                        blockShieldVisual.gameObject.SetActive(true);
                    }
                    break;
                case CharacterState.Hurt:
                    break;
                case CharacterState.Dead:
                    rb.linearVelocity = Vector2.zero;
                    IInputProvider deadInput = InputProvider;
                    if (deadInput != null) deadInput.IsInputBlocked = true;
                    if (dashTrail != null) dashTrail.emitting = false;
                    if (blockShieldVisual != null) blockShieldVisual.gameObject.SetActive(false);
                    break;
            }
        }

        protected virtual void ExitCurrentState(CharacterState state)
        {
            if (state == CharacterState.Attack || state == CharacterState.Skill)
            {
                if (meleeHitbox != null) meleeHitbox.Deactivate();
            }
            else if (state == CharacterState.Block)
            {
                blockTimer = 0f;
                if (blockFlashCoroutine != null)
                {
                    StopCoroutine(blockFlashCoroutine);
                    blockFlashCoroutine = null;
                }
                if (blockShieldVisual != null)
                {
                    blockShieldVisual.color = blockShieldBaseColor;
                    blockShieldVisual.gameObject.SetActive(false);
                }
            }
        }

        protected virtual void UpdateFSM()
        {
            if (currentState == CharacterState.Dead) return;

            if (currentState == CharacterState.Block)
            {
                if (rb != null)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
                blockTimer -= Time.deltaTime;
                if (blockTimer <= 0f)
                {
                    blockTimer = 0f;
                    ChangeState(CharacterState.Idle);
                }
                return;
            }

            // Chuyển đổi trạng thái tự động giữa Idle và Run nếu không bị khóa hành động
            if (CombatRuleGuard.CanPerformAction(ToCombatActorState(currentState), CombatActionType.Move))
            {
                IInputProvider activeInput = InputProvider;
                float h = activeInput != null ? activeInput.HorizontalInput : 0f;
                if (Mathf.Abs(h) > 0.05f)
                {
                    if (currentState != CharacterState.Run) ChangeState(CharacterState.Run);
                    Move(h);
                }
                else
                {
                    if (currentState != CharacterState.Idle) ChangeState(CharacterState.Idle);
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
            }
        }
        #endregion

        #region Buffs, Debuffs & Block Helpers
        protected float GetEffectiveGravityScale() => defaultGravityScale * hazardGravityMultiplier;

        protected void ApplyEffectiveGravity()
        {
            if (rb == null) return;
            if (currentState == CharacterState.Dash || isGravityOverriddenBySkill) return;
            rb.gravityScale = GetEffectiveGravityScale();
        }

        private void UpdateBuffAndHazardTimers()
        {
            if (currentState == CharacterState.Dead) return;

            if (spinalFluidTimer > 0f)
            {
                spinalFluidTimer -= Time.deltaTime;
                if (spinalFluidTimer <= 0f)
                {
                    spinalFluidTimer = 0f;
                    virtualShieldHp = 0f;
                    spinalReflectDamage = 0f;
                    OnShieldChanged?.Invoke(0f, maxVirtualShieldHp);
                }
            }

            if (ultraWaterTimer > 0f)
            {
                ultraWaterTimer -= Time.deltaTime;
                if (ultraWaterRagePerSec > 0f)
                {
                    rageManager?.AddRageFloat(ultraWaterRagePerSec * Time.deltaTime);
                }

                if (ultraWaterTimer <= 0f)
                {
                    ultraWaterTimer = 0f;
                    ultraWaterDamageMultiplier = 1f;
                    ultraWaterSpeedMultiplier = 1f;
                    ultraWaterRagePerSec = 0f;
                }
            }

            float statRageBonus = StatSource.RagePerSecondBonus;
            if (statRageBonus > 0f)
            {
                rageManager?.AddRageFloat(statRageBonus * Time.deltaTime);
            }

            if (hazardSlowTimer > 0f)
            {
                hazardSlowTimer -= Time.deltaTime;
                if (hazardSlowTimer <= 0f)
                {
                    hazardSlowTimer = 0f;
                    hazardSpeedMultiplier = 1f;
                }
            }

            if (hazardGravityTimer > 0f)
            {
                hazardGravityTimer -= Time.deltaTime;
                if (hazardGravityTimer <= 0f)
                {
                    hazardGravityTimer = 0f;
                    hazardGravityMultiplier = 1f;
                    ApplyEffectiveGravity();
                }
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;

            float maxHp = MaxHealth;
            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHp);
            OnHealthChanged?.Invoke(currentHealth, maxHp);
        }

        public void CleanseDebuffs()
        {
            hazardSlowTimer = 0f;
            hazardSpeedMultiplier = 1f;
            hazardGravityTimer = 0f;
            hazardGravityMultiplier = 1f;

            if (rb != null && currentState != CharacterState.Dash && !isGravityOverriddenBySkill)
            {
                rb.gravityScale = defaultGravityScale;
            }

            if (currentState == CharacterState.Hurt)
            {
                if (hurtCoroutine != null)
                {
                    StopCoroutine(hurtCoroutine);
                    hurtCoroutine = null;
                }
                isInvulnerable = false;
                if (spriteRenderer != null) spriteRenderer.color = Color.white;
                ChangeState(CharacterState.Idle);
            }
        }

        public void ActivateSpinalFluidBuff(float duration, float shieldAmount, float reflectDamage)
        {
            if (IsDead) return;

            spinalFluidTimer = Mathf.Max(0f, duration);
            maxVirtualShieldHp = Mathf.Max(0f, shieldAmount);
            virtualShieldHp = maxVirtualShieldHp;
            spinalReflectDamage = Mathf.Max(0f, reflectDamage);
            OnShieldChanged?.Invoke(virtualShieldHp, maxVirtualShieldHp);
        }

        public void ActivateUltraWaterBuff(float duration, float damageMult, float speedMult, float ragePerSecond)
        {
            if (IsDead) return;

            ultraWaterTimer = Mathf.Max(0f, duration);
            ultraWaterDamageMultiplier = damageMult > 0f ? damageMult : 1f;
            ultraWaterSpeedMultiplier = speedMult > 0f ? speedMult : 1f;
            ultraWaterRagePerSec = Mathf.Max(0f, ragePerSecond);
        }

        public void ApplyEnvironmentalModifier(float speedMultiplier, float gravityMultiplier, float duration)
        {
            if (IsDead) return;

            if (duration <= 0f)
            {
                hazardSlowTimer = 0f;
                hazardSpeedMultiplier = 1f;
                hazardGravityTimer = 0f;
                hazardGravityMultiplier = 1f;
                ApplyEffectiveGravity();
                return;
            }

            if (!Mathf.Approximately(speedMultiplier, 1f))
            {
                hazardSpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);
                hazardSlowTimer = duration;
            }

            if (!Mathf.Approximately(gravityMultiplier, 1f))
            {
                hazardGravityMultiplier = Mathf.Max(0.1f, gravityMultiplier);
                hazardGravityTimer = duration;
                ApplyEffectiveGravity();
            }
        }
        #endregion

        #region Movement & Dash Mechanics
        protected virtual float GetCustomSpeedMultiplier() => StatSource.MoveSpeedMultiplier * ultraWaterSpeedMultiplier * hazardSpeedMultiplier;

        protected virtual void Move(float horizontal)
        {
            if (!CombatRuleGuard.CanPerformAction(ToCombatActorState(currentState), CombatActionType.Move)) return;

            float speed = stats != null ? stats.moveSpeed : 6f;
            if (isTransformed && stats != null) speed *= stats.transformSpeedMultiplier;
            speed *= GetCustomSpeedMultiplier();

            rb.linearVelocity = new Vector2(horizontal * speed, rb.linearVelocity.y);

            // Lật hướng nhìn của Stickman
            if (horizontal > 0.05f && facingDirection != 1)
            {
                Flip(1);
            }
            else if (horizontal < -0.05f && facingDirection != -1)
            {
                Flip(-1);
            }
        }

        protected virtual void Flip(int direction)
        {
            facingDirection = direction;
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * direction;
            transform.localScale = scale;
        }

        protected virtual void HandleMoveInput(float horizontal)
        {
            // Được xử lý trong UpdateFSM để giữ tính mượt mà
        }

        protected bool IsDropModifierHeld()
        {
            IInputProvider activeInput = InputProvider;
            return activeInput != null && activeInput.IsDropModifierHeld;
        }

        protected virtual void HandleJumpInput()
        {
            if (!CombatRuleGuard.CanPerformAction(
                ToCombatActorState(currentState),
                CombatActionType.Jump,
                IsBlocking,
                isTransformed,
                IsDropModifierHeld()))
            {
                return;
            }

            if (isGrounded)
            {
                float jump = stats != null ? stats.jumpForce : 12.0f;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jump);
                PlayJumpSfx();
            }
        }

        /// <summary>
        /// Kích hoạt khi Double-tap A hoặc D
        /// </summary>
        protected virtual void HandleDashInput(float direction)
        {
            float cooldown = stats != null ? stats.dashCooldown : 0.5f;
            if (!CombatRuleGuard.CanPerformDash(
                ToCombatActorState(currentState),
                Time.time - lastDashEndTime,
                cooldown,
                IsBlocking))
            {
                return;
            }

            // Đồng bộ hướng dash nếu người chơi bấm ngược hướng nhìn
            int dashDir = direction > 0 ? 1 : -1;
            if (dashDir != facingDirection) Flip(dashDir);

            StartCoroutine(DashRoutine(dashDir));
        }

        protected virtual IEnumerator DashRoutine(int direction)
        {
            ChangeState(CharacterState.Dash);
            PlayDashSfx();

            float duration = stats != null ? stats.dashDuration : 0.25f;
            float speed = stats != null ? stats.dashSpeed : 15f;
            bool invulnerable = stats != null && stats.isInvulnerableDuringDash;

            if (invulnerable) isInvulnerable = true;
            if (dashTrail != null) dashTrail.emitting = true;

            // Tắt trọng lực tạm thời để lướt ngang mượt mà
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(direction * speed, 0f);

            float timer = 0f;
            while (timer < duration)
            {
                // Nếu người chơi bấm phím combo trong khi dash thì trạng thái sẽ chuyển sang Attack/Skill
                if (currentState != CharacterState.Dash)
                {
                    break;
                }
                timer += Time.deltaTime;
                yield return null;
            }

            // Kết thúc Dash
            if (!isGravityOverriddenBySkill)
            {
                rb.gravityScale = GetEffectiveGravityScale();
            }
            if (dashTrail != null) dashTrail.emitting = false;
            if (invulnerable) isInvulnerable = false;
            lastDashEndTime = Time.time;

            if (currentState == CharacterState.Dash)
            {
                ChangeState(CharacterState.Idle);
            }
        }
        #endregion

        #region Combat & Combo Input Handling
        /// <summary>
        /// Bấm J:
        /// - Nếu đang trong Dash -> Combo Cơ Bản (Dash + J)
        /// - Nếu trạng thái bình thường -> Đánh thường J
        /// </summary>
        protected virtual void HandleAttackInput()
        {
            CombatActorState state = ToCombatActorState(currentState);
            if (CombatRuleGuard.CanPerformAction(state, CombatActionType.DashAttackCombo, IsBlocking))
            {
                // COMBO CƠ BẢN: Dash + J
                ExecuteDashAttackCombo();
            }
            else if (CombatRuleGuard.CanPerformAction(state, CombatActionType.NormalAttack, IsBlocking))
            {
                // ĐÁNH THƯỜNG J
                ExecuteNormalAttack();
            }
        }

        /// <summary>
        /// Bấm U: Kỹ năng 1 (Tốn 0 nộ)
        /// </summary>
        protected virtual void HandleSkill1Input()
        {
            CombatActorState state = ToCombatActorState(currentState);
            if (CombatRuleGuard.CanPerformAction(state, CombatActionType.Skill1, IsBlocking))
            {
                ExecuteSkill1();
            }
        }

        /// <summary>
        /// Bấm I:
        /// - Nếu đang trong Dash VÀ Đã Biến Hình VÀ Đầy 30 Nộ -> Combo Đặc Biệt (Dash + I)
        /// - Nếu bình thường VÀ Đủ 10 Nộ -> Kỹ năng 2 (Skill 2)
        /// </summary>
        protected virtual void HandleSkill2Input()
        {
            CombatActorState state = ToCombatActorState(currentState);
            if (CombatRuleGuard.CanPerformAction(state, CombatActionType.SpecialCombo, IsBlocking))
            {
                // COMBO ĐẶC BIỆT: Dash + I (Yêu cầu: Đã biến hình + Đầy 30 nộ)
                if (rageManager != null && CombatRuleGuard.CanExecuteSkillWithRage(state, CombatActionType.SpecialCombo, rageManager.RuleEngine, isTransformed, IsBlocking))
                {
                    rageManager.TryConsumeRage(RageManager.SPECIAL_COMBO_COST);
                    ExecuteSpecialCombo();
                }
                else
                {
                    int currentRage = rageManager != null ? rageManager.CurrentRage : 0;
                    Debug.Log($"<color=yellow>[Combo Đặc Biệt Locked]</color> Cần: Biến Hình ({isTransformed}) & Đầy 30 Nộ (Hiện tại: {currentRage}/30)");
                }
            }
            else if (CombatRuleGuard.CanPerformAction(state, CombatActionType.Skill2, IsBlocking))
            {
                // KỸ NĂNG 2: Tốn 10 Nộ
                if (rageManager != null && CombatRuleGuard.CanExecuteSkillWithRage(state, CombatActionType.Skill2, rageManager.RuleEngine, isTransformed, IsBlocking))
                {
                    rageManager.TryConsumeRage(RageManager.SKILL_I_COST);
                    ExecuteSkill2();
                }
                else
                {
                    Debug.Log("<color=yellow>[Skill I Locked]</color> Không đủ 10 nộ!");
                }
            }
        }

        /// <summary>
        /// Bấm Phím Biến Hình: Yêu cầu Đầy 30 Nộ
        /// </summary>
        protected virtual void HandleTransformInput()
        {
            CombatActorState state = ToCombatActorState(currentState);
            if (!CombatRuleGuard.CanPerformAction(state, CombatActionType.Transform, IsBlocking, isTransformed))
            {
                return;
            }

            if (rageManager != null && CombatRuleGuard.CanExecuteSkillWithRage(state, CombatActionType.Transform, rageManager.RuleEngine, isTransformed, IsBlocking))
            {
                rageManager.TryConsumeRage(RageManager.TRANSFORM_COST);
                ExecuteTransform();
            }
            else
            {
                int currentRage = rageManager != null ? rageManager.CurrentRage : 0;
                Debug.Log($"<color=yellow>[Transform Locked]</color> Cần 30 nộ để biến hình! Hiện tại: {currentRage}/30");
            }
        }

        /// <summary>
        /// Bấm Phím S: Đỡ đòn trong 0.75s (Giảm 80% sát thương, miễn nhiễm Hit-stun, hồi chiêu 0s)
        /// </summary>
        protected virtual void HandleBlockInput()
        {
            if (!CombatRuleGuard.CanPerformBlock(ToCombatActorState(currentState), Time.time - lastBlockTime, blockCooldown))
            {
                return;
            }

            lastBlockTime = Time.time;
            blockTimer = blockDuration;
            ChangeState(CharacterState.Block);
        }
        #endregion

        #region Abstract / Virtual Methods for Concrete Characters
        /// <summary>
        /// Đánh thường (Phím J)
        /// </summary>
        public abstract void ExecuteNormalAttack();

        /// <summary>
        /// Kỹ năng 1 (Phím U) - Tốn 0 nộ
        /// </summary>
        public abstract void ExecuteSkill1();

        /// <summary>
        /// Kỹ năng 2 (Phím I) - Tốn 10 nộ
        /// </summary>
        public abstract void ExecuteSkill2();

        /// <summary>
        /// Combo Cơ Bản (Dash + J)
        /// </summary>
        public abstract void ExecuteDashAttackCombo();

        /// <summary>
        /// Biến hình (Phím Transform, tốn 30 nộ)
        /// </summary>
        public abstract void ExecuteTransform();

        /// <summary>
        /// Combo Đặc Biệt (Dash + I) - Sau biến hình & 30 nộ
        /// </summary>
        public abstract void ExecuteSpecialCombo();

        protected static void PlayCharacterSfx(string resourceSubPath, float volumeScale = 1f, float minInterval = 0f)
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(resourceSubPath)) return;
            AudioManager.Instance?.PlaySfxByResourceName(resourceSubPath, volumeScale, minInterval);
        }

        protected virtual void PlayJumpSfx() { }
        protected virtual void PlayDashSfx() { }
        protected virtual void PlayHurtSfx(DamageData damageData, CombatDamageResult result) { }
        protected virtual void PlayDieSfx() { }
        protected virtual void PlayAttackHitSfx(AttackType attackType, IDamageable target) { }
        #endregion

        #region Combat Hit Notification
        /// <summary>
        /// Thông báo nhân vật đã đánh trúng mục tiêu (tích Nộ và kích hoạt event).
        /// </summary>
        public virtual void OnHitEnemy(AttackType attackType, IDamageable target = null)
        {
            PlayAttackHitSfx(attackType, target);
            rageManager?.AddRageFromHit(attackType, HitRageAmount);
            OnEnemyHit?.Invoke(attackType, target);
        }

        /// <summary>
        /// Gọi khi tiêu diệt thành công một kẻ địch để cộng thưởng Nộ (+2.0 Rage / kill).
        /// </summary>
        public virtual void OnKillEnemy(IDamageable target = null)
        {
            rageManager?.AddRageFromKill();
        }

        /// <summary>
        /// Helper tĩnh định tuyến sự kiện đánh trúng từ Hitbox/Projectile/Combat Effects về BaseCharacter.
        /// </summary>
        public static void NotifyAttackerHit(GameObject attacker, AttackType attackType, IDamageable target = null)
        {
            if (attacker == null) return;

            BaseCharacter character = attacker.GetComponent<BaseCharacter>();
            if (character == null)
            {
                character = attacker.GetComponentInParent<BaseCharacter>();
            }

            if (character != null)
            {
                character.OnHitEnemy(attackType, target);
            }
            else
            {
                attacker.GetComponent<RageManager>()?.AddRageFromHit(attackType);
            }
        }

        /// <summary>
        /// Helper tĩnh cộng thưởng Nộ khi Player kết liễu một Enemy.
        /// </summary>
        public static void NotifyAttackerKill(GameObject attacker, IDamageable target = null)
        {
            if (attacker == null) return;

            BaseCharacter character = attacker.GetComponent<BaseCharacter>();
            if (character == null)
            {
                character = attacker.GetComponentInParent<BaseCharacter>();
            }

            if (character != null)
            {
                character.OnKillEnemy(target);
            }
            else
            {
                attacker.GetComponent<RageManager>()?.AddRageFromKill();
            }
        }
        #endregion

        #region Damage & Life Cycle (IDamageable)
        protected bool IsInTrainingMode()
        {
            return (TrainingModeManager.Instance != null && TrainingModeManager.Instance.IsActive)
                || StageManager.CurrentGameMode == GameMode.Training
                || (LevelManager.Instance != null && LevelManager.Instance.CurrentGameMode == GameMode.Training);
        }

        public virtual void TakeDamage(DamageData damageData)
        {
            bool hasAttacker = damageData.attacker != null;
            float attackerDistance = hasAttacker
                ? Vector2.Distance(transform.position, damageData.attacker.transform.position)
                : float.PositiveInfinity;

            CombatDamageInput damageInput = new CombatDamageInput
            {
                RawDamage = damageData.damageAmount,
                CurrentHealth = currentHealth,
                MaxHealth = MaxHealth,
                CurrentVirtualShield = virtualShieldHp,
                BlockDamageMultiplier = blockDamageMultiplier,
                BonusReflectDamage = spinalReflectDamage,
                IsDead = IsDead,
                IsInvulnerable = TrainingInvincible || isInvulnerable || isTrainingRespawnInvulnerable,
                IsBlocking = IsBlocking,
                IsStunImmune = Time.time < stunImmunityUntilTime,
                IsSkillActive = CurrentState == CharacterState.Skill,
                IsTransforming = CurrentState == CharacterState.Transform,
                AttackKind = ToCombatAttackKind(damageData.attackType),
                HasAttacker = hasAttacker,
                IsSelfAttack = hasAttacker && damageData.attacker == gameObject,
                IsAlreadyReflecting = isReflectingDamage,
                AttackerDistance = attackerDistance,
                MaxReflectRange = DamageCalculator.DEFAULT_MAX_REFLECT_RANGE
            };

            CombatDamageResult result = DamageCalculator.CalculateIncomingDamage(damageInput, StatSource);
            if (result.WasIgnored) return;

            bool blocking = result.WasBlocked;
            bool blockingState = blocking || CurrentState == CharacterState.Block;

            Vector3 hitPos = blockingState && blockShieldVisual != null
                ? HitSparkManager.ResolveHitPoint(transform, null, blockShieldVisual, damageData.attacker)
                : HitSparkManager.ResolveHitPoint(transform, col, spriteRenderer, damageData.attacker);

            if (blockingState)
            {
                HitSparkManager.Spawn(hitPos, HitSparkManager.ShieldBlockCyanColor, 1.25f);
            }
            else
            {
                HitSparkManager.Spawn(hitPos);
            }

            if (blocking)
            {
                TriggerBlockShieldFlash();
            }

            if (result.ShieldWasActive)
            {
                virtualShieldHp = result.RemainingVirtualShield;
                OnShieldChanged?.Invoke(virtualShieldHp, maxVirtualShieldHp);

                if (result.ShouldReflectDamage)
                {
                    TryReflectSpinalDamage(damageData, result.ReflectedDamage);
                }

                if (result.ShieldDepleted)
                {
                    spinalFluidTimer = 0f;
                    spinalReflectDamage = 0f;
                }
            }
            else if (result.ShouldReflectDamage)
            {
                TryReflectSpinalDamage(damageData, result.ReflectedDamage);
            }

            if (result.FinalHealthDamage > 0f)
            {
                currentHealth = result.RemainingHealth;
                OnHealthChanged?.Invoke(currentHealth, MaxHealth);
            }

            Vector3 popupPos = transform.position + Vector3.up * 1.5f;
            if (blocking)
            {
                DamagePopupManager.SpawnBlocked(popupPos, result.FinalHealthDamage);
            }
            else if (result.FinalHealthDamage > 0f)
            {
                DamagePopupManager.SpawnPlayerHit(popupPos, result.FinalHealthDamage);
            }

            if (!result.IsFatal && !blocking && (result.FinalHealthDamage > 0f || result.ShouldTriggerHitStun))
            {
                PlayHurtSfx(damageData, result);
            }

            if (result.IsFatal)
            {
                if (IsInTrainingMode())
                {
                    if (TrainingModeManager.Instance != null)
                    {
                        TrainingModeManager.Instance.OnPlayerFatalDamageInTraining(this);
                    }
                    else
                    {
                        RespawnPlayerFullState();
                    }
                    return;
                }

                Die();
                return;
            }

            if (result.ShouldTriggerHitStun)
            {
                stunImmunityUntilTime = Time.time + hurtDuration + stunImmunityDuration;
                if (hurtCoroutine != null)
                {
                    StopCoroutine(hurtCoroutine);
                }
                hurtCoroutine = StartCoroutine(HurtRoutine(damageData.knockbackForce));
            }
        }

        private void TryReflectSpinalDamage(DamageData damageData, float reflectAmount)
        {
            if (isReflectingDamage) return;
            if (reflectAmount <= 0f || damageData.attacker == null || damageData.attacker == gameObject) return;

            IDamageable attackerDmg = damageData.attacker.GetComponent<IDamageable>()
                ?? damageData.attacker.GetComponentInParent<IDamageable>();
            if (attackerDmg == null || attackerDmg.IsDead || attackerDmg.Transform == transform) return;

            isReflectingDamage = true;
            try
            {
                DamageData reflectData = new DamageData(
                    reflectAmount,
                    Vector2.zero,
                    AttackType.TransformBurst,
                    false,
                    gameObject
                );
                attackerDmg.TakeDamage(reflectData);
            }
            finally
            {
                isReflectingDamage = false;
            }
        }

        private void TriggerBlockShieldFlash()
        {
            if (blockShieldVisual == null || !blockShieldVisual.gameObject.activeInHierarchy) return;
            if (blockFlashCoroutine != null)
            {
                StopCoroutine(blockFlashCoroutine);
            }
            blockFlashCoroutine = StartCoroutine(BlockShieldFlashRoutine());
        }

        private IEnumerator BlockShieldFlashRoutine()
        {
            blockShieldVisual.color = new Color(0.75f, 1f, 1f, 1f);
            yield return new WaitForSeconds(0.1f);
            if (blockShieldVisual != null)
            {
                blockShieldVisual.color = blockShieldBaseColor;
            }
            blockFlashCoroutine = null;
        }

        protected virtual IEnumerator HurtRoutine(Vector2 knockback)
        {
            ChangeState(CharacterState.Hurt);
            isInvulnerable = true;

            if (rb != null)
            {
                ApplyEffectiveGravity();
                // Nhận lực đẩy lùi
                rb.linearVelocity = knockback;
            }

            // Hiệu ứng nhấp nháy đỏ khi bị thương trong thời gian hurtDuration (0.15s)
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.red;
            }

            yield return new WaitForSeconds(hurtDuration);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }

            isInvulnerable = false;
            hurtCoroutine = null;

            // Phục hồi quyền điều khiển ngay lập tức sau hurtDuration (0.15s)
            if (CurrentState == CharacterState.Hurt && CurrentState != CharacterState.Dead)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        protected virtual void Die()
        {
            if (IsInTrainingMode())
            {
                RespawnPlayerFullState();
                return;
            }

            if (hurtCoroutine != null)
            {
                StopCoroutine(hurtCoroutine);
                hurtCoroutine = null;
            }
            if (timedTransformCoroutine != null)
            {
                StopCoroutine(timedTransformCoroutine);
                timedTransformCoroutine = null;
            }
            if (blockFlashCoroutine != null)
            {
                StopCoroutine(blockFlashCoroutine);
                blockFlashCoroutine = null;
            }
            if (trainingRespawnBlinkCoroutine != null)
            {
                StopCoroutine(trainingRespawnBlinkCoroutine);
                trainingRespawnBlinkCoroutine = null;
            }

            isInvulnerable = false;
            isTrainingRespawnInvulnerable = false;
            stunImmunityUntilTime = -999f;
            spinalFluidTimer = 0f;
            virtualShieldHp = 0f;
            spinalReflectDamage = 0f;
            ultraWaterTimer = 0f;
            ultraWaterDamageMultiplier = 1f;
            ultraWaterSpeedMultiplier = 1f;
            ultraWaterRagePerSec = 0f;
            OnShieldChanged?.Invoke(0f, maxVirtualShieldHp);

            ChangeState(CharacterState.Dead);
            PlayDieSfx();
            rb.linearVelocity = Vector2.zero;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.gray;
            }
            Debug.Log($"<color=red>[GAME OVER]</color> {gameObject.name} đã ngã xuống!");
        }

        /// <summary>
        /// Hồi sinh tức thì ở chế độ Training Mode với 100% HP, đầy Nộ, xóa mọi trạng thái khựng/tấn công,
        /// đưa Animator về thẳng Idle và bật nháy sáng bất tử nhẹ 1.0s không khóa điều khiển.
        /// </summary>
        public void RespawnPlayerFullState()
        {
            bool keepTimedTransform = timedTransformCoroutine != null && isTransformed && timedTransformEndTime > Time.time;
            float remainingTransformTime = keepTimedTransform ? (timedTransformEndTime - Time.time) : 0f;

            ExitCurrentState(currentState);

            if (trainingRespawnBlinkCoroutine != null)
            {
                StopCoroutine(trainingRespawnBlinkCoroutine);
                trainingRespawnBlinkCoroutine = null;
            }

            StopAllCoroutines();

            hurtCoroutine = null;
            blockFlashCoroutine = null;
            timedTransformCoroutine = null;

            isInvulnerable = false;
            isTrainingRespawnInvulnerable = true;
            isGravityOverriddenBySkill = false;
            isReflectingDamage = false;

            blockTimer = 0f;
            lastBlockTime = -10f;
            lastDashEndTime = -10f;
            stunImmunityUntilTime = -999f;

            hazardSlowTimer = 0f;
            hazardSpeedMultiplier = 1f;
            hazardGravityTimer = 0f;
            hazardGravityMultiplier = 1f;

            spinalFluidTimer = 0f;
            virtualShieldHp = 0f;
            maxVirtualShieldHp = 0f;
            spinalReflectDamage = 0f;
            OnShieldChanged?.Invoke(0f, 0f);

            if (meleeHitbox != null)
            {
                meleeHitbox.Deactivate();
            }

            if (dashTrail != null)
            {
                dashTrail.emitting = false;
            }

            if (blockShieldVisual != null)
            {
                blockShieldVisual.color = blockShieldBaseColor;
                blockShieldVisual.gameObject.SetActive(false);
            }

            if (col != null)
            {
                col.enabled = true;
            }

            IInputProvider respawnInput = InputProvider;
            if (respawnInput != null)
            {
                respawnInput.IsInputBlocked = false;
            }

            currentHealth = MaxHealth;
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);

            rageManager?.FillMaxRage();

            transform.position = new Vector3(0f, GROUND_Y, 0f);
            isGrounded = true;

            if (rb != null)
            {
                rb.gravityScale = GetEffectiveGravityScale();
                rb.linearVelocity = Vector2.zero;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }

            currentState = CharacterState.Idle;
            EnterNewState(CharacterState.Idle);

            if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            {
                animator.SetInteger(AnimStateHash, (int)CharacterState.Idle);
                animator.SetBool(AnimTransformedHash, isTransformed);
                animator.SetInteger(AnimSkillIndexHash, 0);

                string idleStateName = isTransformed ? "Trans_Idle" : "Normal_Idle";
                int idleStateHash = Animator.StringToHash(idleStateName);
                if (animator.HasState(0, idleStateHash))
                {
                    animator.Play(idleStateHash, 0, 0f);
                }
                animator.Update(0f);
            }

            OnStateChanged?.Invoke(CharacterState.Idle);

            if (gameObject.activeInHierarchy)
            {
                if (keepTimedTransform && remainingTransformTime > 0f)
                {
                    timedTransformCoroutine = StartCoroutine(TimedTransformRoutine(remainingTransformTime));
                }

                trainingRespawnBlinkCoroutine = StartCoroutine(TrainingRespawnInvulnerabilityBlinkRoutine());
            }
            else
            {
                isTrainingRespawnInvulnerable = false;
            }
        }

        private IEnumerator TrainingRespawnInvulnerabilityBlinkRoutine()
        {
            isTrainingRespawnInvulnerable = true;
            const float blinkDuration = 1.0f;
            const float toggleInterval = 0.1f;
            float endTime = Time.time + blinkDuration;
            bool faded = false;

            while (Time.time < endTime)
            {
                if (IsDead) break;

                if (spriteRenderer != null)
                {
                    faded = !faded;
                    Color c = spriteRenderer.color;
                    c.a = faded ? 0.35f : 1f;
                    spriteRenderer.color = c;
                }

                float remaining = endTime - Time.time;
                if (remaining <= 0f) break;
                yield return new WaitForSeconds(Mathf.Min(toggleInterval, remaining));
            }

            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = 1f;
                spriteRenderer.color = c;
            }

            isTrainingRespawnInvulnerable = false;
            trainingRespawnBlinkCoroutine = null;
        }
        #endregion

        private void OnDrawGizmosSelected()
        {
            if (groundCheckPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
            }
        }
    }
}
