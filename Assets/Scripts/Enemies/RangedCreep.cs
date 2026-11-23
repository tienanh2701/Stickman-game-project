using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.CorePure.AI;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// Ranged Creep (Beast Titan, Frieza Blaster Soldier, Kasshin Trooper).
    /// Setup windups (pickup boulder, aim lock, plant staff) are merged into Attack1/Attack2 opening frames.
    /// Delegates kiting/retreat/volley decisions to the injected IAIStrategy (GoF Strategy).
    /// </summary>
    public class RangedCreep : EnemyBase
    {
        [Header("Ranged Combat")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private int volleyCount = 1;
        [SerializeField] private float volleySpreadDeg = 8f;

        private bool useSecondaryShot;

        public void ConfigureRanged(GameObject projPrefab, int burst = 1)
        {
            projectilePrefab = projPrefab;
            volleyCount = Mathf.Max(1, burst);
        }

        protected override IAIStrategy CreateDefaultStrategy()
        {
            return new RangedKiteStrategy();
        }

        public override void ResetForPoolSpawn()
        {
            useSecondaryShot = false;
            base.ResetForPoolSpawn();
        }

        protected override void TickBehavior()
        {
            AIActionDecision decision = AIStrategy.ComputeAction(BuildAIContext());
            float speed = enemyData != null ? enemyData.moveSpeed : 2.8f;

            switch (decision.ActionType)
            {
                case AIActionType.MoveAwayFromPlayer:
                case AIActionType.MoveTowardPlayer:
                    MoveHorizontally(decision.MoveDirectionX, speed);
                    if (decision.ShouldFacePlayer) FaceTarget();
                    SetAnimState(0);
                    break;

                case AIActionType.RangedVolley:
                case AIActionType.LightAttack:
                case AIActionType.HeavyAttack:
                case AIActionType.SpecialPhase:
                    StopHorizontalMovement();
                    if (decision.ShouldFacePlayer) FaceTarget();
                    actionRoutine = StartCoroutine(FireVolleyRoutine());
                    break;

                default:
                    StopHorizontalMovement();
                    if (decision.ShouldFacePlayer) FaceTarget();
                    SetAnimState(0);
                    break;
            }
        }

        private IEnumerator FireVolleyRoutine()
        {
            isActing = true;
            StopHorizontalMovement();
            FaceTarget();

            float telegraph = enemyData != null ? enemyData.telegraphDuration : 0.5f;
            yield return PlayTelegraphFlash(telegraph);
            if (IsDead || isFrozen) yield break;

            int clipState = useSecondaryShot ? 2 : 1;
            useSecondaryShot = !useSecondaryShot;
            SetAnimState(clipState);

            // Opening pickup / aim / plant-staff frames merged into clip start
            yield return new WaitForSeconds(0.24f);
            if (IsDead || isFrozen) yield break;

            SpawnProjectiles();

            yield return new WaitForSeconds(0.32f);
            nextAttackTime = Time.time + EffectiveAttackCooldown;
            SetAnimState(0);
            isActing = false;
        }

        private void SpawnProjectiles()
        {
            if (projectilePrefab == null) return;

            Vector3 spawnPos = firePoint != null
                ? firePoint.position
                : transform.position + new Vector3(facingDirection * 0.9f, 1.0f, 0f);

            float dmg = enemyData != null ? EffectiveAttackDamage : 12f * runtimeDamageMult;
            float projSpeed = enemyData != null ? enemyData.projectileSpeed : 11f;
            Vector2 baseDir = new Vector2(facingDirection, 0f);

            int count = Mathf.Max(1, volleyCount);
            float startAngle = -(count - 1) * 0.5f * volleySpreadDeg;

            for (int i = 0; i < count; i++)
            {
                float ang = startAngle + i * volleySpreadDeg;
                Vector2 dir = Quaternion.Euler(0f, 0f, ang) * baseDir;

                Projectile proj = CombatObjectPool.Instance != null
                    ? CombatObjectPool.Instance.SpawnProjectile(projectilePrefab, spawnPos, Quaternion.identity)
                    : Instantiate(projectilePrefab, spawnPos, Quaternion.identity).GetComponent<Projectile>();
                if (proj != null)
                {
                    proj.SetSpeed(projSpeed);
                    Vector2 kb = new Vector2(facingDirection * 3f, 1.5f);
                    proj.Initialize(new DamageData(dmg, kb, AttackType.EnemyAttack, false, gameObject), dir, playerLayer, false);
                }
            }
        }
    }
}
