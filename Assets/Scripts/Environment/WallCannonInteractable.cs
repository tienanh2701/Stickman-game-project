using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Enemies;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Environment
{
    /// <summary>
    /// Interactive edge or rooftop Wall Cannon. When struck by a Player attack while off its 11.0s cooldown,
    /// sweeps a heavy cannonball across the arena lane dealing 45 armor-breaking damage to all enemies/bosses/cores.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class WallCannonInteractable : MonoBehaviour, IDamageable
    {
        [Header("Cannon Config")]
        [SerializeField] private int fireDirection = 1;
        [SerializeField] private float cooldownDuration = 11.0f;
        [SerializeField] private float cannonDamage = 45f;
        [SerializeField] private float projectileSpeed = 24f;
        [SerializeField] private float sweepRadius = 1.6f;

        [Header("Arena Bounds")]
        [SerializeField] private float arenaMinX = -7f;
        [SerializeField] private float arenaMaxX = 22f;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite cannonballSprite;

        private Collider2D col;
        private Vector3 initialScale = Vector3.one;
        private float nextReadyTime;
        private GameObject activeCannonball;

        public Transform Transform => transform;
        public bool IsDead => Time.time < nextReadyTime || !CampaignHazardSpawner.CanHazardOperate();

        private void Awake()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            gameObject.layer = enemyLayer >= 0 ? enemyLayer : 8;

            col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = CampaignHazardSpawner.GetFallbackSquareSprite();
                spriteRenderer.sortingOrder = 4;
            }

            initialScale = transform.localScale;
            if (Mathf.Approximately(initialScale.x, 0f)) initialScale = Vector3.one;
            ApplyFacingScale();
        }

        private void Start()
        {
            if (col != null)
            {
                col.isTrigger = true;
            }

            if (!CampaignHazardSpawner.CanHazardOperate())
            {
                if (col != null) col.enabled = false;
                if (spriteRenderer != null) spriteRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (activeCannonball != null) Destroy(activeCannonball);
        }

        /// <summary>
        /// Sets firing direction (+1 right, -1 left) and reload cooldown duration.
        /// </summary>
        public void ConfigureDirection(int dir, float cooldown = 11f)
        {
            fireDirection = dir >= 0 ? 1 : -1;
            cooldownDuration = Mathf.Max(0.5f, cooldown);
            ApplyFacingScale();
        }

        /// <summary>
        /// Sets horizontal bounds defining the far edge of the cannonball sweep.
        /// </summary>
        public void ConfigureArenaBounds(float minX, float maxX)
        {
            arenaMinX = Mathf.Min(minX, maxX);
            arenaMaxX = Mathf.Max(minX, maxX);
        }

        private void ApplyFacingScale()
        {
            Vector3 s = initialScale;
            s.x = Mathf.Abs(initialScale.x) * (fireDirection >= 0 ? 1f : -1f);
            transform.localScale = s;
        }

        private void Update()
        {
            if (spriteRenderer == null || !CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }

            if (Time.time >= nextReadyTime)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.5f);
                spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.45f, 1f), pulse * 0.65f);
            }
            else
            {
                spriteRenderer.color = new Color(0.42f, 0.44f, 0.48f, 0.85f);
            }
        }

        public void TakeDamage(DamageData damageData)
        {
            if (Time.time < nextReadyTime || !CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }

            // Ignore accidental enemy hits; allow Player attacks or direct triggers
            if (damageData.attacker != null &&
                damageData.attacker.GetComponentInParent<BaseCharacter>() == null &&
                (damageData.attacker.GetComponentInParent<EnemyBase>() != null ||
                 damageData.attacker.GetComponentInParent<BossBase>() != null))
            {
                return;
            }

            nextReadyTime = Time.time + cooldownDuration;
            GameObject creditAttacker = damageData.attacker != null ? damageData.attacker : gameObject;
            StartCoroutine(FireCannonSweepRoutine(creditAttacker));
        }

        private IEnumerator FireCannonSweepRoutine(GameObject creditAttacker)
        {
            CameraFollowBeatEmUp.Instance?.Shake(0.28f, 0.30f);

            Vector3 startPos = transform.position + new Vector3(fireDirection * 0.9f, 0.30f, 0f);
            float targetEdgeX = fireDirection > 0
                ? Mathf.Max(startPos.x + 12f, arenaMaxX + 2.5f)
                : Mathf.Min(startPos.x - 12f, arenaMinX - 2.5f);
            float endY = EnemyBase.GROUND_Y + 0.85f;
            float totalSpanX = Mathf.Max(1f, Mathf.Abs(targetEdgeX - startPos.x));

            activeCannonball = new GameObject("WallCannon_Projectile");
            activeCannonball.transform.SetParent(transform.parent, false);
            activeCannonball.transform.position = startPos;
            activeCannonball.transform.localScale = new Vector3(0.95f, 0.95f, 1f);

            SpriteRenderer ballSr = activeCannonball.AddComponent<SpriteRenderer>();
            ballSr.sprite = cannonballSprite != null ? cannonballSprite : CampaignHazardSpawner.GetFallbackCircleSprite();
            ballSr.color = cannonballSprite != null ? Color.white : new Color(1f, 0.58f, 0.15f, 1f);
            ballSr.sortingOrder = 7;

            StartCoroutine(CampaignHazardSpawner.AnimateExpandingRingRoutine(
                startPos,
                new Color(1f, 0.65f, 0.15f, 0.9f),
                0.5f,
                2.2f,
                0.22f,
                transform
            ));

            HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

            while (activeCannonball != null)
            {
                if (!CampaignHazardSpawner.CanHazardOperate())
                {
                    break;
                }

                Vector3 pos = activeCannonball.transform.position;
                pos.x += fireDirection * projectileSpeed * Time.deltaTime;
                float progress = Mathf.Clamp01(Mathf.Abs(pos.x - startPos.x) / totalSpanX);
                pos.y = Mathf.Lerp(startPos.y, endY, progress * 0.85f);
                activeCannonball.transform.position = pos;
                activeCannonball.transform.Rotate(0f, 0f, -fireDirection * 540f * Time.deltaTime);

                // Sweep vertical band from ground lane up to elevated trajectory so rooftop cannons still hit ground targets and Crystal Core
                float minSweepY = EnemyBase.GROUND_Y - 0.3f;
                float maxSweepY = Mathf.Max(pos.y + sweepRadius, EnemyBase.GROUND_Y + sweepRadius * 2f);
                Vector2 boxCenter = new Vector2(pos.x, (minSweepY + maxSweepY) * 0.5f);
                Vector2 boxSize = new Vector2(sweepRadius * 1.6f, Mathf.Max(1.5f, maxSweepY - minSweepY));
                Collider2D[] overlaps = Physics2D.OverlapBoxAll(boxCenter, boxSize, 0f);
                for (int i = 0; i < overlaps.Length; i++)
                {
                    Collider2D hit = overlaps[i];
                    if (hit == null || hit.gameObject == gameObject) continue;

                    if (CampaignHazardSpawner.TryResolveEnemyDamageable(hit, out IDamageable target) && !hitTargets.Contains(target))
                    {
                        hitTargets.Add(target);
                        target.TakeDamage(new DamageData(
                            cannonDamage,
                            new Vector2(fireDirection * 9f, 3.5f),
                            AttackType.Combo_DashI,
                            true,
                            creditAttacker
                        ));
                    }
                }

                if ((fireDirection > 0 && pos.x >= targetEdgeX) || (fireDirection < 0 && pos.x <= targetEdgeX))
                {
                    break;
                }

                yield return null;
            }

            if (activeCannonball != null)
            {
                Destroy(activeCannonball);
                activeCannonball = null;
            }
        }
    }
}
