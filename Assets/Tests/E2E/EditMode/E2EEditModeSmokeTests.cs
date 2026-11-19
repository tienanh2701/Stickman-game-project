using System;
using System.IO;
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
    public class E2EEditModeSmokeTests
    {
        private static readonly string[] ExpectedScenePaths =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/Stickman_DemoScene.unity",
            "Assets/Scenes/CharacterSelectScene.unity",
        };

        [Test]
        public void BuildSettings_ThreeScenesRegistered_AndCanvasScaler1920x1080_NoMissingScripts()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.IsNotNull(scenes, "EditorBuildSettings.scenes is null.");
            Assert.GreaterOrEqual(scenes.Length, ExpectedScenePaths.Length, "Expected at least 3 scenes in Build Settings.");

            for (int i = 0; i < ExpectedScenePaths.Length; i++)
            {
                Assert.IsTrue(scenes[i].enabled, $"Scene index {i} ('{scenes[i].path}') must be enabled.");
                Assert.AreEqual(ExpectedScenePaths[i], scenes[i].path, $"Scene index {i} mismatch in EditorBuildSettings.");
            }

            for (int i = 0; i < ExpectedScenePaths.Length; i++)
            {
                string scenePath = ExpectedScenePaths[i];
                Assert.IsTrue(File.Exists(scenePath), $"Scene file missing on disk: {scenePath}");

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Assert.IsTrue(scene.IsValid() && scene.isLoaded, $"Failed to open scene '{scenePath}'.");

                int missingScripts = E2EEditModeHelpers.FindMissingScriptCount(scene);
                Assert.AreEqual(0, missingScripts, $"Scene '{scenePath}' has {missingScripts} missing MonoBehaviour scripts.");

                CanvasScaler foundScaler = null;
                EventSystem foundEventSystem = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    if (foundScaler == null)
                    {
                        Canvas rootCanvas = roots[r].GetComponent<Canvas>();
                        if (rootCanvas != null)
                        {
                            foundScaler = roots[r].GetComponent<CanvasScaler>();
                        }
                    }

                    if (foundEventSystem == null)
                    {
                        foundEventSystem = roots[r].GetComponentInChildren<EventSystem>(true);
                    }
                }

                Assert.IsNotNull(foundScaler, $"Root CanvasScaler not found in scene '{scenePath}'.");
                Assert.AreEqual(
                    CanvasScaler.ScaleMode.ScaleWithScreenSize,
                    foundScaler.uiScaleMode,
                    $"CanvasScaler uiScaleMode mismatch in '{scenePath}'.");
                Assert.AreEqual(1920f, foundScaler.referenceResolution.x, 0.01f, $"CanvasScaler width mismatch in '{scenePath}'.");
                Assert.AreEqual(1080f, foundScaler.referenceResolution.y, 0.01f, $"CanvasScaler height mismatch in '{scenePath}'.");
                Assert.AreEqual(0.5f, foundScaler.matchWidthOrHeight, 0.01f, $"CanvasScaler matchWidthOrHeight mismatch in '{scenePath}'.");

                Assert.IsNotNull(foundEventSystem, $"EventSystem not found in scene '{scenePath}'.");
                InputSystemUIInputModule inputModule = foundEventSystem.GetComponent<InputSystemUIInputModule>();
                Assert.IsNotNull(inputModule, $"InputSystemUIInputModule missing on EventSystem in '{scenePath}'.");
            }
        }

        [Test]
        public void PrefabBuilder_Create4WorldLevelsIntact_AndRuntimeTypesResolvable()
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
                    $"PrefabBuilder.cs is missing required Create4WorldLevels call #{i + 1}: {expectedCalls[i]}");
            }

            Type uiManagerType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.UIManager");
            Type builderType = E2EEditModeHelpers.RequireEditorType("StickmanBeatEmUp.Editor.MainMenuSceneBuilder");

            Assert.AreEqual("UIManager", uiManagerType.Name);
            Assert.AreEqual("MainMenuSceneBuilder", builderType.Name);
        }
    }
}
