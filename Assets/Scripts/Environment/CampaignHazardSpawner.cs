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
    /// Spawns and manages all Campaign-exclusive environmental hazards across 3 Worlds x 4 Stages (12 layouts).
    /// Generates 6-7 jump-validated multi-height one-way platforms per stage and ensures zero hazards exist
    /// or operate during Endless or Training modes.
    /// </summary>
    public class CampaignHazardSpawner : MonoBehaviour
    {
        private struct PlatformSlot
        {
            public OneWayPlatformHazard Platform;
            public float CenterX;
            public float CenterY;
            public float Width;
            public float TopY;
            public bool IsLow;
            public bool IsFragile;
        }

        public static CampaignHazardSpawner Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            fallbackSquareSprite = null;
            fallbackCircleSprite = null;
            sharedSpriteMaterial = null;
        }

        [Header("Campaign Hazard Prefabs (14 Slots)")]
        [SerializeField] private GameObject woodenEavesPrefab;
        [SerializeField] private GameObject gunpowderBarrelPrefab;
        [SerializeField] private GameObject rollingCartPrefab;
        [SerializeField] private GameObject fallingBoulderSpawnerPrefab;
        [SerializeField] private GameObject wallCannonPrefab;
        [SerializeField] private GameObject floatingRockPrefab;
        [SerializeField] private GameObject kiGeyserPrefab;
        [SerializeField] private GameObject gravityZonePrefab;
        [SerializeField] private GameObject titanSmokePrefab;
        [SerializeField] private GameObject stonePillarPrefab;
        [SerializeField] private GameObject cyberPlatformPrefab;
        [SerializeField] private GameObject auroraPortalPairPrefab;
        [SerializeField] private GameObject lowLaserTurretPrefab;
        [SerializeField] private GameObject empTerminalPrefab;

        [Header("Optional New Hazard Prefabs")]
        [SerializeField] private GameObject kiCrystalChainPrefab;
        [SerializeField] private GameObject rageTreasureChestPrefab;

        [Header("Runtime State")]
        [SerializeField] private Transform activeHazardsContainer;

        private GameMode currentGameMode = GameMode.Campaign;
        private bool isCampaignActive;

        private static Sprite fallbackSquareSprite;
        private static Sprite fallbackCircleSprite;
        private static Material sharedSpriteMaterial;

        public GameMode CurrentGameMode => currentGameMode;
        public bool IsCampaignActive => isCampaignActive && currentGameMode == GameMode.Campaign;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            EnsureContainer();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                ClearAllHazards();
                if (activeHazardsContainer != null)
                {
                    Destroy(activeHazardsContainer.gameObject);
                    activeHazardsContainer = null;
                }
                Instance = null;
            }
        }

        /// <summary>
        /// Sets the active game mode; immediately clears all hazards when leaving Campaign.
        /// </summary>
        public void SetGameMode(GameMode mode)
        {
            currentGameMode = mode;
            if (currentGameMode != GameMode.Campaign)
            {
                ClearAllHazards();
            }
        }

        /// <summary>
        /// Global check used by all hazard scripts so hazards never operate outside Campaign mode.
        /// </summary>
        public static bool CanHazardOperate()
        {
            if (LevelManager.Instance != null && LevelManager.Instance.IsSandboxMode)
            {
                return false;
            }

            if (Instance != null)
            {
                return Instance.IsCampaignActive;
            }

            return true;
        }

        /// <summary>
        /// Clears any existing hazards and spawns the stage-specific Campaign layout for (world, stageNumber).
        /// </summary>
        public void SpawnHazardsForStage(WorldType world, int stageNumber, float arenaMinX, float arenaMaxX)
        {
            ClearAllHazards();

            if (currentGameMode != GameMode.Campaign)
            {
                return;
            }

            if (LevelManager.Instance != null && LevelManager.Instance.IsSandboxMode)
            {
                return;
            }

            EnsureContainer();
            isCampaignActive = true;

            if (arenaMaxX - arenaMinX < 6f)
            {
                float center = (arenaMinX + arenaMaxX) * 0.5f;
                arenaMinX = center - 6f;
                arenaMaxX = center + 6f;
            }

            int stage = Mathf.Clamp(stageNumber, 1, 4);
            switch (world)
            {
                case WorldType.World1_RuinedCity:
                    SpawnWorld1Stage(stage, arenaMinX, arenaMaxX);
                    break;
                case WorldType.World2_Wasteland:
                    SpawnWorld2Stage(stage, arenaMinX, arenaMaxX);
                    break;
                case WorldType.World3_NeoCity:
                    SpawnWorld3Stage(stage, arenaMinX, arenaMaxX);
                    break;
            }
        }

        /// <summary>
        /// Destroys all currently spawned hazard instances and marks Campaign hazards inactive.
        /// </summary>
        public void ClearAllHazards()
        {
            StopAllCoroutines();
            isCampaignActive = false;

            if (activeHazardsContainer == null)
            {
                return;
            }

            for (int i = activeHazardsContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = activeHazardsContainer.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void EnsureContainer()
        {
            if (activeHazardsContainer != null)
            {
                return;
            }

            Transform existing = transform.Find("ActiveHazardsContainer");
            if (existing != null)
            {
                activeHazardsContainer = existing;
                return;
            }

            GameObject containerObj = new GameObject("ActiveHazardsContainer");
            containerObj.transform.SetParent(null, false);
            activeHazardsContainer = containerObj.transform;
        }

        #region Procedural Multi-Height Platform Generator
        private List<PlatformSlot> SpawnProceduralPlatforms(
            GameObject prefab,
            string baseName,
            float arenaMinX,
            float arenaMaxX,
            int stage,
            int fragileCount,
            Color fallbackColor)
        {
            bool isBossStage = stage == 4;
            int count = Random.Range(6, 8);

            const float minWidth = 1.6f;
            const float maxWidth = 2.6f;
            const float minGap = 1.8f;
            const float maxGap = 3.0f;

            float spanMinX = isBossStage
                ? Mathf.Max(arenaMinX + 0.9f, -5.8f)
                : Mathf.Max(arenaMinX + 0.55f, -8.5f);
            float spanMaxX = isBossStage
                ? Mathf.Min(arenaMaxX - 0.9f, 21.2f)
                : Mathf.Min(arenaMaxX - 0.55f, 22.0f);

            float minSpanNeeded7 = 7 * minWidth + 6 * minGap;
            if (spanMaxX - spanMinX < minSpanNeeded7)
            {
                float safeLeft = arenaMinX + 0.35f;
                float safeRight = arenaMaxX - 0.35f;
                if (safeRight - safeLeft >= minSpanNeeded7)
                {
                    float mid = (spanMinX + spanMaxX) * 0.5f;
                    spanMinX = Mathf.Clamp(mid - minSpanNeeded7 * 0.5f, safeLeft, safeRight - minSpanNeeded7);
                    spanMaxX = spanMinX + minSpanNeeded7;
                }
                else
                {
                    spanMinX = safeLeft;
                    spanMaxX = safeRight;
                }
            }

            float spanWidth = Mathf.Max(6f, spanMaxX - spanMinX);
            if (count == 7 && spanWidth < minSpanNeeded7)
            {
                count = 6;
            }

            float minSpanNeededCount = count * minWidth + (count - 1) * minGap;
            if (spanWidth < minSpanNeededCount)
            {
                count = Mathf.Max(3, Mathf.FloorToInt((spanWidth + minGap) / (minWidth + minGap)));
            }

            float[] widths = new float[count];
            float[] gaps = new float[Mathf.Max(1, count - 1)];

            float minTotal = count * minWidth + (count - 1) * minGap;
            float maxTotal = count * maxWidth + (count - 1) * maxGap;
            float targetTotal = Mathf.Clamp(
                spanWidth * Random.Range(0.88f, 0.96f),
                minTotal,
                Mathf.Min(spanWidth, maxTotal)
            );

            float currentTotal = 0f;
            for (int i = 0; i < count; i++)
            {
                widths[i] = Random.Range(minWidth, maxWidth);
                currentTotal += widths[i];
            }
            for (int i = 0; i < count - 1; i++)
            {
                gaps[i] = Random.Range(minGap, maxGap);
                currentTotal += gaps[i];
            }

            if (currentTotal < targetTotal && maxTotal > currentTotal + 0.001f)
            {
                float ratio = Mathf.Clamp01((targetTotal - currentTotal) / (maxTotal - currentTotal));
                for (int i = 0; i < count; i++)
                {
                    widths[i] += ratio * (maxWidth - widths[i]);
                }
                for (int i = 0; i < count - 1; i++)
                {
                    gaps[i] += ratio * (maxGap - gaps[i]);
                }
            }
            else if (currentTotal > targetTotal && currentTotal > minTotal + 0.001f)
            {
                float ratio = Mathf.Clamp01((currentTotal - targetTotal) / (currentTotal - minTotal));
                for (int i = 0; i < count; i++)
                {
                    widths[i] -= ratio * (widths[i] - minWidth);
                }
                for (int i = 0; i < count - 1; i++)
                {
                    gaps[i] -= ratio * (gaps[i] - minGap);
                }
            }

            float usedTotal = 0f;
            for (int i = 0; i < count; i++) usedTotal += widths[i];
            for (int i = 0; i < count - 1; i++) usedTotal += gaps[i];

            float slack = Mathf.Max(0f, spanWidth - usedTotal);
            float cursorX = spanMinX + slack * Random.Range(0.25f, 0.75f);

            float[] centerX = new float[count];
            for (int i = 0; i < count; i++)
            {
                centerX[i] = cursorX + widths[i] * 0.5f;
                cursorX += widths[i];
                if (i < count - 1)
                {
                    cursorX += gaps[i];
                }
            }

            bool[] isLow = new bool[count];
            isLow[0] = true;
            isLow[count - 1] = true;
            int midLowIndex = Random.Range(2, Mathf.Max(3, count - 2));
            isLow[midLowIndex] = true;

            float[] heights = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (isLow[i])
                {
                    heights[i] = Random.Range(-1.5f, -0.9f);
                }
                else
                {
                    // Ensure non-low platforms span both mid and high vertical tiers up to +1.1f
                    bool highBias = (i % 2 == 0) || Random.value > 0.45f;
                    heights[i] = highBias ? Random.Range(0.05f, 1.1f) : Random.Range(-0.8f, 0.35f);
                }
            }

            // Forward + backward clamping relaxation guarantees adjacent |dy| <= 1.4f (max player jump = 1.8u)
            for (int pass = 0; pass < 6; pass++)
            {
                for (int i = 1; i < count; i++)
                {
                    float minY = isLow[i] ? -1.5f : -0.8f;
                    float maxY = isLow[i] ? -0.9f : 1.1f;
                    heights[i] = Mathf.Clamp(heights[i], heights[i - 1] - 1.4f, heights[i - 1] + 1.4f);
                    heights[i] = Mathf.Clamp(heights[i], minY, maxY);
                }
                for (int i = count - 2; i >= 0; i--)
                {
                    float minY = isLow[i] ? -1.5f : -0.8f;
                    float maxY = isLow[i] ? -0.9f : 1.1f;
                    heights[i] = Mathf.Clamp(heights[i], heights[i + 1] - 1.4f, heights[i + 1] + 1.4f);
                    heights[i] = Mathf.Clamp(heights[i], minY, maxY);
                }
            }

            bool[] isFragile = new bool[count];
            if (fragileCount > 0)
            {
                int targetFragile = Mathf.Clamp(fragileCount, 0, Mathf.Max(0, count - 2));
                List<int> candidates = new List<int>();

                // Reserve one solid left-half and one solid right-half elevated platform when possible for Wall Cannons
                int reservedSolidLeft = -1;
                int reservedSolidRight = -1;
                float bestLeftScore = float.MinValue;
                float bestRightScore = float.MinValue;
                float arenaMidX = (arenaMinX + arenaMaxX) * 0.5f;

                for (int i = 0; i < count; i++)
                {
                    float score = heights[i] + (isLow[i] ? -1.5f : 0.5f);
                    if (centerX[i] <= arenaMidX)
                    {
                        if (score > bestLeftScore)
                        {
                            bestLeftScore = score;
                            reservedSolidLeft = i;
                        }
                    }
                    else
                    {
                        if (score > bestRightScore)
                        {
                            bestRightScore = score;
                            reservedSolidRight = i;
                        }
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    if (i != reservedSolidLeft && i != reservedSolidRight)
                    {
                        candidates.Add(i);
                    }
                }

                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    int tmp = candidates[i];
                    candidates[i] = candidates[j];
                    candidates[j] = tmp;
                }

                for (int i = 0; i < Mathf.Min(targetFragile, candidates.Count); i++)
                {
                    isFragile[candidates[i]] = true;
                }
            }

            List<PlatformSlot> result = new List<PlatformSlot>(count);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = new Vector3(centerX[i], heights[i], 0f);
                string suffix = isFragile[i] ? "Fragile" : "Solid";
                PlatformSlot slot = SpawnSizedPlatform(
                    prefab,
                    $"{baseName}_{i}_{suffix}",
                    pos,
                    widths[i],
                    isFragile[i],
                    isLow[i],
                    fallbackColor
                );
                result.Add(slot);
            }

            Physics2D.SyncTransforms();
            return result;
        }

        private PlatformSlot SpawnSizedPlatform(
            GameObject prefab,
            string fallbackName,
            Vector3 pos,
            float worldWidth,
            bool fragile,
            bool isLow,
            Color fallbackColor)
        {
            GameObject go = InstantiateOrFallback(prefab, fallbackName, pos, GetFallbackSquareSprite(), fallbackColor);
            if (prefab == null)
            {
                go.transform.localScale = new Vector3(worldWidth, 0.36f, 1f);
            }

            BoxCollider2D boxCol = EnsureComponent<BoxCollider2D>(go);
            EnsureComponent<PlatformEffector2D>(go);
            OneWayPlatformHazard platform = EnsureComponent<OneWayPlatformHazard>(go);
            platform.Configure(fragile, 3.5f, 5.0f);
            platform.ConfigureWidth(worldWidth);

            float worldColliderHeight = Mathf.Abs(boxCol.size.y * go.transform.localScale.y);
            float topY = pos.y + worldColliderHeight * 0.5f;

            return new PlatformSlot
            {
                Platform = platform,
                CenterX = pos.x,
                CenterY = pos.y,
                Width = worldWidth,
                TopY = topY,
                IsLow = isLow,
                IsFragile = fragile
            };
        }
        #endregion

        #region 12 Distinct Stage Layouts
        private void SpawnWorld1Stage(int stage, float minX, float maxX)
        {
            const float groundY = EnemyBase.GROUND_Y;
            float midX = (minX + maxX) * 0.5f;

            // 1. Procedural 6-7 AoT rooftops (2-3 fragile with 3.5s warning + 5s respawn)
            int fragileCount = Random.Range(2, 4);
            List<PlatformSlot> rooftops = SpawnProceduralPlatforms(
                woodenEavesPrefab,
                "WoodenEaves",
                minX,
                maxX,
                stage,
                fragileCount,
                new Color(0.55f, 0.38f, 0.24f, 1f)
            );

            // 2. 3 to 4 Gunpowder Barrels distributed across ground and low solid zones
            int barrelCount = stage <= 2 ? 3 : 4;
            float[] groundBarrelTs = { 0.22f, 0.50f, 0.78f };
            for (int i = 0; i < 3 && i < barrelCount; i++)
            {
                float jitterT = groundBarrelTs[i] + Random.Range(-0.03f, 0.03f);
                SpawnGroundedHazard<GunpowderBarrelHazard>(
                    gunpowderBarrelPrefab,
                    $"GunpowderBarrel_{i}",
                    PosAt(minX, maxX, jitterT, groundY),
                    0.5f
                );
            }

            if (barrelCount >= 4)
            {
                int lowSolidIdx = FindLowSolidPlatformIndex(rooftops);
                if (lowSolidIdx >= 0)
                {
                    PlatformSlot slot = rooftops[lowSolidIdx];
                    SpawnElevatedBarrel(slot.CenterX, slot.TopY);
                }
                else
                {
                    SpawnGroundedHazard<GunpowderBarrelHazard>(
                        gunpowderBarrelPrefab,
                        "GunpowderBarrel_3",
                        PosAt(minX, maxX, 0.64f, groundY),
                        0.5f
                    );
                }
            }

            // 3. 2 Rolling Ammo Carts patrolling left/right across ground (staggered timing)
            SpawnRollingCart(minX, maxX, 0f, "RollingCartHazard_A");
            SpawnRollingCart(minX, maxX, 4.2f, "RollingCartHazard_B");

            // 4. Falling Rubble Boulders at 9.0s interval
            SpawnBoulderSpawner(minX, maxX, 9.0f);

            // 5. Wall Cannon(s) atop high/mid solid AoT rooftops firing toward enemies / Boss Crystal
            int primaryCannonIdx = FindBestSolidRooftopForCannon(rooftops, minX, maxX, preferLeftHalf: true);
            if (primaryCannonIdx >= 0)
            {
                PlatformSlot cannonSlot = rooftops[primaryCannonIdx];
                int dir = cannonSlot.CenterX <= midX ? 1 : -1;
                SpawnWallCannonAt(new Vector3(cannonSlot.CenterX, cannonSlot.TopY + 0.50f, 0f), dir, minX, maxX, "WallCannon_RooftopA");
            }
            else
            {
                SpawnWallCannon(minX + 1.8f, 1, minX, maxX);
            }

            if (stage == 4 || stage == 3)
            {
                int secondCannonIdx = FindBestSolidRooftopForCannon(rooftops, minX, maxX, preferLeftHalf: false, excludeIndex: primaryCannonIdx);
                if (secondCannonIdx >= 0)
                {
                    PlatformSlot secondSlot = rooftops[secondCannonIdx];
                    int dir = secondSlot.CenterX <= midX ? 1 : -1;
                    SpawnWallCannonAt(new Vector3(secondSlot.CenterX, secondSlot.TopY + 0.50f, 0f), dir, minX, maxX, "WallCannon_RooftopB");
                }
            }

            // 6. Stage 3 signature Titan Steam Clouds
            if (stage == 3)
            {
                SpawnGravityOrSmoke(titanSmokePrefab, "TitanSmoke_Left", PosAt(minX, maxX, 0.34f, -1.65f), 0.55f, 1.0f, minX, maxX, 1.15f, true);
                SpawnGravityOrSmoke(titanSmokePrefab, "TitanSmoke_Right", PosAt(minX, maxX, 0.68f, -1.65f), 0.55f, 1.0f, minX, maxX, 1.15f, true);
            }
        }

        private void SpawnWorld2Stage(int stage, float minX, float maxX)
        {
            const float groundY = EnemyBase.GROUND_Y;
            float midX = (minX + maxX) * 0.5f;

            // 1. Procedural 6-7 Floating Rocks
            List<PlatformSlot> rocks = SpawnProceduralPlatforms(
                floatingRockPrefab,
                "FloatingRock",
                minX,
                maxX,
                stage,
                0,
                new Color(0.52f, 0.44f, 0.36f, 1f)
            );

            // 2. 4 Sequential Ki Geysers across the ground
            float[] geyserTs = { 0.19f, 0.40f, 0.60f, 0.81f };
            float[] geyserDelays = { 0.0f, 1.4f, 2.8f, 4.2f };
            for (int i = 0; i < 4; i++)
            {
                SpawnKiGeyser($"KiGeyser_{i}", PosAt(minX, maxX, geyserTs[i], groundY), geyserDelays[i]);
            }

            // 3. 2 Mobile Purple Gravity Zones patrolling horizontally back and forth across ground
            SpawnGravityOrSmoke(
                gravityZonePrefab,
                "GravityZone_Left",
                new Vector3(Mathf.Lerp(minX, midX, 0.35f), -1.4f, 0f),
                0.72f,
                1.6f,
                minX + 1.4f,
                midX + 1.2f,
                2.0f,
                false
            );
            SpawnGravityOrSmoke(
                gravityZonePrefab,
                "GravityZone_Right",
                new Vector3(Mathf.Lerp(midX, maxX, 0.80f), -1.4f, 0f),
                0.72f,
                1.6f,
                midX - 1.2f,
                maxX - 1.4f,
                2.3f,
                false
            );

            // 4. Chain-Exploding Ki Crystals atop 3-4 adjacent floating rocks (guaranteed <= 5.2f neighbor spacing)
            SpawnChainCrystalsOnPlatforms(rocks);

            // 5. 2 Ancient Stone Pillars at left and right edges of the arena
            SpawnGroundedHazard<AncientStonePillar>(stonePillarPrefab, "AncientStonePillar_Left", PosAt(minX, maxX, 0.11f, groundY), 1.0f);
            SpawnGroundedHazard<AncientStonePillar>(stonePillarPrefab, "AncientStonePillar_Right", PosAt(minX, maxX, 0.89f, groundY), 1.0f);

            // Keep Stage 2 signature wasteland boulder drops at 9.0s
            if (stage == 2)
            {
                SpawnBoulderSpawner(minX, maxX, 9.0f);
            }
        }

        private void SpawnWorld3Stage(int stage, float minX, float maxX)
        {
            const float groundY = EnemyBase.GROUND_Y;

            // 1. Procedural 6-7 Ancient/Cyber Slabs
            List<PlatformSlot> slabs = SpawnProceduralPlatforms(
                cyberPlatformPrefab,
                "CyberPlatform",
                minX,
                maxX,
                stage,
                0,
                new Color(0.28f, 0.42f, 0.58f, 1f)
            );

            // 2. 2 Pairs (4 total gates) of Aurora Portals cross-linking Ground <-> High Slabs
            int leftHighIdx = FindHighestPlatformIndexInRange(slabs, 0, slabs.Count / 2 - 1);
            int rightHighIdx = FindHighestPlatformIndexInRange(slabs, slabs.Count / 2, slabs.Count - 1);

            if (rightHighIdx >= 0)
            {
                PlatformSlot rightHigh = slabs[rightHighIdx];
                Vector3 groundLeftPortal = new Vector3(minX + 2.0f, groundY + 0.95f, 0f);
                Vector3 highRightPortal = new Vector3(rightHigh.CenterX, rightHigh.TopY + 1.0f, 0f);
                SpawnAuroraPortals(groundLeftPortal, highRightPortal, "AuroraPortalPair_LeftToRightHigh");
            }

            if (leftHighIdx >= 0)
            {
                PlatformSlot leftHigh = slabs[leftHighIdx];
                Vector3 groundRightPortal = new Vector3(maxX - 2.0f, groundY + 0.95f, 0f);
                Vector3 highLeftPortal = new Vector3(leftHigh.CenterX, leftHigh.TopY + 1.0f, 0f);
                SpawnAuroraPortals(groundRightPortal, highLeftPortal, "AuroraPortalPair_RightToLeftHigh");
            }

            // 3. 3 Staggered Toggling Low Laser Turrets firing sequentially across the arena
            SpawnLaserTurret("LowLaserTurret_Left", minX + 1.25f, minX, maxX, 1, 0.5f);
            SpawnLaserTurret("LowLaserTurret_Right", maxX - 1.25f, minX, maxX, -1, 2.7f);
            SpawnLaserTurret("LowLaserTurret_Mid", PosAt(minX, maxX, 0.50f, 0f).x, minX, maxX, -1, 4.9f);

            // 4. 2 Rage Treasure Chests placed atop elevated slabs (+12 Rage on touch/strike, 14s respawn)
            SpawnRageChestsOnHighSlabs(slabs, leftHighIdx, rightHighIdx);

            // 5. EMP Terminal (IsTrigger = true) to stun cyber mobs and paralyze Mecha Boss + suppress turrets
            SpawnGroundedHazard<EmpTerminalInteractable>(empTerminalPrefab, "EmpTerminal_Arena", PosAt(minX, maxX, 0.25f, groundY), 0.55f);

            // Keep Stage 3 tactical explosive barrel
            if (stage == 3)
            {
                SpawnGroundedHazard<GunpowderBarrelHazard>(gunpowderBarrelPrefab, "HazardBarrel_Mid", PosAt(minX, maxX, 0.72f, groundY), 0.5f);
            }
        }
        #endregion

        #region Layout Placement Helpers
        private static int FindLowSolidPlatformIndex(List<PlatformSlot> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsLow && !slots[i].IsFragile)
                {
                    return i;
                }
            }
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsFragile)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int FindBestSolidRooftopForCannon(
            List<PlatformSlot> slots,
            float minX,
            float maxX,
            bool preferLeftHalf,
            int excludeIndex = -1)
        {
            float midX = (minX + maxX) * 0.5f;
            int bestIdx = -1;
            float bestScore = float.MinValue;

            for (int i = 0; i < slots.Count; i++)
            {
                if (i == excludeIndex || slots[i].IsFragile)
                {
                    continue;
                }

                bool isOnPreferredHalf = preferLeftHalf ? (slots[i].CenterX <= midX) : (slots[i].CenterX > midX);
                float score = slots[i].CenterY + (isOnPreferredHalf ? 3.0f : 0f) + (!slots[i].IsLow ? 0.8f : 0f);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = i;
                }
            }

            return bestIdx;
        }

        private static int FindHighestPlatformIndexInRange(List<PlatformSlot> slots, int startIdx, int endIdx)
        {
            if (slots == null || slots.Count == 0)
            {
                return -1;
            }

            int clampedStart = Mathf.Clamp(startIdx, 0, slots.Count - 1);
            int clampedEnd = Mathf.Clamp(endIdx, clampedStart, slots.Count - 1);

            int bestIdx = clampedStart;
            float bestY = float.MinValue;
            for (int i = clampedStart; i <= clampedEnd; i++)
            {
                if (slots[i].CenterY > bestY)
                {
                    bestY = slots[i].CenterY;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        private void SpawnChainCrystalsOnPlatforms(List<PlatformSlot> rocks)
        {
            if (rocks == null || rocks.Count == 0)
            {
                return;
            }

            int crystalCount = Mathf.Min(rocks.Count, Random.Range(3, 5));
            int maxStart = Mathf.Max(0, rocks.Count - crystalCount);
            int bestStart = 0;
            float bestMaxAdjDist = float.MaxValue;

            for (int s = 0; s <= maxStart; s++)
            {
                float worstDist = 0f;
                for (int k = 0; k < crystalCount - 1; k++)
                {
                    float d = Vector2.Distance(
                        new Vector2(rocks[s + k].CenterX, rocks[s + k].TopY + 0.55f),
                        new Vector2(rocks[s + k + 1].CenterX, rocks[s + k + 1].TopY + 0.55f)
                    );
                    if (d > worstDist) worstDist = d;
                }
                if (worstDist < bestMaxAdjDist)
                {
                    bestMaxAdjDist = worstDist;
                    bestStart = s;
                }
            }

            float[] crystalXs = new float[crystalCount];
            float[] crystalYs = new float[crystalCount];
            for (int k = 0; k < crystalCount; k++)
            {
                PlatformSlot slot = rocks[bestStart + k];
                crystalXs[k] = slot.CenterX;
                crystalYs[k] = slot.TopY + 0.55f;
            }

            // Pull consecutive crystals gently inward within platform bounds if needed so distance <= 5.2f (< 5.5f chainRadius)
            for (int pass = 0; pass < 3; pass++)
            {
                for (int k = 1; k < crystalCount; k++)
                {
                    float dy = crystalYs[k] - crystalYs[k - 1];
                    float maxDx = Mathf.Sqrt(Mathf.Max(0.25f, 5.15f * 5.15f - dy * dy));
                    if (crystalXs[k] - crystalXs[k - 1] > maxDx)
                    {
                        PlatformSlot prevSlot = rocks[bestStart + k - 1];
                        PlatformSlot curSlot = rocks[bestStart + k];
                        float curMinX = curSlot.CenterX - curSlot.Width * 0.36f;
                        float prevMaxX = prevSlot.CenterX + prevSlot.Width * 0.36f;
                        crystalXs[k] = Mathf.Max(curMinX, crystalXs[k - 1] + maxDx);
                        if (crystalXs[k] - crystalXs[k - 1] > maxDx)
                        {
                            crystalXs[k - 1] = Mathf.Min(prevMaxX, crystalXs[k] - maxDx);
                        }
                    }
                }
            }

            for (int k = 0; k < crystalCount; k++)
            {
                SpawnKiCrystalChain($"KiCrystalChain_{k}", new Vector3(crystalXs[k], crystalYs[k], 0f));
            }
        }

        private void SpawnRageChestsOnHighSlabs(List<PlatformSlot> slabs, int portalLeftHighIdx, int portalRightHighIdx)
        {
            if (slabs == null || slabs.Count == 0)
            {
                return;
            }

            List<int> sortedIndices = new List<int>(slabs.Count);
            for (int i = 0; i < slabs.Count; i++)
            {
                sortedIndices.Add(i);
            }

            sortedIndices.Sort((a, b) =>
            {
                bool aHasPortal = a == portalLeftHighIdx || a == portalRightHighIdx;
                bool bHasPortal = b == portalLeftHighIdx || b == portalRightHighIdx;
                float aScore = slabs[a].CenterY + (slabs[a].IsLow ? -2.0f : 1.0f) + (aHasPortal ? -1.5f : 0.8f);
                float bScore = slabs[b].CenterY + (slabs[b].IsLow ? -2.0f : 1.0f) + (bHasPortal ? -1.5f : 0.8f);
                return bScore.CompareTo(aScore);
            });

            int placed = 0;
            for (int i = 0; i < sortedIndices.Count && placed < 2; i++)
            {
                int idx = sortedIndices[i];
                PlatformSlot slot = slabs[idx];
                bool hasPortal = idx == portalLeftHighIdx || idx == portalRightHighIdx;
                float offsetX = hasPortal ? (slot.Width >= 2.0f ? 0.65f : 0.5f) : 0f;
                Vector3 chestPos = new Vector3(slot.CenterX + offsetX, slot.TopY + 0.42f, 0f);
                SpawnRageTreasureChest($"RageTreasureChest_{placed}", chestPos);
                placed++;
            }
        }
        #endregion

        #region Spawn Helpers
        private static Vector3 PosAt(float minX, float maxX, float t, float y)
        {
            return new Vector3(Mathf.Lerp(minX, maxX, Mathf.Clamp01(t)), y, 0f);
        }

        private GameObject InstantiateOrFallback(GameObject prefab, string fallbackName, Vector3 pos, Sprite fallbackSprite, Color fallbackColor)
        {
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, pos, Quaternion.identity, activeHazardsContainer);
            }
            else
            {
                go = new GameObject(fallbackName);
                go.transform.SetParent(activeHazardsContainer, false);
                go.transform.position = pos;
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = fallbackSprite;
                sr.color = fallbackColor;
                sr.sortingOrder = 3;
            }
            return go;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null)
            {
                comp = go.AddComponent<T>();
            }
            return comp;
        }

        private void SpawnElevatedBarrel(float x, float platformTopY)
        {
            Vector3 pos = new Vector3(x, platformTopY + 0.52f, 0f);
            GameObject go = InstantiateOrFallback(gunpowderBarrelPrefab, "GunpowderBarrel_Platform", pos, GetFallbackSquareSprite(), new Color(0.62f, 0.24f, 0.14f, 1f));
            if (go.GetComponent<Collider2D>() == null)
            {
                BoxCollider2D box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(0.95f, 1.05f);
            }
            EnsureComponent<GunpowderBarrelHazard>(go);
        }

        private void SpawnGroundedHazard<T>(GameObject prefab, string fallbackName, Vector3 groundPos, float fallbackHalfHeight) where T : Component
        {
            GameObject go = InstantiateOrFallback(prefab, fallbackName, groundPos, GetFallbackSquareSprite(), Color.white);

            SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.gameObject == go && sr.sprite != null)
            {
                float normPivotY = sr.sprite.pivot.y / Mathf.Max(1f, sr.sprite.rect.height);
                if (normPivotY > 0.2f)
                {
                    float halfH = sr.bounds.extents.y > 0.05f ? sr.bounds.extents.y : fallbackHalfHeight;
                    go.transform.position = new Vector3(groundPos.x, EnemyBase.GROUND_Y + halfH, groundPos.z);
                }
            }

            Collider2D existingCol = go.GetComponent<Collider2D>();
            if (existingCol == null)
            {
                BoxCollider2D box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(1.0f, fallbackHalfHeight * 2f);
            }
            else
            {
                existingCol.isTrigger = true;
            }

            EnsureComponent<T>(go);
        }

        private void SpawnRollingCart(float minX, float maxX, float startDelayOffset = 0f, string fallbackName = "RollingCartHazard")
        {
            GameObject go = InstantiateOrFallback(rollingCartPrefab, fallbackName, new Vector3(minX - 1.5f, -2.25f, 0f), GetFallbackSquareSprite(), new Color(0.58f, 0.42f, 0.24f, 1f));
            Collider2D existingCol = go.GetComponent<Collider2D>();
            if (existingCol == null)
            {
                BoxCollider2D box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(1.4f, 0.9f);
            }
            else
            {
                existingCol.isTrigger = true;
            }

            RollingCartHazard cart = EnsureComponent<RollingCartHazard>(go);
            cart.ConfigureArenaBounds(minX, maxX);

            if (startDelayOffset > 0.05f)
            {
                cart.enabled = false;
                StartCoroutine(EnableRollingCartDelayedRoutine(cart, startDelayOffset));
            }
        }

        private IEnumerator EnableRollingCartDelayedRoutine(RollingCartHazard cart, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (cart != null && IsCampaignActive)
            {
                cart.enabled = true;
            }
        }

        private void SpawnBoulderSpawner(float minX, float maxX, float interval = 9.0f)
        {
            GameObject go = fallingBoulderSpawnerPrefab != null
                ? Instantiate(fallingBoulderSpawnerPrefab, Vector3.zero, Quaternion.identity, activeHazardsContainer)
                : new GameObject("FallingBoulderSpawner");

            if (fallingBoulderSpawnerPrefab == null)
            {
                go.transform.SetParent(activeHazardsContainer, false);
            }

            FallingBoulderSpawner spawner = EnsureComponent<FallingBoulderSpawner>(go);
            spawner.ConfigureArenaBounds(minX, maxX);
            spawner.ConfigureSpawnInterval(interval);
        }

        private void SpawnWallCannon(float x, int dir, float minX, float maxX)
        {
            SpawnWallCannonAt(new Vector3(x, EnemyBase.GROUND_Y + 0.45f, 0f), dir, minX, maxX, "WallCannonInteractable");
        }

        private void SpawnWallCannonAt(Vector3 pos, int dir, float minX, float maxX, string fallbackName)
        {
            GameObject go = InstantiateOrFallback(wallCannonPrefab, fallbackName, pos, GetFallbackSquareSprite(), new Color(0.35f, 0.38f, 0.44f, 1f));
            Collider2D existingCol = go.GetComponent<Collider2D>();
            if (existingCol == null)
            {
                BoxCollider2D box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(1.2f, 1.0f);
            }
            else
            {
                existingCol.isTrigger = true;
            }

            WallCannonInteractable cannon = EnsureComponent<WallCannonInteractable>(go);
            cannon.ConfigureDirection(dir, 11f);
            cannon.ConfigureArenaBounds(minX, maxX);
        }

        private void SpawnKiGeyser(string fallbackName, Vector3 groundPos, float phaseDelay)
        {
            GameObject go = InstantiateOrFallback(kiGeyserPrefab, fallbackName, groundPos, GetFallbackSquareSprite(), new Color(0.3f, 0.85f, 1f, 0.9f));
            if (go.GetComponent<BoxCollider2D>() == null)
            {
                BoxCollider2D box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(1.4f, 4.0f);
                box.offset = new Vector2(0f, 2.0f);
            }

            KiGeyserHazard geyser = EnsureComponent<KiGeyserHazard>(go);
            geyser.ConfigureCycleOffset(phaseDelay);
        }

        private void SpawnGravityOrSmoke(
            GameObject prefab, string fallbackName, Vector3 pos,
            float speedMult, float gravityMult, float minX, float maxX, float driftSpd, bool slowSmokeOnly)
        {
            Color col = slowSmokeOnly ? new Color(0.85f, 0.88f, 0.92f, 0.58f) : new Color(0.70f, 0.35f, 1f, 0.65f);
            GameObject go = InstantiateOrFallback(prefab, fallbackName, pos, GetFallbackCircleSprite(), col);
            Collider2D existingCol = go.GetComponent<Collider2D>();
            if (existingCol == null)
            {
                CircleCollider2D circle = go.AddComponent<CircleCollider2D>();
                circle.isTrigger = true;
                circle.radius = 1.45f;
            }
            else
            {
                existingCol.isTrigger = true;
            }

            GravityZoneHazard zone = EnsureComponent<GravityZoneHazard>(go);
            zone.Configure(speedMult, gravityMult, 0.25f);
            zone.ConfigureArenaDrift(minX, maxX, driftSpd, slowSmokeOnly);
        }

        private void SpawnKiCrystalChain(string fallbackName, Vector3 pos)
        {
            GameObject go = kiCrystalChainPrefab != null
                ? Instantiate(kiCrystalChainPrefab, pos, Quaternion.identity, activeHazardsContainer)
                : new GameObject(fallbackName);

            if (kiCrystalChainPrefab == null)
            {
                go.transform.SetParent(activeHazardsContainer, false);
                go.transform.position = pos;
                go.transform.localScale = new Vector3(0.95f, 1.15f, 1f);
            }

            BoxCollider2D box = EnsureComponent<BoxCollider2D>(go);
            box.isTrigger = true;
            if (kiCrystalChainPrefab == null)
            {
                box.size = new Vector2(0.95f, 1.15f);
            }

            KiCrystalChainHazard crystal = EnsureComponent<KiCrystalChainHazard>(go);
            crystal.Configure(3.6f, 26f, 5.5f, 0.18f, 10.0f);
        }

        private void SpawnRageTreasureChest(string fallbackName, Vector3 pos)
        {
            GameObject go = rageTreasureChestPrefab != null
                ? Instantiate(rageTreasureChestPrefab, pos, Quaternion.identity, activeHazardsContainer)
                : new GameObject(fallbackName);

            if (rageTreasureChestPrefab == null)
            {
                go.transform.SetParent(activeHazardsContainer, false);
                go.transform.position = pos;
                go.transform.localScale = new Vector3(0.92f, 0.85f, 1f);
            }

            BoxCollider2D box = EnsureComponent<BoxCollider2D>(go);
            box.isTrigger = true;
            if (rageTreasureChestPrefab == null)
            {
                box.size = new Vector2(0.95f, 0.85f);
            }

            RageTreasureChestInteractable chest = EnsureComponent<RageTreasureChestInteractable>(go);
            chest.Configure(12f, 14.0f);
        }

        private void SpawnLaserTurret(string fallbackName, float x, float minX, float maxX, int dir, float initialDelay)
        {
            Vector3 pos = new Vector3(x, EnemyBase.GROUND_Y + 0.5f, 0f);
            GameObject go = InstantiateOrFallback(lowLaserTurretPrefab, fallbackName, pos, GetFallbackSquareSprite(), new Color(0.45f, 0.48f, 0.56f, 1f));
            LowLaserTurretHazard turret = EnsureComponent<LowLaserTurretHazard>(go);
            turret.ConfigureArenaBounds(minX, maxX, dir, initialDelay);
        }

        private void SpawnAuroraPortals(Vector3 posA, Vector3 posB, string fallbackName = "AuroraPortalPair")
        {
            GameObject go = auroraPortalPairPrefab != null
                ? Instantiate(auroraPortalPairPrefab, Vector3.zero, Quaternion.identity, activeHazardsContainer)
                : new GameObject(fallbackName);

            if (auroraPortalPairPrefab == null)
            {
                go.transform.SetParent(activeHazardsContainer, false);
            }

            AuroraPortalPair pair = EnsureComponent<AuroraPortalPair>(go);
            pair.ConfigurePortals(posA, posB, 2.0f);
        }
        #endregion

        #region Shared Hazard Interop & Procedural VFX Helpers
        /// <summary>
        /// Resolves any EnemyBase, BossBase, BaseEnemy, or CrystalCore from a hit Collider2D.
        /// </summary>
        public static bool TryResolveEnemyDamageable(Collider2D hit, out IDamageable target)
        {
            target = null;
            if (hit == null)
            {
                return false;
            }

            EnemyBase enemyBase = hit.GetComponent<EnemyBase>() ?? hit.GetComponentInParent<EnemyBase>();
            if (enemyBase != null)
            {
                target = enemyBase;
                return !target.IsDead;
            }

            BossBase bossBase = hit.GetComponent<BossBase>() ?? hit.GetComponentInParent<BossBase>();
            if (bossBase != null)
            {
                target = bossBase;
                return !target.IsDead;
            }

            BaseEnemy baseEnemy = hit.GetComponent<BaseEnemy>() ?? hit.GetComponentInParent<BaseEnemy>();
            if (baseEnemy != null)
            {
                target = baseEnemy;
                return !target.IsDead;
            }

            CrystalCore core = hit.GetComponent<CrystalCore>() ?? hit.GetComponentInParent<CrystalCore>();
            if (core != null)
            {
                target = core;
                return !target.IsDead;
            }

            return false;
        }

        /// <summary>
        /// Resolves BaseCharacter from a hit Collider2D and delivers hazard damage respecting Block (IsBlocking).
        /// </summary>
        public static bool TryResolvePlayer(Collider2D hit, out BaseCharacter character)
        {
            character = null;
            if (hit == null)
            {
                return false;
            }

            character = hit.GetComponent<BaseCharacter>() ?? hit.GetComponentInParent<BaseCharacter>();
            return character != null && !character.IsDead;
        }

        /// <summary>
        /// Sends hazard DamageData to BaseCharacter cleanly, suppressing unblocked knockback when Player is blocking.
        /// </summary>
        public static void ApplyPlayerHazardDamage(
            BaseCharacter player, float damage, Vector2 unblockedKnockback, AttackType attackType, GameObject attacker)
        {
            if (player == null || player.IsDead)
            {
                return;
            }

            Vector2 appliedKnockback = player.IsBlocking ? Vector2.zero : unblockedKnockback;
            player.TakeDamage(new DamageData(damage, appliedKnockback, attackType, false, attacker));
        }

        public static Sprite GetFallbackSquareSprite()
        {
            if (fallbackSquareSprite == null)
            {
                Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[16 * 16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                fallbackSquareSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            }
            return fallbackSquareSprite;
        }

        public static Sprite GetFallbackCircleSprite()
        {
            if (fallbackCircleSprite == null)
            {
                const int size = 32;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[size * size];
                Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
                float radius = size * 0.48f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), center);
                        float alpha = Mathf.Clamp01((radius - dist) * 0.75f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply();
                fallbackCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            }
            return fallbackCircleSprite;
        }

        public static Material GetSpritesDefaultMaterial()
        {
            if (sharedSpriteMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    sharedSpriteMaterial = new Material(shader);
                }
            }
            return sharedSpriteMaterial;
        }

        public static IEnumerator AnimateExpandingRingRoutine(
            Vector3 center, Color color, float startScale, float endScale, float duration, Transform parent)
        {
            GameObject ringObj = new GameObject("HazardEffectRing");
            if (parent != null)
            {
                ringObj.transform.SetParent(parent, false);
            }
            ringObj.transform.position = center;

            SpriteRenderer sr = ringObj.AddComponent<SpriteRenderer>();
            sr.sprite = GetFallbackCircleSprite();
            sr.color = color;
            sr.sortingOrder = 8;

            float elapsed = 0f;
            while (elapsed < duration && ringObj != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                float s = Mathf.Lerp(startScale, endScale, t);
                ringObj.transform.localScale = new Vector3(s, s, 1f);
                Color c = color;
                c.a = Mathf.Lerp(color.a, 0f, t);
                sr.color = c;
                yield return null;
            }

            if (ringObj != null)
            {
                Destroy(ringObj);
            }
        }
        #endregion
    }
}
