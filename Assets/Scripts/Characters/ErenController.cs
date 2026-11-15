using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Combat;
using StickmanBeatEmUp.Enemies;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Characters
{
    /// <summary>
    /// Eren Stickman Controller (Attack on Titan):
    /// - Thường: Đánh thường J (Chém kiếm), U (Lướt chém xuyên thấu), I (Phóng thương sét nổ), Combo 1 Dash+J (Xoay kiếm AOE)
    /// - Biến hình Titan: Tỉ lệ cơ thể phóng to Scale x2, U (Đập đất Knockdown), I (Ném tảng đá Stun), Combo Đặc Biệt Dash+I (Gót chân Titan khổng lồ giẫm ngang màn hình)
    /// </summary>
    public class ErenController : BaseCharacter
    {
        [Header("Eren Normal Form Prefabs")]
        [Tooltip("Prefab Thương Sét (Thunder Spear) cho chiêu I")]
        [SerializeField] private GameObject thunderSpearPrefab;

        [Header("Eren Titan Form Prefabs")]
        [Tooltip("Prefab Sóng xung kích khi Titan Đập Đất (Skill U)")]
        [SerializeField] private GameObject titanShockwavePrefab;

        [Tooltip("Prefab Tảng Đá Ném gây Stun (Skill I)")]
        [SerializeField] private GameObject rockProjectilePrefab;

        [Tooltip("Prefab Gót chân Titan khổng lồ cho Combo Đặc Biệt Dash + I")]
        [SerializeField] private GameObject colossalStompPrefab;

        [Header("Titan Visuals")]
        [SerializeField] private GameObject titanLightningVfx;
        [SerializeField] private Color titanSkinColor = new Color(0.9f, 0.75f, 0.65f, 1f);

        private Vector3 originalScale;

        protected override void Awake()
        {
            base.Awake();
            originalScale = transform.localScale;
        }

        #region Normal Form Attacks
        /// <summary>
        /// Đánh thường (Phím J): Chém kiếm song đao
        /// </summary>
        public override void ExecuteNormalAttack()
        {
            StartCoroutine(BladeSlashRoutine());
        }

        private IEnumerator BladeSlashRoutine()
        {
            SetActionAnimation(CharacterState.Attack, 0);
            rb.linearVelocity = new Vector2(facingDirection * 3f, rb.linearVelocity.y);

            float damage = GetModifiedDamage(isTransformed ? stats.transformedNormalAttackDamage : stats.normalAttackDamage);
            DamageData dData = new DamageData(
                damage,
                stats.normalAttackKnockback,
                AttackType.NormalAttack_J,
                isTransformed, // Titan form có thể phá giáp nhẹ
                gameObject
            );

            if (meleeHitbox != null) meleeHitbox.Activate(dData);
            yield return new WaitForSeconds(0.2f);
            if (meleeHitbox != null) meleeHitbox.Deactivate();

            yield return new WaitForSeconds(0.1f);
            if (currentState == CharacterState.Attack)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        /// <summary>
        /// Kỹ năng 1 (Phím U) - Tốn 0 nộ
        /// - Thường: Lướt chém xuyên thấu (Dash Slash)
        /// - Titan: Đập đất cận chiến tại chỗ gây Knockdown diện rộng
        /// </summary>
        public override void ExecuteSkill1()
        {
            if (!isTransformed)
            {
                StartCoroutine(DashSlashRoutine());
            }
            else
            {
                StartCoroutine(TitanGroundSlamRoutine());
            }
        }

        private IEnumerator DashSlashRoutine()
        {
            SetActionAnimation(CharacterState.Skill, 1);
            isInvulnerable = true;

            // Lướt vọt tới xuyên qua quái và chém
            rb.linearVelocity = new Vector2(facingDirection * 18f, 0f);

            float damage = GetModifiedDamage(stats.skill1Damage);
            DamageData dData = new DamageData(
                damage,
                new Vector2(facingDirection * 5f, 2f),
                AttackType.Skill_U,
                true,
                gameObject
            );

            if (meleeHitbox != null) meleeHitbox.Activate(dData);

            yield return new WaitForSeconds(0.2f);

            if (meleeHitbox != null) meleeHitbox.Deactivate();
            rb.linearVelocity = Vector2.zero;
            isInvulnerable = false;

            yield return new WaitForSeconds(0.15f);
            if (currentState == CharacterState.Skill)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        private IEnumerator TitanGroundSlamRoutine()
        {
            SetActionAnimation(CharacterState.Skill, 1);
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            yield return new WaitForSeconds(0.22f);
            if (currentState != CharacterState.Skill) yield break;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            // Đập đất tại chỗ (không lướt, không tạo sóng chạy ngang bản đồ)
            Vector3 slamCenter = transform.position + new Vector3(facingDirection * 1.4f, 0.3f, 0f);
            float slamDamage = GetModifiedDamage(stats.transformedSkill1Damage);

            Collider2D[] hits = Physics2D.OverlapCircleAll(slamCenter, 3.5f, enemyLayerMask);
            HashSet<IDamageable> damaged = new HashSet<IDamageable>();
            if (hits != null)
            {
                foreach (Collider2D hitCol in hits)
                {
                    IDamageable target = hitCol.GetComponent<IDamageable>() ?? hitCol.GetComponentInParent<IDamageable>();
                    if (target != null && !target.IsDead && !damaged.Contains(target) && target.Transform != transform)
                    {
                        damaged.Add(target);
                        DamageData slamData = new DamageData(
                            slamDamage,
                            new Vector2(facingDirection * 7f, 6f),
                            AttackType.Skill_U,
                            true,
                            gameObject
                        );
                        target.TakeDamage(slamData);
                        NotifyAttackerHit(gameObject, AttackType.Skill_U, target);
                    }
                }
            }

            CameraFollowBeatEmUp.Instance?.Shake(0.24f, 0.22f);
            SpawnLocalizedSlamVfx(slamCenter);

            yield return new WaitForSeconds(0.32f);
            if (currentState == CharacterState.Skill)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        private void SpawnLocalizedSlamVfx(Vector3 center)
        {
            Sprite vfxSprite = titanShockwavePrefab != null
                ? titanShockwavePrefab.GetComponent<SpriteRenderer>()?.sprite
                : null;
            if (vfxSprite == null && spriteRenderer != null)
            {
                vfxSprite = spriteRenderer.sprite;
            }
            if (vfxSprite == null) return;

            GameObject burst = new GameObject("TitanGroundSlamBurst");
            burst.transform.position = center;
            burst.transform.localScale = new Vector3(2.5f, 2.5f, 1f);
            SpriteRenderer sr = burst.AddComponent<SpriteRenderer>();
            sr.sprite = vfxSprite;
            sr.color = new Color(1f, 0.72f, 0.35f, 0.85f);
            sr.sortingOrder = 7;
            Destroy(burst, 0.28f);
        }

        /// <summary>
        /// Kỹ năng 2 (Phím I) - Tốn 10 nộ
        /// - Thường: Phóng thương sét (Thunder Spear) nổ tung
        /// - Titan: Ném tảng đá khổng lồ xuyên Creep và nổ khi gặp Elite/Boss/Tường
        /// </summary>
        public override void ExecuteSkill2()
        {
            if (!isTransformed)
            {
                StartCoroutine(FireThunderSpearRoutine());
            }
            else
            {
                StartCoroutine(ThrowTitanRockRoutine());
            }
        }

        private IEnumerator FireThunderSpearRoutine()
        {
            SetActionAnimation(CharacterState.Skill, 2);
            rb.linearVelocity = Vector2.zero;

            yield return new WaitForSeconds(0.15f);
            if (currentState != CharacterState.Skill) yield break;

            Vector3 spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position + new Vector3(facingDirection * 0.9f, 0.95f, 0f);

            if (thunderSpearPrefab != null)
            {
                GameObject spear = Instantiate(thunderSpearPrefab, spawnPos, Quaternion.identity);
                Projectile proj = spear.GetComponent<Projectile>();
                if (proj != null)
                {
                    float damage = GetModifiedDamage(stats.skill2Damage);
                    DamageData dData = new DamageData(damage, stats.skill2Knockback, AttackType.Skill_I, true, gameObject);
                    proj.Initialize(dData, new Vector2(facingDirection, 0f), enemyLayerMask, false);
                }
            }

            yield return new WaitForSeconds(0.25f);
            if (currentState == CharacterState.Skill)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        private IEnumerator ThrowTitanRockRoutine()
        {
            SetActionAnimation(CharacterState.Skill, 2);
            rb.linearVelocity = Vector2.zero;

            // Titan nhấc tảng đá khổng lồ và ném thẳng
            yield return new WaitForSeconds(0.3f);
            if (currentState != CharacterState.Skill) yield break;

            Vector3 spawnPos = transform.position + new Vector3(facingDirection * 1.5f, 1.15f, 0f);
            if (rockProjectilePrefab != null)
            {
                GameObject rock = Instantiate(rockProjectilePrefab, spawnPos, Quaternion.identity);
                rock.transform.localScale = new Vector3(2.5f, 2.5f, 1f);

                Projectile proj = rock.GetComponent<Projectile>();
                if (proj != null)
                {
                    float rawDamage = (stats != null ? Mathf.Max(22f, stats.transformedSkill2Damage) : 22f) * 2.0f;
                    float rockDamage = GetModifiedDamage(rawDamage);
                    DamageData dData = new DamageData(
                        rockDamage,
                        new Vector2(facingDirection * 10f, 6f),
                        AttackType.Skill_I,
                        true,
                        gameObject
                    );
                    proj.ConfigureTitanBoulder(7.0f, null);
                    proj.Initialize(dData, new Vector2(facingDirection, 0f), enemyLayerMask, true);
                }
            }

            yield return new WaitForSeconds(0.3f);
            if (currentState == CharacterState.Skill)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        /// <summary>
        /// Combo Cơ Bản (Dash + J): Xoay kiếm (Spin Slash 360 độ)
        /// </summary>
        public override void ExecuteDashAttackCombo()
        {
            StartCoroutine(SpinSlashRoutine());
        }

        private IEnumerator SpinSlashRoutine()
        {
            SetActionAnimation(CharacterState.Attack, 1);

            // Xoay kiếm khi lướt tới
            rb.linearVelocity = new Vector2(facingDirection * 10f, 0f);

            float damage = GetModifiedDamage(stats.dashAttackComboDamage);
            DamageData dData = new DamageData(
                damage,
                stats.dashAttackKnockback,
                AttackType.Combo_DashJ,
                true,
                gameObject
            );

            if (meleeHitbox != null) meleeHitbox.Activate(dData);
            yield return new WaitForSeconds(0.25f);
            if (meleeHitbox != null) meleeHitbox.Deactivate();

            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.1f);
            if (currentState == CharacterState.Attack)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        /// <summary>
        /// Biến hình Attack Titan: Tỉ lệ cơ thể phóng to Scale x2, tốn 30 nộ
        /// </summary>
        public override void ExecuteTransform()
        {
            StartCoroutine(TitanTransformRoutine());
        }

        private IEnumerator TitanTransformRoutine()
        {
            ChangeState(CharacterState.Transform);
            rb.linearVelocity = Vector2.zero;
            isInvulnerable = true;

            // Sấm sét giáng xuống khi biến hình Titan
            if (titanLightningVfx != null)
            {
                Instantiate(titanLightningVfx, transform.position, Quaternion.identity);
            }

            yield return new WaitForSeconds(0.2f);

            // Phóng to Scale x2
            transform.localScale = new Vector3(originalScale.x * 2f * facingDirection, originalScale.y * 2f, originalScale.z);

            if (spriteRenderer != null) spriteRenderer.color = Color.white;
            if (stats.transformBonusHealth > 0f)
            {
                Heal(stats.transformBonusHealth);
            }
            SetTransformedState(true);

            // Sát thương nổ do hóa Titan hất văng địch xung quanh
            Collider2D[] enemies = Physics2D.OverlapCircleAll(transform.position + Vector3.up * 1.0f, 5f, enemyLayerMask);
            HashSet<IDamageable> pushed = new HashSet<IDamageable>();
            foreach (var col in enemies)
            {
                IDamageable target = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead && !pushed.Contains(target))
                {
                    pushed.Add(target);
                    Vector2 dir = (col.transform.position - transform.position).normalized;
                    target.TakeDamage(new DamageData(0f, dir * 10f, AttackType.TransformBurst, true, gameObject));
                }
            }

            yield return new WaitForSeconds(0.5f);
            isInvulnerable = false;
            if (currentState == CharacterState.Transform)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        /// <summary>
        /// Combo Đặc Biệt (Dash + I): Rung Chấn (Rumbling) - 6 đến 8 gót chân Titan khổng lồ giẫm ngang toàn màn hình
        /// </summary>
        public override void ExecuteSpecialCombo()
        {
            StartCoroutine(ColossalStompRoutine());
        }

        private IEnumerator ColossalStompRoutine()
        {
            SetActionAnimation(CharacterState.Skill, 3);
            rb.linearVelocity = Vector2.zero;
            isInvulnerable = true;

            try
            {
                yield return StartCoroutine(SpawnRumblingFeet());
                yield return new WaitForSeconds(0.25f);
            }
            finally
            {
                isInvulnerable = false;
            }

            if (currentState == CharacterState.Skill)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        private IEnumerator SpawnRumblingFeet()
        {
            int footCount = UnityEngine.Random.Range(6, 9);
            const float SpawnInterval = 0.11f;

            float minX;
            float maxX;

            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                float camCenterX = cam.transform.position.x;
                float halfWidth = cam.orthographicSize * cam.aspect;
                minX = camCenterX - halfWidth;
                maxX = camCenterX + halfWidth;
            }
            else
            {
                minX = Mathf.Max(transform.position.x - 10f, mapMinX);
                maxX = Mathf.Min(transform.position.x + 10f, mapMaxX);

                if (cachedLeftWall == null || cachedRightWall == null)
                {
                    FindCameraWalls();
                }
                if (cachedLeftWall != null && cachedLeftWall.gameObject.activeInHierarchy)
                {
                    minX = Mathf.Max(minX, cachedLeftWall.bounds.max.x + 0.5f);
                }
                if (cachedRightWall != null && cachedRightWall.gameObject.activeInHierarchy)
                {
                    maxX = Mathf.Min(maxX, cachedRightWall.bounds.min.x - 0.5f);
                }

                if (maxX <= minX + 1f)
                {
                    minX = transform.position.x - 8f;
                    maxX = transform.position.x + 8f;
                }
            }

            float rawStompDmg = stats != null ? Mathf.Max(35f, stats.specialComboDamage) : 35f;
            float stompDmg = GetModifiedDamage(rawStompDmg);
            float groundY = EnemyBase.GROUND_Y + 1.5f;

            CameraFollowBeatEmUp.Instance?.Shake(0.35f, 0.32f);

            for (int i = 0; i < footCount; i++)
            {
                if (IsDead) yield break;

                float t = (i + 0.5f) / footCount;
                float spawnX = Mathf.Lerp(minX, maxX, t);
                Vector3 stompPos = new Vector3(spawnX, groundY, 0f);

                CameraFollowBeatEmUp.Instance?.Shake(0.35f, 0.32f);

                if (colossalStompPrefab != null)
                {
                    GameObject stompObj = Instantiate(colossalStompPrefab, stompPos, Quaternion.identity);
                    TitanStomp stomp = stompObj.GetComponent<TitanStomp>();
                    if (stomp != null)
                    {
                        stomp.Setup(stompDmg, enemyLayerMask, gameObject, new Vector2(3.6f, 6f), 0.16f, 1.6f);
                    }
                }

                yield return new WaitForSeconds(SpawnInterval);
            }
        }
        #endregion
    }
}
