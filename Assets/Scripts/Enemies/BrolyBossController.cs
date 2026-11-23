using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.Level;
using StickmanBeatEmUp.UI;
using StickmanGame.VFX;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// World 2 (Dragon Ball) Boss: Berserker Broly.
    /// Animator States: 0=HeavyStomp, 1=Roar, 2=LariatCharge, 3=GiganticSlam, 4=Stagger, 5=Defeated.
    /// Features heavy stomp tremor, Rage 0..100 -> Green Ki Shield, Lariat wall-crash shield shatter -> 4s Stagger (x2 Damage).
    /// </summary>
    public class BrolyBossController : BossBase
    {
        [Header("Broly Berserker Mechanics")]
        [SerializeField] private float rage;
        [SerializeField] private float maxRage = 100f;
        [SerializeField] private SpriteRenderer kiShieldVisual;
        [SerializeField] private float lariatSpeed = 14.5f;
        [SerializeField] private float lariatDamage = 18f;
        [SerializeField] private float staggerVulnerabilityDuration = 4f;
        [SerializeField] private float arenaHalfExtentX = 8.5f;

        private bool hasGreenShield;
        private float nextStompShakeTime;
        private float nextAttackTime;
        private float nextSpecialTime;
        private Coroutine staggerRoutine;

        public bool IsLariatCharging { get; private set; }
        public override bool IsShielded => hasGreenShield;
        protected override int DeathAnimState => 5;

        public void SetShieldVisual(SpriteRenderer shieldRenderer) => kiShieldVisual = shieldRenderer;

        protected override void Awake()
        {
            base.Awake();
            if (kiShieldVisual != null) kiShieldVisual.enabled = false;
        }

        protected override void Start()
        {
            base.Start();
            NotifyStatusChanged("LEGENDARY SUPER SAIYAN BROLY!");
        }

        protected override EnemyAIContext BuildBossAIContext()
        {
            float distX = playerTarget != null ? Mathf.Abs(playerTarget.position.x - transform.position.x) : 0f;
            float meleeRange = bossData != null ? bossData.attackRange : 2.3f;
            float effectiveRage = hasGreenShield ? 0f : rage;
            float relevantNextTime = distX <= meleeRange ? nextAttackTime : nextSpecialTime;
            return BuildBossAIContext(effectiveRage, Mathf.Max(0f, relevantNextTime - Time.time), 0);
        }

        protected override void TickBossAI()
        {
            float distX = Mathf.Abs(playerTarget.position.x - transform.position.x);
            AIActionDecision decision = AIStrategy.ComputeAction(BuildBossAIContext());

            // Activate Green Ki Shield whenever strategy requests shield (e.g. Rage >= 100 or custom OCP strategy)
            if (decision.ShouldActivateShield && !hasGreenShield &&
                (rage >= maxRage || decision.ActionType == AIActionType.SpecialPhase))
            {
                ActivateGreenKiShield();
                actionRoutine = StartCoroutine(RoarRoutine());
                return;
            }

            switch (decision.ActionType)
            {
                case AIActionType.SpecialPhase:
                    if (Time.time >= nextSpecialTime)
                    {
                        bool useSlam = distX <= 4.2f && (distX < 2.2f || UnityEngine.Random.value > 0.45f);
                        actionRoutine = StartCoroutine(useSlam ? GiganticSlamRoutine() : LariatChargeRoutine());
                        return;
                    }
                    break;

                case AIActionType.HeavyAttack:
                    if (Time.time >= nextAttackTime)
                    {
                        StopHorizontalMovement();
                        FaceTarget();
                        actionRoutine = StartCoroutine(GiganticSlamRoutine());
                        return;
                    }
                    break;

                case AIActionType.LightAttack:
                    if (Time.time >= nextAttackTime)
                    {
                        StopHorizontalMovement();
                        FaceTarget();
                        actionRoutine = StartCoroutine(HeavyStompMeleeRoutine());
                        return;
                    }
                    break;

                case AIActionType.ParryCounter:
                    if (Time.time >= nextAttackTime)
                    {
                        actionRoutine = StartCoroutine(RoarRoutine());
                        return;
                    }
                    break;

                case AIActionType.MoveTowardPlayer:
                case AIActionType.MoveAwayFromPlayer:
                {
                    float spd = EffectiveMoveSpeed * Mathf.Clamp(Mathf.Abs(decision.MoveDirectionX), 0.7f, 1.35f);
                    float moveSign = decision.MoveDirectionX >= 0f ? 1f : -1f;
                    if (decision.ShouldFacePlayer) FaceTarget();
                    if (rb != null) rb.linearVelocity = new Vector2(moveSign * spd, rb.linearVelocity.y);
                    SetAnimState(0);

                    if (decision.ActionType == AIActionType.MoveTowardPlayer && Time.time >= nextStompShakeTime)
                    {
                        nextStompShakeTime = Time.time + 0.55f;
                        CameraFollowBeatEmUp.Instance?.Shake(0.12f, 0.14f);
                    }
                    return;
                }
            }

            StopHorizontalMovement();
            if (decision.ShouldFacePlayer) FaceTarget();
            SetAnimState(0);
        }

        private IEnumerator HeavyStompMeleeRoutine()
        {
            isActing = true;
            StopHorizontalMovement();
            FaceTarget();
            yield return PlayTelegraphFlash(bossData != null ? bossData.telegraphDuration : 0.45f);
            if (IsDead || isFrozen) yield break;

            SetAnimState(0); // HeavyStomp
            yield return new WaitForSeconds(0.2f);

            float dmg = bossData != null ? bossData.attackDamage : 15f;
            ActivateMeleeHitbox(dmg, AttackType.EnemyAttack, false);
            CameraFollowBeatEmUp.Instance?.Shake(0.18f, 0.2f);

            yield return new WaitForSeconds(0.25f);
            DeactivateMeleeHitbox();

            nextAttackTime = Time.time + EffectiveAttackCooldown(bossData != null ? bossData.attackCooldown : 1.4f);
            SetAnimState(0);
            isActing = false;
        }

        public override void TakeDamage(DamageData damageData)
        {
            if (IsDead) return;

            if (hasGreenShield)
            {
                bool breaksShield = damageData.breaksSuperArmor ||
                                    damageData.attackType == AttackType.Skill_I ||
                                    damageData.attackType == AttackType.Combo_DashJ ||
                                    damageData.attackType == AttackType.Combo_DashI;

                if (breaksShield)
                {
                    // Armor-breaking skills directly shatter Broly's Ki Shield!
                    base.TakeDamage(damageData);
                    ShatterGreenShield();
                    return;
                }

                // While hasGreenShield == true, non-armor-breaking hits trigger TriggerWhiteFlash() and deal 0f damage
                Vector3 hitPos = HitSparkManager.ResolveHitPoint(transform, mainCol, spriteRenderer, damageData.attacker);
                HitSparkManager.Spawn(hitPos, HitSparkManager.ShieldBlockCyanColor, 1.15f);
                DamagePopupManager.SpawnBlocked(hitPos + Vector3.up * 0.35f);
                TriggerWhiteFlash();
                return;
            }

            if (!isVulnerableStagger)
            {
                // Unshielded: gain +25f rage per hit (4 hits activates Green Ki Shield)
                rage = Mathf.Min(maxRage, rage + 25f);
            }

            base.TakeDamage(damageData);
        }

        private void ActivateGreenKiShield()
        {
            hasGreenShield = true;
            rage = 0f;
            if (kiShieldVisual != null)
            {
                kiShieldVisual.enabled = true;
                kiShieldVisual.color = new Color(0.25f, 1f, 0.45f, 0.52f);
            }
            NotifyStatusChanged("GREEN KI SHIELD UP! USE ARMOR-BREAK OR WALL-CRASH!");
        }

        public override void Freeze(float duration)
        {
            IsLariatCharging = false;
            base.Freeze(duration);
        }

        protected override void DefeatBoss()
        {
            IsLariatCharging = false;
            if (kiShieldVisual != null) kiShieldVisual.enabled = false;
            base.DefeatBoss();
        }

        public void CrashIntoStonePillar()
        {
            if (IsDead) return;
            IsLariatCharging = false;
            hasGreenShield = false;
            if (kiShieldVisual != null) kiShieldVisual.enabled = false;
            if (actionRoutine != null)
            {
                StopCoroutine(actionRoutine);
                actionRoutine = null;
            }
            isActing = false;
            DeactivateMeleeHitbox();
            StopHorizontalMovement();

            if (staggerRoutine != null)
            {
                StopCoroutine(staggerRoutine);
                staggerRoutine = null;
            }
            staggerRoutine = StartCoroutine(StaggerVulnerabilityRoutine(2.5f, "PILLAR CRASH! STUNNED & VULNERABLE (2.5s)!"));
        }

        private void ShatterGreenShield()
        {
            if (!hasGreenShield && isVulnerableStagger) return;
            IsLariatCharging = false;
            hasGreenShield = false;
            if (kiShieldVisual != null) kiShieldVisual.enabled = false;
            if (actionRoutine != null)
            {
                StopCoroutine(actionRoutine);
                actionRoutine = null;
            }
            isActing = false;
            DeactivateMeleeHitbox();
            if (staggerRoutine != null)
            {
                StopCoroutine(staggerRoutine);
            }
            staggerRoutine = StartCoroutine(StaggerVulnerabilityRoutine(-1f, null));
        }

        private IEnumerator StaggerVulnerabilityRoutine(float durationOverride, string statusMessage)
        {
            isVulnerableStagger = true;
            damageMultiplier = 2f;
            StopHorizontalMovement();
            SetAnimState(4); // Stagger
            NotifyStatusChanged(string.IsNullOrEmpty(statusMessage) ? "SHIELD SHATTERED! 2X DAMAGE STAGGER (4s)!" : statusMessage);
            CameraFollowBeatEmUp.Instance?.Shake(0.36f, 0.42f);

            float dur = durationOverride > 0f ? durationOverride : EffectiveStaggerDuration(staggerVulnerabilityDuration);
            float elapsed = 0f;
            bool tintToggle = true;
            while (elapsed < dur && !IsDead)
            {
                if (!isFrozen && spriteRenderer != null)
                {
                    spriteRenderer.color = tintToggle ? new Color(1f, 0.85f, 0.35f, 1f) : originalColor;
                    tintToggle = !tintToggle;
                }
                yield return new WaitForSeconds(0.12f);
                elapsed += 0.12f;
            }

            if (spriteRenderer != null && !IsDead && !isFrozen)
            {
                spriteRenderer.color = originalColor;
            }
            damageMultiplier = 1f;
            isVulnerableStagger = false;
            staggerRoutine = null;
            if (!IsDead)
            {
                SetAnimState(0);
                NotifyStatusChanged("BROLY RECOVERED!");
            }
        }

        private IEnumerator RoarRoutine()
        {
            isActing = true;
            StopHorizontalMovement();
            SetAnimState(1); // Roar
            CameraFollowBeatEmUp.Instance?.Shake(0.4f, 0.35f);
            yield return new WaitForSeconds(0.7f);
            nextAttackTime = Time.time + EffectiveAttackCooldown(0.8f);
            SetAnimState(0);
            isActing = false;
        }

        private IEnumerator LariatChargeRoutine()
        {
            isActing = true;
            StopHorizontalMovement();
            FaceTarget();
            yield return PlayTelegraphFlash(0.55f, new Color(0.35f, 1f, 0.45f, 1f));
            if (IsDead || isFrozen)
            {
                IsLariatCharging = false;
                isActing = false;
                yield break;
            }

            SetAnimState(2); // LariatCharge
            IsLariatCharging = true;
            float chargeDir = facingDirection;
            float dmg = lariatDamage;
            ActivateMeleeHitbox(dmg, AttackType.EnemyHeavyAttack, true);

            float startX = transform.position.x;
            float wallLimit = Camera.main != null ? Camera.main.transform.position.x + arenaHalfExtentX * chargeDir : startX + 9f * chargeDir;
            float t = 0f;
            bool crashedWall = false;

            while (t < 0.95f && !IsDead && !isFrozen)
            {
                if (rb != null) rb.linearVelocity = new Vector2(chargeDir * lariatSpeed * runtimeMoveSpeedMult, rb.linearVelocity.y);
                if ((chargeDir > 0f && transform.position.x >= wallLimit) || (chargeDir < 0f && transform.position.x <= wallLimit))
                {
                    crashedWall = true;
                    break;
                }
                t += Time.deltaTime;
                yield return null;
            }

            IsLariatCharging = false;
            StopHorizontalMovement();
            DeactivateMeleeHitbox();

            if (crashedWall)
            {
                CameraFollowBeatEmUp.Instance?.Shake(0.35f, 0.42f);
                if (hasGreenShield)
                {
                    isActing = false;
                    ShatterGreenShield();
                    yield break;
                }
            }

            nextSpecialTime = Time.time + EffectiveAttackCooldown(4.4f);
            nextAttackTime = Time.time + EffectiveAttackCooldown(1.5f);
            SetAnimState(0);
            isActing = false;
        }

        private IEnumerator GiganticSlamRoutine()
        {
            isActing = true;
            StopHorizontalMovement();
            FaceTarget();
            yield return PlayTelegraphFlash(0.5f);
            if (IsDead || isFrozen)
            {
                isActing = false;
                yield break;
            }

            SetAnimState(3); // GiganticSlam
            yield return new WaitForSeconds(0.24f);

            float dmg = bossData != null ? bossData.heavyAttackDamage : 25f;
            ActivateMeleeHitbox(dmg, AttackType.EnemyHeavyAttack, true);
            CameraFollowBeatEmUp.Instance?.Shake(0.35f, 0.45f);

            yield return new WaitForSeconds(0.3f);
            DeactivateMeleeHitbox();

            nextSpecialTime = Time.time + EffectiveAttackCooldown(3.8f);
            nextAttackTime = Time.time + EffectiveAttackCooldown(bossData != null ? bossData.attackCooldown : 1.7f);
            SetAnimState(0);
            isActing = false;
        }
    }
}
