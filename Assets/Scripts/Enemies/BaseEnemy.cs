using System;
using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Economy;
using StickmanBeatEmUp.Enemies.Stats;
using StickmanGame.VFX;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// Lớp cơ sở trừu tượng (Abstract Base Class) cho toàn bộ kẻ thù trong game (Grunt, Ranger, Kamikaze, Elite, Boss).
    /// Triển khai FSM, Logic "Telegraphing" (nhấp nháy đỏ 0.5s trước khi ra đòn),
    /// Cơ chế Super Armor (chỉ khựng bởi U, I, Combo), và quản lý Máu/Chết.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public abstract class BaseEnemy : MonoBehaviour, IDamageable, ICombatTargetProfile, IFreezable
    {
        [Header("Stats Config (ScriptableObject)")]
        [SerializeField] protected EnemyStatsSO stats;

        [Header("References")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected Hitbox attackHitbox;
        [SerializeField] protected GameObject telegraphIndicator; // Biểu tượng dấu chấm than (!) hoặc vòng tròn cảnh báo
        [SerializeField] protected LayerMask playerLayer;

        // Core Components
        protected Rigidbody2D rb;
        protected Collider2D col;
        protected Transform playerTransform;

        // FSM & Combat Variables
        protected EnemyState currentState = EnemyState.Idle;
        protected float currentHealth;
        protected int facingDirection = -1; // Mặc định quay mặt sang trái hướng về người chơi
        protected float lastAttackTime = -10f;
        protected Color originalSpriteColor = Color.white;
        protected bool isTelegraphing = false;

        private static Material whiteFlashMaterial;
        private Material defaultSharedMaterial;
        protected Coroutine whiteFlashRoutine;
        protected bool suppressCoinsAndDeathEvents;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            whiteFlashMaterial = null;
        }

        // Properties công khai (IDamageable & ICombatTargetProfile)
        public Transform Transform => transform;
        public bool IsDead => currentState == EnemyState.Dead;
        public virtual bool IsEliteOrBoss => false;
        public EnemyState CurrentState => currentState;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => stats != null ? stats.maxHealth : 50f;

        // Sự kiện báo về LevelManager
        public event Action<BaseEnemy> OnDeathEvent;
        public event Action<float, float> OnHealthChangedEvent;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                originalSpriteColor = spriteRenderer.color;
                defaultSharedMaterial = spriteRenderer.sharedMaterial;
            }

            if (telegraphIndicator != null) telegraphIndicator.SetActive(false);
            currentHealth = stats != null ? stats.maxHealth : 50f;
        }

        protected virtual void Start()
        {
            if (currentHealth <= 0f)
            {
                currentHealth = stats != null ? stats.maxHealth : 50f;
            }
            FindPlayer();
            ChangeState(EnemyBase.GlobalEnemyAiEnabled ? EnemyState.Chase : EnemyState.Idle);
        }

        protected virtual void Update()
        {
            if (currentState == EnemyState.Dead || currentState == EnemyState.Stunned) return;

            if (playerTransform == null)
            {
                FindPlayer();
                return;
            }

            if (!EnemyBase.GlobalEnemyAiEnabled)
            {
                if (currentState != EnemyState.Hurt && rb != null && Mathf.Abs(rb.linearVelocity.y) < 0.15f)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
                if (currentState != EnemyState.Hurt && currentState != EnemyState.Idle)
                {
                    ChangeState(EnemyState.Idle);
                }
                return;
            }

            UpdateFacingDirection();
            UpdateFSM();
        }

        protected virtual void FindPlayer()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }

        protected virtual void UpdateFacingDirection()
        {
            if (playerTransform == null || currentState == EnemyState.Attack || isTelegraphing) return;

            float dirX = playerTransform.position.x - transform.position.x;
            if (dirX > 0.1f && facingDirection != 1)
            {
                Flip(1);
            }
            else if (dirX < -0.1f && facingDirection != -1)
            {
                Flip(-1);
            }
        }

        protected virtual void Flip(int dir)
        {
            facingDirection = dir;
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * dir;
            transform.localScale = s;
        }

        #region FSM Management
        public virtual void ChangeState(EnemyState newState)
        {
            if (currentState == EnemyState.Dead) return;
            if (currentState == newState) return;

            ExitState(currentState);
            currentState = newState;
            EnterState(newState);
        }

        protected virtual void EnterState(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Idle:
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    break;
                case EnemyState.Chase:
                    break;
                case EnemyState.Flee:
                    break;
                case EnemyState.Telegraph:
                    rb.linearVelocity = Vector2.zero;
                    StartCoroutine(TelegraphRoutine());
                    break;
                case EnemyState.Attack:
                    break;
                case EnemyState.Hurt:
                    break;
                case EnemyState.Stunned:
                    rb.linearVelocity = Vector2.zero;
                    break;
                case EnemyState.Dead:
                    rb.linearVelocity = Vector2.zero;
                    col.enabled = false;
                    if (spriteRenderer != null)
                    {
                        if (defaultSharedMaterial != null) spriteRenderer.sharedMaterial = defaultSharedMaterial;
                        spriteRenderer.color = Color.gray;
                    }
                    Die();
                    break;
            }
        }

        public void DestroySilentlyWithoutReward()
        {
            suppressCoinsAndDeathEvents = true;
            currentState = EnemyState.Dead;
            StopAllCoroutines();
            whiteFlashRoutine = null;
            if (attackHitbox != null) attackHitbox.Deactivate();
            if (telegraphIndicator != null) telegraphIndicator.SetActive(false);
            if (col != null) col.enabled = false;
            if (rb != null) rb.linearVelocity = Vector2.zero;
            Destroy(gameObject);
        }

        protected virtual void Die()
        {
            if (!suppressCoinsAndDeathEvents && CurrencyManager.Instance != null && !CurrencyManager.Instance.SuppressEnemyRewards)
            {
                int reward = CurrencyManager.REWARD_NORMAL_CREEP;
                if (this is BossController)
                {
                    reward = CurrencyManager.REWARD_BOSS;
                }
                else if (stats != null && !string.IsNullOrEmpty(stats.enemyName) &&
                         stats.enemyName.IndexOf("Saibaman", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    reward = CurrencyManager.REWARD_SAIBAMAN;
                }
                else if (this is EliteBruiserEnemy || (stats != null && stats.hasSuperArmor))
                {
                    reward = CurrencyManager.REWARD_ELITE;
                }
                CurrencyManager.RewardEnemyKill(reward, transform.position);
            }

            if (!suppressCoinsAndDeathEvents)
            {
                OnDeathEvent?.Invoke(this);
            }
            Destroy(gameObject, 1.2f);
        }

        protected virtual void OnDisable()
        {
            if (spriteRenderer != null && defaultSharedMaterial != null)
            {
                spriteRenderer.sharedMaterial = defaultSharedMaterial;
            }
        }

        protected virtual void ExitState(EnemyState state)
        {
            if (state == EnemyState.Telegraph)
            {
                isTelegraphing = false;
                if (spriteRenderer != null) spriteRenderer.color = originalSpriteColor;
                if (telegraphIndicator != null) telegraphIndicator.SetActive(false);
            }
            else if (state == EnemyState.Attack)
            {
                if (attackHitbox != null) attackHitbox.Deactivate();
            }
        }

        /// <summary>
        /// Logic cập nhật FSM riêng của từng loài quái
        /// </summary>
        protected abstract void UpdateFSM();
        #endregion

        #region Telegraphing & Attack Mechanism
        /// <summary>
        /// Logic Telegraphing: Quái nhấp nháy đỏ 0.5s trước khi tung đòn để người chơi kịp né đòn/lướt
        /// </summary>
        protected virtual IEnumerator TelegraphRoutine()
        {
            isTelegraphing = true;
            if (telegraphIndicator != null) telegraphIndicator.SetActive(true);

            float duration = stats != null ? stats.telegraphDuration : 0.5f;
            float elapsed = 0f;
            float flashInterval = 0.1f;
            bool isRed = false;

            while (elapsed < duration)
            {
                if (currentState != EnemyState.Telegraph) yield break;

                isRed = !isRed;
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = isRed ? Color.red : originalSpriteColor;
                }

                yield return new WaitForSeconds(flashInterval);
                elapsed += flashInterval;
            }

            if (spriteRenderer != null) spriteRenderer.color = originalSpriteColor;
            if (telegraphIndicator != null) telegraphIndicator.SetActive(false);

            isTelegraphing = false;

            // Chuyển sang thực hiện đòn tấn công thực sự
            if (currentState == EnemyState.Telegraph)
            {
                ChangeState(EnemyState.Attack);
                StartCoroutine(AttackRoutine());
            }
        }

        /// <summary>
        /// Coroutine thực hiện đòn đánh tương ứng của từng loại quái
        /// </summary>
        protected abstract IEnumerator AttackRoutine();
        #endregion

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
            spriteRenderer.color = originalSpriteColor;
            whiteFlashRoutine = null;
        }

        #region Damage Handling & Super Armor
        public virtual void TakeDamage(DamageData damageData)
        {
            if (currentState == EnemyState.Dead) return;
            Vector3 hitPos = HitSparkManager.ResolveHitPoint(transform, col, spriteRenderer, damageData.attacker);
            HitSparkManager.Spawn(hitPos);

            float minHealth = EnemyBase.GlobalEnemyInvincible ? 1f : 0f;
            currentHealth -= damageData.damageAmount;
            currentHealth = Mathf.Max(minHealth, currentHealth);
            OnHealthChangedEvent?.Invoke(currentHealth, stats != null ? stats.maxHealth : 50f);

            if (!EnemyBase.GlobalEnemyInvincible && currentHealth <= 0)
            {
                StickmanBeatEmUp.Characters.BaseCharacter.NotifyAttackerKill(damageData.attacker, this);
                ChangeState(EnemyState.Dead);
                return;
            }

            bool hasArmor = stats != null && stats.hasSuperArmor;
            bool shouldStagger = !hasArmor || damageData.breaksSuperArmor;

            if (!shouldStagger)
            {
                TriggerWhiteFlash();
                return;
            }

            StopAllCoroutines();
            isTelegraphing = false;
            if (telegraphIndicator != null) telegraphIndicator.SetActive(false);
            TriggerWhiteFlash();
            StartCoroutine(HurtRoutine(damageData.knockbackForce * 0.35f));
        }

        protected virtual IEnumerator HurtRoutine(Vector2 knockback)
        {
            ChangeState(EnemyState.Hurt);
            rb.linearVelocity = knockback;

            const float MicroHitStunDuration = 0.25f;
            yield return new WaitForSeconds(MicroHitStunDuration);

            if (currentState == EnemyState.Hurt)
            {
                ChangeState(EnemyState.Chase);
            }
        }

        /// <summary>
        /// Hiệu ứng Đóng Băng / Đứng Im (Dùng cho combo Dark Kiva mở cánh)
        /// </summary>
        public virtual void Freeze(float duration)
        {
            if (currentState == EnemyState.Dead) return;
            StopAllCoroutines();
            isTelegraphing = false;
            if (telegraphIndicator != null) telegraphIndicator.SetActive(false);
            if (attackHitbox != null) attackHitbox.Deactivate();
            StartCoroutine(FreezeRoutine(duration));
        }

        protected virtual IEnumerator FreezeRoutine(float duration)
        {
            ChangeState(EnemyState.Stunned);
            rb.linearVelocity = Vector2.zero;

            Color prev = spriteRenderer != null ? spriteRenderer.color : Color.white;
            if (spriteRenderer != null) spriteRenderer.color = new Color(0.2f, 0.4f, 1f, 1f); // Màu tím/xanh băng giá

            yield return new WaitForSeconds(duration);

            if (spriteRenderer != null) spriteRenderer.color = originalSpriteColor;
            if (currentState == EnemyState.Stunned)
            {
                ChangeState(EnemyState.Chase);
            }
        }
        #endregion
    }
}
