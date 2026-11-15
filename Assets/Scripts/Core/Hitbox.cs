using System.Collections.Generic;
using UnityEngine;

namespace StickmanBeatEmUp.Core
{
    /// <summary>
    /// Hitbox kích hoạt trong animation hoặc kỹ năng để gây sát thương lên mục tiêu
    /// Hỗ trợ lọc Layer, ngăn đánh trùng nhiều lần trong 1 nhát chém/đấm
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Hitbox : MonoBehaviour
    {
        [Header("Target Layer")]
        [Tooltip("Layer của mục tiêu nhận đòn (ví dụ: Enemy với Player, Player với Enemy)")]
        [SerializeField] private LayerMask targetLayer;

        private Collider2D col;
        private DamageData currentDamageData;
        private HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
        private bool isActive = false;

        public Collider2D Col => col;

        private void Awake()
        {
            col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
                col.enabled = false;
            }

            if (GetComponent<IDamageable>() == null)
            {
                gameObject.layer = 0;
            }
        }

        private void FixedUpdate()
        {
            if (isActive && col != null && col.enabled)
            {
                ScanOverlap();
            }
        }

        private void ScanOverlap()
        {
            if (!isActive || col == null || !col.enabled) return;

            if (col is BoxCollider2D box)
            {
                Vector2 center = box.transform.TransformPoint(box.offset);
                Vector2 size = new Vector2(Mathf.Abs(box.size.x * box.transform.lossyScale.x), Mathf.Abs(box.size.y * box.transform.lossyScale.y));
                Collider2D[] overlaps = Physics2D.OverlapBoxAll(center, size, box.transform.eulerAngles.z, targetLayer);
                if (overlaps != null)
                {
                    for (int i = 0; i < overlaps.Length; i++)
                    {
                        TryHitCollider(overlaps[i]);
                    }
                }
            }
            else if (col is CircleCollider2D circle)
            {
                Vector2 center = circle.transform.TransformPoint(circle.offset);
                float radius = circle.radius * Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y));
                Collider2D[] overlaps = Physics2D.OverlapCircleAll(center, radius, targetLayer);
                if (overlaps != null)
                {
                    for (int i = 0; i < overlaps.Length; i++)
                    {
                        TryHitCollider(overlaps[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Kích hoạt hitbox với gói sát thương tương ứng
        /// </summary>
        public void Activate(DamageData damageData)
        {
            if (col == null) col = GetComponent<Collider2D>();
            currentDamageData = damageData;
            hitTargets.Clear();
            isActive = true;
            if (col != null) col.enabled = true;
            ScanOverlap();
        }

        /// <summary>
        /// Tắt hitbox sau khi kết thúc đòn đánh
        /// </summary>
        public void Deactivate()
        {
            if (col == null) col = GetComponent<Collider2D>();
            isActive = false;
            if (col != null) col.enabled = false;
            hitTargets.Clear();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryHitCollider(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryHitCollider(other);
        }

        private void TryHitCollider(Collider2D other)
        {
            if (!isActive || other == null) return;

            // Kiểm tra Layer
            if (((1 << other.gameObject.layer) & targetLayer.value) == 0) return;

            // Tìm đối tượng có interface IDamageable
            IDamageable target = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
            if (target != null && !target.IsDead && !hitTargets.Contains(target))
            {
                if (currentDamageData.attacker != null && target.Transform.gameObject == currentDamageData.attacker)
                {
                    return;
                }

                hitTargets.Add(target);

                // Tính hướng knockback theo hướng của hitbox / attacker
                Vector2 knockback = currentDamageData.knockbackForce;
                bool facingLeft = transform.parent != null
                    ? transform.lossyScale.x < 0f
                    : (transform.lossyScale.x < 0f || (currentDamageData.attacker != null && currentDamageData.attacker.transform.localScale.x < 0f));

                knockback.x = facingLeft ? -Mathf.Abs(knockback.x) : Mathf.Abs(knockback.x);

                DamageData deliveredDamage = currentDamageData;
                deliveredDamage.knockbackForce = knockback;

                target.TakeDamage(deliveredDamage);

                StickmanBeatEmUp.Characters.BaseCharacter.NotifyAttackerHit(currentDamageData.attacker, currentDamageData.attackType, target);
            }
        }
    }
}
