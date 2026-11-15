using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Environment
{
    /// <summary>
    /// Interactive gunpowder barrel on the arena floor.
    /// Flashes 0.15s on hit and detonates in a 3.8u radius, heavily damaging enemies (28 DMG, breaks armor)
    /// and lightly damaging unblocked players (8 DMG).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class GunpowderBarrelHazard : MonoBehaviour, IDamageable
    {
        [Header("Explosion Config")]
        [SerializeField] private float explosionRadius = 3.8f;
        [SerializeField] private float enemyDamage = 28f;
        [SerializeField] private float playerDamage = 8f;
        [SerializeField] private float flashDuration = 0.15f;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private GameObject explosionVfxPrefab;

        private Collider2D col;
        private Color originalColor = Color.white;
        private bool isDetonating;

        public Transform Transform => transform;
        public bool IsDead { get; private set; }

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
                spriteRenderer.color = new Color(0.62f, 0.24f, 0.14f, 1f);
                spriteRenderer.sortingOrder = 4;
            }
            originalColor = spriteRenderer.color;
        }

        private void Start()
        {
            if (!CampaignHazardSpawner.CanHazardOperate())
            {
                IsDead = true;
                if (col != null) col.enabled = false;
                if (spriteRenderer != null) spriteRenderer.enabled = false;
            }
        }

        public void TakeDamage(DamageData damageData)
        {
            if (IsDead || isDetonating || !CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }

            StartCoroutine(DetonateRoutine(damageData.attacker));
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsDead || isDetonating || other == null || !CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }

            Projectile proj = other.GetComponent<Projectile>();
            if (proj != null)
            {
                StartCoroutine(DetonateRoutine(proj.Attacker));
            }
        }

        private IEnumerator DetonateRoutine(GameObject triggerAttacker)
        {
            isDetonating = true;

            float elapsed = 0f;
            bool toggle = true;
            while (elapsed < flashDuration)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = toggle ? new Color(1f, 0.45f, 0.1f, 1f) : Color.white;
                }
                toggle = !toggle;
                yield return new WaitForSeconds(0.05f);
                elapsed += 0.05f;
            }

            IsDead = true;
            if (col != null) col.enabled = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
                spriteRenderer.enabled = false;
            }

            GameObject enemyAttacker = gameObject;
            if (triggerAttacker != null && triggerAttacker.GetComponentInParent<BaseCharacter>() != null)
            {
                enemyAttacker = triggerAttacker;
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
            HashSet<IDamageable> hitEnemies = new HashSet<IDamageable>();
            HashSet<BaseCharacter> hitPlayers = new HashSet<BaseCharacter>();

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || hit.gameObject == gameObject) continue;

                if (CampaignHazardSpawner.TryResolveEnemyDamageable(hit, out IDamageable enemyTarget) && !hitEnemies.Contains(enemyTarget))
                {
                    hitEnemies.Add(enemyTarget);
                    float dirX = Mathf.Sign(enemyTarget.Transform.position.x - transform.position.x);
                    if (Mathf.Approximately(dirX, 0f)) dirX = 1f;
                    enemyTarget.TakeDamage(new DamageData(
                        enemyDamage,
                        new Vector2(dirX * 8f, 4f),
                        AttackType.Combo_DashJ,
                        true,
                        enemyAttacker
                    ));
                }

                if (CampaignHazardSpawner.TryResolvePlayer(hit, out BaseCharacter bc) && !hitPlayers.Contains(bc))
                {
                    hitPlayers.Add(bc);
                    float dirX = Mathf.Sign(bc.transform.position.x - transform.position.x);
                    if (Mathf.Approximately(dirX, 0f)) dirX = 1f;
                    CampaignHazardSpawner.ApplyPlayerHazardDamage(
                        bc,
                        playerDamage,
                        new Vector2(dirX * 6f, 3f),
                        AttackType.EnemyAttack,
                        gameObject
                    );
                }
            }

            CameraFollowBeatEmUp.Instance?.Shake(0.25f, 0.2f);

            if (explosionVfxPrefab != null)
            {
                GameObject fx = Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);
                Destroy(fx, 0.6f);
            }
            else
            {
                yield return CampaignHazardSpawner.AnimateExpandingRingRoutine(
                    transform.position,
                    new Color(1f, 0.5f, 0.1f, 0.9f),
                    0.8f,
                    explosionRadius * 1.9f,
                    0.28f,
                    transform
                );
            }

            Destroy(gameObject, 0.05f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
