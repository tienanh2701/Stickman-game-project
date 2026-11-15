using UnityEngine;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Enemies;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Environment
{
    /// <summary>
    /// Linked Neo-City Aurora Portal pair (portalA <-> portalB) sharing a 2.0s teleport cooldown.
    /// Entering either portal warps the Player cleanly to the partner portal with cyan/purple flash VFX.
    /// </summary>
    public class AuroraPortalPair : MonoBehaviour
    {
        [Header("Linked Portals")]
        [SerializeField] private Transform portalA;
        [SerializeField] private Transform portalB;
        [SerializeField] private float teleportCooldown = 2.0f;
        [SerializeField] private Vector2 triggerSize = new Vector2(1.25f, 2.0f);

        private BoxCollider2D colA;
        private BoxCollider2D colB;
        private SpriteRenderer srA;
        private SpriteRenderer srB;
        private float nextTeleportTime;
        private BaseCharacter cachedPlayer;
        private Collider2D cachedPlayerCol;

        private void Awake()
        {
            SetupPortalPairVisuals();
        }

        private void Start()
        {
            if (!CampaignHazardSpawner.CanHazardOperate())
            {
                if (srA != null) srA.enabled = false;
                if (srB != null) srB.enabled = false;
                if (colA != null) colA.enabled = false;
                if (colB != null) colB.enabled = false;
            }
        }

        /// <summary>
        /// Places Portal A and Portal B at world coordinates and sets the shared cooldown duration.
        /// </summary>
        public void ConfigurePortals(Vector3 worldPosA, Vector3 worldPosB, float cooldown = 2.0f)
        {
            SetupPortalPairVisuals();
            teleportCooldown = Mathf.Max(0.25f, cooldown);

            if (portalA != null) portalA.position = worldPosA;
            if (portalB != null) portalB.position = worldPosB;
        }

        private void SetupPortalPairVisuals()
        {
            SpriteRenderer rootSr = GetComponent<SpriteRenderer>();
            Sprite sharedSprite = rootSr != null && rootSr.sprite != null
                ? rootSr.sprite
                : CampaignHazardSpawner.GetFallbackCircleSprite();

            if (rootSr != null)
            {
                rootSr.enabled = false;
            }

            Collider2D rootCol = GetComponent<Collider2D>();
            if (rootCol != null)
            {
                rootCol.enabled = false;
            }

            if (portalA == null)
            {
                Transform existingA = transform.Find("PortalA");
                portalA = existingA != null ? existingA : CreatePortalNode("PortalA", new Vector3(-4f, -1.65f, 0f), sharedSprite).transform;
            }

            if (portalB == null)
            {
                Transform existingB = transform.Find("PortalB");
                portalB = existingB != null ? existingB : CreatePortalNode("PortalB", new Vector3(4f, -1.65f, 0f), sharedSprite).transform;
            }

            EnsurePortalComponents(portalA, sharedSprite, out colA, out srA);
            EnsurePortalComponents(portalB, sharedSprite, out colB, out srB);
        }

        private GameObject CreatePortalNode(string nodeName, Vector3 localPos, Sprite sprite)
        {
            GameObject node = new GameObject(nodeName);
            node.transform.SetParent(transform, false);
            node.transform.localPosition = localPos;
            SpriteRenderer sr = node.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 4;
            return node;
        }

        private void EnsurePortalComponents(Transform node, Sprite fallbackSprite, out BoxCollider2D boxCol, out SpriteRenderer sr)
        {
            sr = node.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = node.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = fallbackSprite;
                sr.sortingOrder = 4;
            }
            else if (sr.sprite == null)
            {
                sr.sprite = fallbackSprite;
            }

            boxCol = node.GetComponent<BoxCollider2D>();
            if (boxCol == null)
            {
                boxCol = node.gameObject.AddComponent<BoxCollider2D>();
                boxCol.size = triggerSize;
            }
            boxCol.isTrigger = true;
        }

        private void Update()
        {
            if (!CampaignHazardSpawner.CanHazardOperate() || portalA == null || portalB == null || colA == null || colB == null)
            {
                return;
            }

            UpdatePortalVisuals();

            if (Time.time < nextTeleportTime)
            {
                return;
            }

            if (cachedPlayer == null || cachedPlayer.IsDead)
            {
                cachedPlayer = LevelManager.Instance != null && LevelManager.Instance.ActivePlayer != null
                    ? LevelManager.Instance.ActivePlayer
                    : FindAnyObjectByType<BaseCharacter>();
                cachedPlayerCol = cachedPlayer != null ? cachedPlayer.GetComponent<Collider2D>() : null;
            }

            if (cachedPlayer == null || cachedPlayer.IsDead || cachedPlayerCol == null)
            {
                return;
            }

            Bounds playerBounds = cachedPlayerCol.bounds;
            if (colA.bounds.Intersects(playerBounds))
            {
                TeleportPlayer(cachedPlayer, portalA, portalB, colB);
            }
            else if (colB.bounds.Intersects(playerBounds))
            {
                TeleportPlayer(cachedPlayer, portalB, portalA, colA);
            }
        }

        private void UpdatePortalVisuals()
        {
            bool ready = Time.time >= nextTeleportTime;
            float pulse = 0.78f + 0.22f * Mathf.Sin(Time.time * 4.5f);
            Color tint = ready
                ? Color.Lerp(new Color(0.55f, 0.95f, 1f, 1f), new Color(0.85f, 0.55f, 1f, 1f), 0.5f + 0.5f * Mathf.Sin(Time.time * 3f))
                : new Color(0.45f, 0.45f, 0.55f, 0.45f);

            if (ready) tint.a = pulse;
            if (srA != null) srA.color = tint;
            if (srB != null) srB.color = tint;
        }

        private void TeleportPlayer(BaseCharacter player, Transform fromPortal, Transform toPortal, BoxCollider2D destinationCol)
        {
            nextTeleportTime = Time.time + teleportCooldown;

            float destY = destinationCol != null
                ? Mathf.Max(EnemyBase.GROUND_Y, destinationCol.bounds.min.y + 0.05f)
                : Mathf.Max(EnemyBase.GROUND_Y, toPortal.position.y - 0.8f);

            Vector3 destPos = new Vector3(toPortal.position.x, destY, player.transform.position.z);
            player.transform.position = destPos;

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.35f, 0f);
            }

            StartCoroutine(CampaignHazardSpawner.AnimateExpandingRingRoutine(
                fromPortal.position,
                new Color(0.35f, 0.92f, 1f, 0.9f),
                0.5f,
                2.5f,
                0.28f,
                transform
            ));
            StartCoroutine(CampaignHazardSpawner.AnimateExpandingRingRoutine(
                toPortal.position,
                new Color(0.75f, 0.42f, 1f, 0.95f),
                0.5f,
                2.8f,
                0.32f,
                transform
            ));
        }
    }
}
