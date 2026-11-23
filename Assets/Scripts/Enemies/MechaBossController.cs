using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.CorePure.AI;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;
using StickmanBeatEmUp.Level;
using StickmanBeatEmUp.UI;
using StickmanGame.VFX;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// World 3 (Diend) Boss: Giant Dai-Shocker Mecha Colossus.
    /// Multi-part destructible LeftArm, RightArm, and ChestCore (takes x2 damage once exposed!).
    /// Animator States: 0=Idle/Sweep, 1=RocketPunch, 2=LaserSweep, 3=CoreExposed, 4=Destroyed.
    /// </summary>
    public class MechaBossController : BossBase
    {
        [Header("Mecha Colossus Parts")]
        [SerializeField] private BossPartHitbox leftArm;
        [SerializeField] private BossPartHitbox rightArm;
        [SerializeField] private BossPartHitbox chestCore;
        [SerializeField] private GameObject armLaserProjectilePrefab;

        private bool isCoreExposed;
        private bool partsInitialized;
        private float nextAttackTime;
        private float leftArmTargetAngle;
        private float rightArmTargetAngle;
        private float leftArmCurrentAngle;
        private float rightArmCurrentAngle;
        private Coroutine empRoutine;

        private static readonly Vector3 LeftShoulderAnchor = new Vector3(-0.35f, 0.18f, 0f);
        private static readonly Vector3 RightShoulderAnchor = new Vector3(0.35f, 0.18f, 0f);

        protected override int DeathAnimState => 4;

        public void ConfigureParts(BossPartHitbox lArm, BossPartHitbox rArm, BossPartHitbox core, GameObject laserPrefab)
        {
            leftArm = lArm;
            rightArm = rArm;
            chestCore = core;
            armLaserProjectilePrefab = laserPrefab;
        }

        protected override void Awake()
        {
            base.Awake();
            Vector3 s = transform.localScale;
            float sign = Mathf.Approximately(s.x, 0f) ? 1f : Mathf.Sign(s.x);
            transform.localScale = new Vector3(4.5f * sign, 4.5f, 1.0f);
            initialScale = new Vector3(4.5f, 4.5f, 1.0f);

            BossPartHitbox[] parts = GetComponentsInChildren<BossPartHitbox>();
            foreach (BossPartHitbox p in parts)
            {
                if (p.Role == BossPartRole.LeftArm && leftArm == null) leftArm = p;
                else if (p.Role == BossPartRole.RightArm && rightArm == null) rightArm = p;
                else if (p.Role == BossPartRole.ChestCore && chestCore == null) chestCore = p;
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            PinShoulderJoints();
        }

        private void PinShoulderJoints()
        {
            leftArmCurrentAngle = Mathf.LerpAngle(leftArmCurrentAngle, leftArmTargetAngle, Time.deltaTime * 10f);
            rightArmCurrentAngle = Mathf.LerpAngle(rightArmCurrentAngle, rightArmTargetAngle, Time.deltaTime * 10f);

            if (leftArm != null && !leftArm.IsDead && leftArm.transform.parent != null && leftArm.transform.parent.name == "Body")
            {
                leftArm.transform.localPosition = LeftShoulderAnchor;
                leftArm.transform.localRotation = Quaternion.Euler(0f, 0f, leftArmCurrentAngle);
            }
            if (rightArm != null && !rightArm.IsDead && rightArm.transform.parent != null && rightArm.transform.parent.name == "Body")
            {
                rightArm.transform.localPosition = RightShoulderAnchor;
                rightArm.transform.localRotation = Quaternion.Euler(0f, 0f, rightArmCurrentAngle);
            }
        }

        protected override void FaceTarget()
        {
            if (playerTarget == null) return;
            float dx = playerTarget.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.1f)
            {
                facingDirection = dx >= 0f ? 1f : -1f;
                // Mecha_Body.png artwork naturally faces LEFT (-X) when scale.x > 0
                Vector3 s = initialScale;
                s.x = Mathf.Abs(initialScale.x) * -facingDirection;
                transform.localScale = s;
            }
        }

        protected override void Start()
        {
            base.Start();
            if (leftArm != null)
            {
                leftArm.Initialize(BossPartRole.LeftArm, 40f * runtimeHealthMult, true);
                leftArm.OnPartDamaged += HandlePartDamaged;
                leftArm.OnPartDestroyed += HandlePartDestroyed;
            }
            if (rightArm != null)
            {
                rightArm.Initialize(BossPartRole.RightArm, 40f * runtimeHealthMult, true);
                rightArm.OnPartDamaged += HandlePartDamaged;
                rightArm.OnPartDestroyed += HandlePartDestroyed;
            }
            if (chestCore != null)
            {
                chestCore.Initialize(BossPartRole.ChestCore, 70f * runtimeHealthMult, false);
                chestCore.OnPartDamaged += HandlePartDamaged;
                chestCore.OnPartDestroyed += HandlePartDestroyed;
            }

            partsInitialized = true;
            float basePartsMax = (leftArm != null ? 40f : 0f) + (rightArm != null ? 40f : 0f) + (chestCore != null ? 70f : 0f);
            if (basePartsMax > 0f) currentMaxHealth = basePartsMax * runtimeHealthMult;
            UpdateTotalHealthFromParts();
            NotifyStatusChanged("MECHA COLOSSUS! DESTROY BOTH ARMS TO EXPOSE CHEST CORE!");
        }

        protected override void OnEndlessModifiersApplied()
        {
            if (!partsInitialized) return;
            if (leftArm != null && !leftArm.IsDead) leftArm.Initialize(BossPartRole.LeftArm, 40f * runtimeHealthMult, leftArm.IsVulnerable);
            if (rightArm != null && !rightArm.IsDead) rightArm.Initialize(BossPartRole.RightArm, 40f * runtimeHealthMult, rightArm.IsVulnerable);
            if (chestCore != null && !chestCore.IsDead) chestCore.Initialize(BossPartRole.ChestCore, 70f * runtimeHealthMult, chestCore.IsVulnerable);
            float basePartsMax = (leftArm != null ? 40f : 0f) + (rightArm != null ? 40f : 0f) + (chestCore != null ? 70f : 0f);
            if (basePartsMax > 0f) currentMaxHealth = basePartsMax * runtimeHealthMult;
            UpdateTotalHealthFromParts();
        }

        private void OnDestroy()
        {
            UnbindPart(leftArm);
            UnbindPart(rightArm);
            UnbindPart(chestCore);
        }

        private void UnbindPart(BossPartHitbox p)
        {
            if (p == null) return;
            p.OnPartDamaged -= HandlePartDamaged;
            p.OnPartDestroyed -= HandlePartDestroyed;
        }

        public override void TakeDamage(DamageData damageData)
        {
            if (IsDead) return;

            // Forward root hits to an active vulnerable part
            BossPartHitbox targetPart = null;
            if (leftArm != null && !leftArm.IsDead && leftArm.IsVulnerable) targetPart = leftArm;
            else if (rightArm != null && !rightArm.IsDead && rightArm.IsVulnerable) targetPart = rightArm;
            else if (chestCore != null && !chestCore.IsDead && chestCore.IsVulnerable) targetPart = chestCore;

            if (targetPart != null)
            {
                DamageData scaled = damageData;
                scaled.damageAmount = damageData.damageAmount * (1f - Mathf.Clamp01(runtimeDamageReduction)) * damageMultiplier;
                targetPart.TakeDamage(scaled);
            }
            else
            {
                Vector3 hitPos = HitSparkManager.ResolveHitPoint(transform, mainCol, spriteRenderer, damageData.attacker);
                HitSparkManager.Spawn(hitPos, HitSparkManager.ShieldBlockCyanColor, 1.15f);
                DamagePopupManager.SpawnBlocked(hitPos);
                TriggerWhiteFlash();
            }
        }

        public void TriggerEmpParalysis(float duration = 4.5f)
        {
            if (IsDead) return;

            if (actionRoutine != null)
            {
                StopCoroutine(actionRoutine);
                actionRoutine = null;
            }
            isActing = false;
            DeactivateMeleeHitbox();
            leftArmTargetAngle = 0f;
            rightArmTargetAngle = 0f;
            StopHorizontalMovement();

            if (empRoutine != null)
            {
                StopCoroutine(empRoutine);
                empRoutine = null;
            }
            empRoutine = StartCoroutine(EmpParalysisRoutine(Mathf.Max(0.5f, duration)));
        }

        private IEnumerator EmpParalysisRoutine(float duration)
        {
            isVulnerableStagger = true;
            bool chestTempExposed = false;
            if (!isCoreExposed && chestCore != null && !chestCore.IsDead)
            {
                chestCore.SetVulnerable(true);
                chestTempExposed = true;
            }

            SetAnimState(3);
            NotifyStatusChanged($"EMP PARALYSIS! SYSTEMS SHORTED ({duration:0.0}s)!");
            CameraFollowBeatEmUp.Instance?.Shake(0.28f, 0.34f);

            float elapsed = 0f;
            bool cyanToggle = true;
            Color electricCyan = new Color(0.25f, 0.95f, 1f, 1f);
            while (elapsed < duration && !IsDead)
            {
                if (!isFrozen && spriteRenderer != null)
                {
                    spriteRenderer.color = cyanToggle ? electricCyan : originalColor;
                    cyanToggle = !cyanToggle;
                }
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (spriteRenderer != null && !IsDead && !isFrozen)
            {
                spriteRenderer.color = originalColor;
            }

            bool bothArmsGone = (leftArm == null || leftArm.IsDead) && (rightArm == null || rightArm.IsDead);
            if (bothArmsGone) isCoreExposed = true;
            if (chestTempExposed && !isCoreExposed && chestCore != null && !chestCore.IsDead)
            {
                chestCore.SetVulnerable(false);
            }

            isVulnerableStagger = false;
            empRoutine = null;
            if (!IsDead)
            {
                nextAttackTime = Time.time + EffectiveAttackCooldown(0.5f);
                SetAnimState(isCoreExposed ? 3 : 0);
                NotifyStatusChanged("MECHA SYSTEMS REBOOTED!");
            }
        }

        private void HandlePartDamaged(BossPartHitbox part, float appliedDmg, DamageData raw)
        {
            if (IsDead) return;
            UpdateTotalHealthFromParts();
            if (CurrentHealth <= 0f)
            {
                StickmanBeatEmUp.Characters.BaseCharacter.NotifyAttackerKill(raw.attacker, this);
                DefeatBoss();
            }
        }

        private void UpdateTotalHealthFromParts()
        {
            float total = 0f;
            if (leftArm != null && !leftArm.IsDead) total += leftArm.CurrentHealth;
            if (rightArm != null && !rightArm.IsDead) total += rightArm.CurrentHealth;
            if (chestCore != null && !chestCore.IsDead) total += chestCore.CurrentHealth;
            CurrentHealth = total;
            NotifyHealthChanged();
        }

        private void HandlePartDestroyed(BossPartHitbox part)
        {
            if (IsDead) return;
            CameraFollowBeatEmUp.Instance?.Shake(0.35f, 0.4f);

            if (part.Role == BossPartRole.ChestCore)
            {
                DefeatBoss();
                return;
            }

            bool leftGone = leftArm == null || leftArm.IsDead;
            bool rightGone = rightArm == null || rightArm.IsDead;

            // Expose Chest Core ONLY when BOTH arms are destroyed
            if (leftGone && rightGone && !isCoreExposed && chestCore != null && !chestCore.IsDead)
            {
                isCoreExposed = true;
                chestCore.SetVulnerable(true);
                SetAnimState(3);
                NotifyStatusChanged("BOTH ARMS DESTROYED! CHEST CORE EXPOSED!");
            }
            else if (!isCoreExposed)
            {
                NotifyStatusChanged(part.Role == BossPartRole.LeftArm ? "LEFT ARM DESTROYED!" : "RIGHT ARM DESTROYED!");
            }

            UpdateTotalHealthFromParts();

            if (leftGone && rightGone && (chestCore == null || chestCore.IsDead || chestCore.CurrentHealth <= 0f))
            {
                DefeatBoss();
            }
        }

        protected override IAIStrategy CreateDefaultStrategy()
        {
            return new BossPhaseStrategy(defaultMeleeRange: 3.5f, specialTriggerMinRange: 3.5f, rageShieldThreshold: 100f);
        }

        protected override EnemyAIContext BuildBossAIContext()
        {
            EnemyAIContext ctx = BuildBossAIContext(0f, Mathf.Max(0f, nextAttackTime - Time.time), 0);
            ctx.AttackRange = Mathf.Max(ctx.AttackRange, 4.2f);
            ctx.PreferredRange = Mathf.Max(ctx.PreferredRange, 18f);
            return ctx;
        }

        protected override void TickBossAI()
        {
            AIActionDecision decision = AIStrategy.ComputeAction(BuildBossAIContext());

            bool leftAlive = leftArm != null && !leftArm.IsDead;
            bool rightAlive = rightArm != null && !rightArm.IsDead;
            bool anyArmAlive = leftAlive || rightAlive;

            switch (decision.ActionType)
            {
                case AIActionType.SpecialPhase:
                case AIActionType.RangedVolley:
                    if (Time.time < nextAttackTime) return;
                    actionRoutine = StartCoroutine(anyArmAlive ? RightArmLaserSweepRoutine() : CoreOverdrivePulseRoutine());
                    break;

                case AIActionType.HeavyAttack:
                    if (Time.time < nextAttackTime) return;
                    if (anyArmAlive)
                    {
                        bool useLaser = UnityEngine.Random.value > 0.45f;
                        actionRoutine = StartCoroutine(useLaser ? RightArmLaserSweepRoutine() : RocketArmPunchRoutine());
                    }
                    else
                    {
                        actionRoutine = StartCoroutine(CoreOverdrivePulseRoutine());
                    }
                    break;

                case AIActionType.LightAttack:
                case AIActionType.ParryCounter:
                    if (Time.time < nextAttackTime) return;
                    actionRoutine = StartCoroutine(RocketArmPunchRoutine());
                    break;

                case AIActionType.MoveTowardPlayer:
                case AIActionType.MoveAwayFromPlayer:
                default:
                    // Giant Mecha remains anchored in place while facing the player between attacks
                    if (decision.ShouldFacePlayer) FaceTarget();
                    break;
            }
        }

        private IEnumerator RocketArmPunchRoutine()
        {
            isActing = true;
            FaceTarget();
            yield return PlayTelegraphFlash(0.55f);
            if (IsDead || isFrozen)
            {
                isActing = false;
                yield break;
            }

            SetAnimState(1);
            leftArmTargetAngle = -28f;
            rightArmTargetAngle = 14f;
            yield return new WaitForSeconds(0.22f);

            float dmg = bossData != null ? bossData.attackDamage : 15f;
            ActivateMeleeHitbox(dmg, AttackType.EnemyHeavyAttack, true);
            CameraFollowBeatEmUp.Instance?.Shake(0.25f, 0.3f);

            yield return new WaitForSeconds(0.28f);
            DeactivateMeleeHitbox();
            leftArmTargetAngle = 0f;
            rightArmTargetAngle = 0f;

            nextAttackTime = Time.time + EffectiveAttackCooldown(2.0f);
            SetAnimState(isCoreExposed ? 3 : 0);
            isActing = false;
        }

        private IEnumerator RightArmLaserSweepRoutine()
        {
            isActing = true;
            FaceTarget();
            yield return PlayTelegraphFlash(0.55f, new Color(1f, 0.3f, 0.65f, 1f));
            if (IsDead || isFrozen)
            {
                isActing = false;
                yield break;
            }

            SetAnimState(2);
            rightArmTargetAngle = -24f;
            yield return new WaitForSeconds(0.2f);

            if (armLaserProjectilePrefab != null && playerTarget != null)
            {
                Vector3 origin = rightArm != null ? rightArm.transform.position : transform.position + new Vector3(facingDirection * 1.4f, 1.35f, 0f);
                Vector3 targetAim = playerTarget.position + Vector3.up * 0.85f;
                for (int i = -1; i <= 1; i++)
                {
                    Vector2 baseDir = (targetAim - origin).normalized;
                    Vector2 dir = Quaternion.Euler(0f, 0f, i * 10f) * baseDir;
                    Projectile proj = CombatObjectPool.Instance != null
                        ? CombatObjectPool.Instance.SpawnProjectile(armLaserProjectilePrefab, origin, Quaternion.identity)
                        : Instantiate(armLaserProjectilePrefab, origin, Quaternion.identity).GetComponent<Projectile>();
                    if (proj != null)
                    {
                        proj.SetSpeed(12.5f);
                        float dmg = ScaleOutgoingDamage(bossData != null ? bossData.heavyAttackDamage : 20f);
                        proj.Initialize(new DamageData(dmg, new Vector2(dir.x * 5f, 2f), AttackType.EnemyAttack, false, gameObject), dir, playerLayer, false);
                    }
                }
            }

            yield return new WaitForSeconds(0.35f);
            rightArmTargetAngle = 0f;
            nextAttackTime = Time.time + EffectiveAttackCooldown(2.2f);
            SetAnimState(isCoreExposed ? 3 : 0);
            isActing = false;
        }

        private IEnumerator CoreOverdrivePulseRoutine()
        {
            isActing = true;
            yield return PlayTelegraphFlash(0.65f, Color.cyan);
            if (IsDead || isFrozen)
            {
                isActing = false;
                yield break;
            }

            SetAnimState(3);
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position + Vector3.up * 1.1f, 4.5f, playerLayer);
            foreach (Collider2D hit in hits)
            {
                IDamageable target = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead)
                {
                    float dir = Mathf.Sign(hit.transform.position.x - transform.position.x);
                    float dmg = ScaleOutgoingDamage(bossData != null ? bossData.attackDamage : 15f);
                    target.TakeDamage(new DamageData(dmg, new Vector2(dir * 7f, 4f), AttackType.EnemyHeavyAttack, true, gameObject));
                }
            }
            CameraFollowBeatEmUp.Instance?.Shake(0.3f, 0.35f);

            yield return new WaitForSeconds(0.45f);
            nextAttackTime = Time.time + EffectiveAttackCooldown(2.4f);
            isActing = false;
        }
    }
}
