using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Combat;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// Kamikaze (Quái tự sát):
    /// - Tốc độ chạy cực nhanh, lao thẳng vào Player
    /// - Khi áp sát cự ly gần: Khựng lại 1.5 giây, phát sáng phồng to nhấp nháy liên tục
    /// - Kích nổ AOE cực mạnh và tự hủy
    /// </summary>
    public class KamikazeEnemy : BaseEnemy
    {
        [Header("Kamikaze Specific")]
        [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private float triggerDistance = 2.0f;
        [SerializeField] private float explosionRadius = 4.0f;
        [SerializeField] private float countdownTime = 1.5f;

        private bool isPrimed = false;

        protected override void UpdateFSM()
        {
            if (isPrimed || currentState == EnemyState.Dead || currentState == EnemyState.Hurt || currentState == EnemyState.Stunned) return;

            float dist = Vector2.Distance(transform.position, playerTransform.position);

            if (dist <= triggerDistance)
            {
                // Bắt đầu đếm ngược nổ
                StartCoroutine(PrimedExplosionCountdown());
            }
            else
            {
                ChangeState(EnemyState.Chase);
                Vector2 dir = (playerTransform.position - transform.position).normalized;
                rb.linearVelocity = new Vector2(dir.x * stats.moveSpeed, rb.linearVelocity.y);
            }
        }

        private IEnumerator PrimedExplosionCountdown()
        {
            isPrimed = true;
            ChangeState(EnemyState.Telegraph);
            rb.linearVelocity = Vector2.zero;

            Vector3 origScale = transform.localScale;
            float elapsed = 0f;
            float flashInterval = 0.15f;
            bool isFlash = false;

            while (elapsed < countdownTime)
            {
                if (currentState == EnemyState.Dead) yield break;

                isFlash = !isFlash;
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = isFlash ? Color.red : Color.yellow;
                }

                // Hiệu ứng phồng to dần
                float progress = elapsed / countdownTime;
                transform.localScale = origScale * (1f + progress * 0.4f);

                yield return new WaitForSeconds(flashInterval);
                elapsed += flashInterval;
            }

            // Kích nổ
            Explode();
        }

        private void Explode()
        {
            if (currentState == EnemyState.Dead) return;

            Vector3 boomPos = transform.position;

            if (explosionPrefab != null)
            {
                GameObject exp = Instantiate(explosionPrefab, boomPos, Quaternion.identity);
                Explosion explosionScript = exp.GetComponent<Explosion>();
                if (explosionScript != null)
                {
                    explosionScript.Setup(stats.attackDamage, explosionRadius, playerLayer, gameObject, AttackType.EnemyHeavyAttack, true);
                }
            }

            // Tự hủy
            ChangeState(EnemyState.Dead);
        }

        protected override IEnumerator AttackRoutine()
        {
            // Kamikaze dùng PrimedExplosionCountdown thay vì attack thông thường
            yield return null;
        }
    }
}
