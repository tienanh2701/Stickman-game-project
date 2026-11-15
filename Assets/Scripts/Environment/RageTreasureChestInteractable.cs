using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Enemies;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Environment
{
    /// <summary>
    /// World 3 (Diend Neo Ruins) elevated glowing Rage Treasure Chest.
    /// Grants +12 Rage to BaseCharacter when touched or struck, then respawns after 14.0s.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class RageTreasureChestInteractable : MonoBehaviour, IDamageable
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            proceduralChestSprite = null;
        }

        [Header("Chest Config")]
        [SerializeField] private float rageBonus = 12f;
        [SerializeField] private float respawnDelay = 14.0f;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private static Sprite proceduralChestSprite;

        private BoxCollider2D boxCol;
        private Vector3 baseLocalPos;
        private Color baseTint = Color.white;
        private bool isCollected;
        private BaseCharacter cachedPlayer;
        private Collider2D cachedPlayerCol;
        private Coroutine respawnRoutine;

        public Transform Transform => transform;
        public bool IsDead => isCollected || !CampaignHazardSpawner.CanHazardOperate();

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
                    boxCol.size = new Vector2(0.95f, 0.85f);
                }
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = GetOrCreateChestSprite();
                spriteRenderer.sortingOrder = 5;
            }
            else if (spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = GetOrCreateChestSprite();
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

        /// <summary>
        /// Configures granted Rage amount and respawn cooldown duration.
        /// </summary>
        public void Configure(float rageAmount = 12f, float respawn = 14.0f)
        {
            rageBonus = Mathf.Max(1f, rageAmount);
            respawnDelay = Mathf.Max(1f, respawn);
        }

        private void Update()
        {
            if (spriteRenderer == null || boxCol == null || !CampaignHazardSpawner.CanHazardOperate() || isCollected)
            {
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.0f + transform.position.x);
            spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.92f, 0.48f, 1f), pulse * 0.55f) * baseTint;

            float bob = Mathf.Sin(Time.time * 2.8f + transform.position.x * 0.5f) * 0.04f;
            transform.localPosition = baseLocalPos + new Vector3(0f, bob, 0f);

            if (cachedPlayer == null || cachedPlayer.IsDead)
            {
                cachedPlayer = LevelManager.Instance != null && LevelManager.Instance.ActivePlayer != null
                    ? LevelManager.Instance.ActivePlayer
                    : FindAnyObjectByType<BaseCharacter>();
                cachedPlayerCol = cachedPlayer != null ? cachedPlayer.GetComponent<Collider2D>() : null;
            }

            if (cachedPlayer != null && !cachedPlayer.IsDead && cachedPlayerCol != null && cachedPlayerCol.enabled)
            {
                if (boxCol.bounds.Intersects(cachedPlayerCol.bounds))
                {
                    CollectChest(cachedPlayer);
                }
            }
        }

        public void TakeDamage(DamageData damageData)
        {
            if (isCollected || !CampaignHazardSpawner.CanHazardOperate())
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

            BaseCharacter player = ResolvePlayerFromAttacker(damageData.attacker);
            if (player != null)
            {
                CollectChest(player);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCollected || other == null || !CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }

            if (CampaignHazardSpawner.TryResolvePlayer(other, out BaseCharacter directPlayer))
            {
                CollectChest(directPlayer);
                return;
            }

            Projectile proj = other.GetComponent<Projectile>();
            if (proj != null && proj.Attacker != null)
            {
                BaseCharacter projOwner = proj.Attacker.GetComponentInParent<BaseCharacter>();
                if (projOwner != null)
                {
                    CollectChest(projOwner);
                }
            }
        }

        private BaseCharacter ResolvePlayerFromAttacker(GameObject attacker)
        {
            if (attacker != null)
            {
                BaseCharacter bc = attacker.GetComponentInParent<BaseCharacter>();
                if (bc != null && !bc.IsDead)
                {
                    return bc;
                }
            }

            if (LevelManager.Instance != null && LevelManager.Instance.ActivePlayer != null && !LevelManager.Instance.ActivePlayer.IsDead)
            {
                return LevelManager.Instance.ActivePlayer;
            }

            return FindAnyObjectByType<BaseCharacter>();
        }

        private void CollectChest(BaseCharacter player)
        {
            if (isCollected || player == null || player.IsDead)
            {
                return;
            }

            isCollected = true;
            transform.localPosition = baseLocalPos;

            player.RageManagerRef?.AddRageFloat(rageBonus);

            if (boxCol != null) boxCol.enabled = false;
            if (spriteRenderer != null)
            {
                Color ghost = baseTint;
                ghost.a = 0.16f;
                spriteRenderer.color = ghost;
            }

            StartCoroutine(CampaignHazardSpawner.AnimateExpandingRingRoutine(
                transform.position,
                new Color(1f, 0.85f, 0.22f, 0.92f),
                0.5f,
                2.8f,
                0.32f,
                transform
            ));
            StartCoroutine(CampaignHazardSpawner.AnimateExpandingRingRoutine(
                transform.position,
                new Color(0.28f, 0.94f, 1f, 0.85f),
                0.35f,
                2.1f,
                0.26f,
                transform
            ));

            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
            }
            respawnRoutine = StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(respawnDelay);

            isCollected = false;
            if (CampaignHazardSpawner.CanHazardOperate())
            {
                if (boxCol != null) boxCol.enabled = true;
                if (spriteRenderer != null) spriteRenderer.color = baseTint;
            }
            respawnRoutine = null;
        }

        private static Sprite GetOrCreateChestSprite()
        {
            if (proceduralChestSprite != null)
            {
                return proceduralChestSprite;
            }

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color[] pixels = new Color[size * size];

            Color darkTrim = new Color(0.16f, 0.14f, 0.24f, 1f);
            Color goldTrim = new Color(1f, 0.82f, 0.20f, 1f);
            Color bodyTech = new Color(0.24f, 0.30f, 0.48f, 1f);
            Color lidTech = new Color(0.32f, 0.38f, 0.58f, 1f);
            Color neoCore = new Color(0.30f, 0.96f, 1f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (x < 3 || x >= size - 3 || y < 3 || y >= size - 5)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    bool isBorder = x == 3 || x == size - 4 || y == 3 || y == size - 6;
                    bool isGoldStrap = x == 7 || x == 8 || x == size - 9 || x == size - 8 || y == 15 || y == 16;
                    bool isLockCore = x >= 14 && x <= 17 && y >= 13 && y <= 17;

                    if (isLockCore)
                    {
                        pixels[y * size + x] = neoCore;
                    }
                    else if (isBorder)
                    {
                        pixels[y * size + x] = darkTrim;
                    }
                    else if (isGoldStrap || y == 4 || y == size - 7)
                    {
                        pixels[y * size + x] = goldTrim;
                    }
                    else
                    {
                        pixels[y * size + x] = y > 16 ? lidTech : bodyTech;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            proceduralChestSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return proceduralChestSprite;
        }
    }
}
