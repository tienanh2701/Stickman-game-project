using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Combat
{
    /// <summary>
    /// Gót chân Titan khổng lồ giẫm ngang màn hình (Combo Đặc Biệt Eren Titan Dash + I)
    /// Quét sạch toàn bộ kẻ thù trong phạm vi với hiệu ứng chấn động mạnh
    /// </summary>
    public class TitanStomp : MonoBehaviour
    {
        [Header("Stomp Attack Settings")]
        [SerializeField] private float damage = 35f;
        [SerializeField] private Vector2 stompAreaSize = new Vector2(3.8f, 6f);
        [SerializeField] private LayerMask targetLayer;
        [SerializeField] private float delayBeforeHit = 0.2f;
        [SerializeField] private float stompScale = 1.6f;

        [Header("Visual & Camera Shake FX")]
        [SerializeField] private GameObject impactVfxPrefab;

        private GameObject attacker;
        private Vector3 landingPosition;
        private Coroutine stompCoroutine;
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        public void Setup(float customDamage, LayerMask mask, GameObject source)
        {
            Setup(customDamage, mask, source, stompAreaSize, delayBeforeHit, stompScale);
        }

        public void Setup(float customDamage, LayerMask mask, GameObject source, Vector2 customAreaSize, float customDelay, float customScale)
        {
            CancelInvoke(nameof(ExecuteStomp));
            if (stompCoroutine != null)
            {
                StopCoroutine(stompCoroutine);
                stompCoroutine = null;
            }

            damage = customDamage;
            targetLayer = mask;
            attacker = source;
            stompAreaSize = customAreaSize;
            delayBeforeHit = Mathf.Max(0.05f, customDelay);
            stompScale = customScale > 0f ? customScale : 1.6f;

            transform.localScale = new Vector3(stompScale, stompScale, 1f);
            landingPosition = transform.position;

            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = 1f;
                spriteRenderer.color = c;
            }

            stompCoroutine = StartCoroutine(StompLifecycleRoutine());
        }

        private IEnumerator StompLifecycleRoutine()
        {
            Vector3 dropStart = landingPosition + Vector3.up * 5.2f;
            transform.position = dropStart;

            float elapsed = 0f;
            while (elapsed < delayBeforeHit)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / delayBeforeHit);
                float eased = t * t; // Gia tốc rơi xuống mạnh mẽ
                transform.position = Vector3.Lerp(dropStart, landingPosition, eased);
                yield return null;
            }

            transform.position = landingPosition;
            ExecuteStomp();

            // Giữ gót chân hiển thị ngắn rồi mờ dần
            yield return new WaitForSeconds(0.28f);

            float fadeTime = 0.25f;
            float fadeElapsed = 0f;
            while (fadeElapsed < fadeTime)
            {
                fadeElapsed += Time.deltaTime;
                if (spriteRenderer != null)
                {
                    Color c = spriteRenderer.color;
                    c.a = Mathf.Lerp(1f, 0f, fadeElapsed / fadeTime);
                    spriteRenderer.color = c;
                }
                yield return null;
            }

            stompCoroutine = null;
            Destroy(gameObject);
        }

        private void ExecuteStomp()
        {
            if (impactVfxPrefab != null)
            {
                GameObject vfx = Instantiate(impactVfxPrefab, landingPosition, Quaternion.identity);
                Destroy(vfx, 1.0f);
            }

            CameraFollowBeatEmUp.Instance?.Shake(0.2f, 0.24f);

            // Quét toàn bộ kẻ thù trong phạm vi giẫm của bàn chân Titan
            Collider2D[] hits = Physics2D.OverlapBoxAll(landingPosition, stompAreaSize, 0f, targetLayer);
            HashSet<IDamageable> damaged = new HashSet<IDamageable>();

            foreach (var col in hits)
            {
                IDamageable target = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead && !damaged.Contains(target))
                {
                    if (attacker != null && target.Transform.gameObject == attacker) continue;

                    damaged.Add(target);

                    DamageData dData = new DamageData(
                        damage,
                        new Vector2(0f, 12f), // Đập dẹp / hất tung mạnh
                        AttackType.Combo_DashI,
                        true,
                        attacker
                    );

                    target.TakeDamage(dData);
                    StickmanBeatEmUp.Characters.BaseCharacter.NotifyAttackerHit(attacker, AttackType.Combo_DashI, target);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position, stompAreaSize);
        }
    }
}
