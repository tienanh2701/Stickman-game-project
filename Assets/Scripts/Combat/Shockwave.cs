using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Combat
{
    /// <summary>
    /// Sóng xung kích di chuyển sát mặt đất do đòn đập đất của Eren Titan hoặc Elite Bruiser
    /// Bắt buộc Player phải dùng phím Nhảy (Space) để né đòn
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
    public class Shockwave : MonoBehaviour
    {
        [Header("Shockwave Movement")]
        [SerializeField] private float speed = 8f;
        [SerializeField] private float lifeTime = 2.5f;
        [SerializeField] private Vector2 direction = Vector2.right;

        [Header("Damage & Mechanics")]
        [SerializeField] private float damage = 25f;
        [SerializeField] private Vector2 knockback = new Vector2(4f, 6f);
        [SerializeField] private AttackType attackType = AttackType.EnemyHeavyAttack;
        [SerializeField] private LayerMask targetLayer;

        private Rigidbody2D rb;
        private GameObject attacker;
        private HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            GetComponent<Collider2D>().isTrigger = true;
        }

        public void Initialize(float customDamage, Vector2 dir, LayerMask mask, GameObject source, float customSpeed = -1f, AttackType customAttackType = AttackType.EnemyHeavyAttack)
        {
            damage = customDamage;
            direction = dir.normalized;
            targetLayer = mask;
            attacker = source;
            attackType = customAttackType;
            if (customSpeed > 0f)
            {
                speed = customSpeed;
            }

            // Lật hình theo hướng chạy
            transform.localScale = new Vector3(Mathf.Sign(direction.x) * Mathf.Abs(transform.localScale.x), transform.localScale.y, 1f);

            rb.linearVelocity = direction * speed;
            Destroy(gameObject, lifeTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (((1 << other.gameObject.layer) & targetLayer.value) == 0) return;

            IDamageable target = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
            if (target != null && !target.IsDead && !damagedTargets.Contains(target))
            {
                if (attacker != null && target.Transform.gameObject == attacker) return;

                damagedTargets.Add(target);

                float dirSign = Mathf.Abs(direction.x) > 0.01f ? Mathf.Sign(direction.x) : 1f;
                DamageData dData = new DamageData(
                    damage,
                    new Vector2(dirSign * Mathf.Abs(knockback.x), knockback.y),
                    attackType,
                    true,
                    attacker
                );

                target.TakeDamage(dData);
                StickmanBeatEmUp.Characters.BaseCharacter.NotifyAttackerHit(attacker, attackType, target);
            }
        }
    }
}
