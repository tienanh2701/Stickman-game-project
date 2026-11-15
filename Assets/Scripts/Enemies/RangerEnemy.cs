using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// Ranger (Quái bắn xa):
    /// - HP thấp, duy trì khoảng cách bắn tỉa
    /// - Bỏ chạy (Flee) nếu Player tiến vào cự ly quá gần (< fleeDistance)
    /// - Nhấp nháy đỏ 0.5s (Telegraph) trước khi bắn tia đạn thẳng
    /// </summary>
    public class RangerEnemy : BaseEnemy
    {
        [Header("Ranger Projectile Settings")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform firePoint;

        protected override void UpdateFSM()
        {
            if (currentState == EnemyState.Telegraph || currentState == EnemyState.Attack || currentState == EnemyState.Hurt) return;

            float dist = Vector2.Distance(transform.position, playerTransform.position);
            float fleeDist = stats != null ? stats.fleeDistance : 3.5f;
            float preferredDist = stats != null ? stats.preferredDistance : 7f;

            // Nếu người chơi áp sát quá gần -> Bỏ chạy ngược hướng
            if (dist < fleeDist)
            {
                ChangeState(EnemyState.Flee);
                Vector2 fleeDir = (transform.position - playerTransform.position).normalized;
                rb.linearVelocity = new Vector2(fleeDir.x * (stats.moveSpeed * 1.2f), rb.linearVelocity.y);
            }
            // Nếu nằm trong khoảng cách lý tưởng -> Chuẩn bị bắn
            else if (dist <= preferredDist)
            {
                rb.linearVelocity = Vector2.zero;
                if (Time.time >= lastAttackTime + stats.attackCooldown)
                {
                    ChangeState(EnemyState.Telegraph);
                }
                else
                {
                    ChangeState(EnemyState.Idle);
                }
            }
            // Nếu người chơi ở quá xa -> Tiến lại gần phạm vi bắn
            else
            {
                ChangeState(EnemyState.Chase);
                Vector2 dir = (playerTransform.position - transform.position).normalized;
                rb.linearVelocity = new Vector2(dir.x * stats.moveSpeed, rb.linearVelocity.y);
            }
        }

        protected override IEnumerator AttackRoutine()
        {
            rb.linearVelocity = Vector2.zero;

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + Vector3.right * facingDirection;

            if (bulletPrefab != null)
            {
                GameObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
                Projectile proj = bullet.GetComponent<Projectile>();
                if (proj != null)
                {
                    DamageData dData = new DamageData(
                        stats.attackDamage,
                        new Vector2(facingDirection * stats.knockbackOnAttack.x, stats.knockbackOnAttack.y),
                        AttackType.EnemyAttack,
                        false,
                        gameObject
                    );

                    proj.Initialize(dData, new Vector2(facingDirection, 0f), playerLayer, false);
                }
            }

            lastAttackTime = Time.time;
            yield return new WaitForSeconds(0.3f);

            if (currentState == EnemyState.Attack)
            {
                ChangeState(EnemyState.Idle);
            }
        }
    }
}
