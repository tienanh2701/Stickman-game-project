using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Environment
{
    /// <summary>
    /// Periodically rolls across the arena at ground ankle height (y = -2.25f) after a 1.3s warning line.
    /// Damages unblocked players (12 DMG) and enemies (16 DMG); jumping over or S-Blocking avoids stun.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class RollingCartHazard : MonoBehaviour
    {
        [Header("Cart Timing & Damage")]
        [SerializeField] private float rollInterval = 7.5f;
        [SerializeField] private float initialDelay = 3.0f;
        [SerializeField] private float warningDuration = 1.3f;
        [SerializeField] private float rollSpeed = 11.5f;
        [SerializeField] private float playerDamage = 12f;
        [SerializeField] private float enemyDamage = 16f;

        [Header("Arena Track")]
        [SerializeField] private float arenaMinX = -7f;
        [SerializeField] private float arenaMaxX = 22f;
        [SerializeField] private float trackY = -2.25f;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Collider2D col;
        private LineRenderer warningLine;
        private GameObject warningBadge;
        private SpriteRenderer warningBadgeSr;
        private Vector3 initialScale = Vector3.one;
        private bool isRolling;
        private int currentDirection = 1;
        private readonly HashSet<IDamageable> hitTargetsThisPass = new HashSet<IDamageable>();

        private void Awake()
        {
            col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
                col.enabled = false;
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = CampaignHazardSpawner.GetFallbackSquareSprite();
                spriteRenderer.color = new Color(0.55f, 0.36f, 0.2f, 1f);
                spriteRenderer.sortingOrder = 5;
            }

            initialScale = transform.localScale;
            if (Mathf.Approximately(initialScale.x, 0f)) initialScale = Vector3.one;

            spriteRenderer.enabled = false;
            CreateWarningVisuals();
        }

        private void Start()
        {
            if (!CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }
            StartCoroutine(CartLoopRoutine());
        }

        private void OnDestroy()
        {
            if (warningLine != null) Destroy(warningLine.gameObject);
            if (warningBadge != null) Destroy(warningBadge);
        }

        /// <summary>
        /// Sets the horizontal arena boundaries for the cart track.
        /// </summary>
        public void ConfigureArenaBounds(float minX, float maxX)
        {
            arenaMinX = Mathf.Min(minX, maxX);
            arenaMaxX = Mathf.Max(minX, maxX);
            if (arenaMaxX - arenaMinX < 4f)
            {
                float mid = (arenaMinX + arenaMaxX) * 0.5f;
                arenaMinX = mid - 5f;
                arenaMaxX = mid + 5f;
            }
        }

        private void CreateWarningVisuals()
        {
            GameObject lineObj = new GameObject("RollingCart_WarningLine");
            lineObj.transform.SetParent(transform.parent, false);
            warningLine = lineObj.AddComponent<LineRenderer>();
            warningLine.material = CampaignHazardSpawner.GetSpritesDefaultMaterial();
            warningLine.positionCount = 2;
            warningLine.startWidth = 0.08f;
            warningLine.endWidth = 0.08f;
            warningLine.sortingOrder = 6;
            warningLine.enabled = false;

            warningBadge = new GameObject("RollingCart_WarningBadge");
            warningBadge.transform.SetParent(transform.parent, false);
            warningBadgeSr = warningBadge.AddComponent<SpriteRenderer>();
            warningBadgeSr.sprite = CampaignHazardSpawner.GetFallbackCircleSprite();
            warningBadgeSr.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            warningBadgeSr.sortingOrder = 7;
            warningBadge.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
            warningBadge.SetActive(false);
        }

        private IEnumerator CartLoopRoutine()
        {
            yield return new WaitForSeconds(initialDelay);

            while (true)
            {
                if (!CampaignHazardSpawner.CanHazardOperate())
                {
                    SetWarningVisible(false, 1);
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                float startX = currentDirection > 0 ? arenaMinX - 1.5f : arenaMaxX + 1.5f;
                float endX = currentDirection > 0 ? arenaMaxX + 1.5f : arenaMinX - 1.5f;

                yield return ShowWarningRoutine(currentDirection);

                if (!CampaignHazardSpawner.CanHazardOperate())
                {
                    continue;
                }

                yield return PerformRollRoutine(startX, endX, currentDirection);

                currentDirection *= -1;
                yield return new WaitForSeconds(rollInterval);
            }
        }

        private IEnumerator ShowWarningRoutine(int dir)
        {
            SetWarningVisible(true, dir);
            float elapsed = 0f;

            while (elapsed < warningDuration)
            {
                if (!CampaignHazardSpawner.CanHazardOperate())
                {
                    SetWarningVisible(false, dir);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float pulse = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(elapsed * 12f));
                Color lineCol = new Color(1f, 0.2f, 0.2f, pulse);
                if (warningLine != null)
                {
                    warningLine.startColor = lineCol;
                    warningLine.endColor = lineCol;
                }
                if (warningBadgeSr != null)
                {
                    warningBadgeSr.color = new Color(1f, 0.15f, 0.15f, pulse);
                    float s = Mathf.Lerp(0.65f, 1.05f, pulse);
                    warningBadge.transform.localScale = new Vector3(s, s, 1f);
                }

                yield return null;
            }

            SetWarningVisible(false, dir);
        }

        private void SetWarningVisible(bool visible, int dir)
        {
            if (warningLine != null)
            {
                warningLine.enabled = visible;
                if (visible)
                {
                    warningLine.SetPosition(0, new Vector3(arenaMinX, trackY - 0.1f, 0f));
                    warningLine.SetPosition(1, new Vector3(arenaMaxX, trackY - 0.1f, 0f));
                }
            }

            if (warningBadge != null)
            {
                warningBadge.SetActive(visible);
                if (visible)
                {
                    float edgeX = dir > 0 ? arenaMinX + 0.8f : arenaMaxX - 0.8f;
                    warningBadge.transform.position = new Vector3(edgeX, trackY + 0.65f, 0f);
                }
            }
        }

        private IEnumerator PerformRollRoutine(float startX, float endX, int dir)
        {
            isRolling = true;
            hitTargetsThisPass.Clear();

            transform.position = new Vector3(startX, trackY, 0f);
            Vector3 s = initialScale;
            s.x = Mathf.Abs(initialScale.x) * dir;
            transform.localScale = s;

            if (spriteRenderer != null) spriteRenderer.enabled = true;
            if (col != null) col.enabled = true;

            while ((dir > 0 && transform.position.x < endX) || (dir < 0 && transform.position.x > endX))
            {
                if (!CampaignHazardSpawner.CanHazardOperate())
                {
                    break;
                }

                transform.position += new Vector3(dir * rollSpeed * Time.deltaTime, 0f, 0f);
                yield return null;
            }

            if (col != null) col.enabled = false;
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            isRolling = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDamageTarget(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDamageTarget(other);
        }

        private void TryDamageTarget(Collider2D other)
        {
            if (!isRolling || other == null || !CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }

            if (CampaignHazardSpawner.TryResolvePlayer(other, out BaseCharacter bc) && !hitTargetsThisPass.Contains(bc))
            {
                hitTargetsThisPass.Add(bc);
                CampaignHazardSpawner.ApplyPlayerHazardDamage(
                    bc,
                    playerDamage,
                    new Vector2(currentDirection * 6.5f, 3.0f),
                    AttackType.EnemyHeavyAttack,
                    gameObject
                );
                return;
            }

            if (CampaignHazardSpawner.TryResolveEnemyDamageable(other, out IDamageable enemyTarget) && !hitTargetsThisPass.Contains(enemyTarget))
            {
                hitTargetsThisPass.Add(enemyTarget);
                enemyTarget.TakeDamage(new DamageData(
                    enemyDamage,
                    new Vector2(currentDirection * 6.5f, 2.5f),
                    AttackType.Combo_DashJ,
                    true,
                    gameObject
                ));
            }
        }
    }
}
