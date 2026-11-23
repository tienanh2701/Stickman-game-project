using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.CorePure.AI;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;
using StickmanBeatEmUp.Economy;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// Specialized kamikaze creep (e.g. Saibaman / suicide creep) inheriting from MeleeCreep.
    /// Runs toward player at 1.5x normal creep speed, latches on with an orange-red warning flash (0.42s),
    /// and detonates if not slain prior to fuse completion.
    /// Delegates rush/detonate decisions to the injected IAIStrategy (GoF Strategy).
    /// </summary>
    public class KamikazeCreep : MeleeCreep
    {
        protected override int GetKillCoinReward() => CurrencyManager.REWARD_SAIBAMAN;

        protected override IAIStrategy CreateDefaultStrategy()
        {
            return new KamikazeRushStrategy();
        }

        protected override void TickBehavior()
        {
            if (playerTarget == null) return;

            AIActionDecision decision = AIStrategy.ComputeAction(BuildAIContext());

            switch (decision.ActionType)
            {
                case AIActionType.Detonate:
                case AIActionType.LightAttack:
                case AIActionType.HeavyAttack:
                case AIActionType.SpecialPhase:
                    StopHorizontalMovement();
                    if (decision.ShouldFacePlayer) FaceTarget();
                    actionRoutine = StartCoroutine(LatchAndExplodeRoutine());
                    break;

                case AIActionType.MoveTowardPlayer:
                case AIActionType.MoveAwayFromPlayer:
                {
                    float baseSpeed = enemyData != null ? enemyData.moveSpeed : 3.5f;
                    // Avoid double-multiplying when EnemyDataSO already stores the boosted 1.5x rush speed (> 4.0f)
                    float normalizedSpeed = baseSpeed > 4.0f ? (baseSpeed / 1.5f) : baseSpeed;
                    float speed = Mathf.Max(normalizedSpeed, 3.5f);
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

        private IEnumerator LatchAndExplodeRoutine()
        {
            isActing = true;
            StopHorizontalMovement();
            FaceTarget();
            SetAnimState(2); // Grab/burst animation

            // Nudge closer onto player
            if (playerTarget != null)
            {
                float nudgeDir = Mathf.Sign(playerTarget.position.x - transform.position.x);
                transform.position += new Vector3(nudgeDir * 0.25f, 0f, 0f);
            }

            // Orange-red warning flash for 0.42s
            Color warningColor = new Color(1f, 0.35f, 0.1f, 1f);
            yield return PlayTelegraphFlash(0.42f, warningColor);

            if (IsDead || isFrozen) yield break;

            // Spawns explosion VFX if assigned
            if (explosionVfxPrefab != null)
            {
                Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);
            }

            // Deals damage via OverlapCircleAll against playerLayer
            float dmg = enemyData != null ? EffectiveHeavyDamage : 12f * runtimeDamageMult;
            float radius = Mathf.Max(kamikazeExplosionRadius, 2.4f);
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, playerLayer);
            if (hits != null)
            {
                HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
                for (int i = 0; i < hits.Length; i++)
                {
                    Collider2D hit = hits[i];
                    if (hit == null || hit.gameObject == gameObject) continue;

                    IDamageable target = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
                    if (target != null && !target.IsDead && !damagedTargets.Contains(target))
                    {
                        damagedTargets.Add(target);
                        float dir = Mathf.Sign(hit.transform.position.x - transform.position.x);
                        target.TakeDamage(new DamageData(dmg, new Vector2(dir * 7f, 4f), AttackType.EnemyHeavyAttack, true, gameObject));
                    }
                }
            }

            CurrentHealth = 0f;
            Die();
        }
    }
}
