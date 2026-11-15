using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// World 3 (Kamen Rider Diend) Elite: Shadow Moon (Menacing Walk -> Corner Teleport -> Crescent Slash Wave).
    /// Animator States: 0=Walk, 1=SwordCharge, 2=CrescentSlash, 3=Teleport, 4=Hit, 5=Death.
    /// </summary>
    public class TeleportElite : EliteEnemy
    {
        [Header("Shadow Moon Teleport & Wave")]
        [SerializeField] private GameObject crescentWavePrefab;
        [SerializeField] private float teleportTriggerDistance = 2.1f;

        public void ConfigureWave(GameObject wavePrefab)
        {
            crescentWavePrefab = wavePrefab;
        }

        protected override void TickBehavior()
        {
            if (!isActing) isSuperArmorActive = false;
            float distX = Mathf.Abs(playerTarget.position.x - transform.position.x);

            // Teleport away behind player when pressured up close
            if (distX <= teleportTriggerDistance && Time.time >= nextSpecialTime)
            {
                actionRoutine = StartCoroutine(TeleportAndWaveRoutine());
                return;
            }

            float range = enemyData != null ? enemyData.attackRange : 1.9f;
            if (distX <= range)
            {
                StopHorizontalMovement();
                FaceTarget();
                if (Time.time >= nextAttackTime)
                {
                    actionRoutine = StartCoroutine(CloseSwordSlashRoutine());
                }
                else
                {
                    SetAnimState(0);
                }
            }
            else if (distX >= 4.5f && Time.time >= nextSpecialTime)
            {
                actionRoutine = StartCoroutine(LongRangeCrescentWaveRoutine());
            }
            else
            {
                // Menacing calm walk toward player
                float speed = enemyData != null ? enemyData.moveSpeed : 2.6f;
                MoveHorizontally(Mathf.Sign(playerTarget.position.x - transform.position.x), speed);
                SetAnimState(0);
            }
        }

        public override void TakeDamage(DamageData damageData)
        {
            base.TakeDamage(damageData);
            if (isStaggered || IsDead)
            {
                isSuperArmorActive = false;
            }
        }

        private IEnumerator TeleportAndWaveRoutine()
        {
            isActing = true;
            isSuperArmorActive = true;
            StopHorizontalMovement();
            SetAnimState(3); // Teleport blink
            if (spriteRenderer != null) spriteRenderer.color = new Color(0.55f, 0.9f, 1f, 0.35f);

            yield return new WaitForSeconds(0.22f);
            if (IsDead || isFrozen) { isSuperArmorActive = false; yield break; }

            // Warp behind / opposite corner of player
            float side = (transform.position.x < playerTarget.position.x) ? 4.4f : -4.4f;
            Vector3 dest = new Vector3(playerTarget.position.x + side, GROUND_Y, transform.position.z);
            transform.position = dest;

            if (spriteRenderer != null) spriteRenderer.color = originalColor;
            FaceTarget();
            yield return LongRangeCrescentWaveBody();
        }

        private IEnumerator LongRangeCrescentWaveRoutine()
        {
            isActing = true;
            isSuperArmorActive = true;
            StopHorizontalMovement();
            FaceTarget();
            yield return LongRangeCrescentWaveBody();
        }

        private IEnumerator LongRangeCrescentWaveBody()
        {
            SetAnimState(1); // SwordCharge
            yield return PlayTelegraphFlash(0.5f, new Color(1f, 0.25f, 0.35f, 1f));
            if (IsDead || isFrozen) { isSuperArmorActive = false; yield break; }

            SetAnimState(2); // CrescentSlash
            yield return new WaitForSeconds(0.16f);

            if (crescentWavePrefab != null)
            {
                Vector3 spawnPos = transform.position + new Vector3(facingDirection * 0.9f, 1.0f, 0f);
                GameObject go = Instantiate(crescentWavePrefab, spawnPos, Quaternion.identity);
                Projectile proj = go.GetComponent<Projectile>();
                if (proj != null)
                {
                    proj.SetSpeed(13f);
                    float dmg = enemyData != null ? EffectiveHeavyDamage : 15f * runtimeDamageMult;
                    proj.Initialize(new DamageData(dmg, new Vector2(facingDirection * 6f, 2f), AttackType.EnemyHeavyAttack, true, gameObject),
                        new Vector2(facingDirection, 0f), playerLayer, true);
                }
            }

            isSuperArmorActive = false;

            yield return new WaitForSeconds(0.32f);
            float spdDiv = Mathf.Max(0.1f, runtimeAttackSpeedMult);
            nextSpecialTime = Time.time + 3.8f / spdDiv;
            nextAttackTime = Time.time + 1.4f / spdDiv;
            SetAnimState(0);
            isActing = false;
        }

        private IEnumerator CloseSwordSlashRoutine()
        {
            isActing = true;
            StopHorizontalMovement();
            FaceTarget();
            SetAnimState(1);
            yield return PlayTelegraphFlash(enemyData != null ? enemyData.telegraphDuration : 0.45f);
            if (IsDead || isFrozen) yield break;

            SetAnimState(2);
            yield return new WaitForSeconds(0.15f);
            float dmg = enemyData != null ? enemyData.attackDamage : 10f;
            ActivateMeleeHitbox(dmg, AttackType.EnemyAttack);
            yield return new WaitForSeconds(0.24f);
            DeactivateMeleeHitbox();

            nextAttackTime = Time.time + EffectiveAttackCooldown;
            SetAnimState(0);
            isActing = false;
        }
    }
}
