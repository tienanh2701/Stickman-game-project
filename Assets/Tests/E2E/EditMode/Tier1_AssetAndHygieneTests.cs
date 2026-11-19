using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StickmanBeatEmUp.Tests.E2E.EditMode
{
    internal static class EditModeTierSupport
    {
        public static readonly string[] ExpectedBuildScenes =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/Stickman_DemoScene.unity",
            "Assets/Scenes/CharacterSelectScene.unity",
        };

        public static readonly string[] SlicedUiFolders =
        {
            "Assets/Sprites/UI/SlicedUI/01_MainMenu",
            "Assets/Sprites/UI/SlicedUI/02_ModeSelect",
            "Assets/Sprites/UI/SlicedUI/03_CharacterAndItemSelect",
            "Assets/Sprites/UI/SlicedUI/04_AuthRegisterLogin",
            "Assets/Sprites/UI/SlicedUI/05_Shop",
            "Assets/Sprites/UI/SlicedUI/06_InstructionsGuide",
            "Assets/Sprites/UI/SlicedUI/07_BattleVitalsHUD",
            "Assets/Sprites/UI/SlicedUI/08_BattleTopHUD",
            "Assets/Sprites/UI/SlicedUI/09_TrainingSpawnerDock",
        };

        public static readonly string[] TenWideSlicedUiAssetPaths =
        {
            "Assets/Sprites/UI/SlicedUI/05_Shop/Frame_Shop_MainPanel_Clean.png",
            "Assets/Sprites/UI/SlicedUI/05_Shop/Row_Shop_ItemStrip_Clean.png",
            "Assets/Sprites/UI/SlicedUI/05_Shop/Row_Shop_ItemStrip_WithIconSlot_Clean.png",
            "Assets/Sprites/UI/SlicedUI/06_InstructionsGuide/Frame_Guide_MainPanel_Clean.png",
            "Assets/Sprites/UI/SlicedUI/07_BattleVitalsHUD/Bar_HUD_HP_Frame_Clean.png",
            "Assets/Sprites/UI/SlicedUI/07_BattleVitalsHUD/Bar_HUD_Rage_3SegFrame_Clean.png",
            "Assets/Sprites/UI/SlicedUI/07_BattleVitalsHUD/Frame_HUD_VitalsBackplate_Clean.png",
            "Assets/Sprites/UI/SlicedUI/08_BattleTopHUD/Frame_HUD_StageWaveBanner_Clean.png",
            "Assets/Sprites/UI/SlicedUI/08_BattleTopHUD/Frame_HUD_TopRightCluster_Clean.png",
            "Assets/Sprites/UI/SlicedUI/09_TrainingSpawnerDock/Frame_Training_MainDock_Clean.png",
        };

        public static List<string> EnumerateSlicedUiPngAssets()
        {
            List<string> results = new List<string>();
            for (int f = 0; f < SlicedUiFolders.Length; f++)
            {
                string folder = SlicedUiFolders[f];
                if (!Directory.Exists(folder))
                {
                    continue;
                }
                string[] files = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < files.Length; i++)
                {
                    results.Add(files[i].Replace('\\', '/'));
                }
            }
            return results;
        }

        public static Sprite LoadSpriteViaTheme(string nameOrRelPath)
        {
            Type themeType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.GlassHudTheme");
            MethodInfo loadMethod = themeType.GetMethod(
                "LoadSprite",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(Vector4) },
                null);
            Assert.IsNotNull(loadMethod, "GlassHudTheme.LoadSprite(string, Vector4) not found.");
            return loadMethod.Invoke(null, new object[] { nameOrRelPath, default(Vector4) }) as Sprite;
        }

        public static void ApplySlicedSpriteViaTheme(Image img, Sprite sprite, float targetWidth, float targetHeight)
        {
            Type themeType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.GlassHudTheme");
            MethodInfo applyMethod = themeType.GetMethod(
                "ApplySlicedSprite",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(Image), typeof(Sprite), typeof(float), typeof(float), typeof(Color?) },
                null);
            Assert.IsNotNull(applyMethod, "GlassHudTheme.ApplySlicedSprite(Image, Sprite, float, float, Color?) not found.");
            applyMethod.Invoke(null, new object[] { img, sprite, targetWidth, targetHeight, null });
        }

        public static object GetStaticFieldValue(Type type, string fieldName)
        {
            FieldInfo fi = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(fi, $"Static field '{type.Name}.{fieldName}' not found.");
            return fi.GetValue(null);
        }

        public static object GetInstanceFieldValue(object target, string fieldName)
        {
            Assert.IsNotNull(target, $"Target instance for field '{fieldName}' is null.");
            FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(fi, $"Instance field '{target.GetType().Name}.{fieldName}' not found.");
            return fi.GetValue(target);
        }

        public static T GetInstanceField<T>(object target, string fieldName) where T : class
        {
            return GetInstanceFieldValue(target, fieldName) as T;
        }

        public static Canvas FindRootCanvas(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Canvas c = roots[i].GetComponent<Canvas>();
                if (c != null)
                {
                    return c;
                }
            }
            return null;
        }

        public static GameObject RequireUIElement(Scene scene, string nameOrPath)
        {
            GameObject go = E2EEditModeHelpers.FindUIElement(scene, nameOrPath);
            Assert.IsNotNull(go, $"Required UI element '{nameOrPath}' not found in scene '{scene.path}'.");
            return go;
        }

        public static Rect GetElementBounds1920x1080(Scene scene, Canvas canvas, string nameOrPath)
        {
            GameObject go = RequireUIElement(scene, nameOrPath);
            RectTransform rt = go.GetComponent<RectTransform>();
            Assert.IsNotNull(rt, $"Element '{nameOrPath}' lacks RectTransform.");
            return E2EEditModeHelpers.GetRectBoundsIn1920x1080(rt, canvas);
        }

        public static void AssertRectInside1920x1080(Rect rect, string label, float tolerance = 2f)
        {
            Assert.GreaterOrEqual(rect.xMin, -tolerance, $"{label} xMin={rect.xMin:0.1} extends left of 1920x1080 canvas.");
            Assert.LessOrEqual(rect.xMax, 1920f + tolerance, $"{label} xMax={rect.xMax:0.1} extends right of 1920x1080 canvas.");
            Assert.GreaterOrEqual(rect.yMin, -tolerance, $"{label} yMin={rect.yMin:0.1} extends below 1920x1080 canvas.");
            Assert.LessOrEqual(rect.yMax, 1080f + tolerance, $"{label} yMax={rect.yMax:0.1} extends above 1920x1080 canvas.");
        }

        public static void AssertRectsDoNotOverlap(Rect a, Rect b, string nameA, string nameB, float tolerance = 0.5f)
        {
            bool separatedX = a.xMax <= b.xMin + tolerance || b.xMax <= a.xMin + tolerance;
            bool separatedY = a.yMax <= b.yMin + tolerance || b.yMax <= a.yMin + tolerance;
            Assert.IsTrue(
                separatedX || separatedY,
                $"Rect overlap detected between '{nameA}' ({a.xMin:0.1}..{a.xMax:0.1}, {a.yMin:0.1}..{a.yMax:0.1}) and '{nameB}' ({b.xMin:0.1}..{b.xMax:0.1}, {b.yMin:0.1}..{b.yMax:0.1}).");
        }

        public static void AssertRectContainedIn(Rect child, Rect parent, string childName, string parentName, float tolerance = 2f)
        {
            Assert.GreaterOrEqual(child.xMin, parent.xMin - tolerance, $"'{childName}' xMin={child.xMin:0.1} < '{parentName}' xMin={parent.xMin:0.1}");
            Assert.LessOrEqual(child.xMax, parent.xMax + tolerance, $"'{childName}' xMax={child.xMax:0.1} > '{parentName}' xMax={parent.xMax:0.1}");
            Assert.GreaterOrEqual(child.yMin, parent.yMin - tolerance, $"'{childName}' yMin={child.yMin:0.1} < '{parentName}' yMin={parent.yMin:0.1}");
            Assert.LessOrEqual(child.yMax, parent.yMax + tolerance, $"'{childName}' yMax={child.yMax:0.1} > '{parentName}' yMax={parent.yMax:0.1}");
        }

        public static bool ReadPngHeader(string filePath, out int width, out int height, out byte bitDepth, out byte colorType)
        {
            width = 0;
            height = 0;
            bitDepth = 0;
            colorType = 0;

            if (!File.Exists(filePath))
            {
                return false;
            }

            byte[] header = new byte[26];
            using (FileStream fs = File.OpenRead(filePath))
            {
                int read = fs.Read(header, 0, header.Length);
                if (read < 26)
                {
                    return false;
                }
            }

            if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47)
            {
                return false;
            }

            width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            bitDepth = header[24];
            colorType = header[25];
            return width > 0 && height > 0;
        }
    }

    public class Tier1_AssetAndHygieneTests
    {
        // ==================== F1: SlicedUI Asset Indexing & 9-Slice Scaling (5 Tests) ====================

        [Test]
        [Category("Tier1")]
        [Category("F1")]
        public void F01_T1_All107SlicedUIPngs_Exist_And_LoadViaGlassHudTheme()
        {
            List<string> pngAssets = EditModeTierSupport.EnumerateSlicedUiPngAssets();
            Assert.AreEqual(107, pngAssets.Count, $"Expected 107 SlicedUI PNGs across 9 groups, found {pngAssets.Count}.");

            for (int i = 0; i < pngAssets.Count; i++)
            {
                string path = pngAssets[i];
                Assert.IsTrue(
                    EditModeTierSupport.ReadPngHeader(path, out int w, out int h, out byte bitDepth, out byte colorType),
                    $"Invalid PNG header in '{path}'.");
                Assert.Greater(w, 0, $"Width <= 0 for '{path}'.");
                Assert.Greater(h, 0, $"Height <= 0 for '{path}'.");
                Assert.AreEqual(8, bitDepth, $"Expected 8-bit PNG in '{path}'.");
                Assert.AreEqual(6, colorType, $"Expected RGBA colorType=6 in '{path}'.");

                string relUnderSliced = path.Substring("Assets/Sprites/UI/SlicedUI/".Length);
                Sprite loaded = EditModeTierSupport.LoadSpriteViaTheme(relUnderSliced);
                Assert.IsNotNull(loaded, $"GlassHudTheme.LoadSprite failed for '{relUnderSliced}'.");
                Assert.IsNotNull(loaded.texture, $"Loaded sprite texture is null for '{relUnderSliced}'.");
            }
        }

        [Test]
        [Category("Tier1")]
        [Category("F1")]
        public void F01_T1_NineSlicedUIGroups_IndexedInGlassHudTheme()
        {
            Type themeType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.GlassHudTheme");
            string[] folders = EditModeTierSupport.GetStaticFieldValue(themeType, "RelativeSearchFolders") as string[];
            Assert.IsNotNull(folders, "GlassHudTheme.RelativeSearchFolders field missing.");

            string[] requiredPrefixes =
            {
                "Sprites/UI/SlicedUI/01_MainMenu/",
                "Sprites/UI/SlicedUI/02_ModeSelect/",
                "Sprites/UI/SlicedUI/03_CharacterAndItemSelect/",
                "Sprites/UI/SlicedUI/04_AuthRegisterLogin/",
                "Sprites/UI/SlicedUI/05_Shop/",
                "Sprites/UI/SlicedUI/06_InstructionsGuide/",
                "Sprites/UI/SlicedUI/07_BattleVitalsHUD/",
                "Sprites/UI/SlicedUI/08_BattleTopHUD/",
                "Sprites/UI/SlicedUI/09_TrainingSpawnerDock/",
            };

            for (int r = 0; r < requiredPrefixes.Length; r++)
            {
                bool found = false;
                for (int f = 0; f < folders.Length; f++)
                {
                    if (string.Equals(folders[f], requiredPrefixes[r], StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                Assert.IsTrue(found, $"GlassHudTheme.RelativeSearchFolders missing '{requiredPrefixes[r]}'.");
            }
        }

        [Test]
        [Category("Tier1")]
        [Category("F1")]
        public void F01_T1_ApplySlicedSprite_ZeroBorder_SetsSimplePreserveAspect()
        {
            Sprite iconSprite = EditModeTierSupport.LoadSpriteViaTheme("02_ModeSelect/Icon_Currency_CoinXu.png");
            Assert.IsNotNull(iconSprite, "Failed to load Icon_Currency_CoinXu.png.");
            Assert.AreEqual(Vector4.zero, iconSprite.border, "Icon_Currency_CoinXu should have Vector4.zero border.");

            GameObject go = new GameObject("TempZeroBorderImage", typeof(RectTransform), typeof(Image));
            try
            {
                Image img = go.GetComponent<Image>();
                EditModeTierSupport.ApplySlicedSpriteViaTheme(img, iconSprite, 36f, 36f);

                Assert.AreEqual(Image.Type.Simple, img.type, "Zero-border sprite must use Image.Type.Simple.");
                Assert.IsTrue(img.preserveAspect, "Zero-border sprite must enforce preserveAspect=true.");
                Assert.AreEqual(1f, img.pixelsPerUnitMultiplier, 0.001f, "Zero-border sprite must use ppuMultiplier=1.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        [Category("Tier1")]
        [Category("F1")]
        public void F01_T1_ApplySlicedSprite_NonZeroBorder_SetsSlicedAndClampedPpu()
        {
            Sprite btnSprite = EditModeTierSupport.LoadSpriteViaTheme("01_MainMenu/Btn_MainMenu_Play_Clean.png");
            Assert.IsNotNull(btnSprite, "Failed to load Btn_MainMenu_Play_Clean.png.");
            Assert.AreNotEqual(Vector4.zero, btnSprite.border, "Btn_MainMenu_Play_Clean must have non-zero 9-slice border.");

            GameObject go = new GameObject("TempSlicedImage", typeof(RectTransform), typeof(Image));
            try
            {
                Image img = go.GetComponent<Image>();
                EditModeTierSupport.ApplySlicedSpriteViaTheme(img, btnSprite, 460f, 106f);

                Assert.AreEqual(Image.Type.Sliced, img.type, "Bordered sprite must use Image.Type.Sliced.");
                Assert.IsFalse(img.preserveAspect, "Sliced 9-patch sprite must disable preserveAspect.");
                Assert.GreaterOrEqual(img.pixelsPerUnitMultiplier, 0.25f);
                Assert.LessOrEqual(img.pixelsPerUnitMultiplier, 12f);

                float scaledHoriz = (btnSprite.border.x + btnSprite.border.z) / img.pixelsPerUnitMultiplier;
                float scaledVert = (btnSprite.border.y + btnSprite.border.w) / img.pixelsPerUnitMultiplier;
                Assert.LessOrEqual(scaledHoriz, 460f + 0.5f, "Scaled horizontal 9-slice borders exceed target width.");
                Assert.LessOrEqual(scaledVert, 106f + 0.5f, "Scaled vertical 9-slice borders exceed target height.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        [Category("Tier1")]
        [Category("F1")]
        public void F01_T1_ResolveSharedTmpFont_And_VietnameseTtfExist()
        {
            const string vnFontPath = "Assets/Fonts/SegoeUIBoldVN.ttf";
            Assert.IsTrue(File.Exists(vnFontPath), $"Required Vietnamese font missing: {vnFontPath}");
            Font vnFont = AssetDatabase.LoadAssetAtPath<Font>(vnFontPath);
            Assert.IsNotNull(vnFont, "AssetDatabase could not load SegoeUIBoldVN.ttf.");

            Type themeType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.GlassHudTheme");
            MethodInfo resolveMethod = themeType.GetMethod(
                "ResolveSharedTmpFont",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(Component) },
                null);
            Assert.IsNotNull(resolveMethod, "GlassHudTheme.ResolveSharedTmpFont(Component) not found.");

            object resolvedFont = resolveMethod.Invoke(null, new object[] { null });
            Assert.IsNotNull(resolvedFont, "GlassHudTheme.ResolveSharedTmpFont(null) returned null.");
        }

        // ==================== F11: Build, Scenes, PrefabBuilder, Hygiene & R3 (5 Tests) ====================

        [Test]
        [Category("Tier1")]
        [Category("F11")]
        public void F11_T1_BuildSettings_Exact3EnabledScenesInOrder()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.IsNotNull(scenes, "EditorBuildSettings.scenes is null.");
            Assert.AreEqual(
                EditModeTierSupport.ExpectedBuildScenes.Length,
                scenes.Length,
                $"Expected exactly {EditModeTierSupport.ExpectedBuildScenes.Length} scenes in EditorBuildSettings.");

            for (int i = 0; i < EditModeTierSupport.ExpectedBuildScenes.Length; i++)
            {
                Assert.IsTrue(scenes[i].enabled, $"Build scene index {i} ('{scenes[i].path}') must be enabled.");
                Assert.AreEqual(
                    EditModeTierSupport.ExpectedBuildScenes[i],
                    scenes[i].path,
                    $"Build scene index {i} path mismatch.");
            }
        }

        [Test]
        [Category("Tier1")]
        [Category("F11")]
        public void F11_T1_All3Scenes_ZeroMissingScripts_CanvasScaler1920x1080_InputSystemModule()
        {
            for (int i = 0; i < EditModeTierSupport.ExpectedBuildScenes.Length; i++)
            {
                string scenePath = EditModeTierSupport.ExpectedBuildScenes[i];
                Assert.IsTrue(File.Exists(scenePath), $"Missing scene file: {scenePath}");

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Assert.IsTrue(scene.IsValid() && scene.isLoaded, $"Failed to open '{scenePath}'.");
                Assert.AreEqual(0, E2EEditModeHelpers.FindMissingScriptCount(scene), $"Missing scripts in '{scenePath}'.");

                Canvas rootCanvas = EditModeTierSupport.FindRootCanvas(scene);
                Assert.IsNotNull(rootCanvas, $"Root Canvas not found in '{scenePath}'.");

                CanvasScaler scaler = rootCanvas.GetComponent<CanvasScaler>();
                Assert.IsNotNull(scaler, $"CanvasScaler missing in '{scenePath}'.");
                Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode, $"uiScaleMode mismatch in '{scenePath}'.");
                Assert.AreEqual(1920f, scaler.referenceResolution.x, 0.01f, $"Reference width mismatch in '{scenePath}'.");
                Assert.AreEqual(1080f, scaler.referenceResolution.y, 0.01f, $"Reference height mismatch in '{scenePath}'.");
                Assert.AreEqual(0.5f, scaler.matchWidthOrHeight, 0.01f, $"matchWidthOrHeight mismatch in '{scenePath}'.");

                EventSystem es = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    es = roots[r].GetComponentInChildren<EventSystem>(true);
                    if (es != null)
                    {
                        break;
                    }
                }
                Assert.IsNotNull(es, $"EventSystem missing in '{scenePath}'.");
                Assert.IsNotNull(es.GetComponent<InputSystemUIInputModule>(), $"InputSystemUIInputModule missing in '{scenePath}'.");
            }
        }

        [Test]
        [Category("Tier1")]
        [Category("F11")]
        public void F11_T1_PrefabBuilder_Create4WorldLevels_ThreeCallsIntact()
        {
            const string prefabBuilderPath = "Assets/Editor/PrefabBuilder.cs";
            Assert.IsTrue(File.Exists(prefabBuilderPath), $"Missing {prefabBuilderPath}.");

            string src = File.ReadAllText(prefabBuilderPath);
            string[] expectedCalls =
            {
                "Create4WorldLevels(1, WorldType.World1_RuinedCity, \"Attack on Titan\", mapW1, new Color(0.22f, 0.21f, 0.19f, 1f), w1PureTitan, w1BeastTitan, null, w1FemaleTitan, w1WarHammerBoss)",
                "Create4WorldLevels(2, WorldType.World2_Wasteland, \"Dragon Ball Wasteland\", mapW2, new Color(0.27f, 0.20f, 0.14f, 1f), w2Saibamen, w2FriezaSoldier, w2SaibamanKamikaze, w2Nappa, w2BrolyBoss)",
                "Create4WorldLevels(3, WorldType.World3_NeoCity, \"Diend Neo Ruins\", mapW3, new Color(0.16f, 0.16f, 0.24f, 1f), w3Shocker, w3Kasshin, null, w3ShadowMoon, w3MechaBoss)",
            };

            for (int i = 0; i < expectedCalls.Length; i++)
            {
                Assert.IsTrue(
                    src.Contains(expectedCalls[i]),
                    $"PrefabBuilder.cs missing Create4WorldLevels call #{i + 1}: {expectedCalls[i]}");
            }
        }

        [Test]
        [Category("Tier1")]
        [Category("F11")]
        public void F11_T1_WorkspaceHygiene_NoAsmdefInScriptsOrEditor_NoPycache_NoReleases()
        {
            string[] forbiddenAsmdefDirs = { "Assets/Scripts", "Assets/Editor" };
            for (int i = 0; i < forbiddenAsmdefDirs.Length; i++)
            {
                if (Directory.Exists(forbiddenAsmdefDirs[i]))
                {
                    string[] asmdefs = Directory.GetFiles(forbiddenAsmdefDirs[i], "*.asmdef", SearchOption.AllDirectories);
                    Assert.AreEqual(0, asmdefs.Length, $"Forbidden .asmdef inside {forbiddenAsmdefDirs[i]}.");
                }
            }

            string[] pycaches = Directory.GetDirectories(".", "__pycache__", SearchOption.AllDirectories);
            Assert.AreEqual(0, pycaches.Length, "Workspace must contain zero __pycache__ directories.");

            Assert.IsFalse(Directory.Exists("Releases"), "Releases/ directory must not exist in Stickman game.");
            Assert.IsFalse(File.Exists("Releases"), "Releases file must not exist in Stickman game.");
        }

        [Test]
        [Category("Tier1")]
        [Category("F11")]
        public void F11_T1_R3ProtectedAssets_ExistAndNonEmpty_WithNineReferenceJfifs()
        {
            string[] protectedDirs =
            {
                "Assets/Sprites/Characters",
                "Assets/Sprites/Combat",
                "Assets/Sprites/Enemies",
                "Assets/Sprites/Maps",
                "Assets/Sprites/VFX",
                "Assets/Sprites/UI/Items",
            };
            int[] expectedPngCounts = { 173, 52, 86, 30, 5, 5 };

            int inRepoProtectedCount = 0;
            for (int i = 0; i < protectedDirs.Length; i++)
            {
                Assert.IsTrue(Directory.Exists(protectedDirs[i]), $"Protected directory missing: {protectedDirs[i]}");
                string[] pngs = Directory.GetFiles(protectedDirs[i], "*.png", SearchOption.AllDirectories);
                Assert.Greater(pngs.Length, 0, $"Protected directory '{protectedDirs[i]}' has 0 PNGs.");
                Assert.AreEqual(expectedPngCounts[i], pngs.Length, $"Protected directory '{protectedDirs[i]}' PNG count mismatch.");

                for (int p = 0; p < pngs.Length; p++)
                {
                    string pngPath = pngs[p];
                    Assert.Greater(new FileInfo(pngPath).Length, 0L, $"Protected PNG empty: {pngPath}");
                    Assert.IsTrue(
                        EditModeTierSupport.ReadPngHeader(pngPath, out int pw, out int ph, out _, out _),
                        $"Invalid PNG header in protected asset: {pngPath}");
                    Assert.Greater(pw, 0, $"Protected PNG width <= 0: {pngPath}");
                    Assert.Greater(ph, 0, $"Protected PNG height <= 0: {pngPath}");
                    Assert.IsTrue(File.Exists(pngPath + ".meta"), $"Missing .meta for protected asset: {pngPath}");
                }

                string[] metas = Directory.GetFiles(protectedDirs[i], "*.meta", SearchOption.AllDirectories);
                inRepoProtectedCount += pngs.Length + metas.Length;
            }

            string[] protectedFiles =
            {
                "Assets/Sprites/UI/MenuGlobalBackground.png",
                "Assets/Sprites/UI/MainMenu_ThreeHeroes_Thumb.png",
            };
            for (int i = 0; i < protectedFiles.Length; i++)
            {
                Assert.IsTrue(File.Exists(protectedFiles[i]), $"Protected file missing: {protectedFiles[i]}");
                Assert.Greater(new FileInfo(protectedFiles[i]).Length, 0L, $"Protected file empty: {protectedFiles[i]}");
                Assert.IsTrue(
                    EditModeTierSupport.ReadPngHeader(protectedFiles[i], out int fw, out int fh, out _, out _),
                    $"Invalid PNG header in protected UI file: {protectedFiles[i]}");
                Assert.Greater(fw, 0, $"Protected UI PNG width <= 0: {protectedFiles[i]}");
                Assert.Greater(fh, 0, $"Protected UI PNG height <= 0: {protectedFiles[i]}");
                Assert.IsTrue(File.Exists(protectedFiles[i] + ".meta"), $"Missing .meta for protected UI file: {protectedFiles[i]}");
                inRepoProtectedCount += 2;
            }

            Assert.AreEqual(716, inRepoProtectedCount, $"Expected 716 in-repo R3 protected files (PNG + .meta), found {inRepoProtectedCount}.");

            // External reference .jfif check waived per official user confirmation in ORIGINAL_REQUEST.md (2026-11-19T09:54:42Z):
            // the 9 external .jfif files outside 'Stickman game/' were intentionally removed by the user.
        }
    }
}
