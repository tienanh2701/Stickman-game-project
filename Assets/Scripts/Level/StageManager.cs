using System;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Level
{
    /// <summary>
    /// Manages the 3 Worlds x 4 Stages (12 levels), applying each selected World's background map & ground styling dynamically.
    /// Also tracks and routes GameMode selection (Campaign / Endless / Training).
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        public static StageManager Instance { get; private set; }
        public static GameMode CurrentGameMode { get; set; } = GameMode.Campaign;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            CurrentGameMode = GameMode.Campaign;
        }

        private const string SELECTED_STAGE_INDEX_KEY = "SelectedStageIndex";
        private const string SELECTED_GAME_MODE_KEY = "SelectedGameMode";

        [Header("12 World-Exclusive Levels (W1:0..3, W2:4..7, W3:8..11)")]
        [SerializeField] private List<LevelDataSO> allLevels = new List<LevelDataSO>();

        [Header("Scene Environment Targets")]
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private SpriteRenderer groundRenderer;
        [SerializeField] private LevelManager levelManager;

        private bool hasAppliedLevel;

        public LevelDataSO ActiveLevel { get; private set; }
        public IReadOnlyList<LevelDataSO> AllLevels => allLevels;

        public static void SetSelectedGameMode(GameMode mode)
        {
            CurrentGameMode = mode;
            PlayerPrefs.SetInt(SELECTED_GAME_MODE_KEY, (int)mode);
            PlayerPrefs.Save();
        }

        public static GameMode LoadSelectedGameMode()
        {
            int raw = PlayerPrefs.GetInt(SELECTED_GAME_MODE_KEY, (int)GameMode.Campaign);
            CurrentGameMode = Enum.IsDefined(typeof(GameMode), raw) ? (GameMode)raw : GameMode.Campaign;
            return CurrentGameMode;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(this); return; }

            if (levelManager == null) levelManager = GetComponent<LevelManager>();
            LoadSelectedGameMode();
            ApplySelectedLevel();
        }

        private void Start()
        {
            if (!hasAppliedLevel) ApplySelectedLevel();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ApplySelectedLevel()
        {
            hasAppliedLevel = true;
            GameMode mode = LoadSelectedGameMode();

            if (mode != GameMode.Campaign)
            {
                ActiveLevel = null;
                if (levelManager != null)
                {
                    levelManager.SetCurrentStage(null);
                }

                // Endless & Training share World 1 Stage 1 (Attack on Titan) visuals without Campaign wave progression.
                LevelDataSO aotLevel = GetDefaultAttackOnTitanLevel();
                ApplyLevelVisuals(aotLevel);
                return;
            }

            if (allLevels == null || allLevels.Count == 0) return;

            int idx = Mathf.Clamp(PlayerPrefs.GetInt(SELECTED_STAGE_INDEX_KEY, 0), 0, allLevels.Count - 1);
            ActiveLevel = allLevels[idx] ?? GetDefaultAttackOnTitanLevel();
            if (ActiveLevel == null) return;

            if (levelManager != null)
            {
                levelManager.SetCurrentStage(ActiveLevel);
            }

            ApplyLevelVisuals(ActiveLevel);
        }

        private LevelDataSO GetDefaultAttackOnTitanLevel()
        {
            if (allLevels == null || allLevels.Count == 0) return null;
            if (allLevels[0] != null) return allLevels[0];

            for (int i = 1; i < allLevels.Count; i++)
            {
                if (allLevels[i] != null) return allLevels[i];
            }
            return null;
        }

        private void ApplyLevelVisuals(LevelDataSO level)
        {
            if (backgroundRenderer != null)
            {
                if (level != null && level.backgroundSprite != null)
                {
                    backgroundRenderer.enabled = true;
                    backgroundRenderer.sprite = level.backgroundSprite;
                    backgroundRenderer.color = Color.white;
                    backgroundRenderer.sortingOrder = -50;

                    Vector3 bgPos = backgroundRenderer.transform.position;
                    bgPos.x = 10f;
                    bgPos.z = 10f;
                    backgroundRenderer.transform.position = bgPos;

                    Vector2 spriteSize = level.backgroundSprite.bounds.size;
                    if (spriteSize.x > 0.01f && spriteSize.y > 0.01f)
                    {
                        float sx = Mathf.Max(1.25f, (84f / spriteSize.x) * 1.25f);
                        float sy = Mathf.Max(1.25f, (15f / spriteSize.y) * 1.25f);
                        backgroundRenderer.transform.localScale = new Vector3(sx, Mathf.Max(sx * 0.75f, sy), 1f);
                    }
                    else
                    {
                        backgroundRenderer.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
                    }
                }
                else
                {
                    backgroundRenderer.color = Color.white;
                    backgroundRenderer.sortingOrder = -50;
                    backgroundRenderer.enabled = backgroundRenderer.sprite != null;
                }
            }

            // Determine bright, visible world arena ground base and blend with level.groundColor,
            // clamping luminance into [0.52, 0.72] so dark stickmen stay clearly readable.
            Color worldGroundBase;
            if (level != null)
            {
                switch (level.worldType)
                {
                    case WorldType.World2_Wasteland:
                        worldGroundBase = new Color(0.73f, 0.63f, 0.50f, 1f);
                        break;
                    case WorldType.World3_NeoCity:
                        worldGroundBase = new Color(0.55f, 0.59f, 0.68f, 1f);
                        break;
                    default:
                        // World1_RuinedCity
                        worldGroundBase = new Color(0.68f, 0.64f, 0.58f, 1f);
                        break;
                }
            }
            else
            {
                worldGroundBase = new Color(0.68f, 0.64f, 0.58f, 1f);
            }

            Color levelGround = level != null ? level.groundColor : worldGroundBase;
            Color groundColor = Color.Lerp(worldGroundBase, levelGround, 0.20f);
            float gLum = 0.2126f * groundColor.r + 0.7152f * groundColor.g + 0.0722f * groundColor.b;
            if (gLum < 0.52f)
            {
                if (gLum > 0.0001f)
                {
                    float scale = 0.52f / gLum;
                    groundColor = new Color(
                        Mathf.Clamp01(groundColor.r * scale),
                        Mathf.Clamp01(groundColor.g * scale),
                        Mathf.Clamp01(groundColor.b * scale),
                        1f);
                }
                else
                {
                    groundColor = worldGroundBase;
                }
            }
            else if (gLum > 0.72f)
            {
                float scale = 0.72f / gLum;
                groundColor = new Color(groundColor.r * scale, groundColor.g * scale, groundColor.b * scale, 1f);
            }
            else
            {
                groundColor.a = 1f;
            }

            if (groundRenderer != null)
            {
                groundRenderer.enabled = true;
                groundRenderer.color = groundColor;
                EnsureGroundArenaCoverage(groundRenderer.transform, 85f, 10f, -2.5f);
            }

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.backgroundColor = Color.Lerp(groundColor, Color.white, 0.25f);
            }

            // --- MapParallaxController — called AFTER EnsureGroundArenaCoverage has sized groundRenderer ---
            MapParallaxController parallax = GetComponent<MapParallaxController>();
            if (parallax == null) parallax = gameObject.AddComponent<MapParallaxController>();
            parallax.Setup(level, backgroundRenderer, groundRenderer);
        }

        private static void EnsureGroundArenaCoverage(Transform groundTransform, float minWidth, float centerX, float groundY)
        {
            if (groundTransform == null) return;

            Vector3 scale = groundTransform.localScale;
            if (Mathf.Abs(scale.x) < minWidth)
            {
                scale.x = minWidth;
                groundTransform.localScale = scale;
            }

            Vector3 pos = groundTransform.position;
            pos.x = centerX;
            groundTransform.position = pos;

            BoxCollider2D box = groundTransform.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                float absScaleX = Mathf.Max(0.01f, Mathf.Abs(groundTransform.lossyScale.x));
                if (box.size.x * absScaleX < minWidth)
                {
                    Vector2 size = box.size;
                    size.x = minWidth / absScaleX;
                    box.size = size;
                }
            }

            EnsureGroundTopAtArenaFloor(groundTransform, groundY);
        }

        private static void EnsureGroundTopAtArenaFloor(Transform groundTransform, float groundY)
        {
            if (groundTransform == null) return;
            BoxCollider2D box = groundTransform.GetComponent<BoxCollider2D>();
            if (box == null) return;

            float worldTopOffset = (box.offset.y + box.size.y * 0.5f) * groundTransform.lossyScale.y;
            Vector3 p = groundTransform.position;
            p.y = groundY - worldTopOffset;
            groundTransform.position = p;
        }
    }
}
