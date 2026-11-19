using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// World 1 (AoT) Boss: War Hammer Titan + Cart Titan artillery + Crystal Core Tether.
    /// Animator States: 0=HammerSmash, 1=GroundSpikes, 2=Hit/Downed, 3=Destroyed.
    /// When War Hammer HP hits 0 with CrystalCore intact, kneels for 10s and revives to 100% HP unless the 500 HP CrystalCore is shattered!
    /// </summary>
    public class TetherBossController : BossBase
    {
        [Header("Tether & Partners")]
        [SerializeField] private CrystalCore crystalCore;
        [SerializeField] private TetherCableHitbox tetherCable;
        [SerializeField] private Transform cartTitanVisual;
        [SerializeField] private LineRenderer tetherLine;
        [SerializeField] private GameObject groundSpikesPrefab;
        [SerializeField] private GameObject cartTurretProjectilePrefab;
        [SerializeField] private Transform cartTurretFirePoint;

        private bool detachedPartners;
        private bool hasStarted;

        [Header("Timing")]
        [SerializeField] private float downedReviveDelay = 10f;
        [SerializeField] private float cartArtilleryInterval = 4.2f;
        [SerializeField] private float cartCannonDamage = 8f;

        private bool isTetherIntact = true;
        private bool isDowned;
        private float nextAttackTime;
        private float nextCartFireTime;
        private Coroutine downedRoutine;

        protected override int DeathAnimState => 3;
        public bool IsDowned => isDowned;
        public bool IsMainBodyDefeated => IsDead || isDowned || (hasStarted && CurrentHealth <= 0f);

        public void ConfigureBossPrefabs(GameObject spikesPrefab, GameObject cartProjPrefab)
        {
            groundSpikesPrefab = spikesPrefab;
            cartTurretProjectilePrefab = cartProjPrefab;
        }

        protected override void Awake()
        {
            base.Awake();
            if (downedReviveDelay < 10f) downedReviveDelay = 10f;
            if (crystalCore == null) crystalCore = GetComponentInChildren<CrystalCore>();
            if (tetherCable == null) tetherCable = GetComponentInChildren<TetherCableHitbox>();
            if (tetherLine == null) tetherLine = GetComponentInChildren<LineRenderer>();

            if (crystalCore != null)
            {
                crystalCore.BindMainTitanBody(this);
            }

            if (tetherLine != null)
            {
                tetherLine.positionCount = 2;
                tetherLine.startWidth = 0.14f;
                tetherLine.endWidth = 0.14f;
            }
        }

        protected override void Start()
        {
            base.Start();
            hasStarted = true;

            float cartHalfWidth = 1.45f;
            if (cartTitanVisual != null)
            {
                cartTitanVisual.SetParent(null, true);
                cartTitanVisual.localScale = new Vector3(-Mathf.Abs(cartTitanVisual.localScale.x), Mathf.Abs(cartTitanVisual.localScale.y), 1f);
                SpriteRenderer cartSr = cartTitanVisual.GetComponentInChildren<SpriteRenderer>();
                if (cartSr != null && cartSr.sprite != null)
                {
                    cartHalfWidth = cartSr.sprite.bounds.extents.x * Mathf.Abs(cartTitanVisual.localScale.x);
                }
                detachedPartners = true;
            }

            Vector3 crystalAnchorPosition = new Vector3(transform.position.x + 4.5f, GROUND_Y, 0f);
            if (crystalCore != null)
            {
                crystalCore.transform.SetParent(null, true);
                crystalCore.transform.localScale = Vector3.one * CrystalCore.ENLARGED_SCALE;
                crystalCore.EnsureEnlargedScaleAndBounds();

                float coreX = crystalAnchorPosition.x;
                float crystalHalfWidth = crystalCore.GetWorldHalfWidth();
                float requiredCartSeparation = crystalHalfWidth + cartHalfWidth + 0.25f;
                float maxAllowedRight = mapMaxX - 0.5f;
                if (cartTitanVisual != null && coreX + requiredCartSeparation + cartHalfWidth > maxAllowedRight)
                {
                    coreX = Mathf.Max(transform.position.x + 2.6f, maxAllowedRight - requiredCartSeparation - cartHalfWidth);
                }

                crystalAnchorPosition = new Vector3(coreX, crystalCore.GetGroundPlacementY(GROUND_Y), 0f);
                crystalCore.transform.position = crystalAnchorPosition;
                crystalCore.BindMainTitanBody(this);
                crystalCore.RefreshProtectionState();
                crystalCore.OnCoreDestroyed -= HandleCoreDestroyed;
                crystalCore.OnCoreDestroyed += HandleCoreDestroyed;
                detachedPartners = true;
            }
            if (tetherCable != null)
            {
                tetherCable.transform.SetParent(null, true);
                tetherCable.transform.localScale = Vector3.one;
                detachedPartners = true;
            }
            if (cartTitanVisual != null)
            {
                float coreX = crystalCore != null ? crystalCore.transform.position.x : crystalAnchorPosition.x;
                float crystalHalfWidth = crystalCore != null ? crystalCore.GetWorldHalfWidth() : 1.32f;
                float cartOffsetX = crystalHalfWidth + cartHalfWidth + 0.25f;
                cartTitanVisual.position = new Vector3(coreX + cartOffsetX, GROUND_Y, 0f);
            }

            UpdateTetherVisuals();
            nextCartFireTime = Time.time + 2.5f;
            NotifyStatusChanged("WAR HAMMER TETHER ACTIVE");
        }

        private void OnDestroy()
        {
            if (crystalCore != null) crystalCore.OnCoreDestroyed -= HandleCoreDestroyed;
            if (detachedPartners && !gameObject.scene.isLoaded) return;
            CleanupDetachedPartners();
        }

        protected override void DefeatBoss()
        {
            CleanupDetachedPartners();
            base.DefeatBoss();
        }

        private void CleanupDetachedPartners()
        {
            if (crystalCore != null) Destroy(crystalCore.gameObject);
            if (tetherCable != null) Destroy(tetherCable.gameObject);
            if (cartTitanVisual != null) Destroy(cartTitanVisual.gameObject);
        }

        protected override void Update()
        {
            UpdateTetherVisuals();
            base.Update();

            if (!IsDead && !isFrozen && isTetherIntact && Time.time >= nextCartFireTime && playerTarget != null)
            {
                nextCartFireTime = Time.time + cartArtilleryInterval;
                FireCartArtillery();
            }
        }

        private void UpdateTetherVisuals()
        {
            if (tetherLine == null) return;
            bool show = !IsDead && isTetherIntact && crystalCore != null && !crystalCore.IsDead;
            tetherLine.enabled = show;
            if (!show) return;

            Vector3 start = transform.position + new Vector3(0f, 0.9f, 0f);
            Vector3 end = crystalCore.GetTetherAnchorWorldPosition();
            tetherLine.SetPosition(0, start);
            tetherLine.SetPosition(1, end);

            if (tetherCable != null && !tetherCable.IsDead)
            {
                tetherCable.transform.position = Vector3.Lerp(start, end, 0.5f);
            }
        }

        protected override void TickBossAI()
        {
            if (isDowned) return;

            float dx = playerTarget.position.x - transform.position.x;
            float distX = Mathf.Abs(dx);
            float meleeRange = bossData != null ? bossData.attackRange : 2.6f;

            if (Time.time < nextAttackTime)
            {
                if (distX > meleeRange)
                {
                    float spd = bossData != null ? bossData.moveSpeed : 2.5f;
                    FaceTarget();
                    if (rb != null) rb.linearVelocity = new Vector2(Mathf.Sign(dx) * spd, rb.linearVelocity.y);
                }
                else
                {
                    StopHorizontalMovement();
                }
                return;
            }

            if (distX <= meleeRange)
            {
                actionRoutine = StartCoroutine(HammerSmashRoutine());
            }
            else
            {
                actionRoutine = StartCoroutine(GroundSpikesRoutine());
            }
        }

        private IEnumerator HammerSmashRoutine()
        {
            isActing = true;
            StopHorizontalMovement();
            FaceTarget();

            yield return PlayTelegraphFlash(bossData != null ? bossData.telegraphDuration : 0.55f);
            if (IsDead || isFrozen || isDowned) yield break;

            SetAnimState(0); // HammerSmash
            yield return new WaitForSeconds(0.24f);

            float dmg = bossData != null ? bossData.attackDamage : 15f;
            ActivateMeleeHitbox(dmg, AttackType.EnemyHeavyAttack, true);
            CameraFollowBeatEmUp.Instance?.Shake(0.22f, 0.25f);

            yield return new WaitForSeconds(0.28f);
            DeactivateMeleeHitbox();

            nextAttackTime = Time.time + (bossData != null ? bossData.attackCooldown : 1.9f);
            isActing = false;
        }

        private IEnumerator GroundSpikesRoutine()
        {
            isActing = true;
            StopHorizontalMovement();
            FaceTarget();

            yield return PlayTelegraphFlash(0.65f, new Color(0.85f, 0.92f, 1f, 1f));
            if (IsDead || isFrozen || isDowned) yield break;

            SetAnimState(1); // GroundSpikes
            float targetX = playerTarget != null ? playerTarget.position.x : transform.position.x + facingDirection * 3f;
            float groundY = GROUND_Y;

            yield return new WaitForSeconds(0.22f);
            if (groundSpikesPrefab != null)
            {
                for (int i = -1; i <= 1; i++)
                {
                    GameObject sp = Instantiate(groundSpikesPrefab, new Vector3(targetX + i * 1.45f, groundY + 0.6f, 0f), Quaternion.identity);
                    Destroy(sp, 0.85f);
                }
            }

            Collider2D[] hits = Physics2D.OverlapBoxAll(new Vector2(targetX, groundY + 0.8f), new Vector2(4.2f, 2.8f), 0f, playerLayer);
            float dmg = bossData != null ? bossData.heavyAttackDamage : 20f;
            foreach (Collider2D hit in hits)
            {
                IDamageable target = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead)
                {
                    target.TakeDamage(new DamageData(dmg, new Vector2(facingDirection * 4f, 6.5f), AttackType.EnemyHeavyAttack, true, gameObject));
                }
            }

            CameraFollowBeatEmUp.Instance?.Shake(0.2f, 0.2f);
            yield return new WaitForSeconds(0.35f);
            nextAttackTime = Time.time + (bossData != null ? bossData.attackCooldown + 0.4f : 2.3f);
            isActing = false;
        }

        private void FireCartArtillery()
        {
            if (cartTurretProjectilePrefab == null) return;
            Vector3 pos = cartTurretFirePoint != null
                ? cartTurretFirePoint.position
                : (cartTitanVisual != null
                    ? cartTitanVisual.position + new Vector3(-0.9f, 1.15f, 0f)
                    : (crystalCore != null ? crystalCore.GetTetherAnchorWorldPosition() + new Vector3(1.5f, 0.2f, 0f) : transform.position + Vector3.right * 3f));

            Vector3 targetAim = playerTarget.position + Vector3.up * 0.85f;
            Vector2 dir = (targetAim - pos).normalized;
            GameObject go = Instantiate(cartTurretProjectilePrefab, pos, Quaternion.identity);
            Projectile proj = go.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.SetSpeed(12f);
                proj.Initialize(new DamageData(cartCannonDamage, new Vector2(dir.x * 4f, 1.5f), AttackType.EnemyAttack, false, gameObject), dir, playerLayer, false);
            }
        }

        public override void TakeDamage(DamageData damageData)
        {
            if (IsDead || isDowned) return;
            base.TakeDamage(damageData);
        }

        protected override void OnHealthDepleted()
        {
            if (crystalCore != null && !crystalCore.IsDead && crystalCore.CurrentHealth > 0f)
            {
                if (!isDowned)
                {
                    if (actionRoutine != null)
                    {
                        StopCoroutine(actionRoutine);
                        actionRoutine = null;
                    }
                    isActing = false;
                    DeactivateMeleeHitbox();
                    if (!isFrozen && spriteRenderer != null && whiteFlashRoutine == null)
                    {
                        spriteRenderer.color = originalColor;
                    }
                    downedRoutine = StartCoroutine(DownedTetherReviveRoutine());
                }
                return;
            }

            if (crystalCore == null || crystalCore.IsDead)
            {
                DefeatBoss();
            }
        }

        private IEnumerator DownedTetherReviveRoutine()
        {
            isDowned = true;
            isVulnerableStagger = true;
            crystalCore?.RefreshProtectionState();
            StopHorizontalMovement();
            SetAnimState(2); // Kneel / Hit

            float remaining = downedReviveDelay;
            int lastDisplayedSecond = -1;
            while (remaining > 0f && crystalCore != null && !crystalCore.IsDead && !IsDead)
            {
                int sec = Mathf.CeilToInt(remaining);
                if (sec != lastDisplayedSecond)
                {
                    lastDisplayedSecond = sec;
                    NotifyStatusChanged($"TITAN DOWNED! BREAK CRYSTAL ({Mathf.CeilToInt(crystalCore != null ? crystalCore.CurrentHealth : 0f)}/500) - {Mathf.CeilToInt(remaining)}s!");
                }
                remaining -= Time.deltaTime;
                yield return null;
            }

            downedRoutine = null;
            if (IsDead) yield break;

            if (crystalCore == null || crystalCore.IsDead)
            {
                DefeatBoss();
                yield break;
            }

            // Revive to 100% full HP via intact CrystalCore tether
            isDowned = false;
            isVulnerableStagger = false;
            CurrentHealth = MaxHealth;
            NotifyHealthChanged();
            crystalCore?.RefreshProtectionState();
            SetAnimState(0);
            nextAttackTime = Time.time + 0.8f;
            NotifyStatusChanged("CRYSTAL INTACT! WAR HAMMER REVIVED FULL HP!");
        }

        private void HandleCoreDestroyed()
        {
            if (IsDead) return;
            isTetherIntact = false;
            if (tetherLine != null) tetherLine.enabled = false;
            if (downedRoutine != null)
            {
                StopCoroutine(downedRoutine);
                downedRoutine = null;
            }
            isDowned = false;
            CurrentHealth = 0f;
            NotifyHealthChanged();
            NotifyStatusChanged("CRYSTAL SHATTERED! WAR HAMMER DEFEATED!");
            CameraFollowBeatEmUp.Instance?.Shake(0.35f, 0.35f);
            DefeatBoss();
        }
    }
}
