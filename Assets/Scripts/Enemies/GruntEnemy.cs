using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// Grunt (Cận chiến cỏ):
    /// - HP thấp, lao vào đánh gần
    /// - Không có Super Armor, dễ bị khựng (Stagger) bởi bất kỳ đòn đánh nào
    /// - Nhấp nháy đỏ 0.5s (Telegraph) trước khi vung đấm
    /// </summary>
    public class GruntEnemy : BaseEnemy
    {
        protected override void UpdateFSM()
        {
            if (currentState == EnemyState.Telegraph || currentState == EnemyState.Attack || currentState == EnemyState.Hurt) return;

            float dist = Vector2.Distance(transform.position, playerTransform.position);

            if (dist <= stats.attackRange)
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
            else
            {
                ChangeState(EnemyState.Chase);
                Vector2 dir = (playerTransform.position - transform.position).normalized;
                rb.linearVelocity = new Vector2(dir.x * stats.moveSpeed, rb.linearVelocity.y);
            }
        }

        protected override IEnumerator AttackRoutine()
        {
            // Lao tới phía trước một chút và vung đấm
            rb.linearVelocity = new Vector2(facingDirection * 4f, 0f);

            DamageData dData = new DamageData(
                stats.attackDamage,
                new Vector2(facingDirection * stats.knockbackOnAttack.x, stats.knockbackOnAttack.y),
                AttackType.EnemyAttack,
                false,
                gameObject
            );

            if (attackHitbox != null) attackHitbox.Activate(dData);
            yield return new WaitForSeconds(0.2f);
            if (attackHitbox != null) attackHitbox.Deactivate();

            rb.linearVelocity = Vector2.zero;
            lastAttackTime = Time.time;

            yield return new WaitForSeconds(0.2f);
            if (currentState == EnemyState.Attack)
            {
                ChangeState(EnemyState.Chase);
            }
        }
    }
}
