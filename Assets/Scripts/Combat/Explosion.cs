using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Combat
{
    /// <summary>
    /// Vùng nổ diện rộng AOE (Dùng cho Nổ Ki của Goku, Genki Dama, Bom Tự Sát Kamikaze, Thunder Spear)
    /// </summary>
    public class Explosion : MonoBehaviour, IPoolableCombatObject
    {
        [Header("Explosion Settings")]
        [SerializeField] private float radius = 3f;
        [SerializeField] private float damage = 40f;
        [SerializeField] private Vector2 knockback = new Vector2(8f, 5f);
        [SerializeField] private AttackType attackType = AttackType.Combo_DashJ;
        [SerializeField] private bool breaksSuperArmor = true;
        [SerializeField] private LayerMask targetLayer;
        [SerializeField] private float duration = 0.3f;

        [Header("Visual")]
        [SerializeField] private GameObject visualEffectPrefab;

        private GameObject sourceAttacker;
        private bool hasTriggered = false;
        private Action<Explosion> returnToPoolCallback;
        private Coroutine despawnRoutine;

        public void SetReturnToPoolCallback(Action<Explosion> callback)
        {
            returnToPoolCallback = callback;
        }

        public void SetPoolReturnAction(Action<Explosion> callback)
        {
            returnToPoolCallback = callback;
        }

        public void OnSpawnedFromPool()
        {
            hasTriggered = false;
            sourceAttacker = null;
            if (despawnRoutine != null)
            {
                StopCoroutine(despawnRoutine);
                despawnRoutine = null;
            }
        }

        public void OnReturnedToPool()
        {
            hasTriggered = false;
            sourceAttacker = null;
            if (despawnRoutine != null)
            {
                StopCoroutine(despawnRoutine);
                despawnRoutine = null;
            }
        }

        public void Setup(float customDamage, float customRadius, LayerMask targetMask, GameObject attacker, AttackType type, bool breaksArmor)
        {
            damage = customDamage;
            radius = customRadius;
            targetLayer = targetMask;
            sourceAttacker = attacker;
            attackType = type;
            breaksSuperArmor = breaksArmor;

            TriggerExplosion();
        }

        public void Setup(float customDamage, float customRadius, Vector2 customKnockback, LayerMask targetMask, GameObject attacker, AttackType type, bool breaksArmor)
        {
            damage = customDamage;
            radius = customRadius;
            knockback = customKnockback;
            targetLayer = targetMask;
            sourceAttacker = attacker;
            attackType = type;
            breaksSuperArmor = breaksArmor;

            TriggerExplosion();
        }

        private void Start()
        {
            if (!hasTriggered && sourceAttacker == null)
            {
                TriggerExplosion();
            }
        }

        private void TriggerExplosion()
        {
            if (hasTriggered) return;
            hasTriggered = true;

            if (visualEffectPrefab != null)
            {
                Instantiate(visualEffectPrefab, transform.position, Quaternion.identity);
            }

            // Quét mục tiêu trúng nổ
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, targetLayer);
            HashSet<IDamageable> damaged = new HashSet<IDamageable>();

            foreach (var col in hits)
            {
                IDamageable target = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead && !damaged.Contains(target))
                {
                    if (sourceAttacker != null && target.Transform.gameObject == sourceAttacker) continue;

                    damaged.Add(target);

                    Vector2 dir = (col.transform.position - transform.position).normalized;
                    Vector2 kb = new Vector2(Mathf.Sign(dir.x) * Mathf.Abs(knockback.x), knockback.y);

                    DamageData data = new DamageData(damage, kb, attackType, breaksSuperArmor, sourceAttacker);
                    target.TakeDamage(data);

                    StickmanBeatEmUp.Characters.BaseCharacter.NotifyAttackerHit(sourceAttacker, attackType, target);
                }
            }

            ScheduleReleaseOrDestroy();
        }

        private void ScheduleReleaseOrDestroy()
        {
            if (returnToPoolCallback != null)
            {
                if (despawnRoutine != null)
                {
                    StopCoroutine(despawnRoutine);
                }
                despawnRoutine = StartCoroutine(ReleaseAfterDurationRoutine());
                return;
            }

            Destroy(gameObject, duration);
        }

        private IEnumerator ReleaseAfterDurationRoutine()
        {
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }
            despawnRoutine = null;
            returnToPoolCallback?.Invoke(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
