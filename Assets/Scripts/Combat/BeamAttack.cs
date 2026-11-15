using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Combat
{
    /// <summary>
    /// Tia năng lượng / Laser kéo dài liên tục hoặc bắn quét (Kamehameha, Tia Lazer Boss, Đạn xuyên Diend)
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class BeamAttack : MonoBehaviour
    {
        [Header("Beam Config")]
        [SerializeField] private float beamLength = 15f;
        [SerializeField] private float beamWidth = 1.5f;
        [SerializeField] private float duration = 1.0f;
        [SerializeField] private float tickRate = 0.2f;
        [SerializeField] private LayerMask targetLayer;

        [Header("Visual")]
        [SerializeField] private LineRenderer lineRenderer;

        private BoxCollider2D boxCol;
        private DamageData damageData;
        private float timer = 0f;
        private float nextTickTime = 0f;
        private int direction = 1;

        private void Awake()
        {
            boxCol = GetComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            UpdateColliderSize();
        }

        public void SetupBeam(DamageData damage, int dir, LayerMask mask, float activeDuration, float length = 15f, float width = 1.5f)
        {
            damageData = damage;
            direction = dir;
            targetLayer = mask;
            duration = activeDuration;
            beamLength = length;
            beamWidth = width;

            UpdateColliderSize();
            RenderBeamVisual();
        }

        private void UpdateColliderSize()
        {
            if (boxCol != null)
            {
                boxCol.size = new Vector2(beamLength, beamWidth);
                boxCol.offset = new Vector2(direction * (beamLength / 2f), 0f);
            }
        }

        private void RenderBeamVisual()
        {
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, transform.position);
                lineRenderer.SetPosition(1, transform.position + new Vector3(direction * beamLength, 0, 0));
            }
        }

        private void Update()
        {
            timer += Time.deltaTime;
            RenderBeamVisual();

            if (Time.time >= nextTickTime)
            {
                ApplyDamageTick();
                nextTickTime = Time.time + tickRate;
            }

            if (timer >= duration)
            {
                Destroy(gameObject);
            }
        }

        private void ApplyDamageTick()
        {
            Vector2 origin = (Vector2)transform.position + new Vector2(direction * (beamLength / 2f), 0f);
            Vector2 size = new Vector2(beamLength, beamWidth);

            Collider2D[] hits = Physics2D.OverlapBoxAll(origin, size, 0f, targetLayer);
            HashSet<IDamageable> damaged = new HashSet<IDamageable>();

            foreach (var col in hits)
            {
                IDamageable target = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead && !damaged.Contains(target))
                {
                    if (damageData.attacker != null && target.Transform.gameObject == damageData.attacker) continue;

                    damaged.Add(target);

                    DamageData tickDamage = damageData;
                    tickDamage.knockbackForce = new Vector2(direction * Mathf.Abs(damageData.knockbackForce.x), damageData.knockbackForce.y);

                    target.TakeDamage(tickDamage);
                    StickmanBeatEmUp.Characters.BaseCharacter.NotifyAttackerHit(damageData.attacker, damageData.attackType, target);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector2 center = (Vector2)transform.position + new Vector2(direction * (beamLength / 2f), 0f);
            Gizmos.DrawWireCube(center, new Vector2(beamLength, beamWidth));
        }
    }
}
