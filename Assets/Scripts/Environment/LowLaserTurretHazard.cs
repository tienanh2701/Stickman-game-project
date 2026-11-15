using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Environment
{
    /// <summary>
    /// Neo-City edge turret firing a low ankle-height horizontal laser (y = -2.2f .. -1.8f).
    /// Alternates 4.5s idle -> 1.1s thin red targeting line -> 0.9s thick beam (14 DMG).
    /// Jumping clears it cleanly; S-Blocking reduces damage by 80% & prevents stun; EMP terminals suppress it for 5s.
    /// </summary>
    public class LowLaserTurretHazard : MonoBehaviour
    {
        [Header("Cycle Timings")]
        [SerializeField] private float idleDuration = 4.5f;
        [SerializeField] private float warningDuration = 1.1f;
        [SerializeField] private float fireDuration = 0.9f;
        [SerializeField] private float initialDelay = 0f;

        [Header("Laser Specs")]
        [SerializeField] private float beamDamage = 14f;
        [SerializeField] private float beamWorldY = -2.0f;
        [SerializeField] private float beamThickness = 0.36f;
        [SerializeField] private float arenaMinX = -7f;
        [SerializeField] private float arenaMaxX = 22f;
        [SerializeField] private int facingDirection = -1;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private LineRenderer laserLine;
        private Vector3 initialScale = Vector3.one;
        private float empSuppressedUntil;
        private bool isFiring;
        private readonly HashSet<BaseCharacter> hitPlayersThisBeam = new HashSet<BaseCharacter>();

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = CampaignHazardSpawner.GetFallbackSquareSprite();
                spriteRenderer.color = new Color(0.45f, 0.5f, 0.58f, 1f);
                spriteRenderer.sortingOrder = 4;
            }

            initialScale = transform.localScale;
            if (Mathf.Approximately(initialScale.x, 0f)) initialScale = Vector3.one;

            CreateLaserLine();
        }

        private void Start()
        {
            if (!CampaignHazardSpawner.CanHazardOperate())
            {
                if (spriteRenderer != null) spriteRenderer.enabled = false;
                if (laserLine != null) laserLine.enabled = false;
                return;
            }

            StartCoroutine(TurretCycleRoutine());
        }

        private void OnDestroy()
        {
            if (laserLine != null) Destroy(laserLine.gameObject);
        }

        /// <summary>
        /// Configures arena laser span, turret facing direction, and optional initial phase delay.
        /// </summary>
        public void ConfigureArenaBounds(float minX, float maxX, int facingDir = -1, float startDelay = 0f)
        {
            arenaMinX = Mathf.Min(minX, maxX);
            arenaMaxX = Mathf.Max(minX, maxX);
            facingDirection = facingDir >= 0 ? 1 : -1;
            initialDelay = Mathf.Max(0f, startDelay);

            Vector3 s = initialScale;
            s.x = Mathf.Abs(initialScale.x) * facingDirection;
            transform.localScale = s;
        }

        /// <summary>
        /// Temporarily disables the turret and aborts any active beam when triggered by an EMP Terminal.
        /// </summary>
        public void SuppressByEmp(float duration)
        {
            empSuppressedUntil = Mathf.Max(empSuppressedUntil, Time.time + duration);
            isFiring = false;
            if (laserLine != null)
            {
                laserLine.enabled = false;
            }
        }

        private void CreateLaserLine()
        {
            GameObject lineObj = new GameObject("LowLaser_BeamLine");
            lineObj.transform.SetParent(transform, false);
            laserLine = lineObj.AddComponent<LineRenderer>();
            laserLine.material = CampaignHazardSpawner.GetSpritesDefaultMaterial();
            laserLine.positionCount = 2;
            laserLine.sortingOrder = 7;
            laserLine.enabled = false;
        }

        private IEnumerator TurretCycleRoutine()
        {
            SetTurretIdleVisual();
            if (initialDelay > 0f)
            {
                yield return new WaitForSeconds(initialDelay);
            }

            while (true)
            {
                if (!CampaignHazardSpawner.CanHazardOperate() || Time.time < empSuppressedUntil)
                {
                    SetTurretSuppressedOrIdleVisual();
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                // 1. Idle (4.5s)
                float idleTimer = 0f;
                while (idleTimer < idleDuration)
                {
                    if (!CampaignHazardSpawner.CanHazardOperate() || Time.time < empSuppressedUntil)
                    {
                        break;
                    }
                    SetTurretIdleVisual();
                    idleTimer += Time.deltaTime;
                    yield return null;
                }

                if (!CampaignHazardSpawner.CanHazardOperate() || Time.time < empSuppressedUntil)
                {
                    continue;
                }

                // 2. Thin red targeting line (1.1s)
                yield return TelegraphLineRoutine();
                if (!CampaignHazardSpawner.CanHazardOperate() || Time.time < empSuppressedUntil)
                {
                    continue;
                }

                // 3. Thick active laser beam (0.9s)
                yield return FireBeamRoutine();
            }
        }

        private void SetTurretIdleVisual()
        {
            isFiring = false;
            if (laserLine != null) laserLine.enabled = false;
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
        }

        private void SetTurretSuppressedOrIdleVisual()
        {
            isFiring = false;
            if (laserLine != null) laserLine.enabled = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Time.time < empSuppressedUntil
                    ? new Color(0.35f, 0.72f, 0.92f, 0.8f)
                    : Color.white;
            }
        }

        private void UpdateLaserEndpoints()
        {
            if (laserLine == null) return;
            float startX = transform.position.x;
            float endX = facingDirection > 0 ? arenaMaxX + 0.5f : arenaMinX - 0.5f;
            laserLine.SetPosition(0, new Vector3(startX, beamWorldY, 0f));
            laserLine.SetPosition(1, new Vector3(endX, beamWorldY, 0f));
        }

        private IEnumerator TelegraphLineRoutine()
        {
            if (laserLine != null)
            {
                laserLine.enabled = true;
                laserLine.startWidth = 0.06f;
                laserLine.endWidth = 0.06f;
            }

            float elapsed = 0f;
            while (elapsed < warningDuration)
            {
                if (!CampaignHazardSpawner.CanHazardOperate() || Time.time < empSuppressedUntil)
                {
                    SetTurretSuppressedOrIdleVisual();
                    yield break;
                }

                elapsed += Time.deltaTime;
                UpdateLaserEndpoints();

                float pulse = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(elapsed * 16f));
                Color warnCol = new Color(1f, 0.15f, 0.15f, pulse);
                if (laserLine != null)
                {
                    laserLine.startColor = warnCol;
                    laserLine.endColor = warnCol;
                }
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.4f, 0.4f, 1f), pulse);
                }

                yield return null;
            }
        }

        private IEnumerator FireBeamRoutine()
        {
            isFiring = true;
            hitPlayersThisBeam.Clear();

            if (laserLine != null)
            {
                laserLine.enabled = true;
                laserLine.startWidth = beamThickness;
                laserLine.endWidth = beamThickness;
                Color beamCol = new Color(1f, 0.12f, 0.24f, 0.95f);
                laserLine.startColor = beamCol;
                laserLine.endColor = beamCol;
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(1f, 0.5f, 0.55f, 1f);
            }

            float elapsed = 0f;
            while (elapsed < fireDuration && isFiring)
            {
                if (!CampaignHazardSpawner.CanHazardOperate() || Time.time < empSuppressedUntil)
                {
                    break;
                }

                UpdateLaserEndpoints();
                ScanBeamHits();

                elapsed += Time.deltaTime;
                if (laserLine != null)
                {
                    float w = beamThickness * (0.88f + 0.15f * Mathf.Sin(elapsed * 36f));
                    laserLine.startWidth = w;
                    laserLine.endWidth = w;
                }
                yield return null;
            }

            SetTurretSuppressedOrIdleVisual();
        }

        private void ScanBeamHits()
        {
            float startX = transform.position.x;
            float endX = facingDirection > 0 ? arenaMaxX + 0.5f : arenaMinX - 0.5f;
            float minX = Mathf.Min(startX, endX);
            float maxX = Mathf.Max(startX, endX);
            Vector2 center = new Vector2((minX + maxX) * 0.5f, beamWorldY);
            Vector2 size = new Vector2(Mathf.Max(1f, maxX - minX), beamThickness);

            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
            for (int i = 0; i < hits.Length; i++)
            {
                if (CampaignHazardSpawner.TryResolvePlayer(hits[i], out BaseCharacter bc) && !hitPlayersThisBeam.Contains(bc))
                {
                    hitPlayersThisBeam.Add(bc);
                    CampaignHazardSpawner.ApplyPlayerHazardDamage(
                        bc,
                        beamDamage,
                        new Vector2(facingDirection * 5.5f, 2.5f),
                        AttackType.EnemyAttack,
                        gameObject
                    );
                }
            }
        }
    }
}
