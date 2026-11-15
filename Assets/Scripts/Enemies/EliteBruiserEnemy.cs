using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Combat;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// Elite Bruiser (Quái tinh anh trâu bò):
    /// - HP cực cao, kích thước to lớn (Scale 1.4x)
    /// - Có "Super Armor": Miễn nhiễm khựng từ đòn đánh thường J, chỉ bị ngắt đòn bởi U, I hoặc Combo!
    /// - Đập đất tạo Sóng Xung Kích (Shockwave) bò sát mặt đất: Bắt buộc Player phải Nhảy (Space) để né!
    /// </summary>
    public class EliteBruiserEnemy : BaseEnemy
    {
        [Header("Elite Bruiser Settings")]
        [SerializeField] private GameObject groundShockwavePrefab;
        [SerializeField] private Transform slamImpactPoint;
        [SerializeField] private float shockwaveSpeed = 9f;

        protected override void Awake()
        {
            base.Awake();
            // Đảm bảo kích thước to lớn hơn quái thường
            transform.localScale = new Vector3(1.4f, 1.4f, 1f);
        }

        protected override void UpdateFSM()
        {
            if (currentState == EnemyState.Telegraph || currentState == EnemyState.Attack ||
                currentState == EnemyState.Hurt || currentState == EnemyState.Stunned) return;

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
            rb.linearVelocity = Vector2.zero;

            // Nhấc 2 tay lên cao chuẩn bị đập đất
            yield return new WaitForSeconds(0.2f);

            Vector3 impactPos = slamImpactPoint != null ? slamImpactPoint.position : transform.position + new Vector3(facingDirection * 1f, -0.5f, 0);

            // Gây sát thương cận chiến vùng đập đất
            Collider2D[] meleeHits = Physics2D.OverlapCircleAll(impactPos, 1.5f, playerLayer);
            foreach (var col in meleeHits)
            {
                IDamageable target = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead)
                {
                    target.TakeDamage(new DamageData(
                        stats.attackDamage,
                        new Vector2(facingDirection * stats.knockbackOnAttack.x, stats.knockbackOnAttack.y),
                        AttackType.EnemyHeavyAttack,
                        true,
                        gameObject
                    ));
                }
            }

            // Phóng sóng xung kích sát mặt đất hướng về phía Player
            if (groundShockwavePrefab != null)
            {
                GameObject wave = Instantiate(groundShockwavePrefab, impactPos, Quaternion.identity);
                Shockwave sw = wave.GetComponent<Shockwave>();
                if (sw != null)
                {
                    sw.Initialize(stats.attackDamage * 0.8f, new Vector2(facingDirection, 0f), playerLayer, gameObject, shockwaveSpeed);
                }
            }

            lastAttackTime = Time.time;
            yield return new WaitForSeconds(0.4f);

            if (currentState == EnemyState.Attack)
            {
                ChangeState(EnemyState.Idle);
            }
        }
    }
}
