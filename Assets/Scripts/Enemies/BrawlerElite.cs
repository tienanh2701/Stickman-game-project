using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// World 2 (Dragon Ball) Elite: Nappa (Super-Armor Giant Brawler).
    /// Animator States: 0=Advance, 1=ChargeUp, 2=HeavyHaymaker, 3=GroundEruption, 4=Hit, 5=Death.
    /// </summary>
    public class BrawlerElite : EliteEnemy
    {
        [Header("Nappa Giant Storm & Summon")]
        [SerializeField] private GameObject giantStormPillarPrefab;
        [SerializeField] private GameObject bomberDxPrefab;
        [SerializeField] private GameObject summonSaibamanPrefab;
        [SerializeField] private float giantStormChargeTime = 1.5f;
        [SerializeField] private float bomberDxDamage = 12f;

        private bool hasSummonedSaibamen;

        public void ConfigurePrefabs(GameObject pillarPrefab, GameObject kiPrefab, GameObject saibamanPrefab)
        {
            giantStormPillarPrefab = pillarPrefab;
            bomberDxPrefab = kiPrefab;
            summonSaibamanPrefab = saibamanPrefab;
        }

        protected override void TickBehavior()
        {
            if (!hasSummonedSaibamen && CurrentHealth <= MaxHealth * 0.65f && summonSaibamanPrefab != null)
            {
                hasSummonedSaibamen = true;
                SpawnSaibamanReinforcement();
            }

            float distX = Mathf.Abs(playerTarget.position.x - transform.position.x);
            float range = enemyData != null ? enemyData.attackRange : 2.1f;

            if (Time.time >= nextSpecialTime && distX <= 9.5f)
            {
                bool castStorm = distX > range || UnityEngine.Random.value > 0.4f;
                actionRoutine = StartCoroutine(castStorm ? GiantStormRoutine() : BomberDxRoutine());
                return;
            }

            if (distX <= range)
            {
                StopHorizontalMovement();
                FaceTarget();
                if (Time.time >= nextAttackTime)
                {
                    actionRoutine = StartCoroutine(HeavyHaymakerRoutine());
                }
                else
                {
                    SetAnimState(0);
                }
            }
            else
            {
                float speed = enemyData != null ? enemyData.moveSpeed : 2.9f;
                MoveHorizontally(Mathf.Sign(playerTarget.position.x - transform.position.x), speed);
                SetAnimState(0);
            }
        }

        private IEnumerator HeavyHaymakerRoutine()
        {
            isActing = true;
            StopHorizontalMovement();
            FaceTarget();
            yield return PlayTelegraphFlash(enemyData != null ? enemyData.telegraphDuration : 0.5f);
            if (IsDead || isFrozen) yield break;

            SetAnimState(2); // HeavyHaymaker
            yield return new WaitForSeconds(0.22f);
            float dmg = enemyData != null ? enemyData.attackDamage : 10f;
            ActivateMeleeHitbox(dmg, AttackType.EnemyHeavyAttack, true);
            yield return new WaitForSeconds(0.26f);
            DeactivateMeleeHitbox();

            nextAttackTime = Time.time + EffectiveAttackCooldown;
            SetAnimState(0);
            isActing = false;
        }

        private IEnumerator GiantStormRoutine()
        {
            isActing = true;
            StopHorizontalMovement();
            FaceTarget();
            SetAnimState(1); // ChargeUp (1.5s unmistakable telegraph)

            float lockedTargetX = playerTarget != null ? playerTarget.position.x : transform.position.x + facingDirection * 3f;
            float groundY = GROUND_Y;

            yield return PlayTelegraphFlash(giantStormChargeTime, new Color(1f, 0.72f, 0.15f, 1f));
            if (IsDead || isFrozen) yield break;

            SetAnimState(3); // GroundEruption
            if (giantStormPillarPrefab != null)
            {
                Vector3 pillarPos = new Vector3(lockedTargetX, groundY + 0.9f, 0f);
                GameObject pillar = Instantiate(giantStormPillarPrefab, pillarPos, Quaternion.identity);
                Destroy(pillar, 0.85f);
            }

            if (!hasSummonedSaibamen && summonSaibamanPrefab != null)
            {
                hasSummonedSaibamen = true;
                SpawnSaibamanReinforcement();
            }

            // Pillar AoE burst
            float dmg = enemyData != null ? EffectiveHeavyDamage : 18f * runtimeDamageMult;
            Collider2D[] hits = Physics2D.OverlapBoxAll(new Vector2(lockedTargetX, groundY + 1.5f), new Vector2(2.4f, 5.2f), 0f, playerLayer);
            foreach (Collider2D hit in hits)
            {
                IDamageable target = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead)
                {
                    target.TakeDamage(new DamageData(dmg, new Vector2(facingDirection * 4f, 8f), AttackType.EnemyHeavyAttack, true, gameObject));
                }
            }

            yield return new WaitForSeconds(0.55f);
            float spdDiv = Mathf.Max(0.1f, runtimeAttackSpeedMult);
            nextSpecialTime = Time.time + 5.0f / spdDiv;
            nextAttackTime = Time.time + 1.4f / spdDiv;
            SetAnimState(0);
            isActing = false;
        }

        private IEnumerator BomberDxRoutine()
        {
            isActing = true;
            StopHorizontalMovement();
            FaceTarget();
            SetAnimState(1);
            yield return PlayTelegraphFlash(0.6f);
            if (IsDead || isFrozen) yield break;

            SetAnimState(2);
            yield return new WaitForSeconds(0.18f);

            if (bomberDxPrefab != null)
            {
                Vector3 spawnPos = transform.position + new Vector3(facingDirection * 1.1f, 1.0f, 0f);
                GameObject go = Instantiate(bomberDxPrefab, spawnPos, Quaternion.identity);
                Projectile proj = go.GetComponent<Projectile>();
                if (proj != null)
                {
                    proj.SetSpeed(10.5f);
                    float dmg = bomberDxDamage * runtimeDamageMult;
                    proj.Initialize(new DamageData(dmg, new Vector2(facingDirection * 6f, 2f), AttackType.EnemyHeavyAttack, true, gameObject),
                        new Vector2(facingDirection, 0f), playerLayer, false);
                }
            }

            yield return new WaitForSeconds(0.35f);
            nextSpecialTime = Time.time + 4.2f / Mathf.Max(0.1f, runtimeAttackSpeedMult);
            SetAnimState(0);
            isActing = false;
        }

        private void SpawnSaibamanReinforcement()
        {
            if (summonSaibamanPrefab == null) return;
            for (int i = 0; i < 2; i++)
            {
                Vector3 spawnPos = new Vector3(transform.position.x + (i == 0 ? -2.2f : 2.2f), GROUND_Y, 0f);
                Instantiate(summonSaibamanPrefab, spawnPos, Quaternion.identity);
            }
        }
    }
}
