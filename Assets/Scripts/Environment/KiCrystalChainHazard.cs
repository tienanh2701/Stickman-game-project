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
    /// World 2 (Dragon Ball Wasteland) glowing cyan/gold Ki Crystal perched atop floating rocks.
    /// When struck by Player melee or projectile, unleashes an AOE Ki blast and chains after 0.18s
    /// to neighboring KiCrystalChainHazards within 5.5f units, respawning after 10.0s.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class KiCrystalChainHazard : MonoBehaviour, IDamageable
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            proceduralCrystalSprite = null;
        }

        [Header("Chain Blast Config")]
        [SerializeField] private float explosionRadius = 3.6f;
        [SerializeField] private float enemyDamage = 26f;
        [SerializeField] private float chainRadius = 5.5f;
        [SerializeField] private float chainDelay = 0.18f;
        [SerializeField] private float respawnDelay = 10.0f;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private static Sprite proceduralCrystalSprite;

        private BoxCollider2D boxCol;
        private Vector3 baseLocalPos;
        private Color baseTint = Color.white;
        private bool isDetonated;
        private bool isChainQueued;
        private Coroutine activeRoutine;
        private readonly List<GameObject> activeArcObjects = new List<GameObject>();

        public Transform Transform => transform;
        public bool IsDead => isDetonated || !CampaignHazardSpawner.CanHazardOperate();

        private void Awake()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            gameObject.layer = enemyLayer >= 0 ? enemyLayer : 8;

            boxCol = GetComponent<BoxCollider2D>();
            if (boxCol != null)
            {
                boxCol.isTrigger = true;
                if (boxCol.size.x < 0.2f || boxCol.size.y < 0.2f)
                {
                    boxCol.size = new Vector2(0.95f, 1.15f);
                }
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = GetOrCreateCrystalSprite();
                spriteRenderer.sortingOrder = 5;
            }
            else if (spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = GetOrCreateCrystalSprite();
            }

            baseTint = spriteRenderer.color;
            baseLocalPos = transform.localPosition;
        }

        private void Start()
        {
            baseLocalPos = transform.localPosition;
            if (boxCol != null)
            {
                boxCol.isTrigger = true;
            }

            if (!CampaignHazardSpawner.CanHazardOperate())
            {
                if (boxCol != null) boxCol.enabled = false;
                if (spriteRenderer != null) spriteRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < activeArcObjects.Count; i++)
            {
                if (activeArcObjects[i] != null)
                {
                    Destroy(activeArcObjects[i]);
                }
            }
            activeArcObjects.Clear();
        }

        /// <summary>
        /// Configures blast, chain propagation, and respawn parameters at runtime.
        /// </summary>
        public void Configure(float radius = 3.6f, float damage = 26f, float chainRange = 5.5f, float chainWait = 0.18f, float respawn = 10.0f)
        {
            explosionRadius = Mathf.Max(0.5f, radius);
            enemyDamage = Mathf.Max(1f, damage);
            chainRadius = Mathf.Max(0.5f, chainRange);
            chainDelay = Mathf.Max(0.02f, chainWait);
            respawnDelay = Mathf.Max(1.0f, respawn);
        }

        private void Update()
        {
            if (spriteRenderer == null || !CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }

            if (isDetonated)
            {
                return;
            }

            if (isChainQueued)
            {
                float flash = 0.5f + 0.5f * Mathf.Sin(Time.time * 36f);
                spriteRenderer.color = Color.Lerp(new Color(0.25f, 0.95f, 1f, 1f), new Color(1f, 0.92f, 0.35f, 1f), flash);
                return;
            }

            float shimmer = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.2f + transform.position.x);
            Color cyanGlow = new Color(0.32f, 0.96f, 1f, 1f);
            Color goldGlow = new Color(1f, 0.86f, 0.28f, 1f);
            spriteRenderer.color = Color.Lerp(cyanGlow, goldGlow, shimmer * 0.55f) * baseTint;

            float bob = Mathf.Sin(Time.time * 3.0f + transform.position.x * 0.7f) * 0.08f;
            transform.localPosition = baseLocalPos + new Vector3(0f, bob, 0f);
        }

        public void TakeDamage(DamageData damageData)
        {
            if (isDetonated || isChainQueued || !CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }

            if (damageData.attacker != null &&
                damageData.attacker.GetComponentInParent<BaseCharacter>() == null &&
                (damageData.attacker.GetComponentInParent<EnemyBase>() != null ||
                 damageData.attacker.GetComponentInParent<BossBase>() != null))
            {
                return;
            }

            GameObject creditAttacker = damageData.attacker != null ? damageData.attacker : gameObject;
            BeginDetonation(0f, creditAttacker);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isDetonated || isChainQueued || other == null || !CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }

            Projectile proj = other.GetComponent<Projectile>();
            if (proj != null && proj.Attacker != null && proj.Attacker.GetComponentInParent<BaseCharacter>() != null)
            {
                BeginDetonation(0f, proj.Attacker);
            }
        }

        private void TriggerFromChain(Vector3 sourcePos, float delay, GameObject creditAttacker)
        {
            if (isDetonated || isChainQueued || !CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }

            SpawnChainLightningArc(sourcePos, transform.position, delay);
            BeginDetonation(delay, creditAttacker);
        }

        private void BeginDetonation(float delayBeforeBlast, GameObject creditAttacker)
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
            }
            activeRoutine = StartCoroutine(ChainExplosionAndRespawnRoutine(delayBeforeBlast, creditAttacker));
        }

        private IEnumerator ChainExplosionAndRespawnRoutine(float delayBeforeBlast, GameObject creditAttacker)
        {
            isChainQueued = true;

            if (delayBeforeBlast > 0f)
            {
                yield return new WaitForSeconds(delayBeforeBlast);
            }

            if (!CampaignHazardSpawner.CanHazardOperate())
            {
                isChainQueued = false;
                activeRoutine = null;
                yield break;
            }

            isChainQueued = false;
            isDetonated = true;
            transform.localPosition = baseLocalPos;

            if (boxCol != null) boxCol.enabled = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.35f, 0.85f, 1f, 0.14f);
            }

            DealBlastDamage(creditAttacker);
            PropagateChainToNeighbors(creditAttacker);

            CameraFollowBeatEmUp.Instance?.Shake(0.20f, 0.18f);
            StartCoroutine(CampaignHazardSpawner.AnimateExpandingRingRoutine(
                transform.position,
                new Color(0.35f, 0.96f, 1f, 0.92f),
                0.6f,
                explosionRadius * 1.9f,
                0.28f,
                transform
            ));
            StartCoroutine(CampaignHazardSpawner.AnimateExpandingRingRoutine(
                transform.position,
                new Color(1f, 0.86f, 0.25f, 0.88f),
                0.4f,
                explosionRadius * 1.4f,
                0.22f,
                transform
            ));

            yield return new WaitForSeconds(respawnDelay);

            isDetonated = false;
            isChainQueued = false;
            if (CampaignHazardSpawner.CanHazardOperate())
            {
                if (boxCol != null) boxCol.enabled = true;
                if (spriteRenderer != null) spriteRenderer.color = baseTint;
            }
            activeRoutine = null;
        }

        private void DealBlastDamage(GameObject creditAttacker)
        {
            GameObject resolvedAttacker = creditAttacker != null ? creditAttacker : gameObject;
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
            HashSet<IDamageable> hitEnemies = new HashSet<IDamageable>();

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || hit.gameObject == gameObject) continue;

                if (CampaignHazardSpawner.TryResolveEnemyDamageable(hit, out IDamageable target) && !hitEnemies.Contains(target))
                {
                    hitEnemies.Add(target);
                    float dirX = Mathf.Sign(target.Transform.position.x - transform.position.x);
                    if (Mathf.Approximately(dirX, 0f)) dirX = 1f;
                    target.TakeDamage(new DamageData(
                        enemyDamage,
                        new Vector2(dirX * 7.5f, 4.0f),
                        AttackType.Combo_DashJ,
                        true,
                        resolvedAttacker
                    ));
                }
            }
        }

        private void PropagateChainToNeighbors(GameObject creditAttacker)
        {
            KiCrystalChainHazard[] allCrystals = FindObjectsByType<KiCrystalChainHazard>();
            for (int i = 0; i < allCrystals.Length; i++)
            {
                KiCrystalChainHazard neighbor = allCrystals[i];
                if (neighbor == null || neighbor == this || neighbor.isDetonated || neighbor.isChainQueued)
                {
                    continue;
                }

                float dist = Vector2.Distance(transform.position, neighbor.transform.position);
                if (dist <= chainRadius)
                {
                    neighbor.TriggerFromChain(transform.position, chainDelay, creditAttacker);
                }
            }
        }

        private void SpawnChainLightningArc(Vector3 fromPos, Vector3 toPos, float duration)
        {
            GameObject arcObj = new GameObject("KiCrystal_ChainArc");
            arcObj.transform.SetParent(transform.parent, false);
            LineRenderer lr = arcObj.AddComponent<LineRenderer>();
            lr.material = CampaignHazardSpawner.GetSpritesDefaultMaterial();
            lr.positionCount = 3;
            lr.startWidth = 0.14f;
            lr.endWidth = 0.10f;
            lr.startColor = new Color(0.3f, 0.96f, 1f, 0.95f);
            lr.endColor = new Color(1f, 0.88f, 0.3f, 0.95f);
            lr.sortingOrder = 8;

            Vector3 mid = (fromPos + toPos) * 0.5f + new Vector3(0f, 0.32f, 0f);
            lr.SetPosition(0, fromPos);
            lr.SetPosition(1, mid);
            lr.SetPosition(2, toPos);

            activeArcObjects.Add(arcObj);
            Destroy(arcObj, Mathf.Max(0.1f, duration + 0.08f));
        }

        private static Sprite GetOrCreateCrystalSprite()
        {
            if (proceduralCrystalSprite != null)
            {
                return proceduralCrystalSprite;
            }

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color[] pixels = new Color[size * size];

            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = Mathf.Abs(x - half + 0.5f) / (half * 0.65f);
                    float ny = Mathf.Abs(y - half + 0.5f) / (half * 0.92f);
                    float diamond = nx + ny;

                    if (diamond <= 1.0f)
                    {
                        float rim = Mathf.Clamp01((1.0f - diamond) * 4.5f);
                        bool isInnerCore = diamond < 0.45f;
                        bool isFacet = x >= half;
                        Color c = isInnerCore
                            ? new Color(1f, 0.94f, 0.55f, 1f)
                            : (isFacet
                                ? new Color(0.28f, 0.88f, 1f, 1f)
                                : new Color(0.65f, 1f, 1f, 1f));
                        if (diamond > 0.82f)
                        {
                            c = Color.Lerp(c, new Color(1f, 0.9f, 0.35f, 1f), 0.7f);
                        }
                        c.a = rim;
                        pixels[y * size + x] = c;
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            proceduralCrystalSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return proceduralCrystalSprite;
        }
    }
}
