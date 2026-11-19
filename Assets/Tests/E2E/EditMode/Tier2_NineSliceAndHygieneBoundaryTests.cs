using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StickmanBeatEmUp.Tests.E2E.EditMode
{
    public class Tier2_NineSliceAndHygieneBoundaryTests
    {
        // ==================== F1: 9-Slice & >2048px Boundary Tests (5 Tests) ====================

        [Test]
        [Category("Tier2")]
        [Category("F1")]
        public void F01_T2_TenWideSlicedUITextures_HaveMaxTextureSize4096_And_FullRectMesh()
        {
            Assert.AreEqual(10, EditModeTierSupport.TenWideSlicedUiAssetPaths.Length);

            for (int i = 0; i < EditModeTierSupport.TenWideSlicedUiAssetPaths.Length; i++)
            {
                string path = EditModeTierSupport.TenWideSlicedUiAssetPaths[i];
                Assert.IsTrue(
                    EditModeTierSupport.ReadPngHeader(path, out int w, out int h, out _, out _),
                    $"Invalid PNG '{path}'.");
                Assert.IsTrue(w > 2048 || h > 2048, $"Expected >2048px texture for '{path}', got {w}x{h}.");

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"TextureImporter missing for '{path}'.");
                Assert.GreaterOrEqual(importer.maxTextureSize, 4096, $"maxTextureSize < 4096 for wide texture '{path}'.");

                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.AreEqual(SpriteMeshType.FullRect, settings.spriteMeshType, $"spriteMeshType must be FullRect for '{path}'.");
            }
        }

        [Test]
        [Category("Tier2")]
        [Category("F1")]
        public void F01_T2_All107SlicedUIMetas_UseFullRectMesh_And_NonNegativeBorders()
        {
            List<string> pngs = EditModeTierSupport.EnumerateSlicedUiPngAssets();
            Assert.AreEqual(107, pngs.Count);

            for (int i = 0; i < pngs.Count; i++)
            {
                string path = pngs[i];
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"TextureImporter missing for '{path}'.");

                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.AreEqual(SpriteMeshType.FullRect, settings.spriteMeshType, $"Non-FullRect spriteMeshType on '{path}'.");

                Vector4 b = importer.spriteBorder;
                Assert.GreaterOrEqual(b.x, 0f, $"Negative left border on '{path}'.");
                Assert.GreaterOrEqual(b.y, 0f, $"Negative bottom border on '{path}'.");
                Assert.GreaterOrEqual(b.z, 0f, $"Negative right border on '{path}'.");
                Assert.GreaterOrEqual(b.w, 0f, $"Negative top border on '{path}'.");
            }
        }

        [Test]
        [Category("Tier2")]
        [Category("F1")]
        public void F01_T2_ApplySlicedSprite_BVA_TargetHeights_ClampPpuAndPreventCornerOverlap()
        {
            Sprite frameSprite = EditModeTierSupport.LoadSpriteViaTheme("08_BattleTopHUD/Frame_HUD_StageWaveBanner_Clean.png");
            Assert.IsNotNull(frameSprite, "Failed to load Frame_HUD_StageWaveBanner_Clean.png.");
            Assert.Greater(frameSprite.border.y + frameSprite.border.w, 100f, "Expected large vertical border on banner sprite.");

            float[] targetHeights = { 32f, 58f, 182f, 500f };
            float[] targetWidths = { 180f, 300f, 540f, 1400f };

            GameObject go = new GameObject("TempBvaImage", typeof(RectTransform), typeof(Image));
            try
            {
                Image img = go.GetComponent<Image>();
                for (int i = 0; i < targetHeights.Length; i++)
                {
                    float tw = targetWidths[i];
                    float th = targetHeights[i];
                    EditModeTierSupport.ApplySlicedSpriteViaTheme(img, frameSprite, tw, th);

                    Assert.AreEqual(Image.Type.Sliced, img.type);
                    float ppu = img.pixelsPerUnitMultiplier;
                    Assert.GreaterOrEqual(ppu, 0.25f, $"PPU below lower clamp at {tw}x{th}.");
                    Assert.LessOrEqual(ppu, 12f, $"PPU above upper clamp at {tw}x{th}.");

                    if (th >= 58f)
                    {
                        float scaledV = (frameSprite.border.y + frameSprite.border.w) / ppu;
                        Assert.LessOrEqual(scaledV, th + 0.5f, $"Scaled vertical borders {scaledV:0.1} overlap at height {th}.");
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        [Category("Tier2")]
        [Category("F1")]
        public void F01_T2_LoadSprite_MissingOrNullPath_ReturnsNullSafely()
        {
            Assert.IsNull(EditModeTierSupport.LoadSpriteViaTheme(null), "Null sprite path should return null.");
            Assert.IsNull(EditModeTierSupport.LoadSpriteViaTheme(string.Empty), "Empty sprite path should return null.");
            Assert.IsNull(EditModeTierSupport.LoadSpriteViaTheme("01_MainMenu/NonExistent_Sprite_999.png"), "Missing sprite should return null.");
        }

        [Test]
        [Category("Tier2")]
        [Category("F1")]
        public void F01_T2_ApplySlicedSprite_ZeroSizeTarget_FallsBackToRectTransformSize()
        {
            Sprite cardSprite = EditModeTierSupport.LoadSpriteViaTheme("02_ModeSelect/Card_Mode_Campaign_BlankFrame.png");
            Assert.IsNotNull(cardSprite);

            GameObject go = new GameObject("TempFallbackSize", typeof(RectTransform), typeof(Image));
            try
            {
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(368f, 520f);
                Image img = go.GetComponent<Image>();

                EditModeTierSupport.ApplySlicedSpriteViaTheme(img, cardSprite, 0f, 0f);
                Assert.AreEqual(Image.Type.Sliced, img.type);
                Assert.GreaterOrEqual(img.pixelsPerUnitMultiplier, 0.25f);
                Assert.LessOrEqual(img.pixelsPerUnitMultiplier, 12f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ==================== F11: Builder Parity, Level Assets & Static Suite Compliance (4 Tests) ====================

        [Test]
        [Category("Tier2")]
        [Category("F11")]
        public void F11_T2_CanvasUIBuilder_CreateGameUICanvas_MatchesSceneHudContract()
        {
            Scene previous = EditorSceneManager.GetActiveScene();
            string previousPath = previous.path;

            try
            {
                Scene tempScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Type builderType = E2EEditModeHelpers.RequireEditorType("StickmanBeatEmUp.Editor.CanvasUIBuilder");
                MethodInfo createMethod = builderType.GetMethod(
                    "CreateGameUICanvas",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    Type.EmptyTypes,
                    null);
                Assert.IsNotNull(createMethod, "CanvasUIBuilder.CreateGameUICanvas() not found.");

                GameObject canvasGo = createMethod.Invoke(null, null) as GameObject;
                Assert.IsNotNull(canvasGo, "CanvasUIBuilder.CreateGameUICanvas() returned null.");

                Canvas canvas = canvasGo.GetComponent<Canvas>();
                Assert.IsNotNull(canvas, "Created GameCanvas lacks Canvas component.");
                Assert.AreEqual(0, E2EEditModeHelpers.FindMissingScriptCount(tempScene), "Created GameCanvas has missing scripts.");

                EditModeTierSupport.RequireUIElement(tempScene, "PlayerHUD");
                EditModeTierSupport.RequireUIElement(tempScene, "Rage3SegFrame");
                EditModeTierSupport.RequireUIElement(tempScene, "TrainingTopBar");
                EditModeTierSupport.RequireUIElement(tempScene, "Divider_Left");
                EditModeTierSupport.RequireUIElement(tempScene, "Divider_Right");
                EditModeTierSupport.RequireUIElement(tempScene, "TopRightClusterBg");
                EditModeTierSupport.RequireUIElement(tempScene, "EndlessWaveBanner");

                Rect playerBounds = EditModeTierSupport.GetElementBounds1920x1080(tempScene, canvas, "PlayerHUD");
                Rect trainingBounds = EditModeTierSupport.GetElementBounds1920x1080(tempScene, canvas, "TrainingTopBar");
                Rect rightBounds = EditModeTierSupport.GetElementBounds1920x1080(tempScene, canvas, "TopRightClusterBg");

                Assert.GreaterOrEqual(playerBounds.xMin, 19f);
                Assert.LessOrEqual(playerBounds.xMax, 453f);
                Assert.GreaterOrEqual(trainingBounds.xMin, 463f);
                Assert.LessOrEqual(trainingBounds.xMax, 1433f);
                Assert.GreaterOrEqual(rightBounds.xMin, 1439f);
                Assert.LessOrEqual(rightBounds.xMax, 1907f);
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousPath) && File.Exists(previousPath))
                {
                    EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
                }
            }
        }

        [Test]
        [Category("Tier2")]
        [Category("F11")]
        public void F11_T2_StageAndLevelAssets_12LevelsAcross3Worlds_Exist()
        {
            const string levelsDir = "Assets/Data/Levels";
            Assert.IsTrue(Directory.Exists(levelsDir), $"Missing Levels directory '{levelsDir}'.");

            Type levelDataType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.Level.LevelDataSO");
            Type worldTypeEnum = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.WorldType");
            string[] expectedWorldEnumNames =
            {
                "World1_RuinedCity",
                "World2_Wasteland",
                "World3_NeoCity",
            };
            int[] expectedWaveCounts = { 3, 4, 6, 2 };

            int foundCount = 0;
            for (int w = 1; w <= 3; w++)
            {
                int expectedWorldIndex = (int)Enum.Parse(worldTypeEnum, expectedWorldEnumNames[w - 1]);
                for (int s = 1; s <= 4; s++)
                {
                    string assetPath = $"{levelsDir}/Level_W{w}_S{s}.asset";
                    Assert.IsTrue(File.Exists(assetPath), $"Missing level asset '{assetPath}'.");
                    Assert.IsTrue(File.Exists(assetPath + ".meta"), $"Missing .meta for '{assetPath}'.");

                    ScriptableObject lvl = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                    Assert.IsNotNull(lvl, $"Failed to load '{assetPath}' as ScriptableObject.");
                    Assert.AreEqual(levelDataType, lvl.GetType(), $"'{assetPath}' must be LevelDataSO.");

                    int actualWorld = Convert.ToInt32(EditModeTierSupport.GetInstanceFieldValue(lvl, "worldType"));
                    Assert.AreEqual(expectedWorldIndex, actualWorld, $"worldType mismatch in '{assetPath}'.");

                    int actualStage = (int)EditModeTierSupport.GetInstanceFieldValue(lvl, "stageNumber");
                    Assert.AreEqual(s, actualStage, $"stageNumber mismatch in '{assetPath}'.");

                    string stageName = EditModeTierSupport.GetInstanceField<string>(lvl, "stageName");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(stageName), $"Empty stageName in '{assetPath}'.");
                    Assert.IsTrue(
                        stageName.Contains($"World {w}") && stageName.Contains($"Stage {s}"),
                        $"Unexpected stageName '{stageName}' in '{assetPath}'.");

                    System.Collections.IList waves = EditModeTierSupport.GetInstanceFieldValue(lvl, "waves") as System.Collections.IList;
                    Assert.IsNotNull(waves, $"waves list is null in '{assetPath}'.");
                    Assert.AreEqual(expectedWaveCounts[s - 1], waves.Count, $"Wave count mismatch in '{assetPath}'.");

                    bool isBossStage = (bool)EditModeTierSupport.GetInstanceFieldValue(lvl, "isBossStage");
                    GameObject bossPrefab = EditModeTierSupport.GetInstanceField<GameObject>(lvl, "bossPrefab");
                    if (s == 4)
                    {
                        Assert.IsTrue(isBossStage, $"Stage 4 '{assetPath}' must have isBossStage=true.");
                        Assert.IsNotNull(bossPrefab, $"Stage 4 '{assetPath}' must assign bossPrefab.");
                    }
                    else
                    {
                        Assert.IsFalse(isBossStage, $"Non-boss stage '{assetPath}' must have isBossStage=false.");
                        Assert.IsNull(bossPrefab, $"Non-boss stage '{assetPath}' must have null bossPrefab.");
                    }

                    Assert.IsNotNull(EditModeTierSupport.GetInstanceField<Sprite>(lvl, "backgroundSprite"), $"backgroundSprite missing in '{assetPath}'.");
                    Assert.IsNotNull(EditModeTierSupport.GetInstanceField<GameObject>(lvl, "supportMeleeCreepPrefab"), $"supportMeleeCreepPrefab missing in '{assetPath}'.");
                    Assert.IsNotNull(EditModeTierSupport.GetInstanceField<GameObject>(lvl, "supportRangedCreepPrefab"), $"supportRangedCreepPrefab missing in '{assetPath}'.");
                    Assert.IsNotNull(EditModeTierSupport.GetInstanceField<GameObject>(lvl, "supportElitePrefab"), $"supportElitePrefab missing in '{assetPath}'.");

                    foundCount++;
                }
            }

            Assert.AreEqual(12, foundCount, "Expected exactly 12 Level_W{1..3}_S{1..4}.asset files.");
            string[] allLevelAssets = Directory.GetFiles(levelsDir, "*.asset", SearchOption.TopDirectoryOnly);
            Assert.AreEqual(12, allLevelAssets.Length, $"Expected exactly 12 .asset files in '{levelsDir}', found {allLevelAssets.Length}.");
        }

        [Test]
        [Category("Tier2")]
        [Category("F11")]
        public void F11_T2_E2ETestSuite_HasZeroSystemLinq_ZeroUnityEditorInPlayMode_UniqueMetas()
        {
            const string e2eRoot = "Assets/Tests/E2E";
            Assert.IsTrue(Directory.Exists(e2eRoot));
            string[] csFiles = Directory.GetFiles(e2eRoot, "*.cs", SearchOption.AllDirectories);
            Assert.GreaterOrEqual(csFiles.Length, 10, "Expected >= 10 E2E test/helper .cs files.");

            Regex linqUsing = new Regex(@"^\s*using\s+System\.Linq\s*;", RegexOptions.Multiline);
            Regex linqCall = new Regex(@"\.(Where|Any|Select|ToArray|Count|FirstOrDefault)\s*\(");
            Regex unityEditorWord = new Regex(@"\bUnityEditor\b");

            HashSet<string> guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] allMetas = Directory.GetFiles("Assets", "*.meta", SearchOption.AllDirectories);
            Regex guidRegex = new Regex(@"^guid:\s*([0-9a-fA-F]{32})\s*$", RegexOptions.Multiline);
            for (int i = 0; i < allMetas.Length; i++)
            {
                string metaTxt = File.ReadAllText(allMetas[i]);
                Match m = guidRegex.Match(metaTxt);
                Assert.IsTrue(m.Success, $"Missing/invalid GUID in '{allMetas[i]}'.");
                Assert.IsTrue(guids.Add(m.Groups[1].Value), $"Duplicate GUID '{m.Groups[1].Value}' at '{allMetas[i]}'.");
            }

            for (int i = 0; i < csFiles.Length; i++)
            {
                string normalized = csFiles[i].Replace('\\', '/');
                Assert.IsTrue(File.Exists(normalized + ".meta"), $"Missing .meta for '{normalized}'.");
                string src = File.ReadAllText(normalized);
                Assert.IsFalse(linqUsing.IsMatch(src), $"Forbidden 'using System.Linq;' in '{normalized}'.");
                Assert.IsFalse(linqCall.IsMatch(src), $"Forbidden LINQ extension call in '{normalized}'.");

                if (normalized.Contains("/PlayMode/"))
                {
                    Assert.IsFalse(unityEditorWord.IsMatch(src), $"Forbidden UnityEditor reference in PlayMode file '{normalized}'.");
                }
            }
        }

        [Test]
        [Category("Tier2")]
        [Category("F11")]
        public void F11_T2_ToolsDirectory_HasExact10PythonScripts_And_NoStrayPyFiles()
        {
            string[] allPy = Directory.GetFiles(".", "*.py", SearchOption.AllDirectories);
            Assert.AreEqual(10, allPy.Length, $"Expected exactly 10 .py files under Tools/, found {allPy.Length}.");

            for (int i = 0; i < allPy.Length; i++)
            {
                string norm = allPy[i].Replace('\\', '/');
                if (norm.StartsWith("./"))
                {
                    norm = norm.Substring(2);
                }
                Assert.IsTrue(norm.StartsWith("Tools/", StringComparison.Ordinal), $"Stray Python script outside Tools/: '{norm}'.");
            }
        }
    }
}
