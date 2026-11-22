using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Combat;

namespace StickmanBeatEmUp.Enemies
{
    public enum BossPhase
    {
        Phase1_Grounded,
        PhaseTransition,
        Phase2_Airborne
    }

    /// <summary>
    /// BossController: Quản lý Boss cuối thế giới với FSM 2 Phase:
    /// - Phase 1: HP trâu, Super Armor, triệu hồi liên tục quái Kamikaze, bắn Lazer quét mặt đất.
    /// - Phase 2 (< 50% HP): Lơ lửng trên không trung, dội mưa bom AOE diện rộng đỏ rực mặt đất,
    ///   buộc Player phải Dash liên tục để né tránh và phản công bằng Kỹ năng (U, I, Combo).
    /// </summary>
    public class BossController : BaseEnemy
    {
        [Header("Boss Phases & Logic")]
        [SerializeField] private BossPhase currentPhase = BossPhase.Phase1_Grounded;
        [SerializeField] private float hoverHeight = 4.5f;

        [Header("Phase 1 Attacks")]
        [SerializeField] private GameObject groundLaserPrefab;
        [SerializeField] private GameObject kamikazeMinionPrefab;
        [SerializeField] private Transform minionSpawnPoint;
        [SerializeField] private float minionSummonCooldown = 9f;

        [Header("Phase 2 AOE Attacks")]
        [SerializeField] private GameObject carpetBombExplosionPrefab;
        [SerializeField] private GameObject aoeWarningIndicatorPrefab;
        [SerializeField] private int aoeBombCount = 4;
        [SerializeField] private float aoeCooldown = 4f;

        [Header("Boss Visuals")]
        [SerializeField] private GameObject phase2AuraVfx;
        [SerializeField] private Color phase2Color = new Color(0.8f, 0.1f, 0.1f, 1f);

        private float nextSummonTime = 0f;
        private float nextAoeTime = 0f;
        private bool hasTriggeredPhase2 = false;
        private Vector3 groundOriginPos;

        public BossPhase CurrentPhase => currentPhase;
        public override bool IsEliteOrBoss => true;

        protected override void Awake()
        {
            base.Awake();
            transform.localScale = new Vector3(1.6f, 1.6f, 1f); // Kích thước Boss hoành tráng
            groundOriginPos = transform.position;
            if (phase2AuraVfx != null) phase2AuraVfx.SetActive(false);
        }

        protected override void Start()
        {
            base.Start();
            nextSummonTime = Time.time + 4f;
            nextAoeTime = Time.time + 2f;
        }

        protected override void UpdateFSM()
        {
            if (currentState == EnemyState.Dead || currentState == EnemyState.Hurt ||
                currentState == EnemyState.Stunned || currentState == EnemyState.Telegraph ||
                currentState == EnemyState.Attack) return;

            // Kiểm tra chuyển Phase khi HP < 50%
            if (!hasTriggeredPhase2 && currentHealth <= (stats.maxHealth * 0.5f))
            {
                TriggerPhase2Transition();
                return;
            }

            if (currentPhase == BossPhase.Phase1_Grounded)
            {
                HandlePhase1Logic();
            }
            else if (currentPhase == BossPhase.Phase2_Airborne)
            {
                HandlePhase2Logic();
            }
        }

        #region Phase 1 Logic
        private void HandlePhase1Logic()
        {
            // Triệu hồi Kamikaze hỗ trợ định kỳ
            if (Time.time >= nextSummonTime)
            {
                SummonKamikazeMinion();
                nextSummonTime = Time.time + minionSummonCooldown;
            }

            float dist = Vector2.Distance(transform.position, playerTransform.position);

            if (dist <= stats.attackRange)
            {
                // Đòn cận chiến
                if (Time.time >= lastAttackTime + stats.attackCooldown)
                {
                    ChangeState(EnemyState.Telegraph);
                }
            }
            else if (dist > 5f && Time.time >= lastAttackTime + stats.attackCooldown)
            {
                // Bắn lazer quét mặt đất tầm xa
                StartCoroutine(FireGroundLaserSweep());
            }
            else
            {
                // Tiến lại gần player
                ChangeState(EnemyState.Chase);
                Vector2 dir = (playerTransform.position - transform.position).normalized;
                rb.linearVelocity = new Vector2(dir.x * stats.moveSpeed, rb.linearVelocity.y);
            }
        }

        private void SummonKamikazeMinion()
        {
            if (kamikazeMinionPrefab != null)
            {
                Vector3 spawnPos = minionSpawnPoint != null ? minionSpawnPoint.position : transform.position + new Vector3(-facingDirection * 2f, 1f, 0);
                Instantiate(kamikazeMinionPrefab, spawnPos, Quaternion.identity);
                Debug.Log("<color=red>[BOSS SKILL]</color> Boss triệu hồi quái Kamikaze tự sát!");
            }
        }

        private IEnumerator FireGroundLaserSweep()
        {
            // Giữ trạng thái Attack để UpdateFSM không kích hoạt đòn khác hoặc TelegraphRoutine thứ hai
            ChangeState(EnemyState.Attack);
            rb.linearVelocity = Vector2.zero;

            // Telegraph nhấp nháy đỏ 0.5s trước khi bắn Laser
            isTelegraphing = true;
            if (telegraphIndicator != null) telegraphIndicator.SetActive(true);
            float telegraphTime = stats != null ? stats.telegraphDuration : 0.5f;
            float elapsed = 0f;
            while (elapsed < telegraphTime)
            {
                if (currentState == EnemyState.Dead || currentState == EnemyState.Stunned)
                {
                    if (telegraphIndicator != null) telegraphIndicator.SetActive(false);
                    if (spriteRenderer != null) spriteRenderer.color = originalSpriteColor;
                    isTelegraphing = false;
                    yield break;
                }
                if (spriteRenderer != null) spriteRenderer.color = Color.red;
                yield return new WaitForSeconds(0.1f);
                if (spriteRenderer != null) spriteRenderer.color = originalSpriteColor;
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.2f;
            }
            if (telegraphIndicator != null) telegraphIndicator.SetActive(false);
            isTelegraphing = false;

            Vector3 spawnPos = transform.position + new Vector3(facingDirection * 1.5f, -0.2f, 0);

            if (groundLaserPrefab != null)
            {
                GameObject laser = Instantiate(groundLaserPrefab, spawnPos, Quaternion.identity);
                BeamAttack beam = laser.GetComponent<BeamAttack>();
                if (beam != null)
                {
                    DamageData dData = new DamageData(stats.attackDamage * 1.2f, new Vector2(facingDirection * 6f, 3f), AttackType.EnemyHeavyAttack, true, gameObject);
                    beam.SetupBeam(dData, facingDirection, playerLayer, 1.5f, 22f, 1.8f);
                }
            }

            lastAttackTime = Time.time + 1.5f;
            yield return new WaitForSeconds(1.5f);
            ChangeState(EnemyState.Idle);
        }

        protected override IEnumerator AttackRoutine()
        {
            // Đòn vỗ đập mặt đất cận chiến
            rb.linearVelocity = new Vector2(facingDirection * 4f, 0);

            DamageData dData = new DamageData(
                stats.attackDamage,
                new Vector2(facingDirection * stats.knockbackOnAttack.x, stats.knockbackOnAttack.y),
                AttackType.EnemyHeavyAttack,
                true,
                gameObject
            );

            if (attackHitbox != null) attackHitbox.Activate(dData);
            yield return new WaitForSeconds(0.25f);
            if (attackHitbox != null) attackHitbox.Deactivate();

            rb.linearVelocity = Vector2.zero;
            lastAttackTime = Time.time;
            yield return new WaitForSeconds(0.3f);
            ChangeState(EnemyState.Idle);
        }
        #endregion

        #region Phase 2 Logic (< 50% HP)
        private void TriggerPhase2Transition()
        {
            hasTriggeredPhase2 = true;
            currentPhase = BossPhase.PhaseTransition;
            StartCoroutine(Phase2TransitionRoutine());
        }

        private IEnumerator Phase2TransitionRoutine()
        {
            ChangeState(EnemyState.Idle);
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f; // Bắt đầu bay

            Debug.Log("<color=red><b>[BOSS ENRAGED!]</b></color> Boss bước vào Phase 2: Bay lên không và dội bom diện rộng!");

            if (phase2AuraVfx != null) phase2AuraVfx.SetActive(true);
            if (spriteRenderer != null) spriteRenderer.color = phase2Color;

            // Bay dần lên độ cao hoverHeight
            Vector3 targetHover = new Vector3(transform.position.x, groundOriginPos.y + hoverHeight, transform.position.z);
            float t = 0f;
            Vector3 startPos = transform.position;

            while (t < 1.5f)
            {
                transform.position = Vector3.Lerp(startPos, targetHover, t / 1.5f);
                t += Time.deltaTime;
                yield return null;
            }

            currentPhase = BossPhase.Phase2_Airborne;
            nextAoeTime = Time.time + 1f;
        }

        private void HandlePhase2Logic()
        {
            // Lơ lửng di chuyển theo trục ngang phía trên người chơi
            float targetX = playerTransform != null ? playerTransform.position.x : transform.position.x;
            float newX = Mathf.MoveTowards(transform.position.x, targetX, stats.moveSpeed * 1.5f * Time.deltaTime);
            transform.position = new Vector3(newX, groundOriginPos.y + hoverHeight, transform.position.z);

            // Xả mưa bom AOE diện rộng
            if (Time.time >= nextAoeTime)
            {
                StartCoroutine(ExecuteCarpetBombing());
                nextAoeTime = Time.time + aoeCooldown;
            }
        }

        /// <summary>
        /// Mưa bom AOE diện rộng: Đánh dấu các vùng đỏ rực trên mặt đất,
        /// bắt buộc Player phải dùng Dash liên tục để né thoát khỏi vùng nguy hiểm!
        /// </summary>
        private IEnumerator ExecuteCarpetBombing()
        {
            if (playerTransform == null) yield break;

            Vector3[] bombPositions = new Vector3[aoeBombCount];
            float startX = playerTransform.position.x - (aoeBombCount * 2f);

            // Đặt cảnh báo đỏ rực mặt đất
            for (int i = 0; i < aoeBombCount; i++)
            {
                bombPositions[i] = new Vector3(startX + i * 4f, groundOriginPos.y, 0f);

                if (aoeWarningIndicatorPrefab != null)
                {
                    GameObject warning = Instantiate(aoeWarningIndicatorPrefab, bombPositions[i], Quaternion.identity);
                    Destroy(warning, 0.7f);
                }
            }

            // Thời gian Telegraph đỏ rực cảnh báo (0.7s) để Player nhận biết và Dash
            yield return new WaitForSeconds(0.7f);

            // Nổ bom tại các tọa độ đánh dấu
            for (int i = 0; i < aoeBombCount; i++)
            {
                if (carpetBombExplosionPrefab != null)
                {
                    GameObject exp = Instantiate(carpetBombExplosionPrefab, bombPositions[i], Quaternion.identity);
                    Explosion expScript = exp.GetComponent<Explosion>();
                    if (expScript != null)
                    {
                        expScript.Setup(stats.attackDamage * 1.5f, 3.2f, playerLayer, gameObject, AttackType.EnemyHeavyAttack, true);
                    }
                }
            }
        }
        #endregion
    }
}
