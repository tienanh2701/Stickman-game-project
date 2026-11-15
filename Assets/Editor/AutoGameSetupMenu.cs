using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using StickmanBeatEmUp.Enemies;
using StickmanBeatEmUp.Environment;
using StickmanBeatEmUp.GameModes;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Editor
{
    /// <summary>
    /// Master Editor Menu: Cung cấp nút bấm 'Tools -> Setup Game Automatically'
    /// Tự động hóa 100% và chạy an toàn qua [InitializeOnLoad] nếu setup v2 chưa hoàn tất.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoGameSetupMenu
    {
        private const string SETUP_VERSION_TAG = "SETUP_VERSION=v11_autotrim_training_endless";
        private static bool queuedAutoCheck = false;

        static AutoGameSetupMenu()
        {
            if (!queuedAutoCheck)
            {
                queuedAutoCheck = true;
                Debug.Log("[AutoGameSetupMenu] Static ctor initialized, hooking EditorApplication.update.");
                EditorApplication.update += OnFirstEditorUpdate;
            }
        }

        private static void OnFirstEditorUpdate()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            EditorApplication.update -= OnFirstEditorUpdate;
            TryAutoRunOnce();
        }

        private static void TryAutoRunOnce()
        {
            if (Application.isBatchMode)
            {
                Debug.Log("[AutoGameSetupMenu] Skipped TryAutoRunOnce: batchMode=true");
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.Log("[AutoGameSetupMenu] Skipped TryAutoRunOnce: isPlayingOrWillChangePlaymode=true");
                return;
            }

            if (IsSetupUpToDate())
            {
                return;
            }

            Debug.Log("[AutoGameSetupMenu] Starting automatic setup v2...");
            RunSetupInternal(interactive: false);
        }

        private static bool IsSetupUpToDate()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string markerPath = Path.Combine(projectRoot, "Logs", "AutoSetupResult.txt");
            if (!File.Exists(markerPath)) return false;

            string content = File.ReadAllText(markerPath);
            if (!content.Contains(SETUP_VERSION_TAG)) return false;

            bool hasGokuAnim = File.Exists(Path.Combine(Application.dataPath, "Animations", "Goku", "Goku.controller"));
            bool hasMainMenu = File.Exists(Path.Combine(Application.dataPath, "Scenes", "MainMenu.unity"));
            bool hasSelectScene = File.Exists(Path.Combine(Application.dataPath, "Scenes", "CharacterSelectScene.unity"));
            bool hasBattleScene = File.Exists(Path.Combine(Application.dataPath, "Scenes", "Stickman_DemoScene.unity"));
            bool hasMechaBoss = File.Exists(Path.Combine(Application.dataPath, "Prefabs", "Enemies", "World3_Diend", "W3_Boss_MechaColossus.prefab"));
            bool hasItems = File.Exists(Path.Combine(Application.dataPath, "Resources", "Items", "SenzuBeanSO.asset"));
            bool hasHazards = File.Exists(Path.Combine(Application.dataPath, "Prefabs", "Hazards", "Hazard_WoodenEaves.prefab"));
            return hasGokuAnim && hasMainMenu && hasSelectScene && hasBattleScene && hasMechaBoss && hasItems && hasHazards;
        }

        [MenuItem("Tools/Setup Game Automatically", false, 1)]
        public static void FullAutoSetupGame()
        {
            RunSetupInternal(interactive: true);
        }

        public static void RunSetupBatchMode()
        {
            RunSetupInternal(interactive: false);
        }

        private static void RunSetupInternal(bool interactive)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string logsDir = Path.Combine(projectRoot, "Logs");
            string markerPath = Path.Combine(logsDir, "AutoSetupResult.txt");

            try
            {
                if (interactive)
                {
                    EditorUtility.DisplayProgressBar("Stickman Auto Setup", "1/5 Đang cấu hình Tags và Layers...", 0.1f);
                }
                TagAndLayerSetup.EnsureTagsAndLayers();
                PlayerSettings.productName = "MULTI-DIMENSION STICKMAN";

                if (interactive)
                {
                    EditorUtility.DisplayProgressBar("Stickman Auto Setup", "2/5 Đang cấu hình Sprite, Animation, ScriptableObjects & Prefabs...", 0.35f);
                }
                SpriteAutoTrimmer.TrimAndConfigureAllSprites();
                AssetDatabase.Refresh();
                PrefabBuilder.GenerateAllAssets(
                    out GameObject gokuPrefab, out GameObject erenPrefab, out GameObject diendPrefab,
                    out _, out _, out _, out _, out _,
                    out List<StageDataSO> allStages,
                    out List<LevelDataSO> allLevels
                );

                if (interactive)
                {
                    EditorUtility.DisplayProgressBar("Stickman Auto Setup", "3/5 Đang dựng Battle Scene & Canvas UI...", 0.65f);
                }

                string battleScenePath = BuildBattleScene(gokuPrefab, erenPrefab, diendPrefab, allStages, allLevels);

                if (interactive)
                {
                    EditorUtility.DisplayProgressBar("Stickman Auto Setup", "4/5 Đang dựng Character Select Scene...", 0.85f);
                }

                string selectScenePath = CharacterSelectSceneBuilder.BuildCharacterSelectScene();
                string mainMenuScenePath = MainMenuSceneBuilder.BuildMainMenuScene(interactive: false);

                // Đăng ký Build Settings: [0] MainMenu, [1] Stickman_DemoScene, [2] CharacterSelectScene
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(mainMenuScenePath, true),
                    new EditorBuildSettingsScene(battleScenePath, true),
                    new EditorBuildSettingsScene(selectScenePath, true)
                };

                AssetDatabase.SaveAssets();
                EditorSceneManager.OpenScene(mainMenuScenePath, OpenSceneMode.Single);

                Directory.CreateDirectory(logsDir);
                string summary =
                    $"{SETUP_VERSION_TAG}\n" +
                    $"Timestamp={DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    $"StagesCount={allStages.Count}\n" +
                    $"BuildScenes={mainMenuScenePath};{battleScenePath};{selectScenePath}\n" +
                    "Prefabs=Player_Goku,Player_Eren,Player_Diend,Enemy_Grunt,Enemy_Ranger,Enemy_Kamikaze,Enemy_EliteBruiser,Boss_Warlord,LordKivaAI,W3_Boss_MechaColossus\n";
                File.WriteAllText(markerPath, summary);

                Debug.Log("[AutoGameSetupMenu] Setup hoàn tất 100%! Đã tạo Sprite Animations, 12 StageDataSO, Prefabs, Battle Scene và Character Select Scene.");

                if (interactive)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog(
                        "Setup Hoàn Tất 100%!",
                        "Toàn bộ Game Stickman Beat 'em Up đã được tự động setup:\n\n" +
                        "✓ Đã cấu hình Tags & Layers + Sprites (146 assets) & 4 Animator Controllers\n" +
                        "✓ Đã tạo 3 Prefab Nhân vật (Goku, Eren, Diend) + Lord Kiva AI\n" +
                        "✓ Đã tạo 4 Prefab Quái & 1 Boss + 12 StageDataSO (3 Worlds x 4 Stages)\n" +
                        "✓ Đã dựng CharacterSelectScene.unity & Stickman_DemoScene.unity + Build Settings\n\n" +
                        "Chỉ cần bấm nút [PLAY] ▶ để bắt đầu từ màn hình Chọn Nhân Vật & Màn Chơi!",
                        "Bắt Đầu Chơi Ngay!"
                    );
                }
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(logsDir);
                File.WriteAllText(markerPath, $"SETUP_FAILED\n{ex}");
                Debug.LogException(ex);
                throw;
            }
            finally
            {
                if (interactive)
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }

        private static string BuildBattleScene(
            GameObject gokuPrefab, GameObject erenPrefab, GameObject diendPrefab,
            List<StageDataSO> allStages, List<LevelDataSO> allLevels)
        {
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 0. Stage World Background (Bright & pushed back to z=10, sortingOrder=-50, scale 1.25x)
            GameObject bgObj = new GameObject("StageBackground");
            bgObj.transform.position = new Vector3(10f, 1.65f, 10f);
            bgObj.transform.localScale = new Vector3(2.75f, 1.5f, 1f);
            SpriteRenderer bgSr = bgObj.AddComponent<SpriteRenderer>();
            bgSr.sprite = SpriteHelper.LoadSprite("Assets/Sprites/Maps/Map_W1_S1.png", SpriteHelper.GetOrCreateSquareSprite());
            bgSr.color = Color.white;
            bgSr.sortingOrder = -50;

            // 1. Tạo Sàn Đấu (Ground): Mặt trên tại groundY = -2.5f khớp pivot Bottom Center (0.5, 0.0)
            GameObject ground = new GameObject("Ground_Arena");
            ground.layer = 6; // Ground Layer
            ground.transform.position = new Vector3(10f, -3.35f, 0f);
            ground.transform.localScale = new Vector3(80f, 1.7f, 1f);

            SpriteRenderer groundSr = ground.AddComponent<SpriteRenderer>();
            groundSr.sprite = SpriteHelper.GetOrCreateSquareSprite();
            groundSr.color = new Color(0.18f, 0.20f, 0.24f);

            BoxCollider2D groundCol = ground.AddComponent<BoxCollider2D>();
            groundCol.size = Vector2.one;

            // 2. Tạo 2 Bức Tường Vô Hình cho Camera Khóa Màn
            GameObject leftWall = new GameObject("LeftCameraWall");
            BoxCollider2D leftCol = leftWall.AddComponent<BoxCollider2D>();
            leftCol.size = new Vector2(1f, 20f);
            leftWall.SetActive(false);

            GameObject rightWall = new GameObject("RightCameraWall");
            BoxCollider2D rightCol = rightWall.AddComponent<BoxCollider2D>();
            rightCol.size = new Vector2(1f, 20f);
            rightWall.SetActive(false);

            // 2b. Tường giới hạn 2 đầu bản đồ (khớp Clamp [-22f, 42f])
            GameObject leftMapBoundary = new GameObject("LeftMapBoundary");
            leftMapBoundary.layer = 6;
            leftMapBoundary.transform.position = new Vector3(-22.5f, 2f, 0f);
            BoxCollider2D leftMapCol = leftMapBoundary.AddComponent<BoxCollider2D>();
            leftMapCol.size = new Vector2(1f, 20f);

            GameObject rightMapBoundary = new GameObject("RightMapBoundary");
            rightMapBoundary.layer = 6;
            rightMapBoundary.transform.position = new Vector3(42.5f, 2f, 0f);
            BoxCollider2D rightMapCol = rightMapBoundary.AddComponent<BoxCollider2D>();
            rightMapCol.size = new Vector2(1f, 20f);

            // 3. Tạo Main Camera với CameraFollowBeatEmUp
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0f, 0.8f, -10f);
            Camera cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.backgroundColor = new Color(0.18f, 0.21f, 0.26f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camObj.AddComponent<AudioListener>();

            CameraFollowBeatEmUp camFollow = camObj.AddComponent<CameraFollowBeatEmUp>();
            SerializedObject camSO = new SerializedObject(camFollow);
            camSO.FindProperty("offset").vector3Value = new Vector3(0f, 3.3f, -10f);
            camSO.FindProperty("smoothSpeed").floatValue = 0.12f;
            camSO.FindProperty("leftWallCollider").objectReferenceValue = leftCol;
            camSO.FindProperty("rightWallCollider").objectReferenceValue = rightCol;
            camSO.ApplyModifiedProperties();

            // 4. Tạo Player Spawn Point (LevelManager sẽ spawn nhân vật đã chọn tại groundY = -2.5f)
            GameObject spawnPointObj = new GameObject("PlayerSpawnPoint");
            spawnPointObj.transform.position = new Vector3(0f, StickmanBeatEmUp.Enemies.EnemyBase.GROUND_Y, 0f);
            camFollow.SetTarget(spawnPointObj.transform);

            // 4b. Rage Sandbox Enemies Container (empty in Campaign Mode; [T] spawns test pack on demand)
            GameObject sandboxContainer = new GameObject("RageSandboxEnemies");

            GameObject w1PureTitanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/World1_AoT/W1_Creep_PureTitan.prefab");
            GameObject w2SaibamenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/World2_DB/W2_Creep_Saibamen.prefab");
            GameObject w2SaibamanKamikazePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/World2_DB/W2_Creep_SaibamanKamikaze.prefab");
            GameObject w3ShockerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/World3_Diend/W3_Creep_Shocker.prefab");

            GameObject w2FriezaSoldierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/World2_DB/W2_Range_FriezaSoldier.prefab");
            GameObject w3KasshinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/World3_Diend/W3_Range_Kasshin.prefab");
            GameObject w1BeastTitanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/World1_AoT/W1_Range_BeastTitan.prefab");

            GameObject w1FemaleTitanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/World1_AoT/W1_Elite_FemaleTitan.prefab");
            GameObject w2NappaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/World2_DB/W2_Elite_Nappa.prefab");
            GameObject w3ShadowMoonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/World3_Diend/W3_Elite_ShadowMoon.prefab");

            GameObject w1WarHammerBossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/World1_AoT/W1_Boss_WarHammer.prefab");
            GameObject w2BrolyBossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/World2_DB/W2_Boss_Broly.prefab");
            GameObject w3MechaBossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/World3_Diend/W3_Boss_MechaColossus.prefab");

            // 5. Tạo Canvas UI tự động + EventSystem
            CanvasUIBuilder.CreateGameUICanvas(out CanvasUIBuilder.TrainingHudRefs trainingRefs);

            // Reload assets fresh AFTER NewScene() so UnloadUnusedAssets cannot null-out scene references!
            GameObject safeGoku = gokuPrefab != null ? gokuPrefab : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Player_Goku.prefab");
            GameObject safeEren = erenPrefab != null ? erenPrefab : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Player_Eren.prefab");
            GameObject safeDiend = diendPrefab != null ? diendPrefab : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Player_Diend.prefab");

            List<LevelDataSO> reloadedLevels = new List<LevelDataSO>();
            List<StageDataSO> reloadedStages = new List<StageDataSO>();
            for (int w = 1; w <= 3; w++)
            {
                for (int s = 1; s <= 4; s++)
                {
                    LevelDataSO loadedLvl = AssetDatabase.LoadAssetAtPath<LevelDataSO>($"Assets/Data/Levels/Level_W{w}_S{s}.asset");
                    if (loadedLvl != null)
                    {
                        reloadedLevels.Add(loadedLvl);
                        reloadedStages.Add(loadedLvl);
                    }
                }
            }
            if (reloadedLevels.Count == 0 && allLevels != null)
            {
                foreach (LevelDataSO lvl in allLevels)
                {
                    if (lvl != null)
                    {
                        reloadedLevels.Add(lvl);
                        reloadedStages.Add(lvl);
                    }
                }
            }
            if (reloadedStages.Count == 0 && allStages != null)
            {
                foreach (StageDataSO st in allStages)
                {
                    if (st != null) reloadedStages.Add(st);
                }
            }

            // 6. Tạo LevelManager GameObject
            GameObject lmObj = new GameObject("LevelManager");
            LevelManager lm = lmObj.AddComponent<LevelManager>();

            SerializedObject lmSO = new SerializedObject(lm);
            lmSO.FindProperty("sandboxMode").boolValue = false;
            if (reloadedStages.Count > 0)
            {
                lmSO.FindProperty("currentStageData").objectReferenceValue = reloadedStages[0];
                SerializedProperty allStagesProp = lmSO.FindProperty("allStages");
                allStagesProp.arraySize = reloadedStages.Count;
                for (int i = 0; i < reloadedStages.Count; i++)
                {
                    allStagesProp.GetArrayElementAtIndex(i).objectReferenceValue = reloadedStages[i];
                }
            }
            lmSO.FindProperty("cameraFollow").objectReferenceValue = camFollow;
            lmSO.FindProperty("playerSpawnPoint").objectReferenceValue = spawnPointObj.transform;

            SerializedProperty charPrefabsProp = lmSO.FindProperty("characterPrefabs");
            charPrefabsProp.arraySize = 3;
            charPrefabsProp.GetArrayElementAtIndex(0).objectReferenceValue = safeGoku;
            charPrefabsProp.GetArrayElementAtIndex(1).objectReferenceValue = safeEren;
            charPrefabsProp.GetArrayElementAtIndex(2).objectReferenceValue = safeDiend;
            lmSO.ApplyModifiedProperties();

            // 6b. StageManager & BossMinionSpawner
            StageManager sm = lmObj.AddComponent<StageManager>();
            lmObj.AddComponent<BossMinionSpawner>();
            SerializedObject smSO = new SerializedObject(sm);
            if (reloadedLevels.Count > 0)
            {
                SerializedProperty allLevelsProp = smSO.FindProperty("allLevels");
                allLevelsProp.arraySize = reloadedLevels.Count;
                for (int i = 0; i < reloadedLevels.Count; i++)
                {
                    allLevelsProp.GetArrayElementAtIndex(i).objectReferenceValue = reloadedLevels[i];
                }
            }
            smSO.FindProperty("backgroundRenderer").objectReferenceValue = bgSr;
            smSO.FindProperty("groundRenderer").objectReferenceValue = groundSr;
            smSO.FindProperty("levelManager").objectReferenceValue = lm;
            smSO.ApplyModifiedProperties();

            // 6b2. CampaignHazardSpawner + EndlessModeManager + TrainingModeManager
            GameObject hazardsContainer = new GameObject("ActiveCampaignHazards");
            CampaignHazardSpawner hazardSpawner = lmObj.AddComponent<CampaignHazardSpawner>();
            SerializedObject hSO = new SerializedObject(hazardSpawner);
            hSO.FindProperty("woodenEavesPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Hazard_WoodenEaves.prefab");
            hSO.FindProperty("gunpowderBarrelPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Hazard_GunpowderBarrel.prefab");
            hSO.FindProperty("rollingCartPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Hazard_RollingCart.prefab");
            hSO.FindProperty("fallingBoulderSpawnerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Hazard_FallingBoulderSpawner.prefab");
            hSO.FindProperty("wallCannonPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Hazard_WallCannon.prefab");
            hSO.FindProperty("floatingRockPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Hazard_FloatingRock.prefab");
            hSO.FindProperty("kiGeyserPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Hazard_KiGeyser.prefab");
            hSO.FindProperty("gravityZonePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Hazard_GravityZone.prefab");
            hSO.FindProperty("titanSmokePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Hazard_TitanSmoke.prefab");
            hSO.FindProperty("stonePillarPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Hazard_AncientStonePillar.prefab");
            hSO.FindProperty("cyberPlatformPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Hazard_CyberPlatform.prefab");
            hSO.FindProperty("auroraPortalPairPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Hazard_AuroraPortalPair.prefab");
            hSO.FindProperty("lowLaserTurretPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Hazard_LowLaserTurret.prefab");
            hSO.FindProperty("empTerminalPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Hazard_EmpTerminal.prefab");
            hSO.FindProperty("activeHazardsContainer").objectReferenceValue = hazardsContainer.transform;
            hSO.ApplyModifiedProperties();

            GameObject[] endlessMeleeCreeps = { w1PureTitanPrefab, w2SaibamenPrefab, w3ShockerPrefab };
            GameObject[] endlessRangedCreeps = { w1BeastTitanPrefab, w2FriezaSoldierPrefab, w3KasshinPrefab };
            GameObject[] endlessCreeps = { w1PureTitanPrefab, w1BeastTitanPrefab, w2SaibamenPrefab, w2FriezaSoldierPrefab, w3ShockerPrefab, w3KasshinPrefab };
            GameObject[] endlessElites = { w1FemaleTitanPrefab, w2NappaPrefab, w3ShadowMoonPrefab };
            GameObject[] endlessBosses = { w1WarHammerBossPrefab, w2BrolyBossPrefab, w3MechaBossPrefab };

            EndlessModeManager endlessMgr = lmObj.AddComponent<EndlessModeManager>();
            endlessMgr.ConfigureCategorizedPrefabPools(endlessMeleeCreeps, endlessRangedCreeps, endlessElites, endlessBosses);
            SerializedObject endlessSO = new SerializedObject(endlessMgr);
            AssignObjectArray(endlessSO.FindProperty("meleeCreeps"), endlessMeleeCreeps);
            AssignObjectArray(endlessSO.FindProperty("rangedCreeps"), endlessRangedCreeps);
            AssignObjectArray(endlessSO.FindProperty("elites"), endlessElites);
            AssignObjectArray(endlessSO.FindProperty("bosses"), endlessBosses);
            AssignObjectArray(endlessSO.FindProperty("creepPrefabs"), endlessCreeps);
            AssignObjectArray(endlessSO.FindProperty("elitePrefabs"), endlessElites);
            AssignObjectArray(endlessSO.FindProperty("bossPrefabs"), endlessBosses);
            endlessSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(endlessMgr);

            TrainingModeManager trainingMgr = lmObj.AddComponent<TrainingModeManager>();
            trainingMgr.ConfigureUnitRoster(new[]
            {
                new TrainingUnitEntry { displayName = "TG1: Pure Titan", prefab = w1PureTitanPrefab },
                new TrainingUnitEntry { displayName = "TG1: Beast Titan", prefab = w1BeastTitanPrefab },
                new TrainingUnitEntry { displayName = "TG1: Female Titan (Elite)", prefab = w1FemaleTitanPrefab },
                new TrainingUnitEntry { displayName = "TG1: War Hammer (Boss)", prefab = w1WarHammerBossPrefab },
                new TrainingUnitEntry { displayName = "TG2: Saibaman", prefab = w2SaibamenPrefab },
                new TrainingUnitEntry { displayName = "TG2: Kamikaze", prefab = w2SaibamanKamikazePrefab },
                new TrainingUnitEntry { displayName = "TG2: Lính Frieza", prefab = w2FriezaSoldierPrefab },
                new TrainingUnitEntry { displayName = "TG2: Nappa (Elite)", prefab = w2NappaPrefab },
                new TrainingUnitEntry { displayName = "TG2: Broly (Boss)", prefab = w2BrolyBossPrefab },
                new TrainingUnitEntry { displayName = "TG3: Lính Shocker", prefab = w3ShockerPrefab },
                new TrainingUnitEntry { displayName = "TG3: Kasshin", prefab = w3KasshinPrefab },
                new TrainingUnitEntry { displayName = "TG3: Shadow Moon (Elite)", prefab = w3ShadowMoonPrefab },
                new TrainingUnitEntry { displayName = "TG3: Mecha Boss", prefab = w3MechaBossPrefab }
            });
            trainingMgr.ConfigureTopBarUI(
                trainingRefs.panel,
                trainingRefs.playerInvincibleBtn, trainingRefs.playerInvincibleTxt,
                trainingRefs.autoMaxRageBtn, trainingRefs.autoMaxRageTxt,
                trainingRefs.enemyAiBtn, trainingRefs.enemyAiTxt,
                trainingRefs.enemyInvincibleBtn, trainingRefs.enemyInvincibleTxt,
                trainingRefs.clearBtn,
                trainingRefs.spawnBtns, trainingRefs.spawnLbls, trainingRefs.spawnIcons
            );
            EditorUtility.SetDirty(trainingMgr);

            lmSO.Update();
            lmSO.FindProperty("endlessModeManager").objectReferenceValue = endlessMgr;
            lmSO.FindProperty("trainingModeManager").objectReferenceValue = trainingMgr;
            lmSO.ApplyModifiedProperties();

            // 6c. RageTestSpawner on isolated sandboxContainer (never on lmObj so deactivating it cannot disable LevelManager/Endless/Training)
            RageTestSpawner spawner = sandboxContainer.AddComponent<RageTestSpawner>();
            SerializedObject spawnerSO = new SerializedObject(spawner);
            spawnerSO.FindProperty("enableSandbox").boolValue = false;
            spawnerSO.FindProperty("meleeDummyHP").floatValue = 0f;
            spawnerSO.FindProperty("respawnDelay").floatValue = 2.0f;
            spawnerSO.FindProperty("sandboxContainer").objectReferenceValue = sandboxContainer.transform;

            GameObject[] meleePool = new[] { w1PureTitanPrefab, w2SaibamenPrefab, w3ShockerPrefab };
            AssignObjectArray(spawnerSO.FindProperty("meleeCreepPrefabs"), meleePool);

            GameObject[] rangedPool = new[] { w2FriezaSoldierPrefab, w3KasshinPrefab, w1BeastTitanPrefab };
            AssignObjectArray(spawnerSO.FindProperty("rangedCreepPrefabs"), rangedPool);

            GameObject[] elitePool = new[] { w1FemaleTitanPrefab, w2NappaPrefab, w3ShadowMoonPrefab };
            AssignObjectArray(spawnerSO.FindProperty("elitePrefabs"), elitePool);

            spawnerSO.ApplyModifiedProperties();

            // 7. Lưu Scene
            SpriteHelper.EnsureFolder("Assets/Scenes");
            const string scenePath = "Assets/Scenes/Stickman_DemoScene.unity";
            EditorSceneManager.MarkSceneDirty(newScene);
            EditorSceneManager.SaveScene(newScene, scenePath);
            return scenePath;
        }

        private static void AssignObjectArray(SerializedProperty arrayProp, GameObject[] items)
        {
            if (arrayProp == null) return;
            if (items == null)
            {
                arrayProp.arraySize = 0;
                return;
            }
            arrayProp.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }

        [MenuItem("Tools/Regenerate Prefabs Only", false, 2)]
        public static void GeneratePrefabsOnly()
        {
            TagAndLayerSetup.EnsureTagsAndLayers();
            PrefabBuilder.GenerateAllAssets(
                out _, out _, out _,
                out _, out _, out _, out _, out _,
                out List<StageDataSO> _
            );
            EditorUtility.DisplayDialog("Thành Công", "Đã tạo/cập nhật toàn bộ Sprite, Animation, StageData và Prefab!", "OK");
        }
    }
}
