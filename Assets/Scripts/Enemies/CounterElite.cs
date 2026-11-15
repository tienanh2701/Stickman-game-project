using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// World 1 (AoT) Elite: Female Titan (Counter/Martial Brawler).
    /// Animator States: 0=FightStance, 1=DashRush, 2=HighKick, 3=HardenedPunch, 4=Parry, 5=Death.
    /// </summary>
    public class CounterElite : EliteEnemy
    {
        [Header("Female Titan Martial Mechanics")]
        [SerializeField] private float dashRushSpeed = 11f;
        [SerializeField] private float parryWindowHits = 3f;

        private int recentLightHits;
        private float lastHitTimestamp;
        private bool isParrying;

        protected override void TickBehavior()
        {
            if (!isActing) isSuperArmorActive = false;
            float distX = Mathf.Abs(playerTarget.position.x - transform.position.x);
            float range = enemyData != null ? enemyData.attackRange : 1.9f;

            if (distX > range && distX <= 7.5f && Time.time >= nextSpecialTime)
            {
                actionRoutine = StartCoroutine(DashRushHighKickRoutine());
                return;
            }

            if (distX <= range)
            {
                StopHorizontalMovement();
                FaceTarget();

                if (Time.time >= nextAttackTime)
                {
                    bool usePunch = UnityEngine.Random.value > 0.45f;
                    actionRoutine = StartCoroutine(usePunch ? HardenedPunchRoutine() : CloseHighKickRoutine());
                }
                else
                {
                    SetAnimState(0);
                }
            }
            else
            {
                float speed = enemyData != null ? enemyData.moveSpeed : 4.2f;
                MoveHorizontally(Mathf.Sign(playerTarget.position.x - transform.position.x), speed);
                SetAnimState(0);
            }
        }

        public override void TakeDamage(DamageData damageData)
        {
            if (IsDead) return;

            // Count rapid light J combos to trigger hardened Martial Parry counter
            if (damageData.attackType == AttackType.NormalAttack_J && !isActing && !isStaggered && !isParrying)
            {
                if (Time.time - lastHitTimestamp > 1.8f) recentLightHits = 0;
                recentLightHits++;
                lastHitTimestamp = Time.time;

                if (recentLightHits >= parryWindowHits)
                {
                    recentLightHits = 0;
                    actionRoutine = StartCoroutine(ParryCounterRoutine(damageData.attacker));
                    DamageData reduced = damageData;
                    reduced.damageAmount *= 0.35f;
                    base.TakeDamage(reduced);
                    return;
                }
            }

            base.TakeDamage(damageData);
            if (isStaggered || IsDead)
            {
                isSuperArmorActive = false;
                isParrying = false;
            }
        }

        private IEnumerator ParryCounterRoutine(GameObject attacker)
        {
            isActing = true;
            isParrying = true;
            isSuperArmorActive = true;
            StopHorizontalMovement();
            FaceTarget();
            SetAnimState(4); // Parry stance

            if (spriteRenderer != null) spriteRenderer.color = new Color(0.75f, 0.92f, 1f, 1f);
            yield return new WaitForSeconds(0.22f);

            // Counter kick/shock pushback
            if (attacker != null)
            {
                IDamageable target = attacker.GetComponent<IDamageable>() ?? attacker.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead)
                {
                    float dmg = enemyData != null ? EffectiveHeavyDamage : 15f * runtimeDamageMult;
                    target.TakeDamage(new DamageData(dmg, new Vector2(facingDirection * 6f, 2.5f), AttackType.EnemyAttack, false, gameObject));
                }
            }

            yield return new WaitForSeconds(0.25f);
            if (spriteRenderer != null && !IsDead) spriteRenderer.color = originalColor;
            isSuperArmorActive = false;
            isParrying = false;
            SetAnimState(0);
            isActing = false;
        }

        private IEnumerator DashRushHighKickRoutine()
        {
            isActing = true;
            isSuperArmorActive = true;
            FaceTarget();
            yield return PlayTelegraphFlash(0.45f);
            if (IsDead || isFrozen) { isSuperArmorActive = false; yield break; }

            SetAnimState(1); // DashRush
            float t = 0f;
            while (t < 0.32f && !IsDead && !isFrozen)
            {
                MoveHorizontally(facingDirection, dashRushSpeed);
                if (playerTarget != null && Mathf.Abs(playerTarget.position.x - transform.position.x) <= 1.7f)
                    break;
                t += Time.deltaTime;
                yield return null;
            }

            StopHorizontalMovement();
            FaceTarget();
            SetAnimState(2); // HighKick
            yield return new WaitForSeconds(0.12f);

            float dmg = enemyData != null ? enemyData.attackDamage : 10f;
            ActivateMeleeHitbox(dmg, AttackType.EnemyHeavyAttack, true);
            yield return new WaitForSeconds(0.25f);
            DeactivateMeleeHitbox();

            yield return new WaitForSeconds(0.2f);
            nextSpecialTime = Time.time + 3.6f / Mathf.Max(0.1f, runtimeAttackSpeedMult);
            nextAttackTime = Time.time + EffectiveAttackCooldown;
            isSuperArmorActive = false;
            SetAnimState(0);
            isActing = false;
        }

        private IEnumerator CloseHighKickRoutine()
        {
            isActing = true;
            isSuperArmorActive = true;
            StopHorizontalMovement();
            FaceTarget();
            yield return PlayTelegraphFlash(enemyData != null ? enemyData.telegraphDuration : 0.45f);
            if (IsDead || isFrozen) { isSuperArmorActive = false; yield break; }

            SetAnimState(2);
            yield return new WaitForSeconds(0.16f);
            float dmg = enemyData != null ? enemyData.attackDamage : 10f;
            ActivateMeleeHitbox(dmg, AttackType.EnemyAttack);
            yield return new WaitForSeconds(0.25f);
            DeactivateMeleeHitbox();

            nextAttackTime = Time.time + EffectiveAttackCooldown;
            isSuperArmorActive = false;
            SetAnimState(0);
            isActing = false;
        }

        private IEnumerator HardenedPunchRoutine()
        {
            isActing = true;
            isSuperArmorActive = true;
            StopHorizontalMovement();
            FaceTarget();
            yield return PlayTelegraphFlash(0.55f, new Color(0.6f, 0.9f, 1f, 1f));
            if (IsDead || isFrozen) { isSuperArmorActive = false; yield break; }

            SetAnimState(3); // HardenedPunch
            yield return new WaitForSeconds(0.18f);
            float dmg = enemyData != null ? enemyData.attackDamage : 10f;
            ActivateMeleeHitbox(dmg, AttackType.EnemyHeavyAttack, true);
            yield return new WaitForSeconds(0.28f);
            DeactivateMeleeHitbox();

            nextAttackTime = Time.time + EffectiveAttackCooldown + (0.4f / Mathf.Max(0.1f, runtimeAttackSpeedMult));
            isSuperArmorActive = false;
            SetAnimState(0);
            isActing = false;
        }
    }
}
