using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// Ranged Creep (Beast Titan, Frieza Blaster Soldier, Kasshin Trooper).
    /// Setup windups (pickup boulder, aim lock, plant staff) are merged into Attack1/Attack2 opening frames.
    /// </summary>
    public class RangedCreep : EnemyBase
    {
        [Header("Ranged Combat")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private int volleyCount = 1;
        [SerializeField] private float volleySpreadDeg = 8f;

        private bool useSecondaryShot;

        public void ConfigureRanged(GameObject projPrefab, int burst = 1)
        {
            projectilePrefab = projPrefab;
            volleyCount = Mathf.Max(1, burst);
        }

        protected override void TickBehavior()
        {
            float dx = playerTarget.position.x - transform.position.x;
            float distX = Mathf.Abs(dx);
            float pref = enemyData != null ? enemyData.preferredRange : 6.5f;
            float flee = enemyData != null ? enemyData.fleeDistance : 2.5f;
            float speed = enemyData != null ? enemyData.moveSpeed : 2.8f;

            if (distX < flee)
            {
                // Keep safe spacing while retreating
                MoveHorizontally(-Mathf.Sign(dx), speed * 0.9f);
                SetAnimState(0);
                return;
            }

            if (distX > pref * 1.15f)
            {
                MoveHorizontally(Mathf.Sign(dx), speed);
                SetAnimState(0);
                return;
            }

            StopHorizontalMovement();
            FaceTarget();

            if (Time.time >= nextAttackTime)
            {
                actionRoutine = StartCoroutine(FireVolleyRoutine());
            }
            else
            {
                SetAnimState(0);
            }
        }

        private IEnumerator FireVolleyRoutine()
        {
            isActing = true;
            StopHorizontalMovement();
            FaceTarget();

            float telegraph = enemyData != null ? enemyData.telegraphDuration : 0.5f;
            yield return PlayTelegraphFlash(telegraph);
            if (IsDead || isFrozen) yield break;

            int clipState = useSecondaryShot ? 2 : 1;
            useSecondaryShot = !useSecondaryShot;
            SetAnimState(clipState);

            // Opening pickup / aim / plant-staff frames merged into clip start
            yield return new WaitForSeconds(0.24f);
            if (IsDead || isFrozen) yield break;

            SpawnProjectiles();

            yield return new WaitForSeconds(0.32f);
            nextAttackTime = Time.time + EffectiveAttackCooldown;
            SetAnimState(0);
            isActing = false;
        }

        private void SpawnProjectiles()
        {
            if (projectilePrefab == null) return;

            Vector3 spawnPos = firePoint != null
                ? firePoint.position
                : transform.position + new Vector3(facingDirection * 0.9f, 1.0f, 0f);

            float dmg = enemyData != null ? EffectiveAttackDamage : 12f * runtimeDamageMult;
            float projSpeed = enemyData != null ? enemyData.projectileSpeed : 11f;
            Vector2 baseDir = new Vector2(facingDirection, 0f);

            int count = Mathf.Max(1, volleyCount);
            float startAngle = -(count - 1) * 0.5f * volleySpreadDeg;

            for (int i = 0; i < count; i++)
            {
                float ang = startAngle + i * volleySpreadDeg;
                Vector2 dir = Quaternion.Euler(0f, 0f, ang) * baseDir;

                GameObject go = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
                Projectile proj = go.GetComponent<Projectile>();
                if (proj != null)
                {
                    proj.SetSpeed(projSpeed);
                    Vector2 kb = new Vector2(facingDirection * 3f, 1.5f);
                    proj.Initialize(new DamageData(dmg, kb, AttackType.EnemyAttack, false, gameObject), dir, playerLayer, false);
                }
            }
        }
    }
}
