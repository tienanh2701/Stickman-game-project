using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Enemies;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Environment
{
    /// <summary>
    /// World 2 Stage 4 (Broly Boss Arena) destructible stone pillar.
    /// Shatters when Broly crashes into it at high LariatCharge velocity, triggering BrolyBossController.CrashIntoStonePillar().
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class AncientStonePillar : MonoBehaviour
    {
        private static readonly int AnimStateHash = Animator.StringToHash("State");

        [Header("Crash Detection")]
        [SerializeField] private float minImpactVelocityX = 5.5f;
        [SerializeField] private float spawnGraceTime = 0.35f;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Collider2D col;
        private bool hasShattered;
        private float safeTime;

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
                spriteRenderer.sprite = CampaignHazardSpawner.GetFallbackSquareSprite();
                spriteRenderer.color = new Color(0.62f, 0.56f, 0.48f, 1f);
                spriteRenderer.sortingOrder = 4;
            }

            safeTime = Time.time + spawnGraceTime;
        }

        private void Start()
        {
            if (!CampaignHazardSpawner.CanHazardOperate())
            {
                if (col != null) col.enabled = false;
                if (spriteRenderer != null) spriteRenderer.enabled = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryHandleBrolyImpact(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryHandleBrolyImpact(other);
        }

        private void TryHandleBrolyImpact(Collider2D other)
        {
            if (hasShattered || other == null || Time.time < safeTime || !CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }

            BrolyBossController broly = other.GetComponent<BrolyBossController>() ?? other.GetComponentInParent<BrolyBossController>();
            if (broly == null || broly.IsDead || broly.CurrentHealth <= 0f)
            {
                return;
            }

            Rigidbody2D brolyRb = broly.GetComponent<Rigidbody2D>();
            Animator brolyAnim = broly.GetComponentInChildren<Animator>();

            bool movingFast = brolyRb != null && Mathf.Abs(brolyRb.linearVelocity.x) > minImpactVelocityX;
            bool inLariatAnim = brolyAnim != null && brolyAnim.runtimeAnimatorController != null && brolyAnim.GetInteger(AnimStateHash) == 2;

            if (!movingFast && !inLariatAnim)
            {
                return;
            }

            hasShattered = true;
            if (col != null) col.enabled = false;

            broly.CrashIntoStonePillar();
            CameraFollowBeatEmUp.Instance?.Shake(0.45f, 0.45f);

            StartCoroutine(ShatterVfxAndDestroyRoutine());
        }

        private IEnumerator ShatterVfxAndDestroyRoutine()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }

            // Spawn flying stone rubble fragments & dust burst
            const int chunkCount = 6;
            GameObject[] chunks = new GameObject[chunkCount];
            Vector2[] velocities = new Vector2[chunkCount];

            for (int i = 0; i < chunkCount; i++)
            {
                GameObject chunk = new GameObject($"PillarRubble_{i}");
                chunk.transform.SetParent(transform, false);
                chunk.transform.position = transform.position + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.4f, 0.8f), 0f);
                chunk.transform.localScale = new Vector3(0.28f, 0.28f, 1f);

                SpriteRenderer sr = chunk.AddComponent<SpriteRenderer>();
                sr.sprite = CampaignHazardSpawner.GetFallbackSquareSprite();
                sr.color = new Color(0.65f, 0.58f, 0.50f, 0.95f);
                sr.sortingOrder = 7;

                chunks[i] = chunk;
                velocities[i] = new Vector2(Random.Range(-5.5f, 5.5f), Random.Range(3.5f, 7.5f));
            }

            StartCoroutine(CampaignHazardSpawner.AnimateExpandingRingRoutine(
                transform.position,
                new Color(0.88f, 0.80f, 0.68f, 0.85f),
                0.7f,
                3.6f,
                0.35f,
                transform
            ));

            const float duration = 0.45f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < chunkCount; i++)
                {
                    if (chunks[i] == null) continue;
                    velocities[i].y -= 18f * Time.deltaTime;
                    chunks[i].transform.position += (Vector3)(velocities[i] * Time.deltaTime);
                    chunks[i].transform.Rotate(0f, 0f, 360f * Time.deltaTime);
                    SpriteRenderer sr = chunks[i].GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        Color c = sr.color;
                        c.a = alpha;
                        sr.color = c;
                    }
                }
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
