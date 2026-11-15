using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Environment
{
    /// <summary>
    /// Elevated one-way jump platform (Wooden Eaves, Floating Rock, Cyber Platform).
    /// Supports optional fragility (tremble 3.5s -> break -> respawn 5.0s) and fast S+Space / Down+Jump drop-through.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(PlatformEffector2D))]
    public class OneWayPlatformHazard : MonoBehaviour
    {
        [Header("Platform Settings")]
        [SerializeField] private bool isFragile = false;
        [SerializeField] private float trembleDuration = 3.5f;
        [SerializeField] private float respawnDelay = 5.0f;
        [SerializeField] private float dropThroughDuration = 0.25f;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private BoxCollider2D boxCol;
        private PlatformEffector2D effector;
        private Color originalColor = Color.white;
        private Vector3 baseVisualLocalPos;
        private Vector3 baseRootPos;
        private bool isVisualChild;
        private bool isBreaking;
        private bool isBroken;
        private Collider2D standingPlayerCollider;
        private BaseCharacter cachedPlayer;
        private Collider2D cachedPlayerCol;
        private Coroutine fragileRoutine;

        private float lastSTapTime = -10f;
        private Rigidbody2D pendingDropRb;
        private int dropSuppressJumpFrame = -1;

        public bool IsFragile => isFragile;
        public float TopSurfaceY => boxCol != null ? boxCol.bounds.max.y : transform.position.y + 0.18f;

        private void Awake()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            gameObject.layer = groundLayer >= 0 ? groundLayer : 6;

            boxCol = GetComponent<BoxCollider2D>();
            boxCol.isTrigger = false;
            boxCol.usedByEffector = true;

            effector = GetComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.useOneWayGrouping = false;
            effector.surfaceArc = 165f;

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer < 0) enemyLayer = 8;
            effector.useColliderMask = true;
            effector.colliderMask = ~(1 << enemyLayer);

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = CampaignHazardSpawner.GetFallbackSquareSprite();
                spriteRenderer.color = new Color(0.6f, 0.42f, 0.25f, 1f);
                spriteRenderer.sortingOrder = 2;
            }

            originalColor = spriteRenderer.color;
            isVisualChild = spriteRenderer.gameObject != gameObject;
            baseVisualLocalPos = spriteRenderer.transform.localPosition;
            baseRootPos = transform.position;
        }

        private void Start()
        {
            baseRootPos = transform.position;

            if (!CampaignHazardSpawner.CanHazardOperate())
            {
                if (boxCol != null) boxCol.enabled = false;
                if (spriteRenderer != null) spriteRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Configures whether this elevated platform breaks briefly after the Player lands on it.
        /// </summary>
        public void Configure(bool fragile)
        {
            isFragile = fragile;
        }

        /// <summary>
        /// Configures fragility along with explicit tremble and respawn durations.
        /// </summary>
        public void Configure(bool fragile, float customTrembleDuration, float customRespawnDelay)
        {
            isFragile = fragile;
            trembleDuration = Mathf.Max(0.25f, customTrembleDuration);
            respawnDelay = Mathf.Max(0.5f, customRespawnDelay);
        }

        /// <summary>
        /// Scales visual sprite width and BoxCollider2D width to match targetWorldWidth cleanly.
        /// </summary>
        public void ConfigureWidth(float targetWorldWidth)
        {
            float clampedWidth = Mathf.Max(0.8f, targetWorldWidth);

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (boxCol == null)
            {
                boxCol = GetComponent<BoxCollider2D>();
            }

            float spriteLocalWidth = spriteRenderer != null && spriteRenderer.sprite != null
                ? Mathf.Max(0.1f, spriteRenderer.sprite.bounds.size.x)
                : 1.0f;

            Vector3 scale = transform.localScale;
            scale.x = clampedWidth / spriteLocalWidth;
            if (Mathf.Approximately(scale.y, 0f))
            {
                scale.y = 1f;
            }
            transform.localScale = scale;

            if (boxCol != null)
            {
                Vector2 colSize = boxCol.size;
                colSize.x = spriteLocalWidth;
                if (colSize.y <= 0.05f)
                {
                    colSize.y = 0.35f / Mathf.Max(0.1f, Mathf.Abs(scale.y));
                }
                boxCol.size = colSize;
                boxCol.offset = Vector2.zero;
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || !CampaignHazardSpawner.CanHazardOperate() || isBroken || boxCol == null || !boxCol.enabled)
            {
                return;
            }

            Collider2D activePlayerCol = ResolveStandingOrRestingPlayerCollider();
            if (activePlayerCol == null)
            {
                return;
            }

            if (WasDropThroughTriggered())
            {
                InitiateDropThrough(activePlayerCol);
            }
        }

        private void LateUpdate()
        {
            if (pendingDropRb != null && Time.frameCount == dropSuppressJumpFrame)
            {
                Vector2 vel = pendingDropRb.linearVelocity;
                pendingDropRb.linearVelocity = new Vector2(vel.x, Mathf.Min(vel.y, -2.5f));
                pendingDropRb = null;
            }
        }

        private Collider2D ResolveStandingOrRestingPlayerCollider()
        {
            if (standingPlayerCollider != null && standingPlayerCollider.enabled)
            {
                return standingPlayerCollider;
            }

            if (cachedPlayer == null || cachedPlayer.IsDead)
            {
                cachedPlayer = LevelManager.Instance != null && LevelManager.Instance.ActivePlayer != null
                    ? LevelManager.Instance.ActivePlayer
                    : FindAnyObjectByType<BaseCharacter>();
                cachedPlayerCol = cachedPlayer != null ? cachedPlayer.GetComponent<Collider2D>() : null;
            }

            if (cachedPlayer == null || cachedPlayer.IsDead || cachedPlayerCol == null || !cachedPlayerCol.enabled)
            {
                return null;
            }

            if (Physics2D.GetIgnoreCollision(cachedPlayerCol, boxCol))
            {
                return null;
            }

            Bounds playerBounds = cachedPlayerCol.bounds;
            Bounds platBounds = boxCol.bounds;
            bool overlapsX = playerBounds.max.x >= platBounds.min.x - 0.12f && playerBounds.min.x <= platBounds.max.x + 0.12f;
            bool restingAtopY = Mathf.Abs(playerBounds.min.y - platBounds.max.y) <= 0.22f;

            if (overlapsX && restingAtopY)
            {
                Rigidbody2D rb = cachedPlayer.GetComponent<Rigidbody2D>();
                if (rb == null || rb.linearVelocity.y <= 0.15f)
                {
                    return cachedPlayerCol;
                }
            }

            return null;
        }

        private void InitiateDropThrough(Collider2D playerCol)
        {
            if (playerCol == null || boxCol == null)
            {
                return;
            }

            Physics2D.IgnoreCollision(playerCol, boxCol, true);

            Rigidbody2D playerRb = playerCol.GetComponent<Rigidbody2D>() ?? playerCol.GetComponentInParent<Rigidbody2D>();
            if (playerRb != null)
            {
                Vector2 vel = playerRb.linearVelocity;
                playerRb.linearVelocity = new Vector2(vel.x, Mathf.Min(vel.y, -2.5f));
                pendingDropRb = playerRb;
                dropSuppressJumpFrame = Time.frameCount;
            }

            standingPlayerCollider = null;
            StartCoroutine(DropThroughRoutine(playerCol));
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            HandlePlayerLanding(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            HandlePlayerLanding(collision);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (collision != null && collision.collider == standingPlayerCollider)
            {
                standingPlayerCollider = null;
            }
        }

        private void HandlePlayerLanding(Collision2D collision)
        {
            if (!CampaignHazardSpawner.CanHazardOperate() || isBroken || collision == null || collision.collider == null)
            {
                return;
            }

            BaseCharacter bc = collision.collider.GetComponent<BaseCharacter>() ?? collision.collider.GetComponentInParent<BaseCharacter>();
            if (bc == null || bc.IsDead)
            {
                return;
            }

            bool landedFromAbove = bc.transform.position.y >= boxCol.bounds.max.y - 0.35f;
            if (!landedFromAbove && collision.contactCount > 0)
            {
                for (int i = 0; i < collision.contactCount; i++)
                {
                    if (collision.GetContact(i).normal.y < -0.45f)
                    {
                        landedFromAbove = true;
                        break;
                    }
                }
            }

            if (!landedFromAbove)
            {
                return;
            }

            standingPlayerCollider = collision.collider;

            if (isFragile && !isBreaking && !isBroken && fragileRoutine == null)
            {
                fragileRoutine = StartCoroutine(FragileBreakAndRespawnRoutine());
            }
        }

        private IEnumerator FragileBreakAndRespawnRoutine()
        {
            isBreaking = true;
            baseRootPos = transform.position;
            float elapsed = 0f;

            while (elapsed < trembleDuration)
            {
                if (!CampaignHazardSpawner.CanHazardOperate())
                {
                    ResetVisualTransform();
                    isBreaking = false;
                    fragileRoutine = null;
                    yield break;
                }

                elapsed += Time.deltaTime;
                float urgency = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, trembleDuration));
                float freq = Mathf.Lerp(10f, 26f, urgency);
                float pulse = Mathf.PingPong(elapsed * freq, 1f);

                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.Lerp(originalColor, new Color(1f, 0.42f, 0.25f, 1f), pulse);
                }

                float shakeX = Mathf.Sin(elapsed * 55f) * Mathf.Lerp(0.03f, 0.065f, urgency);
                if (isVisualChild && spriteRenderer != null)
                {
                    spriteRenderer.transform.localPosition = baseVisualLocalPos + new Vector3(shakeX, 0f, 0f);
                }
                else
                {
                    transform.position = baseRootPos + new Vector3(shakeX, 0f, 0f);
                }

                yield return null;
            }

            ResetVisualTransform();
            isBreaking = false;
            isBroken = true;
            standingPlayerCollider = null;

            if (boxCol != null) boxCol.enabled = false;
            if (spriteRenderer != null)
            {
                Color faded = originalColor;
                faded.a = 0.14f;
                spriteRenderer.color = faded;
            }

            yield return new WaitForSeconds(respawnDelay);

            isBroken = false;
            if (CampaignHazardSpawner.CanHazardOperate())
            {
                if (boxCol != null) boxCol.enabled = true;
                if (spriteRenderer != null) spriteRenderer.color = originalColor;
                if (cachedPlayerCol != null && boxCol != null && cachedPlayerCol.enabled && boxCol.enabled &&
                    cachedPlayerCol.gameObject.activeInHierarchy && gameObject.activeInHierarchy)
                {
                    Physics2D.IgnoreCollision(cachedPlayerCol, boxCol, false);
                }
            }
            fragileRoutine = null;
        }

        private IEnumerator DropThroughRoutine(Collider2D playerCol)
        {
            if (playerCol == null || boxCol == null)
            {
                yield break;
            }

            yield return new WaitForSeconds(Mathf.Max(0.1f, dropThroughDuration));
            if (playerCol != null && boxCol != null && playerCol.enabled && boxCol.enabled &&
                playerCol.gameObject.activeInHierarchy && gameObject.activeInHierarchy)
            {
                Physics2D.IgnoreCollision(playerCol, boxCol, false);
            }
        }

        private void ResetVisualTransform()
        {
            if (isVisualChild && spriteRenderer != null)
            {
                spriteRenderer.transform.localPosition = baseVisualLocalPos;
            }
            else
            {
                transform.position = baseRootPos;
            }
        }

        private bool WasDropThroughTriggered()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return false;
            }

            bool downHeld = kb.sKey.isPressed || kb.downArrowKey.isPressed;
            bool spacePressed = kb.spaceKey.wasPressedThisFrame;
            if (downHeld && spacePressed)
            {
                return true;
            }

            if (kb.downArrowKey.wasPressedThisFrame)
            {
                return true;
            }

            if (kb.sKey.wasPressedThisFrame)
            {
                if (Time.time - lastSTapTime <= 0.28f)
                {
                    lastSTapTime = -10f;
                    return true;
                }
                lastSTapTime = Time.time;
            }

            return false;
#else
            bool downHeld = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            bool spacePressed = Input.GetKeyDown(KeyCode.Space);
            if (downHeld && spacePressed)
            {
                return true;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                return true;
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                if (Time.time - lastSTapTime <= 0.28f)
                {
                    lastSTapTime = -10f;
                    return true;
                }
                lastSTapTime = Time.time;
            }

            return false;
#endif
        }
    }
}
