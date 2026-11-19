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
    /// Goku Stickman Controller (Kế thừa BaseCharacter - Thể hiện tính Đa hình & Kế thừa trong OOP)
    /// - Thường: Đánh thường J (Đấm), U (Bắn Ki nhỏ), I (Kaioken Buff tốc/sát thương), Combo 1 Dash+J (Nổ Ki AOE nhỏ)
    /// - Super Saiyan (Biến hình): U (Đấm đá Multi-hit), I (Kamehameha to), Combo Đặc Biệt Dash+I (Genki Dama)
    /// </summary>
    public class GokuController : BaseCharacter
    {
        [Header("Goku Prefabs & Spawns")]
        [Tooltip("Prefab đạn Ki nhỏ bắn từ chiêu U (dạng thường)")]
        [SerializeField] private GameObject kiBlastPrefab;

        [Tooltip("Prefab Nổ Ki AOE cho combo Dash + J")]
        [SerializeField] private GameObject kiExplosionPrefab;

        [Tooltip("Prefab chùm tia Kamehameha to cho chiêu I (dạng Super Saiyan)")]
        [SerializeField] private GameObject kamehamehaPrefab;

        [Tooltip("Prefab quả cầu Genki Dama cho Combo Đặc Biệt Dash + I")]
        [SerializeField] private GameObject genkiDamaPrefab;

        [Header("Kaioken Buff Settings")]
        [SerializeField] private float kaiokenDuration = 8f;
        [SerializeField] private float kaiokenDamageBonus = 1.5f;
        [SerializeField] private float kaiokenSpeedBonus = 1.3f;
        [SerializeField] private Color kaiokenColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color superSaiyanColor = new Color(1f, 0.92f, 0.016f, 1f);

        private bool isKaiokenActive = false;
        private Coroutine kaiokenCoroutine;

        private BoxCollider2D cachedMeleeBox;
        private Vector2 originalMeleeBoxSize;
        private Vector2 originalMeleeBoxOffset;
        private Vector3 originalMeleeLocalPos;
        private bool isMeleeBoxModified = false;
        private GameObject activeChargingGenkiDama;
        private BeamAttack activeChargingKamehameha;

        protected override void Awake()
        {
            base.Awake();
            CacheMeleeBoxDefaults();
        }

        private void OnDisable()
        {
            RestoreNormalHitbox();
            CleanupUnlaunchedGenkiDama();
            CancelChargingKamehameha();
        }

        protected override void ExitCurrentState(CharacterState state)
        {
            base.ExitCurrentState(state);
            if (state == CharacterState.Skill)
            {
                RestoreNormalHitbox();
                CancelChargingKamehameha();
            }
        }

        protected override void OnTimedTransformExpired()
        {
            RestoreNormalHitbox();
            base.OnTimedTransformExpired();
        }

        protected override void Die()
        {
            RestoreNormalHitbox();
            CleanupUnlaunchedGenkiDama();
            CancelChargingKamehameha();
            base.Die();
        }

        private void CancelChargingKamehameha()
        {
            if (activeChargingKamehameha != null)
            {
                activeChargingKamehameha.CancelCharge();
                activeChargingKamehameha = null;
            }
        }

        private void CleanupUnlaunchedGenkiDama()
        {
            if (activeChargingGenkiDama != null)
            {
                Destroy(activeChargingGenkiDama);
                activeChargingGenkiDama = null;
            }
        }

        private void CacheMeleeBoxDefaults()
        {
            if (cachedMeleeBox == null && meleeHitbox != null)
            {
                cachedMeleeBox = meleeHitbox.GetComponent<BoxCollider2D>();
                originalMeleeLocalPos = meleeHitbox.transform.localPosition;
                if (cachedMeleeBox != null)
                {
                    originalMeleeBoxSize = cachedMeleeBox.size;
                    originalMeleeBoxOffset = cachedMeleeBox.offset;
                }
            }
        }

        private void ApplySuperSaiyanHitboxBoost()
        {
            CacheMeleeBoxDefaults();
            if (cachedMeleeBox == null || isMeleeBoxModified) return;

            isMeleeBoxModified = true;
            cachedMeleeBox.size = new Vector2(originalMeleeBoxSize.x * 2.0f, originalMeleeBoxSize.y * 2.0f);
            float forwardOffsetX = Mathf.Abs(originalMeleeBoxOffset.x + originalMeleeLocalPos.x) > 0.01f
                ? (originalMeleeBoxOffset.x * 2.0f) + originalMeleeLocalPos.x
                : originalMeleeBoxSize.x * 0.5f;
            cachedMeleeBox.offset = new Vector2(forwardOffsetX, originalMeleeBoxOffset.y);
        }

        private void RestoreNormalHitbox()
        {
            if (cachedMeleeBox != null && isMeleeBoxModified)
            {
                cachedMeleeBox.size = originalMeleeBoxSize;
                cachedMeleeBox.offset = originalMeleeBoxOffset;
            }
            isMeleeBoxModified = false;
        }

        protected override float GetCustomSpeedMultiplier()
        {
            return base.GetCustomSpeedMultiplier() * (isKaiokenActive ? kaiokenSpeedBonus : 1f);
        }

        #region Normal Attacks & Skills
        /// <summary>
        /// Đánh thường (Phím J): Chuỗi đấm cận chiến
        /// </summary>
        public override void ExecuteNormalAttack()
        {
            StartCoroutine(PunchRoutine());
        }

        private IEnumerator PunchRoutine()
        {
            SetActionAnimation(CharacterState.Attack, 0);
            rb.linearVelocity = new Vector2(facingDirection * 2f, rb.linearVelocity.y);

            float rawDamage = (isTransformed ? stats.transformedNormalAttackDamage : stats.normalAttackDamage) * (isKaiokenActive ? kaiokenDamageBonus : 1f);
            float damage = GetModifiedDamage(rawDamage);

            DamageData damageData = new DamageData(
                damage,
                stats.normalAttackKnockback,
                AttackType.NormalAttack_J,
                false,
                gameObject
            );

            if (meleeHitbox != null) meleeHitbox.Activate(damageData);

            yield return new WaitForSeconds(0.25f);

            if (meleeHitbox != null) meleeHitbox.Deactivate();
            yield return new WaitForSeconds(0.1f);

            if (currentState == CharacterState.Attack)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        /// <summary>
        /// Kỹ năng 1 (Phím U) - Tốn 0 nộ
        /// - Dạng thường: Bắn Ki nhỏ
        /// - Dạng Super Saiyan: Đấm đá Multi-hit liên hoàn
        /// </summary>
        public override void ExecuteSkill1()
        {
            if (!isTransformed)
            {
                // Bắn Ki nhỏ
                StartCoroutine(ShootKiBlastRoutine());
            }
            else
            {
                // Super Saiyan: Multi-hit Flurry
                StartCoroutine(SuperSaiyanMultiHitRoutine());
            }
        }

        private IEnumerator ShootKiBlastRoutine()
        {
            SetActionAnimation(CharacterState.Skill, 1);
            rb.linearVelocity = Vector2.zero;

            yield return new WaitForSeconds(0.15f);
            if (currentState != CharacterState.Skill) yield break;

            Vector3 spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position + new Vector3(facingDirection * 0.9f, 0.85f, 0f);

            if (kiBlastPrefab != null)
            {
                GameObject kiObj = Instantiate(kiBlastPrefab, spawnPos, Quaternion.identity);
                Projectile projectile = kiObj.GetComponent<Projectile>();
                if (projectile != null)
                {
                    float rawDamage = stats.skill1Damage * (isKaiokenActive ? kaiokenDamageBonus : 1f);
                    float damage = GetModifiedDamage(rawDamage);
                    DamageData dData = new DamageData(damage, stats.skill1Knockback, AttackType.Skill_U, false, gameObject);
                    projectile.Initialize(dData, new Vector2(facingDirection, 0f), enemyLayerMask, false);
                }
            }

            yield return new WaitForSeconds(0.2f);
            if (currentState == CharacterState.Skill)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        private IEnumerator SuperSaiyanMultiHitRoutine()
        {
            SetActionAnimation(CharacterState.Skill, 1);
            ApplySuperSaiyanHitboxBoost();

            try
            {
                // Lao tới phía trước và tung 4 đòn đánh nhanh với tầm đánh 2x
                for (int i = 0; i < 4; i++)
                {
                    if (currentState != CharacterState.Skill) yield break;

                    rb.linearVelocity = new Vector2(facingDirection * 5f, 0f);

                    float punchDamage = GetModifiedDamage(stats.transformedSkill1Damage / 4f);
                    DamageData dData = new DamageData(
                        punchDamage,
                        new Vector2(2f, 1f),
                        AttackType.Skill_U,
                        true, // Multi-hit phá giáp nhẹ
                        gameObject
                    );

                    if (meleeHitbox != null) meleeHitbox.Activate(dData);
                    yield return new WaitForSeconds(0.08f);
                    if (meleeHitbox != null) meleeHitbox.Deactivate();
                    yield return new WaitForSeconds(0.05f);
                }
            }
            finally
            {
                RestoreNormalHitbox();
            }

            if (currentState == CharacterState.Skill)
            {
                rb.linearVelocity = Vector2.zero;
                ChangeState(CharacterState.Idle);
            }
        }

        /// <summary>
        /// Kỹ năng 2 (Phím I) - Tốn 10 nộ
        /// - Dạng thường: Kaioken (buff tốc + dame trong 8s)
        /// - Dạng Super Saiyan: Bắn Kamehameha to cực mạnh
        /// </summary>
        public override void ExecuteSkill2()
        {
            if (!isTransformed)
            {
                ActivateKaioken();
            }
            else
            {
                StartCoroutine(FireKamehamehaRoutine());
            }
        }

        private void ActivateKaioken()
        {
            if (kaiokenCoroutine != null) StopCoroutine(kaiokenCoroutine);
            kaiokenCoroutine = StartCoroutine(KaiokenBuffRoutine());
            StartCoroutine(KaiokenCastAnimRoutine());
        }

        private IEnumerator KaiokenCastAnimRoutine()
        {
            SetActionAnimation(CharacterState.Skill, 2);
            if (rb != null) rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.35f);
            if (currentState == CharacterState.Skill)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        private IEnumerator KaiokenBuffRoutine()
        {
            isKaiokenActive = true;
            Debug.Log("<color=red>[GOKU KAIOKEN ACTIVE!]</color> Tăng 50% Sát thương & 30% Tốc chạy trong 8s!");

            if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 0.75f, 0.75f, 1f);

            yield return new WaitForSeconds(kaiokenDuration);

            isKaiokenActive = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
            Debug.Log("<color=white>[Goku Kaioken End]</color>");
        }

        private IEnumerator FireKamehamehaRoutine()
        {
            SetActionAnimation(CharacterState.Skill, 2);
            rb.linearVelocity = Vector2.zero;

            Transform anchor = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
            Vector3 anchorOffset = projectileSpawnPoint != null ? Vector3.zero : new Vector3(facingDirection * 0.9f, 0.85f, 0f);
            Vector3 spawnPos = anchor.position + anchorOffset;

            BeamAttack beam = null;
            if (kamehamehaPrefab != null)
            {
                GameObject beamObj = Instantiate(kamehamehaPrefab, spawnPos, Quaternion.identity);
                beam = beamObj.GetComponent<BeamAttack>();
                if (beam != null)
                {
                    float beamTickDamage = GetModifiedDamage(stats.transformedSkill2Damage / 5f);
                    DamageData dData = new DamageData(
                        beamTickDamage,
                        stats.skill2Knockback,
                        AttackType.Skill_I,
                        true,
                        gameObject
                    );
                    beam.SetupBeam(dData, facingDirection, enemyLayerMask, 0.9f, 18f, 2.5f);
                    beam.ConfigureSkillVfx(BeamAttack.BeamVisualStyle.GokuKamehameha, anchor, anchorOffset, 0.3f, 0.18f, false);
                    activeChargingKamehameha = beam;
                }
            }

            // Gồng Kamehameha 0.3s (hiệu ứng tụ cầu Ki phát trực tiếp ở tay)
            yield return new WaitForSeconds(0.3f);
            if (currentState != CharacterState.Skill || activeChargingKamehameha != beam)
            {
                if (beam != null)
                {
                    beam.CancelCharge();
                }
                if (activeChargingKamehameha == beam)
                {
                    activeChargingKamehameha = null;
                }
                yield break;
            }

            activeChargingKamehameha = null;

            yield return new WaitForSeconds(0.9f);
            if (currentState == CharacterState.Skill)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        /// <summary>
        /// Combo Cơ Bản (Dash + J): Nổ Ki AOE nhỏ xung quanh Goku
        /// </summary>
        public override void ExecuteDashAttackCombo()
        {
            StartCoroutine(KiExplosionComboRoutine());
        }

        private IEnumerator KiExplosionComboRoutine()
        {
            SetActionAnimation(CharacterState.Attack, 1);
            rb.linearVelocity = Vector2.zero;

            Vector3 spawnPos = transform.position + Vector3.up * 0.85f;
            if (kiExplosionPrefab != null)
            {
                GameObject expObj = Instantiate(kiExplosionPrefab, spawnPos, Quaternion.identity);
                Explosion exp = expObj.GetComponent<Explosion>();
                if (exp != null)
                {
                    float rawDmg = stats.dashAttackComboDamage * (isKaiokenActive ? kaiokenDamageBonus : 1f);
                    float dmg = GetModifiedDamage(rawDmg);
                    exp.Setup(dmg, 3.5f, enemyLayerMask, gameObject, AttackType.Combo_DashJ, true);
                }
            }

            yield return new WaitForSeconds(0.35f);
            if (currentState == CharacterState.Attack)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        /// <summary>
        /// Biến hình (Super Saiyan): Tốn 30 nộ
        /// </summary>
        public override void ExecuteTransform()
        {
            StartCoroutine(TransformRoutine());
        }

        private IEnumerator TransformRoutine()
        {
            ChangeState(CharacterState.Transform);
            rb.linearVelocity = Vector2.zero;
            isInvulnerable = true;

            if (transformAuraEffect != null) transformAuraEffect.SetActive(true);
            if (spriteRenderer != null) spriteRenderer.color = Color.white;

            // Sóng xung kích nổ năng lượng khi biến hình
            Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(transform.position + Vector3.up * 0.85f, 4f, enemyLayerMask);
            HashSet<IDamageable> pushed = new HashSet<IDamageable>();
            foreach (var col in nearbyEnemies)
            {
                IDamageable target = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead && !pushed.Contains(target))
                {
                    pushed.Add(target);
                    Vector2 pushDir = (col.transform.position - transform.position).normalized;
                    target.TakeDamage(new DamageData(0f, pushDir * 8f, AttackType.TransformBurst, true, gameObject));
                }
            }

            if (stats.transformBonusHealth > 0f)
            {
                Heal(stats.transformBonusHealth);
            }
            SetTransformedState(true);

            yield return new WaitForSeconds(0.6f);

            isInvulnerable = false;
            if (currentState == CharacterState.Transform)
            {
                ChangeState(CharacterState.Idle);
            }
        }

        /// <summary>
        /// Combo Đặc Biệt (Dash + I): Genki Dama (Spirit Bomb)
        /// Yêu cầu: Đã biến hình Super Saiyan & Đầy 30 nộ
        /// </summary>
        public override void ExecuteSpecialCombo()
        {
            StartCoroutine(GenkiDamaRoutine());
        }

        private IEnumerator GenkiDamaRoutine()
        {
            SetActionAnimation(CharacterState.Skill, 3);
            if (rb != null) rb.linearVelocity = Vector2.zero;
            isGravityOverriddenBySkill = true;
            if (rb != null) rb.gravityScale = 0f;
            isInvulnerable = true;

            bool bombLaunched = false;

            try
            {
                // Nhảy lơ lửng lên không trung tích quả cầu Genki Dama
                Vector3 startPos = transform.position;
                Vector3 airPos = new Vector3(startPos.x, Mathf.Min(startPos.y + 3f, mapMaxY - 0.5f), startPos.z);
                float t = 0f;
                while (t < 0.2f)
                {
                    if (IsDead || currentState != CharacterState.Skill) yield break;
                    isInvulnerable = true;
                    if (rb != null) rb.linearVelocity = Vector2.zero;
                    transform.position = Vector3.Lerp(startPos, airPos, t / 0.2f);
                    t += Time.deltaTime;
                    yield return null;
                }
                transform.position = airPos;

                if (IsDead || currentState != CharacterState.Skill) yield break;

                Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
                float mapVerticalHeight = (cam != null && cam.orthographic) ? cam.orthographicSize * 2f : 10f;
                Vector3 overheadOffset = new Vector3(0f, Mathf.Max(2.8f, mapVerticalHeight * 0.28f), 0f);
                Vector3 spawnPos = transform.position + overheadOffset;

                GameObject bombObj = genkiDamaPrefab != null
                    ? Instantiate(genkiDamaPrefab, spawnPos, Quaternion.identity)
                    : new GameObject("GenkiDama_Runtime");
                bombObj.transform.position = spawnPos;
                activeChargingGenkiDama = bombObj;

                if (bombObj.TryGetComponent(out Explosion legacyExp))
                {
                    legacyExp.enabled = false;
                    Destroy(legacyExp);
                }
                if (bombObj.TryGetComponent(out Projectile legacyProj))
                {
                    legacyProj.enabled = false;
                    Destroy(legacyProj);
                }

                GenkiDamaProjectile genkiProj = bombObj.GetComponent<GenkiDamaProjectile>();
                if (genkiProj == null)
                {
                    genkiProj = bombObj.AddComponent<GenkiDamaProjectile>();
                }

                float rawDamage = (stats != null ? stats.specialComboDamage : 35f) * (isKaiokenActive ? kaiokenDamageBonus : 1f);
                float genkiDamage = GetModifiedDamage(rawDamage);
                genkiProj.Initialize(gameObject, genkiDamage, facingDirection, enemyLayerMask, EnemyBase.GROUND_Y);

                // Gồng tụ năng lượng Spirit Bomb trên đỉnh đầu trong ~0.55s
                const float chargeDuration = 0.55f;
                float chargeElapsed = 0f;
                while (chargeElapsed < chargeDuration)
                {
                    if (IsDead || currentState != CharacterState.Skill) yield break;
                    isInvulnerable = true;
                    if (rb != null) rb.linearVelocity = Vector2.zero;

                    float progress = Mathf.Clamp01(chargeElapsed / chargeDuration);
                    float easedProgress = 1f - Mathf.Pow(1f - progress, 2f);
                    genkiProj.SetChargeProgress(easedProgress);
                    bombObj.transform.position = transform.position + overheadOffset;

                    CameraFollowBeatEmUp.Instance?.Shake(0.08f, Mathf.Lerp(0.03f, 0.09f, progress));

                    chargeElapsed += Time.deltaTime;
                    yield return null;
                }

                if (IsDead || currentState != CharacterState.Skill) yield break;

                genkiProj.SetChargeProgress(1f);
                bombObj.transform.position = transform.position + overheadOffset;

                // Phóng quả cầu Genki Dama hướng về mục tiêu gần nhất phía trước & mặt đất
                Vector2 launchVelocity = genkiProj.ComputeTargetAimVelocity(bombObj.transform.position);
                genkiProj.Launch(launchVelocity);
                bombLaunched = true;
                activeChargingGenkiDama = null;

                yield return new WaitForSeconds(0.35f);
            }
            finally
            {
                if (!bombLaunched)
                {
                    CleanupUnlaunchedGenkiDama();
                }
                isGravityOverriddenBySkill = false;
                ApplyEffectiveGravity();
                isInvulnerable = false;
            }

            if (currentState == CharacterState.Skill)
            {
                ChangeState(CharacterState.Idle);
            }
        }
        #endregion
    }
}
