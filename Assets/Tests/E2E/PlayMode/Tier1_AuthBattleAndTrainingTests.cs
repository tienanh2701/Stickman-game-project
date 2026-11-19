using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StickmanBeatEmUp.Tests.E2E.PlayMode
{
    public class Tier1_AuthBattleAndTrainingTests
    {
        // ==================== F1: Runtime Sprite Indexing & 9-Slice (1 Test) ====================

        [UnityTest]
        [Category("Tier1")]
        [Category("F1")]
        public IEnumerator F01_T1_RuntimeLoadSprite_ResolvesAll9Groups_WithoutWarnings()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            using (ScopedLogCapture logs = E2ETestHelpers.CaptureLogs())
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");

                Type themeType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.GlassHudTheme");
                MethodInfo loadMethod = themeType.GetMethod(
                    "LoadSprite",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(string), typeof(Vector4) },
                    null);
                Assert.IsNotNull(loadMethod);

                string[] probes =
                {
                    "01_MainMenu/Btn_MainMenu_Play_Clean.png",
                    "02_ModeSelect/Card_Mode_Campaign_BlankFrame.png",
                    "03_CharacterAndItemSelect/Frame_CharSelect_MainPanel_Clean.png",
                    "04_AuthRegisterLogin/Frame_Auth_ModalPanel_Clean.png",
                    "05_Shop/Frame_Shop_MainPanel_Clean.png",
                    "06_InstructionsGuide/Frame_Guide_MainPanel_Clean.png",
                    "07_BattleVitalsHUD/Frame_HUD_VitalsBackplate_Clean.png",
                    "08_BattleTopHUD/Frame_HUD_TopRightCluster_Clean.png",
                    "09_TrainingSpawnerDock/Frame_Training_MainDock_Clean.png",
                };

                for (int i = 0; i < probes.Length; i++)
                {
                    Sprite sp = loadMethod.Invoke(null, new object[] { probes[i], default(Vector4) }) as Sprite;
                    Assert.IsNotNull(sp, $"Runtime GlassHudTheme.LoadSprite returned null for '{probes[i]}'.");
                }

                logs.AssertNoWarningsOrErrors();
            }
        }

        // ==================== F7: 04_AuthRegisterLogin Runtime (3 Tests) ====================

        [UnityTest]
        [Category("Tier1")]
        [Category("F7")]
        public IEnumerator F07_T1_AccountManager_TryRegister_CreatesProfileAndPersistsJson()
        {
            using (var backup = new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");

                Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                bool ok = PlayModeTierSupport.TryRegister(acctMgr, "HeroOne", "secret123", "secret123", out string err);
                Assert.IsTrue(ok, $"Expected TryRegister to succeed, got error: {err}");
                Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(acctMgr, "HasActiveSession"));

                Assert.IsTrue(File.Exists(backup.AccountsFilePath), "stickman_accounts.json must be created on disk.");
                string json = File.ReadAllText(backup.AccountsFilePath);
                Assert.IsTrue(json.Contains("HeroOne"), "Accounts JSON must record username 'HeroOne'.");
                Assert.IsFalse(json.Contains("secret123"), "Accounts JSON must NOT store plaintext password.");
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F7")]
        public IEnumerator F07_T1_AccountManager_LogoutAndTryLogin_AuthenticatesRegisteredUser()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");

                Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "LoginUser", "pass4321", "pass4321", out _));
                PlayModeTierSupport.InvokeMethod(acctMgr, "Logout");
                Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(acctMgr, "HasActiveSession"));

                bool loggedIn = PlayModeTierSupport.TryLogin(acctMgr, "LoginUser", "pass4321", out string err);
                Assert.IsTrue(loggedIn, $"TryLogin failed: {err}");
                Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(acctMgr, "HasActiveSession"));

                object activeRec = PlayModeTierSupport.GetInstanceProperty(acctMgr, "ActiveAccount");
                Assert.AreEqual("LoginUser", PlayModeTierSupport.GetInstanceField(activeRec, "username"));
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F7")]
        public IEnumerator F07_T1_AuthUI_OpensSubScreenAuth_And_SwitchesRegisterAndLoginTabs()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Auth");
                yield return null;

                GameObject authPanel = E2ETestHelpers.FindUIElement("SubScreen_Auth");
                Assert.IsNotNull(authPanel, "SubScreen_Auth not found in MainMenu.");
                Assert.IsTrue(authPanel.activeInHierarchy, "SubScreen_Auth must be active on MainMenuScreen.Auth.");

                Type authCtrlType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Auth.AuthUIController");
                Component ctrl = authPanel.GetComponent(authCtrlType);
                Assert.IsNotNull(ctrl, "AuthUIController component missing on SubScreen_Auth.");

                Button loginTab = PlayModeTierSupport.GetInstanceProperty(ctrl, "LoginTabButton") as Button;
                Button registerTab = PlayModeTierSupport.GetInstanceProperty(ctrl, "RegisterTabButton") as Button;
                TMP_InputField passInput = PlayModeTierSupport.GetInstanceProperty(ctrl, "PasswordInput") as TMP_InputField;
                TMP_InputField confirmInput = PlayModeTierSupport.GetInstanceProperty(ctrl, "ConfirmPasswordInput") as TMP_InputField;
                TMP_Text feedbackTxt = PlayModeTierSupport.GetInstanceProperty(ctrl, "FeedbackText") as TMP_Text;
                Button backBtn = PlayModeTierSupport.GetInstanceProperty(ctrl, "BackButton") as Button;
                Assert.IsNotNull(loginTab);
                Assert.IsNotNull(registerTab);
                Assert.IsNotNull(passInput);
                Assert.IsNotNull(confirmInput);
                Assert.IsNotNull(feedbackTxt);
                Assert.IsNotNull(backBtn);

                E2ETestHelpers.ClickButton(registerTab);
                yield return null;
                Assert.AreEqual("Register", PlayModeTierSupport.GetInstanceProperty(ctrl, "CurrentTab").ToString());
                Assert.IsTrue(confirmInput.gameObject.activeInHierarchy, "ConfirmPasswordInput must be visible on Register tab.");

                passInput.text = "tempPass";
                confirmInput.text = "mismatch";
                E2ETestHelpers.ClickButton(PlayModeTierSupport.GetInstanceProperty(ctrl, "SubmitButton") as Button);
                yield return null;
                Assert.IsFalse(string.IsNullOrEmpty(feedbackTxt.text), "Invalid Register submit should show feedback.");

                E2ETestHelpers.ClickButton(loginTab);
                yield return null;
                Assert.AreEqual("Login", PlayModeTierSupport.GetInstanceProperty(ctrl, "CurrentTab").ToString());
                Assert.IsFalse(confirmInput.gameObject.activeInHierarchy, "ConfirmPasswordInput must be hidden on Login tab.");
                Assert.AreEqual(string.Empty, passInput.text, "PasswordInput must reset on tab switch.");
                Assert.AreEqual(string.Empty, feedbackTxt.text, "FeedbackText must reset on tab switch.");

                E2ETestHelpers.ClickButton(backBtn);
                yield return null;
                Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen1_MainMenu").activeInHierarchy, "AuthBackButton must return to Screen1_MainMenu without soft-lock.");
            }
        }

        // ==================== F8: 07_BattleVitalsHUD Runtime (3 Tests) ====================

        [UnityTest]
        [Category("Tier1")]
        [Category("F8")]
        public IEnumerator F08_T1_CampaignLaunch_CreatesRage3SegFrame_And_DisplaysFullHP()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);

                Component vitals = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.UI.PlayerVitalsHudUI");
                Assert.IsNotNull(vitals);

                GameObject rageFrame = E2ETestHelpers.FindUIElement("RageContainer/Rage3SegFrame");
                Assert.IsNotNull(rageFrame, "PlayerVitalsHudUI must create Rage3SegFrame at runtime.");
                Assert.IsTrue(rageFrame.activeInHierarchy);

                Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                Assert.IsNotNull(player);
                float maxHp = (float)PlayModeTierSupport.GetInstanceProperty(player, "MaxHealth");
                Slider hpSlider = E2ETestHelpers.FindUIElement("PlayerHUD/PlayerHPBar").GetComponent<Slider>();
                Assert.AreEqual(maxHp, hpSlider.maxValue, 0.5f);
                Assert.AreEqual(maxHp, hpSlider.value, 0.5f);

                string blockTxt = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("BlockStatusBadge/BlockStatusText"));
                Assert.IsTrue(blockTxt.Contains("SẴN SÀNG"), $"Expected block ready label, got '{blockTxt}'.");
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F8")]
        public IEnumerator F08_T1_PlayerVitalsHud_Applies07SlicedSprites_And_StaysIn20to452Budget()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Canvas rootCanvas = E2ETestHelpers.FindUIElement("GameCanvas").GetComponent<Canvas>();
                Rect hudBounds = E2ETestHelpers.GetRectBoundsIn1920x1080(
                    E2ETestHelpers.FindUIElement("GameCanvas/PlayerHUD").GetComponent<RectTransform>(),
                    rootCanvas);

                Assert.GreaterOrEqual(hudBounds.xMin, 19f);
                Assert.LessOrEqual(hudBounds.xMax, 453f);

                Assert.AreEqual("Frame_HUD_VitalsBackplate_Clean", E2ETestHelpers.FindUIElement("PlayerHUD/VitalsBackplate").GetComponent<Image>().sprite.name);
                Assert.AreEqual("Bar_HUD_Rage_3SegFrame_Clean", E2ETestHelpers.FindUIElement("RageContainer/Rage3SegFrame").GetComponent<Image>().sprite.name);
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F8")]
        public IEnumerator F08_T1_RageContainer_HasThreeActiveSegments_And_RagePointsLabel()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                for (int i = 1; i <= 3; i++)
                {
                    GameObject seg = E2ETestHelpers.FindUIElement($"RageContainer/RageSegment_{i}");
                    Assert.IsNotNull(seg);
                    Assert.IsTrue(seg.activeInHierarchy);
                    Assert.IsNotNull(seg.GetComponent<Slider>());
                }

                string pts = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("RageContainer/RagePointsText"));
                Assert.IsTrue(pts.Contains("NỘ"), $"Expected 'NỘ' label in RagePointsText, got '{pts}'.");
            }
        }

        // ==================== F9: 08_BattleTopHUD Runtime (3 Tests) ====================

        [UnityTest]
        [Category("Tier1")]
        [Category("F9")]
        public IEnumerator F09_T1_PauseHudButton_OpensPauseModal_And_ResumeRestoresTimeScale1()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);

                Component pauseUi = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.PauseMenuUI");
                Assert.IsNotNull(pauseUi);

                E2ETestHelpers.ClickButton("GameCanvas/PauseHudButton");
                yield return null;

                Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(pauseUi, "IsPaused"));
                Assert.IsTrue(E2ETestHelpers.FindUIElement("GameCanvas/PauseModalPanel").activeInHierarchy);
                Assert.AreEqual(0f, Time.timeScale, 0.001f);

                E2ETestHelpers.ClickButton("PauseBox/ResumeButton");
                yield return null;

                Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(pauseUi, "IsPaused"));
                Assert.IsFalse(E2ETestHelpers.FindUIElement("GameCanvas/PauseModalPanel").activeInHierarchy);
                Assert.AreEqual(1f, Time.timeScale, 0.001f);
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F9")]
        public IEnumerator F09_T1_BattleCoinBadge_DisplaysCurrentCoins_WithXuSuffix()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.GrantCoins(250);
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                GameObject badge = E2ETestHelpers.FindUIElement("GameCanvas/BattleCoinBadge");
                Assert.AreEqual("Badge_HUD_InGameCoin_Clean", badge.GetComponent<Image>().sprite.name);
                string txt = PlayModeTierSupport.ReadAnyText(badge).Trim();
                Assert.AreEqual("250 Xu", txt, $"Expected '250 Xu' on BattleCoinBadge, got '{txt}'.");
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F9")]
        public IEnumerator F09_T1_ControlsHintToggleButton_FlipsControlsHintVisibility()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                GameObject hint = E2ETestHelpers.FindUIElement("GameCanvas/ControlsHint");
                Assert.IsNotNull(hint);
                bool initial = hint.activeSelf;

                E2ETestHelpers.ClickButton("GameCanvas/ControlsHintToggleButton");
                yield return null;
                Assert.AreEqual(!initial, hint.activeSelf);

                E2ETestHelpers.ClickButton("GameCanvas/ControlsHintToggleButton");
                yield return null;
                Assert.AreEqual(initial, hint.activeSelf);
            }
        }

        // ==================== F10: 09_TrainingSpawnerDock Runtime (3 Tests) ====================

        [UnityTest]
        [Category("Tier1")]
        [Category("F10")]
        public IEnumerator F10_T1_TrainingLaunch_ActivatesDock_HidesWaveBanner_CreatesDividers()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Training");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component tm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.GameModes.TrainingModeManager");
                Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(tm, "IsActive"));

                Assert.IsTrue(E2ETestHelpers.FindUIElement("GameCanvas/TrainingTopBar").activeInHierarchy);
                Assert.IsFalse(E2ETestHelpers.FindUIElement("GameCanvas/EndlessWaveBanner").activeInHierarchy);
                Assert.IsTrue(E2ETestHelpers.FindUIElement("TrainingTopBar/Divider_Left").activeInHierarchy);
                Assert.IsTrue(E2ETestHelpers.FindUIElement("Group3_SpawnAndClear/Divider_Right").activeInHierarchy);
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F10")]
        public IEnumerator F10_T1_TrainingMode_EnablesFreeUseMode_And_SuppressesEnemyRewards()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayerPrefs.SetInt("MDS_EquippedSlot_0", 1);
                PlayerPrefs.Save();
                PlayModeTierSupport.SetSelectedGameMode("Training");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component inv = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Items.InventoryManager");
                Component cm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Economy.CurrencyManager");

                Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(inv, "IsTrainingFreeUseMode"));
                Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(cm, "SuppressEnemyRewards"));

                string slotQCount = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("HudItemSlotsPanel/SlotQ/CountPill/CountText")).Trim();
                Assert.AreEqual("FREE", slotQCount, $"Expected 'FREE' in SlotQ CountText during Training, got '{slotQCount}'.");
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F10")]
        public IEnumerator F10_T1_TrainingMode_ToggleButtons_FlipPublicFlags()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Training");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component tm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.GameModes.TrainingModeManager");
                bool pInvBefore = (bool)PlayModeTierSupport.GetInstanceProperty(tm, "PlayerInvincible");
                bool rageBefore = (bool)PlayModeTierSupport.GetInstanceProperty(tm, "AutoMaxRage");
                bool aiBefore = (bool)PlayModeTierSupport.GetInstanceProperty(tm, "EnableEnemyAI");
                bool eInvBefore = (bool)PlayModeTierSupport.GetInstanceProperty(tm, "EnemyInvincible");

                E2ETestHelpers.ClickButton("Group1_PlayerControls/PlayerInvincibleButton");
                E2ETestHelpers.ClickButton("Group1_PlayerControls/AutoMaxRageButton");
                E2ETestHelpers.ClickButton("Group2_EnemyControls/EnemyAiButton");
                E2ETestHelpers.ClickButton("Group2_EnemyControls/EnemyInvincibleButton");
                yield return null;

                Assert.AreEqual(!pInvBefore, (bool)PlayModeTierSupport.GetInstanceProperty(tm, "PlayerInvincible"));
                Assert.AreEqual(!rageBefore, (bool)PlayModeTierSupport.GetInstanceProperty(tm, "AutoMaxRage"));
                Assert.AreEqual(!aiBefore, (bool)PlayModeTierSupport.GetInstanceProperty(tm, "EnableEnemyAI"));
                Assert.AreEqual(!eInvBefore, (bool)PlayModeTierSupport.GetInstanceProperty(tm, "EnemyInvincible"));
            }
        }

        // ==================== F11: Cross-Scene Boot Integrity (1 Test) ====================

        [UnityTest]
        [Category("Tier1")]
        [Category("F11")]
        public IEnumerator F11_T1_SequentialLoadOfAll3BuildScenes_SucceedsWithActiveEventSystems()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                string[] scenes = { "MainMenu", "CharacterSelectScene", "Stickman_DemoScene" };
                for (int i = 0; i < scenes.Length; i++)
                {
                    yield return E2ETestHelpers.LoadSceneAndWait(scenes[i]);
                    Assert.AreEqual(scenes[i], SceneManager.GetActiveScene().name);
                    Assert.IsNotNull(UnityEngine.EventSystems.EventSystem.current, $"EventSystem missing in '{scenes[i]}'.");
                }
            }
        }
    }
}
