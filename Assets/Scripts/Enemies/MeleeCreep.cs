using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.CorePure.AI;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// Standard close-combat Creep (Pure Titan, Frieza Soldier, Shocker Combatman, optional Saibaman Kamikaze).
    /// Shares the trimmed BaseCreep Animator layout: 0=Move, 1=Attack1 (with windup merged), 2=Attack2, 3=Hit, 4=Death.
    /// Delegates chase/attack/detonate decisions to the injected IAIStrategy (GoF Strategy).
    /// </summary>
    public class MeleeCreep : EnemyBase
    {
        [Header("Melee Creep Options")]
        [SerializeField] private bool useAlternatingAttack2 = true;
        [SerializeField] private bool isKamikaze = false;
        [SerializeField] protected float kamikazeExplosionRadius = 3.2f;
        [SerializeField] protected GameObject explosionVfxPrefab;

        private bool flipAttackToggle;

        protected override IAIStrategy CreateDefaultStrategy()
        {
            if (isKamikaze)
            {
                return new KamikazeRushStrategy();
            }

            return new MeleeChaseStrategy();
        }

        public override void ResetForPoolSpawn()
        {
            flipAttackToggle = false;
            base.ResetForPoolSpawn();
        }

        protected override void TickBehavior()
        {
            AIActionDecision decision = AIStrategy.ComputeAction(BuildAIContext());

            switch (decision.ActionType)
            {
                case AIActionType.Detonate:
                    StopHorizontalMovement();
                    if (decision.ShouldFacePlayer) FaceTarget();
                    actionRoutine = StartCoroutine(KamikazeRoutine());
                    break;

                case AIActionType.HeavyAttack:
                    StopHorizontalMovement();
                    if (decision.ShouldFacePlayer) FaceTarget();
                    actionRoutine = StartCoroutine(isKamikaze ? KamikazeRoutine() : MeleeAttackRoutine(true));
                    break;

                case AIActionType.LightAttack:
                case AIActionType.SpecialPhase:
                case AIActionType.ParryCounter:
                    StopHorizontalMovement();
                    if (decision.ShouldFacePlayer) FaceTarget();
                    actionRoutine = StartCoroutine(isKamikaze ? KamikazeRoutine() : MeleeAttackRoutine(false));
                    break;

                case AIActionType.MoveTowardPlayer:
                case AIActionType.MoveAwayFromPlayer:
                {
                    float speed = enemyData != null ? enemyData.moveSpeed : 3.5f;
                    MoveHorizontally(decision.MoveDirectionX, speed);
                    if (decision.ShouldFacePlayer) FaceTarget();
                    SetAnimState(0);
                    break;
                }

                default:
                    StopHorizontalMovement();
                    if (decision.ShouldFacePlayer) FaceTarget();
                    SetAnimState(0);
                    break;
            }
        }

        private IEnumerator MeleeAttackRoutine(bool forceHeavy = false)
        {
            isActing = true;
            StopHorizontalMovement();
            FaceTarget();

            float telegraph = enemyData != null ? enemyData.telegraphDuration : 0.5f;
            SetAnimState(0);
            yield return PlayTelegraphFlash(telegraph);

            if (IsDead || isFrozen) yield break;

            int attackClip = (forceHeavy || (useAlternatingAttack2 && flipAttackToggle)) ? 2 : 1;
            flipAttackToggle = !flipAttackToggle;
            SetAnimState(attackClip);

            // Opening wind-up frames of trimmed Attack1/Attack2 include lunge/telegraph
            yield return new WaitForSeconds(0.18f);
            if (IsDead || isFrozen) yield break;

            float dmg = attackClip == 2 && enemyData != null ? enemyData.heavyAttackDamage : (enemyData != null ? enemyData.attackDamage : 10f);
            // Slight forward step during strike
            if (rb != null) rb.linearVelocity = new Vector2(facingDirection * 2.5f, rb.linearVelocity.y);
            ActivateMeleeHitbox(dmg, AttackType.EnemyAttack);

            yield return new WaitForSeconds(0.24f);
            DeactivateMeleeHitbox();
            StopHorizontalMovement();

            yield return new WaitForSeconds(0.18f);
            nextAttackTime = Time.time + EffectiveAttackCooldown;
            SetAnimState(0);
            isActing = false;
        }

        private IEnumerator KamikazeRoutine()
        {
            isActing = true;
            StopHorizontalMovement();
            SetAnimState(2); // Kamikaze burst anim

            yield return PlayTelegraphFlash(enemyData != null ? enemyData.telegraphDuration : 0.6f, new Color(1f, 0.35f, 0.1f, 1f));
            if (IsDead || isFrozen) yield break;

            Vector3 center = transform.position + Vector3.up * 0.8f;
            if (explosionVfxPrefab != null)
            {
                Instantiate(explosionVfxPrefab, center, Quaternion.identity);
            }

            float dmg = enemyData != null ? EffectiveHeavyDamage : 28f * runtimeDamageMult;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, kamikazeExplosionRadius, playerLayer);
            foreach (Collider2D hit in hits)
            {
                IDamageable target = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead && hit.gameObject != gameObject)
                {
                    float dir = Mathf.Sign(hit.transform.position.x - transform.position.x);
                    target.TakeDamage(new DamageData(dmg, new Vector2(dir * 7f, 4f), AttackType.EnemyHeavyAttack, true, gameObject));
                }
            }

            CurrentHealth = 0f;
            Die();
        }
    }
}
