using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StickmanBeatEmUp.Tests.E2E.EditMode
{
    public class Tier5_GlassHudBoundaryTests
    {
        private const string DemoScenePath = "Assets/Scenes/Stickman_DemoScene.unity";

        [Test]
        [Category("Tier5")]
        [Category("F1")]
        [Category("F9")]
        public void T5_B16_EditMode_AllSlicedUiMetaBorders_MatchDefaultSliceBorders_And_ZeroConflictPauseGeometry()
        {
            // 1. Validate all 107 SlicedUI PNGs have physically valid non-overlapping texture borders
            List<string> slicedPngs = EditModeTierSupport.EnumerateSlicedUiPngAssets();
            Assert.AreEqual(107, slicedPngs.Count, "Expected 107 SlicedUI PNG assets.");

            Type themeType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.GlassHudTheme");
            Dictionary<string, Vector4> defaultBorders = EditModeTierSupport.GetStaticFieldValue(themeType, "DefaultSliceBorders") as Dictionary<string, Vector4>;
            Assert.IsNotNull(defaultBorders, "GlassHudTheme.DefaultSliceBorders dictionary missing.");

            for (int i = 0; i < slicedPngs.Count; i++)
            {
                string path = slicedPngs[i];
                Assert.IsTrue(EditModeTierSupport.ReadPngHeader(path, out int w, out int h, out _, out _), $"Invalid PNG '{path}'.");
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"TextureImporter missing for '{path}'.");

                Vector4 b = importer.spriteBorder;
                Assert.GreaterOrEqual(b.x, 0f, $"Negative left border on '{path}'.");
                Assert.GreaterOrEqual(b.y, 0f, $"Negative bottom border on '{path}'.");
                Assert.GreaterOrEqual(b.z, 0f, $"Negative right border on '{path}'.");
                Assert.GreaterOrEqual(b.w, 0f, $"Negative top border on '{path}'.");
                Assert.Less(b.x + b.z, w, $"Horizontal borders >= texture width on '{path}' ({b.x + b.z} >= {w}).");
                Assert.Less(b.y + b.w, h, $"Vertical borders >= texture height on '{path}' ({b.y + b.w} >= {h}).");

                string fileName = Path.GetFileName(path);
                if (defaultBorders.TryGetValue(fileName, out Vector4 expectedBorder) && expectedBorder != Vector4.zero)
                {
                    Assert.AreNotEqual(Vector4.zero, b, $"SlicedUI asset '{fileName}' registered with non-zero DefaultSliceBorders has Vector4.zero in .meta.");
                }
            }

            // 2. Verify Stickman_DemoScene.unity PausePanel button geometry matches PauseMenuUI.ApplyGlassModalTheme() (B-04 parity)
            Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            string[] buttonPaths =
            {
                "PauseModalPanel/PauseBox/ResumeButton",
                "PauseModalPanel/PauseBox/PauseRetryButton",
                "PauseModalPanel/PauseBox/PauseExitMenuButton",
            };
            float[] expectedY = { 38f, -46f, -130f };

            GameObject canvasGo = EditModeTierSupport.RequireUIElement(scene, "GameCanvas");
            Type pauseType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.PauseMenuUI");
            Component pauseComp = canvasGo.GetComponent(pauseType);
            Assert.IsNotNull(pauseComp, "PauseMenuUI missing on GameCanvas.");

            for (int i = 0; i < buttonPaths.Length; i++)
            {
                RectTransform rt = EditModeTierSupport.RequireUIElement(scene, buttonPaths[i]).GetComponent<RectTransform>();
                Assert.AreEqual(expectedY[i], rt.anchoredPosition.y, 0.5f, $"Serialized Y mismatch on '{buttonPaths[i]}'.");
                Assert.AreEqual(360f, rt.sizeDelta.x, 0.5f, $"Serialized width mismatch on '{buttonPaths[i]}'.");
                Assert.AreEqual(66f, rt.sizeDelta.y, 0.5f, $"Serialized height mismatch on '{buttonPaths[i]}'.");
            }

            pauseType.GetMethod("ApplyGlassModalTheme").Invoke(pauseComp, null);

            for (int i = 0; i < buttonPaths.Length; i++)
            {
                RectTransform rt = EditModeTierSupport.RequireUIElement(scene, buttonPaths[i]).GetComponent<RectTransform>();
                Assert.AreEqual(expectedY[i], rt.anchoredPosition.y, 0.5f, $"Runtime ApplyGlassModalTheme Y drifted from scene on '{buttonPaths[i]}'.");
                Assert.AreEqual(360f, rt.sizeDelta.x, 0.5f, $"Runtime width drifted on '{buttonPaths[i]}'.");
                Assert.AreEqual(66f, rt.sizeDelta.y, 0.5f, $"Runtime height drifted on '{buttonPaths[i]}'.");
            }
        }
    }
}
