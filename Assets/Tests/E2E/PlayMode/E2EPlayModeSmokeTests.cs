using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StickmanBeatEmUp.Tests.E2E.PlayMode
{
    public class E2EPlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator MainMenu_LoadsCleanly_PlayButtonFound_ReflectionResolvesUIManager_NoWarnings()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            using (ScopedLogCapture logs = E2ETestHelpers.CaptureLogs())
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");

                Type uiManagerType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.UIManager");
                GameObject canvasGo = E2ETestHelpers.FindUIElement("MainMenuCanvas");
                Assert.IsNotNull(canvasGo, "MainMenuCanvas GameObject not found in MainMenu scene.");

                Component uiManagerComp = canvasGo.GetComponent(uiManagerType);
                Assert.IsNotNull(uiManagerComp, "UIManager component not found on MainMenuCanvas.");

                Canvas rootCanvas = canvasGo.GetComponent<Canvas>();
                Assert.IsNotNull(rootCanvas, "Canvas component missing on MainMenuCanvas.");

                CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
                Assert.IsNotNull(scaler, "CanvasScaler component missing on MainMenuCanvas.");
                Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
                Assert.AreEqual(1920f, scaler.referenceResolution.x, 0.01f);
                Assert.AreEqual(1080f, scaler.referenceResolution.y, 0.01f);

                GameObject playGo = E2ETestHelpers.FindUIElement("PlayButton");
                Assert.IsNotNull(playGo, "PlayButton not found in MainMenu scene.");
                Assert.IsTrue(playGo.activeInHierarchy, "PlayButton should be active on MainMenu launch.");

                Button playBtn = playGo.GetComponent<Button>();
                Assert.IsNotNull(playBtn, "Button component missing on PlayButton.");
                Assert.IsTrue(playBtn.IsInteractable(), "PlayButton should be interactable.");

                Rect playBounds = E2ETestHelpers.GetRectBoundsIn1920x1080(playGo.GetComponent<RectTransform>(), rootCanvas);
                Assert.Greater(playBounds.width, 100f, "PlayButton normalized width should be > 100px.");
                Assert.Greater(playBounds.height, 30f, "PlayButton normalized height should be > 30px.");

                GameObject modeSelectGo = E2ETestHelpers.FindUIElement("SubScreen_ModeSelect");
                Assert.IsNotNull(modeSelectGo, "SubScreen_ModeSelect should be discoverable even when inactive.");
                Assert.IsFalse(modeSelectGo.activeInHierarchy, "SubScreen_ModeSelect should start inactive on MainMenu launch.");

                logs.AssertNoWarningsOrErrors();
            }
        }

        [Test]
        public void ScopedPlayerStateBackup_IsolatesAndRestores_PlayerPrefsAndAccountsFile()
        {
            using (var outer = new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayerPrefs.SetInt("MDS_PlayerCoins", 777);
                PlayerPrefs.SetInt("SelectedCharacter", 2);
                PlayerPrefs.SetString("MDS_ActiveAccountUsername", "HeroOuter");
                PlayerPrefs.Save();

                string accountsDir = Path.GetDirectoryName(outer.AccountsFilePath);
                if (!string.IsNullOrEmpty(accountsDir) && !Directory.Exists(accountsDir))
                {
                    Directory.CreateDirectory(accountsDir);
                }

                const string outerPayload = "{\"accounts\":[{\"username\":\"HeroOuter\"}]}";
                File.WriteAllText(outer.AccountsFilePath, outerPayload);

                using (var inner = new ScopedPlayerStateBackup(clearForCleanTest: true))
                {
                    Assert.IsFalse(PlayerPrefs.HasKey("MDS_PlayerCoins"), "Inner clean backup should clear MDS_PlayerCoins.");
                    Assert.IsFalse(PlayerPrefs.HasKey("SelectedCharacter"), "Inner clean backup should clear SelectedCharacter.");
                    Assert.IsFalse(PlayerPrefs.HasKey("MDS_ActiveAccountUsername"), "Inner clean backup should clear MDS_ActiveAccountUsername.");
                    Assert.IsFalse(File.Exists(inner.AccountsFilePath), "Inner clean backup should remove stickman_accounts.json.");

                    PlayerPrefs.SetInt("MDS_PlayerCoins", 9999);
                    PlayerPrefs.SetInt("SelectedCharacter", 1);
                    PlayerPrefs.SetString("MDS_ActiveAccountUsername", "TempInnerUser");
                    PlayerPrefs.Save();

                    File.WriteAllText(inner.AccountsFilePath, "{\"accounts\":[{\"username\":\"TempInnerUser\"}]}");
                    File.WriteAllText(inner.AccountsFilePath + ".tmp", "temp-leftover");
                }

                Assert.AreEqual(777, PlayerPrefs.GetInt("MDS_PlayerCoins"), "Outer MDS_PlayerCoins was not restored.");
                Assert.AreEqual(2, PlayerPrefs.GetInt("SelectedCharacter"), "Outer SelectedCharacter was not restored.");
                Assert.AreEqual("HeroOuter", PlayerPrefs.GetString("MDS_ActiveAccountUsername"), "Outer MDS_ActiveAccountUsername was not restored.");
                Assert.IsTrue(File.Exists(outer.AccountsFilePath), "Outer stickman_accounts.json should be restored.");
                Assert.AreEqual(outerPayload, File.ReadAllText(outer.AccountsFilePath), "Outer stickman_accounts.json content mismatch.");
                Assert.IsFalse(File.Exists(outer.AccountsFilePath + ".tmp"), "Stray .tmp account file should be removed on Dispose.");
            }
        }
    }
}
