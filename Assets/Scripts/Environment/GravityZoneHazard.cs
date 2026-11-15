using UnityEngine;
using StickmanBeatEmUp.Characters;

namespace StickmanBeatEmUp.Environment
{
    /// <summary>
    /// Environmental modifier field: either a Dragon Ball High-Gravity Zone (slows speed + boosts gravity)
    /// or an Attack on Titan Steam Cloud (isSlowSmokeOnly = true, speed slow only).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class GravityZoneHazard : MonoBehaviour
    {
        [Header("Modifier Mode")]
        [SerializeField] private bool isSlowSmokeOnly = false;
        [SerializeField] private float speedMultiplier = 0.72f;
        [SerializeField] private float gravityMultiplier = 1.6f;
        [SerializeField] private float refreshInterval = 0.25f;
        [SerializeField] private float modifierDuration = 0.45f;

        [Header("Horizontal Drift")]
        [SerializeField] private float driftSpeed = 1.4f;
        [SerializeField] private float minDriftX = -5f;
        [SerializeField] private float maxDriftX = 18f;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Collider2D col;
        private Color baseColor = Color.white;
        private Vector3 basePosition;
        private int driftDir = 1;
        private float nextApplyTime;

        private void Awake()
        {
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
                spriteRenderer.sprite = CampaignHazardSpawner.GetFallbackCircleSprite();
                spriteRenderer.sortingOrder = 5;
            }

            ApplyModeTint();
            basePosition = transform.position;
        }

        private void Start()
        {
            if (!CampaignHazardSpawner.CanHazardOperate())
            {
                if (col != null) col.enabled = false;
                if (spriteRenderer != null) spriteRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Configures the movement speed and gravity multipliers applied to the Player inside this field.
        /// </summary>
        public void Configure(float speedMult, float gravityMult, float interval = 0.25f)
        {
            speedMultiplier = Mathf.Clamp(speedMult, 0.2f, 2.0f);
            gravityMultiplier = Mathf.Clamp(gravityMult, 0.2f, 3.0f);
            refreshInterval = Mathf.Max(0.05f, interval);
        }

        /// <summary>
        /// Configures horizontal patrol drift bounds and whether the zone represents steam slow smoke only.
        /// </summary>
        public void ConfigureArenaDrift(float minX, float maxX, float speed, bool slowSmokeOnly = false)
        {
            minDriftX = Mathf.Min(minX, maxX);
            maxDriftX = Mathf.Max(minX, maxX);
            driftSpeed = Mathf.Max(0f, speed);
            isSlowSmokeOnly = slowSmokeOnly;
            if (isSlowSmokeOnly)
            {
                gravityMultiplier = 1.0f;
            }
            ApplyModeTint();
        }

        private void ApplyModeTint()
        {
            if (spriteRenderer == null) return;

            baseColor = isSlowSmokeOnly
                ? new Color(0.86f, 0.90f, 0.94f, 0.56f)
                : new Color(0.76f, 0.44f, 1f, 0.65f);
            spriteRenderer.color = baseColor;
        }

        private void Update()
        {
            if (!CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }

            Vector3 pos = transform.position;
            if (driftSpeed > 0f && maxDriftX > minDriftX)
            {
                pos.x += driftDir * driftSpeed * Time.deltaTime;
                if (pos.x >= maxDriftX)
                {
                    pos.x = maxDriftX;
                    driftDir = -1;
                }
                else if (pos.x <= minDriftX)
                {
                    pos.x = minDriftX;
                    driftDir = 1;
                }
            }

            pos.y = basePosition.y + Mathf.Sin(Time.time * 2.2f) * 0.14f;
            transform.position = pos;

            if (spriteRenderer != null)
            {
                float alphaPulse = baseColor.a * (0.85f + 0.15f * Mathf.Sin(Time.time * 4f));
                Color c = baseColor;
                c.a = alphaPulse;
                spriteRenderer.color = c;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            ApplyModifierToPlayer(other, forceImmediate: true);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            ApplyModifierToPlayer(other, forceImmediate: false);
        }

        private void ApplyModifierToPlayer(Collider2D other, bool forceImmediate)
        {
            if (!CampaignHazardSpawner.CanHazardOperate() || other == null)
            {
                return;
            }

            if (!forceImmediate && Time.time < nextApplyTime)
            {
                return;
            }

            if (CampaignHazardSpawner.TryResolvePlayer(other, out BaseCharacter bc))
            {
                nextApplyTime = Time.time + refreshInterval;
                float effectiveGravity = isSlowSmokeOnly ? 1.0f : gravityMultiplier;
                bc.ApplyEnvironmentalModifier(speedMultiplier, effectiveGravity, modifierDuration);
            }
        }
    }
}
