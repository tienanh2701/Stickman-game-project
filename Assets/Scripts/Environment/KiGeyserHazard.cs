using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Environment
{
    /// <summary>
    /// Dragon Ball Wasteland energy geyser cycling: 4.5s idle -> 1.2s cyan floor charge -> 1.0s vertical beam burst (y = -2.5f..+1.5f).
    /// Deals 16 DMG + upward launch knockback to enemies and unblocked players inside its column.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class KiGeyserHazard : MonoBehaviour
    {
        [Header("Cycle Timings")]
        [SerializeField] private float idleDuration = 4.5f;
        [SerializeField] private float chargeDuration = 1.2f;
        [SerializeField] private float burstDuration = 1.0f;
        [SerializeField] private float initialPhaseDelay = 0f;

        [Header("Burst Damage")]
        [SerializeField] private float damage = 16f;
        [SerializeField] private float upwardLaunchForce = 10.5f;
        [SerializeField] private float columnWidth = 1.4f;
        [SerializeField] private float columnHeight = 4.0f;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private BoxCollider2D boxCol;
        private GameObject beamVisual;
        private SpriteRenderer beamSr;
        private bool isBursting;
        private readonly HashSet<IDamageable> hitTargetsThisBurst = new HashSet<IDamageable>();

        private void Awake()
        {
            boxCol = GetComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector2(columnWidth, columnHeight);
            boxCol.offset = new Vector2(0f, columnHeight * 0.5f);

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

            CreateBeamVisual();
        }

        private void Start()
        {
            if (!CampaignHazardSpawner.CanHazardOperate())
            {
                if (boxCol != null) boxCol.enabled = false;
                if (spriteRenderer != null) spriteRenderer.enabled = false;
                return;
            }

            StartCoroutine(GeyserCycleRoutine());
        }

        private void OnDestroy()
        {
            if (beamVisual != null) Destroy(beamVisual);
        }

        /// <summary>
        /// Sets an initial phase offset so multiple geysers erupt asynchronously.
        /// </summary>
        public void ConfigureCycleOffset(float delay)
        {
            initialPhaseDelay = Mathf.Max(0f, delay);
        }

        private void CreateBeamVisual()
        {
            beamVisual = new GameObject("KiGeyser_BeamVisual");
            beamVisual.transform.SetParent(transform, false);
            beamVisual.transform.localPosition = new Vector3(0f, columnHeight * 0.5f, 0f);
            beamVisual.transform.localScale = new Vector3(columnWidth * 0.85f, columnHeight, 1f);

            beamSr = beamVisual.AddComponent<SpriteRenderer>();
            beamSr.sprite = CampaignHazardSpawner.GetFallbackSquareSprite();
            beamSr.color = new Color(0.25f, 0.92f, 1f, 0.82f);
            beamSr.sortingOrder = 6;
            beamVisual.SetActive(false);
        }

        private IEnumerator GeyserCycleRoutine()
        {
            SetIdleVisual();
            if (initialPhaseDelay > 0f)
            {
                yield return new WaitForSeconds(initialPhaseDelay);
            }

            while (true)
            {
                if (!CampaignHazardSpawner.CanHazardOperate())
                {
                    SetIdleVisual();
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                // 1. Idle phase (4.5s)
                SetIdleVisual();
                yield return new WaitForSeconds(idleDuration);
                if (!CampaignHazardSpawner.CanHazardOperate()) continue;

                // 2. Floor charge warning (1.2s)
                yield return ChargeWarningRoutine();
                if (!CampaignHazardSpawner.CanHazardOperate()) continue;

                // 3. Active vertical burst (1.0s)
                yield return ActiveBurstRoutine();
            }
        }

        private void SetIdleVisual()
        {
            isBursting = false;
            if (beamVisual != null) beamVisual.SetActive(false);
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.55f, 0.72f, 0.78f, 0.78f);
            }
        }

        private IEnumerator ChargeWarningRoutine()
        {
            if (beamVisual != null)
            {
                beamVisual.SetActive(true);
                beamVisual.transform.localScale = new Vector3(0.22f, columnHeight, 1f);
            }

            float elapsed = 0f;
            while (elapsed < chargeDuration)
            {
                if (!CampaignHazardSpawner.CanHazardOperate())
                {
                    SetIdleVisual();
                    yield break;
                }

                elapsed += Time.deltaTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 20f);
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.Lerp(new Color(0.3f, 0.9f, 1f, 1f), Color.white, pulse);
                }
                if (beamSr != null)
                {
                    beamSr.color = new Color(0.35f, 0.96f, 1f, Mathf.Lerp(0.18f, 0.52f, pulse));
                }
                yield return null;
            }
        }

        private IEnumerator ActiveBurstRoutine()
        {
            isBursting = true;
            hitTargetsThisBurst.Clear();

            if (beamVisual != null)
            {
                beamVisual.SetActive(true);
                beamVisual.transform.localScale = new Vector3(columnWidth * 0.9f, columnHeight, 1f);
            }
            if (beamSr != null)
            {
                beamSr.color = new Color(0.45f, 0.98f, 1f, 0.88f);
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.6f, 1f, 1f, 1f);
            }

            CameraFollowBeatEmUp.Instance?.Shake(0.16f, 0.14f);

            float elapsed = 0f;
            while (elapsed < burstDuration)
            {
                if (!CampaignHazardSpawner.CanHazardOperate())
                {
                    break;
                }

                ScanColumnOverlaps();
                elapsed += Time.deltaTime;
                if (beamVisual != null)
                {
                    float shimmer = columnWidth * (0.82f + 0.18f * Mathf.Sin(elapsed * 32f));
                    beamVisual.transform.localScale = new Vector3(shimmer, columnHeight, 1f);
                }
                yield return null;
            }

            isBursting = false;
            SetIdleVisual();
        }

        private void ScanColumnOverlaps()
        {
            if (boxCol == null) return;

            Vector2 center = boxCol.transform.TransformPoint(boxCol.offset);
            Vector2 size = new Vector2(
                Mathf.Abs(boxCol.size.x * boxCol.transform.lossyScale.x),
                Mathf.Abs(boxCol.size.y * boxCol.transform.lossyScale.y)
            );
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
            for (int i = 0; i < hits.Length; i++)
            {
                TryDamageTarget(hits[i]);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isBursting)
            {
                TryDamageTarget(other);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (isBursting)
            {
                TryDamageTarget(other);
            }
        }

        private void TryDamageTarget(Collider2D other)
        {
            if (!isBursting || other == null || !CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }

            if (CampaignHazardSpawner.TryResolvePlayer(other, out BaseCharacter bc) && !hitTargetsThisBurst.Contains(bc))
            {
                hitTargetsThisBurst.Add(bc);
                CampaignHazardSpawner.ApplyPlayerHazardDamage(
                    bc,
                    damage,
                    new Vector2(0f, upwardLaunchForce),
                    AttackType.EnemyHeavyAttack,
                    gameObject
                );
                return;
            }

            if (CampaignHazardSpawner.TryResolveEnemyDamageable(other, out IDamageable enemyTarget) && !hitTargetsThisBurst.Contains(enemyTarget))
            {
                hitTargetsThisBurst.Add(enemyTarget);
                enemyTarget.TakeDamage(new DamageData(
                    damage,
                    new Vector2(0f, upwardLaunchForce * 0.85f),
                    AttackType.EnemyHeavyAttack,
                    true,
                    gameObject
                ));
            }
        }
    }
}
