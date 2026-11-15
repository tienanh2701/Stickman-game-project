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
    /// Periodically spawns ground target warnings (y = -2.45f, 1.1s) near the player and drops
    /// falling rubble boulders from y = +5.5f to -2.5f, dealing 15 AOE damage (1.8u radius) to Player and Enemies.
    /// </summary>
    public class FallingBoulderSpawner : MonoBehaviour
    {
        [Header("Sprites")]
        [SerializeField] private Sprite boulderSprite;
        [SerializeField] private Sprite warningSprite;

        [Header("Spawner Config")]
        [SerializeField] private float spawnInterval = 9.0f;
        [SerializeField] private float initialDelay = 2.2f;
        [SerializeField] private float warningDuration = 1.1f;
        [SerializeField] private float impactRadius = 1.8f;
        [SerializeField] private float damage = 15f;
        [SerializeField] private float dropStartY = 5.5f;
        [SerializeField] private float groundImpactY = EnemyBase.GROUND_Y;

        [Header("Arena Bounds")]
        [SerializeField] private float arenaMinX = -7f;
        [SerializeField] private float arenaMaxX = 22f;

        private GameObject activeWarningObj;
        private GameObject activeBoulderObj;

        private void Start()
        {
            if (!CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }
            StartCoroutine(BoulderCycleRoutine());
        }

        private void OnDestroy()
        {
            if (activeWarningObj != null) Destroy(activeWarningObj);
            if (activeBoulderObj != null) Destroy(activeBoulderObj);
        }

        /// <summary>
        /// Sets the horizontal arena clamping boundaries for boulder drop selection.
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

        /// <summary>
        /// Sets the delay in seconds between consecutive boulder drops.
        /// </summary>
        public void ConfigureSpawnInterval(float interval)
        {
            spawnInterval = Mathf.Max(1.0f, interval);
        }

        private IEnumerator BoulderCycleRoutine()
        {
            yield return new WaitForSeconds(initialDelay);

            while (true)
            {
                if (!CampaignHazardSpawner.CanHazardOperate())
                {
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                float targetX = PickTargetX();
                yield return DropSingleBoulderRoutine(targetX);

                yield return new WaitForSeconds(spawnInterval);
            }
        }

        private float PickTargetX()
        {
            float clampedMin = arenaMinX + 1f;
            float clampedMax = arenaMaxX - 1f;
            if (clampedMax <= clampedMin)
            {
                clampedMin = arenaMinX;
                clampedMax = arenaMaxX;
            }

            BaseCharacter player = LevelManager.Instance != null && LevelManager.Instance.ActivePlayer != null
                ? LevelManager.Instance.ActivePlayer
                : FindAnyObjectByType<BaseCharacter>();

            if (player != null && !player.IsDead)
            {
                float nearX = player.transform.position.x + Random.Range(-2.4f, 2.4f);
                return Mathf.Clamp(nearX, clampedMin, clampedMax);
            }

            return Random.Range(clampedMin, clampedMax);
        }

        private IEnumerator DropSingleBoulderRoutine(float targetX)
        {
            // 1. Show ground warning marker at y = -2.45f for 1.1s
            activeWarningObj = new GameObject("BoulderWarningMarker");
            activeWarningObj.transform.SetParent(transform, false);
            activeWarningObj.transform.position = new Vector3(targetX, -2.45f, 0f);

            SpriteRenderer warnSr = activeWarningObj.AddComponent<SpriteRenderer>();
            warnSr.sprite = warningSprite != null ? warningSprite : CampaignHazardSpawner.GetFallbackCircleSprite();
            warnSr.color = new Color(1f, 0.25f, 0.15f, 0.75f);
            warnSr.sortingOrder = 3;

            float elapsed = 0f;
            while (elapsed < warningDuration)
            {
                if (!CampaignHazardSpawner.CanHazardOperate())
                {
                    if (activeWarningObj != null) Destroy(activeWarningObj);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 16f);
                float sx = Mathf.Lerp(1.3f, 2.0f, pulse);
                activeWarningObj.transform.localScale = new Vector3(sx, 0.45f, 1f);
                warnSr.color = new Color(1f, Mathf.Lerp(0.15f, 0.45f, pulse), 0.1f, Mathf.Lerp(0.45f, 0.9f, pulse));
                yield return null;
            }

            if (activeWarningObj != null)
            {
                Destroy(activeWarningObj);
                activeWarningObj = null;
            }

            // 2. Spawn and drop boulder from y = +5.5f down to y = -2.5f
            activeBoulderObj = new GameObject("FallingBoulder");
            activeBoulderObj.transform.SetParent(transform, false);
            activeBoulderObj.transform.position = new Vector3(targetX, dropStartY, 0f);
            activeBoulderObj.transform.localScale = new Vector3(1.15f, 1.15f, 1f);

            SpriteRenderer boulderSr = activeBoulderObj.AddComponent<SpriteRenderer>();
            boulderSr.sprite = boulderSprite != null ? boulderSprite : CampaignHazardSpawner.GetFallbackCircleSprite();
            boulderSr.color = boulderSprite != null ? Color.white : new Color(0.55f, 0.44f, 0.34f, 1f);
            boulderSr.sortingOrder = 7;

            float fallSpeed = 10f;
            const float fallGravity = 26f;
            while (activeBoulderObj != null && activeBoulderObj.transform.position.y > groundImpactY + 0.25f)
            {
                if (!CampaignHazardSpawner.CanHazardOperate())
                {
                    Destroy(activeBoulderObj);
                    activeBoulderObj = null;
                    yield break;
                }

                fallSpeed += fallGravity * Time.deltaTime;
                Vector3 pos = activeBoulderObj.transform.position;
                pos.y -= fallSpeed * Time.deltaTime;
                activeBoulderObj.transform.position = pos;
                activeBoulderObj.transform.Rotate(0f, 0f, 260f * Time.deltaTime);
                yield return null;
            }

            // 3. Impact AOE (1.8u radius, 15 DMG to Player and Enemies)
            Vector3 impactPos = new Vector3(targetX, groundImpactY, 0f);
            DealImpactDamage(impactPos);
            CameraFollowBeatEmUp.Instance?.Shake(0.18f, 0.2f);

            if (activeBoulderObj != null)
            {
                Destroy(activeBoulderObj);
                activeBoulderObj = null;
            }

            yield return CampaignHazardSpawner.AnimateExpandingRingRoutine(
                impactPos + Vector3.up * 0.2f,
                new Color(0.85f, 0.65f, 0.4f, 0.8f),
                0.5f,
                impactRadius * 1.8f,
                0.24f,
                transform
            );
        }

        private void DealImpactDamage(Vector3 impactPos)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(impactPos, impactRadius);
            HashSet<IDamageable> hitEnemies = new HashSet<IDamageable>();
            HashSet<BaseCharacter> hitPlayers = new HashSet<BaseCharacter>();

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;

                if (CampaignHazardSpawner.TryResolvePlayer(hit, out BaseCharacter bc) && !hitPlayers.Contains(bc))
                {
                    hitPlayers.Add(bc);
                    float dirX = Mathf.Sign(bc.transform.position.x - impactPos.x);
                    if (Mathf.Approximately(dirX, 0f)) dirX = 1f;
                    CampaignHazardSpawner.ApplyPlayerHazardDamage(
                        bc,
                        damage,
                        new Vector2(dirX * 5f, 2.5f),
                        AttackType.EnemyHeavyAttack,
                        gameObject
                    );
                }
                else if (CampaignHazardSpawner.TryResolveEnemyDamageable(hit, out IDamageable enemyTarget) && !hitEnemies.Contains(enemyTarget))
                {
                    hitEnemies.Add(enemyTarget);
                    float dirX = Mathf.Sign(enemyTarget.Transform.position.x - impactPos.x);
                    if (Mathf.Approximately(dirX, 0f)) dirX = 1f;
                    enemyTarget.TakeDamage(new DamageData(
                        damage,
                        new Vector2(dirX * 5f, 2.5f),
                        AttackType.Combo_DashJ,
                        true,
                        gameObject
                    ));
                }
            }
        }
    }
}
