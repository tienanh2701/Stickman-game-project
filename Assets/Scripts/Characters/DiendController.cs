using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Combat;

namespace StickmanBeatEmUp.Characters
{
    /// <summary>
    /// Diend Stickman Controller (Kamen Rider Diend):
    /// - Thường: Đánh thường J (Bắn súng), U (Gọi ảo ảnh Rider chém rồi biến mất), I (Bắn đạn xuyên thấu), Combo 1 Dash+J (Bắn quạt 3 tia)
    /// - Biến hình (Triệu hồi Lord Kiva 15.0s): Diend giữ nguyên bộ skill, sinh ra AI đồng minh Lord Kiva tự chiến đấu.
    /// - Combo Đặc Biệt Dash+I: Lệnh cho Dark Kiva mở cánh làm đóng băng (Freeze) toàn bộ địch trên màn hình.
    /// </summary>
    public class DiendController : BaseCharacter
    {
        [Header("Diend Prefabs")]
        [Tooltip("Prefab đạn súng thường của Diend")]
        [SerializeField] private GameObject bulletPrefab;

        [Tooltip("Prefab ảo ảnh chém cho kỹ năng U")]
        [SerializeField] private GameObject phantomSlashVisualPrefab;

        [Tooltip("Prefab tia đạn xuyên thấu cho kỹ năng I")]
        [SerializeField] private GameObject piercingBeamPrefab;

        [Tooltip("Prefab AI đồng minh Lord Kiva triệu hồi khi biến hình")]
        [SerializeField] private GameObject lordKivaPrefab;

        [Header("Lord Kiva Summon Settings")]
        [SerializeField] private float kivaLifetime = 15.0f;

        private LordKivaAI activeLordKivaInstance;
        private BeamAttack activeChargingPiercingBeam;

        protected override float HitRageAmount => 0.75f;

        protected override void Awake()
        {
            base.Awake();
            kivaLifetime = 15.0f;
        }

        private void OnDisable()
        {
            CancelChargingPiercingBeam();
        }

        protected override void ExitCurrentState(CharacterState state)
        {
            base.ExitCurrentState(state);
            if (state == CharacterState.Skill)
            {
                CancelChargingPiercingBeam();
            }
        }

        protected override void OnDestroy()
        {
            CancelChargingPiercingBeam();
            DespawnLordKiva();
            base.OnDestroy();
        }

        protected override void Die()
        {
            CancelChargingPiercingBeam();
            DespawnLordKiva();
            base.Die();
        }

        private void CancelChargingPiercingBeam()
        {
            if (activeChargingPiercingBeam != null)
            {
                activeChargingPiercingBeam.CancelCharge();
                activeChargingPiercingBeam = null;
            }
        }

        protected override void OnTimedTransformExpired()
        {
            base.OnTimedTransformExpired();
            DespawnLordKiva();
        }

        private void DespawnLordKiva()
        {
            if (activeLordKivaInstance != null)
            {
                Destroy(activeLordKivaInstance.gameObject);
                activeLordKivaInstance = null;
            }
        }

        #region Normal Form Attacks
        /// <summary>
        /// Đánh thường (Phím J): Bắn đạn súng Diend Driver
        /// </summary>
        public override void ExecuteNormalAttack()
        {
            StartCoroutine(ShootBulletRoutine());
        }

        private IEnumerator ShootBulletRoutine()
        {
            SetActionAnimation(CharacterState.Attack, 0);
            rb.linearVelocity = Vector2.zero;

            Vector3 spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position + new Vector3(facingDirection * 0.9f, 0.85f, 0f);

            if (bulletPrefab != null)
            {
                GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
                Projectile proj = bulletObj.GetComponent<Projectile>();
                if (proj != null)
                {
                    float damage = GetModifiedDamage(isTransformed ? stats.transformedNormalAttackDamage : stats.normalAttackDamage);
                    DamageData dData = new DamageData(
                        damage,
                        stats.normalAttackKnockback,
                        AttackType.NormalAttack_J,
                        false,
                        gameObject
                    );
                    proj.Initialize(dData, new Vector2(facingDirection, 0f), enemyLayerMask, false);
                }
            }

            yield return new WaitForSeconds(0.2f);
            if (currentState == CharacterState.Attack)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        /// <summary>
        /// Kỹ năng 1 (Phím U) - Tốn 0 nộ:
        /// Gọi ảo ảnh Rider lướt chém 1 nhát rồi tan biến (Visual Illusion Slash)
        /// </summary>
        public override void ExecuteSkill1()
        {
            StartCoroutine(PhantomSlashRoutine());
        }

        private IEnumerator PhantomSlashRoutine()
        {
            SetActionAnimation(CharacterState.Skill, 1);
            rb.linearVelocity = Vector2.zero;

            Vector3 phantomSpawnPos = transform.position + new Vector3(facingDirection * 2.5f, 0.85f, 0f);

            // Sinh ảo ảnh chém
            if (phantomSlashVisualPrefab != null)
            {
                GameObject illusion = Instantiate(phantomSlashVisualPrefab, phantomSpawnPos, Quaternion.identity);
                illusion.transform.localScale = new Vector3(facingDirection * Mathf.Abs(illusion.transform.localScale.x), illusion.transform.localScale.y, 1f);
                Destroy(illusion, 0.6f);
            }

            // Gây sát thương nhát chém ảo ảnh
            float slashDamage = GetModifiedDamage(isTransformed ? stats.transformedSkill1Damage : stats.skill1Damage);
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(phantomSpawnPos, 2f, enemyLayerMask);
            HashSet<IDamageable> damaged = new HashSet<IDamageable>();
            foreach (var col in hitEnemies)
            {
                IDamageable target = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead && !damaged.Contains(target))
                {
                    damaged.Add(target);
                    DamageData dData = new DamageData(
                        slashDamage,
                        stats.skill1Knockback,
                        AttackType.Skill_U,
                        true,
                        gameObject
                    );
                    target.TakeDamage(dData);

                    // Cộng nộ vì đây là chiêu U trúng địch (+0.75 cho Diend)
                    OnHitEnemy(AttackType.Skill_U, target);
                }
            }

            yield return new WaitForSeconds(0.3f);
            if (currentState == CharacterState.Skill)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        /// <summary>
        /// Kỹ năng 2 (Phím I) - Tốn 10 nộ: Bắn đạn xuyên thấu toàn bộ địch trên hàng ngang
        /// </summary>
        public override void ExecuteSkill2()
        {
            StartCoroutine(PiercingCardShotRoutine());
        }

        private BeamAttack SpawnPiercingBeam(Vector3 spawnPos, float rawDamage, float activeDuration, float width)
        {
            if (piercingBeamPrefab == null)
            {
                return null;
            }

            GameObject beamObj = Instantiate(piercingBeamPrefab, spawnPos, Quaternion.identity);
            BeamAttack beam = beamObj.GetComponent<BeamAttack>();
            if (beam != null)
            {
                float beamDamage = GetModifiedDamage(rawDamage);
                DamageData dData = new DamageData(
                    beamDamage,
                    stats.skill2Knockback,
                    AttackType.Skill_I,
                    true,
                    gameObject
                );
                beam.SetupBeam(dData, facingDirection, enemyLayerMask, activeDuration, 20f, width);
            }
            return beam;
        }

        private IEnumerator PiercingCardShotRoutine()
        {
            SetActionAnimation(CharacterState.Skill, 2);
            rb.linearVelocity = Vector2.zero;

            if (isTransformed)
            {
                Transform anchor = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
                Vector3 anchorOffset = projectileSpawnPoint != null ? Vector3.zero : new Vector3(facingDirection * 0.9f, 0.85f, 0f);
                Vector3 spawnPos = anchor.position + anchorOffset;

                BeamAttack beam = SpawnPiercingBeam(spawnPos, stats.transformedSkill2Damage, 0.20f, 1.5f);
                if (beam != null)
                {
                    beam.ConfigureSkillVfx(BeamAttack.BeamVisualStyle.DiendPiercingCard, anchor, anchorOffset, 0.18f, 0.28f, true);
                    activeChargingPiercingBeam = beam;
                }

                yield return new WaitForSeconds(0.18f);
                if (currentState != CharacterState.Skill || activeChargingPiercingBeam != beam)
                {
                    if (beam != null)
                    {
                        beam.CancelCharge();
                    }
                    if (activeChargingPiercingBeam == beam)
                    {
                        activeChargingPiercingBeam = null;
                    }
                    yield break;
                }

                activeChargingPiercingBeam = null;

                yield return new WaitForSeconds(0.22f);
                if (currentState == CharacterState.Skill)
                {
                    ChangeState(CharacterState.Idle);
                }
                yield break;
            }

            yield return new WaitForSeconds(0.2f);
            if (currentState != CharacterState.Skill) yield break;

            Vector3 normalSpawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position + new Vector3(facingDirection * 0.9f, 0.85f, 0f);
            SpawnPiercingBeam(normalSpawnPos, stats.skill2Damage, 0.15f, 1.2f);

            yield return new WaitForSeconds(0.2f);
            if (currentState == CharacterState.Skill)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        /// <summary>
        /// Combo Cơ Bản (Dash + J): Bắn quạt 3 tia hình nón trong khi lướt
        /// </summary>
        public override void ExecuteDashAttackCombo()
        {
            StartCoroutine(FanSpreadShotRoutine());
        }

        private IEnumerator FanSpreadShotRoutine()
        {
            SetActionAnimation(CharacterState.Attack, 1);

            Vector3 spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position + new Vector3(facingDirection * 0.9f, 0.85f, 0f);

            // Bắn 3 tia: Thẳng, Hướng lên trên 15 độ, Hướng xuống dưới 15 độ
            Vector2[] directions = new Vector2[]
            {
                new Vector2(facingDirection, 0f),
                new Vector2(facingDirection, 0.26f).normalized,
                new Vector2(facingDirection, -0.26f).normalized
            };

            float perBulletDamage = GetModifiedDamage(stats.dashAttackComboDamage / 3f);

            foreach (var dir in directions)
            {
                if (bulletPrefab != null)
                {
                    GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
                    Projectile proj = bulletObj.GetComponent<Projectile>();
                    if (proj != null)
                    {
                        DamageData dData = new DamageData(
                            perBulletDamage,
                            stats.dashAttackKnockback,
                            AttackType.Combo_DashJ,
                            true,
                            gameObject
                        );
                        proj.Initialize(dData, dir, enemyLayerMask, false);
                    }
                }
            }

            yield return new WaitForSeconds(0.25f);
            if (currentState == CharacterState.Attack)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        /// <summary>
        /// Biến hình (Kamen Ride: Lord Kiva): Tốn 30 nộ
        /// Diend giữ nguyên kỹ năng, sinh ra AI đồng minh Lord Kiva chiến đấu hỗ trợ trong 15.0s
        /// </summary>
        public override void ExecuteTransform()
        {
            StartCoroutine(SummonLordKivaRoutine());
        }

        private IEnumerator SummonLordKivaRoutine()
        {
            ChangeState(CharacterState.Transform);
            rb.linearVelocity = Vector2.zero;
            isInvulnerable = true;

            Debug.Log("<color=cyan>[KAMEN RIDE: LORD KIVA!]</color> Triệu hồi đồng minh Lord Kiva trong 15s!");

            yield return new WaitForSeconds(0.3f);
            if (IsDead) yield break;

            DespawnLordKiva();
            kivaLifetime = 15.0f;

            if (lordKivaPrefab != null)
            {
                GameObject kivaObj = Instantiate(lordKivaPrefab, transform.position + new Vector3(-facingDirection * 1.5f, 0.25f, 0f), Quaternion.identity);
                activeLordKivaInstance = kivaObj.GetComponent<LordKivaAI>();
                if (activeLordKivaInstance != null)
                {
                    activeLordKivaInstance.Initialize(transform);
                    activeLordKivaInstance.SetLifetime(kivaLifetime);
                }
            }

            if (transformAuraEffect != null) transformAuraEffect.SetActive(true);
            SetTransformedState(true, kivaLifetime);
            if (stats.transformBonusHealth > 0f)
            {
                Heal(stats.transformBonusHealth);
            }

            yield return new WaitForSeconds(0.4f);
            isInvulnerable = false;
            if (currentState == CharacterState.Transform)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        /// <summary>
        /// Combo Đặc Biệt (Dash + I): Dark Kiva mở cánh làm toàn bộ kẻ thù bị Freeze (đứng im)
        /// Yêu cầu: Đã biến hình (gọi Kiva) & Đầy 30 nộ
        /// </summary>
        public override void ExecuteSpecialCombo()
        {
            if (activeLordKivaInstance != null)
            {
                StartCoroutine(SpecialCommandRoutine());
            }
            else
            {
                Debug.LogWarning("Lord Kiva chưa được triệu hồi!");
            }
        }

        private IEnumerator SpecialCommandRoutine()
        {
            SetActionAnimation(CharacterState.Skill, 3);
            if (rb != null) rb.linearVelocity = Vector2.zero;

            float specialDamage = GetModifiedDamage(stats.specialComboDamage);
            if (activeLordKivaInstance != null)
            {
                activeLordKivaInstance.ExecuteDarkKivaWingFreeze(4.0f, specialDamage);
            }

            yield return new WaitForSeconds(0.4f);
            if (currentState == CharacterState.Skill)
            {
                ChangeState(CharacterState.Idle);
            }
        }
        #endregion
    }
}
