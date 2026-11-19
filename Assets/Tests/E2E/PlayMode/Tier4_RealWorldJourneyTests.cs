using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StickmanBeatEmUp.Tests.E2E.PlayMode
{
    public class Tier4_RealWorldJourneyTests
    {
        // Journey 1 — Full Multi-Account Player Lifecycle & Cross-Session Persistence
        [UnityTest]
        [Category("Tier4")]
        [Category("F1")]
        [Category("F2")]
        [Category("F3")]
        [Category("F4")]
        [Category("F5")]
        [Category("F7")]
        [Category("F10")]
        [Category("F11")]
        public IEnumerator T4_J01_MultiAccount_PlayerLifecycle_And_CrossSessionPersistence()
        {
            using (var backup = new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                    Assert.AreEqual("Chưa đăng nhập", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("AccountStatusPill/AccountStatusText")).Trim());

                    // Register Account A via PlayButton -> SubScreen_Auth
                    E2ETestHelpers.ClickButton("Screen1_MainMenu/PlayButton");
                    yield return null;
                    Assert.IsTrue(E2ETestHelpers.FindUIElement("SubScreen_Auth").activeInHierarchy);

                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputUsername", "JourneyUserA");
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputPassword", "alphaPass1");
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputConfirmPassword", "alphaPass1");
                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthSubmitButton");
                    yield return null;
                    Assert.IsTrue(E2ETestHelpers.FindUIElement("SubScreen_ModeSelect").activeInHierarchy);

                    // Earn & spend coins in Shop
                    PlayModeTierSupport.GrantCoins(140);
                    E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Shop");
                    yield return null;
                    E2ETestHelpers.ClickButton("ShopRow_SenzuBean/BuyButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("ShopRow_SpinalFluid/BuyButton");
                    yield return null;
                    Assert.AreEqual(50, PlayModeTierSupport.GetCoins());

                    // Verify CharSelect loadout & enter Training -> return to MainMenu
                    E2ETestHelpers.ClickButton("MainMenuCanvas/BackButton_FixedTopLeft");
                    yield return null;
                    E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Training");
                    yield return null;
                    Assert.AreEqual("SenzuBean", PlayModeTierSupport.GetEquippedItemName(0));
                    Assert.AreEqual("SpinalFluid", PlayModeTierSupport.GetEquippedItemName(1));
                    E2ETestHelpers.ClickButton("Screen2_CharacterSelect/CharacterNextButton");
                    yield return Tier34Support.WaitForScene("Stickman_DemoScene");

                    Assert.IsTrue(E2ETestHelpers.FindUIElement("GameCanvas/TrainingTopBar").activeInHierarchy);
                    E2ETestHelpers.ClickButton("GameCanvas/HudMenuButton");
                    yield return Tier34Support.WaitForScene("MainMenu");

                    // Logout Account A & Register Account B
                    E2ETestHelpers.ClickButton("AccountStatusPill/AccountLogoutButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("AccountStatusPill/AccountActionButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/TabRegisterButton");
                    yield return null;
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputUsername", "JourneyUserB");
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputPassword", "betaPass2");
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputConfirmPassword", "betaPass2");
                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthSubmitButton");
                    yield return null;

                    Assert.AreEqual(0, PlayModeTierSupport.GetCoins(), "Account B must have 0 Xu.");
                    Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SenzuBean"));
                    Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SpinalFluid"));
                    Assert.AreEqual(1, PlayerPrefs.GetInt("Stickman_UnlockedStage", 1));

                    // Cold-reload AccountManager from stickman_accounts.json and login as Account A
                    Assert.IsTrue(File.Exists(backup.AccountsFilePath));
                    Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                    UnityEngine.Object.DestroyImmediate(acctMgr.gameObject);
                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");

                    E2ETestHelpers.ClickButton("AccountStatusPill/AccountActionButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/TabLoginButton");
                    yield return null;
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputUsername", "JourneyUserA");
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputPassword", "alphaPass1");
                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthSubmitButton");
                    yield return null;

                    Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen1_MainMenu").activeInHierarchy, "Returning from Auth pill must land cleanly on MainMenu.");
                    Assert.AreEqual(50, PlayModeTierSupport.GetCoins());
                    Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SenzuBean"));
                    Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SpinalFluid"));
                    Assert.IsTrue(PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("AccountStatusPill/AccountStatusText")).Contains("JourneyUserA"));
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // Journey 2 — Pre-Match Shop, Loadout, World/Stage Select -> Campaign Launch
        [UnityTest]
        [Category("Tier4")]
        [Category("F2")]
        [Category("F3")]
        [Category("F4")]
        [Category("F5")]
        [Category("F8")]
        [Category("F9")]
        public IEnumerator T4_J02_PreMatch_Shop_Loadout_WorldStageSelect_To_CampaignLaunch()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                    Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                    Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "JourneyUser2", "pass1234", "pass1234", out string err), err);

                    PlayModeTierSupport.GrantCoins(120);
                    E2ETestHelpers.ClickButton("Screen1_MainMenu/PlayButton");
                    yield return null;

                    // ModeSelect -> Shop purchase
                    E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Shop");
                    yield return null;
                    E2ETestHelpers.ClickButton("ShopRow_SenzuBean/BuyButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("ShopRow_SpinalFluid/BuyButton");
                    yield return null;
                    Assert.AreEqual(30, PlayModeTierSupport.GetCoins());

                    // Back -> Campaign -> CharSelect (select Eren, toggle preview form, cycle Q/E)
                    E2ETestHelpers.ClickButton("MainMenuCanvas/BackButton_FixedTopLeft");
                    yield return null;
                    E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Campaign");
                    yield return null;

                    E2ETestHelpers.ClickButton("CharCard_Eren/CardInner");
                    yield return null;
                    Assert.AreEqual(1, PlayerPrefs.GetInt("SelectedCharacter", -1));

                    Image previewImg = E2ETestHelpers.FindUIElement("Screen2_CharacterSelect/BgCharacterPreview").GetComponent<Image>();
                    Sprite baseSprite = previewImg.sprite;
                    E2ETestHelpers.ClickButton("Screen2_CharacterSelect/TogglePreviewFormButton");
                    yield return null;
                    Assert.AreNotEqual(baseSprite, previewImg.sprite);

                    E2ETestHelpers.ClickButton("LoadoutBar/LoadoutSlotQ/CircleSlot");
                    yield return null;
                    E2ETestHelpers.ClickButton("LoadoutBar/LoadoutSlotE/CircleSlot");
                    yield return null;
                    Assert.AreEqual("None", PlayModeTierSupport.GetEquippedItemName(1), "Cycling E past last owned item must allow unequipping to None.");
                    E2ETestHelpers.ClickButton("LoadoutBar/LoadoutSlotE/CircleSlot");
                    yield return null;
                    Assert.AreEqual("SpinalFluid", PlayModeTierSupport.GetEquippedItemName(0));
                    Assert.AreEqual("SenzuBean", PlayModeTierSupport.GetEquippedItemName(1));

                    E2ETestHelpers.ClickButton("Screen2_CharacterSelect/CharacterNextButton");
                    yield return null;

                    // WorldSelect (World 2) -> StageSelect (Stage 1) -> Stickman_DemoScene
                    E2ETestHelpers.ClickButton("WorldCard_THẾ GIỚI 2_DRAGON BALL/CardInner");
                    yield return null;
                    E2ETestHelpers.ClickButton("Screen3_WorldSelect/WorldNextButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("Screen4_StageSelect/StageCard_STAGE 1");
                    yield return Tier34Support.WaitForScene("Stickman_DemoScene");

                    Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                    Assert.AreEqual("Campaign", PlayModeTierSupport.GetInstanceProperty(levelMgr, "CurrentGameMode").ToString());
                    Assert.AreEqual(4, PlayerPrefs.GetInt("SelectedStageIndex", -1));
                    Assert.AreEqual(1, PlayerPrefs.GetInt("SelectedCharacter", -1));
                    Assert.IsTrue(E2ETestHelpers.FindUIElement("GameCanvas/PlayerHUD").activeInHierarchy);
                    Assert.IsFalse(E2ETestHelpers.FindUIElement("GameCanvas/TrainingTopBar").activeInHierarchy);
                    Assert.AreEqual("30 Xu", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("GameCanvas/BattleCoinBadge/CoinsText")).Trim());
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // Journey 3 — Battle Vitals HUD, Q/E Consumable, Pause, Retry (no item refund) & Exit to MainMenu
        [UnityTest]
        [Category("Tier4")]
        [Category("F4")]
        [Category("F5")]
        [Category("F8")]
        [Category("F9")]
        [Category("F11")]
        public IEnumerator T4_J03_BattleVitals_QEConsumable_Pause_RetryNoRefund_And_ExitToMainMenu()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    PlayModeTierSupport.AddOwnedItem("SenzuBean", 1);
                    PlayModeTierSupport.AddOwnedItem("SpinalFluid", 1);
                    PlayerPrefs.SetInt("MDS_EquippedSlot_0", 1);
                    PlayerPrefs.SetInt("MDS_EquippedSlot_1", 2);
                    PlayerPrefs.SetInt("SelectedStageIndex", 0);
                    PlayerPrefs.Save();

                    PlayModeTierSupport.SetSelectedGameMode("Campaign");
                    yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                    Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                    PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                    Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                    Component rageMgr = PlayModeTierSupport.GetInstanceProperty(player, "RageManager") as Component;
                    Component itemCtrl = Tier34Support.RequirePlayerItemController(player);

                    PlayModeTierSupport.ApplyFatalDamageToPlayer(player, 30f);
                    PlayModeTierSupport.InvokeMethod(rageMgr, "AddRageFloat", 15f);
                    yield return null;

                    float damagedHp = (float)PlayModeTierSupport.GetInstanceProperty(player, "CurrentHealth");
                    Slider seg1 = E2ETestHelpers.FindUIElement("RageContainer/RageSegment_1").GetComponent<Slider>();
                    Assert.GreaterOrEqual(seg1.normalizedValue, 0.99f);

                    // Use Q slot -> HP heals and owned decrements
                    Assert.IsTrue((bool)PlayModeTierSupport.InvokeMethod(itemCtrl, "TryUseSlotQ"));
                    yield return null;
                    Assert.Greater((float)PlayModeTierSupport.GetInstanceProperty(player, "CurrentHealth"), damagedHp);
                    Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SenzuBean"));

                    // Pause -> Resume
                    E2ETestHelpers.ClickButton("GameCanvas/PauseHudButton");
                    yield return null;
                    Assert.AreEqual(0f, Time.timeScale, 0.001f);
                    Assert.IsTrue(E2ETestHelpers.FindUIElement("GameCanvas/PauseModalPanel").activeSelf);

                    E2ETestHelpers.ClickButton("PauseBox/ResumeButton");
                    yield return null;
                    Assert.AreEqual(1f, Time.timeScale, 0.001f);

                    // Pause -> Retry (wait for fresh ActivePlayer instance & verify no item refund)
                    Component prevPlayer = player;
                    E2ETestHelpers.ClickButton("GameCanvas/PauseHudButton");
                    yield return null;
                    Assert.AreEqual(0f, Time.timeScale, 0.001f);
                    E2ETestHelpers.ClickButton("PauseBox/PauseRetryButton");

                    yield return E2ETestHelpers.WaitForCondition(
                        () =>
                        {
                            Component lm = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.Level.LevelManager");
                            if (lm == null) return false;
                            Component ap = PlayModeTierSupport.GetInstanceProperty(lm, "ActivePlayer") as Component;
                            return ap != null && !ReferenceEquals(ap, prevPlayer);
                        },
                        8f,
                        "Expected Stickman_DemoScene reload with fresh ActivePlayer after PauseRetryButton.");
                    yield return null;
                    yield return null;
                    Canvas.ForceUpdateCanvases();

                    Assert.AreEqual(1f, Time.timeScale, 0.001f);
                    Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SenzuBean"), "Consumed SenzuBean must NOT be refunded after Retry.");

                    // Pause -> ExitMatch -> returns to MainMenu
                    E2ETestHelpers.ClickButton("GameCanvas/PauseHudButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("PauseBox/PauseExitMenuButton");
                    yield return Tier34Support.WaitForScene("MainMenu");
                    Assert.AreEqual(1f, Time.timeScale, 0.001f);
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // Journey 4 — Campaign Victory, Stage Progression, Stage 4 Boss & Defeat Summary
        [UnityTest]
        [Category("Tier4")]
        [Category("F8")]
        [Category("F9")]
        [Category("F11")]
        public IEnumerator T4_J04_CampaignVictory_StageProgression_Stage4Boss_And_DefeatSummary()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    // Stage 1 Victory
                    PlayerPrefs.SetInt("Stickman_UnlockedStage", 1);
                    PlayerPrefs.SetInt("SelectedStageIndex", 0);
                    PlayerPrefs.Save();
                    PlayModeTierSupport.SetSelectedGameMode("Campaign");
                    yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                    Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                    PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                    Component cm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Economy.CurrencyManager");
                    Tier34Support.AddCoinsExact(cm, 40);
                    yield return null;

                    Tier34Support.CompleteCurrentStage(levelMgr);
                    yield return null;
                    yield return null;

                    GameObject victoryPanel = E2ETestHelpers.FindUIElement("GameCanvas/VictoryPanel");
                    Assert.IsTrue(victoryPanel.activeSelf, "VictoryPanel must open on Stage 1 clear.");
                    string rewardTxt = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("VictoryModalCard/VictoryRewardBadge/RewardText"));
                    Assert.IsTrue(rewardTxt.Contains("+40 Xu") && rewardTxt.Contains("Tổng: 40 Xu"), $"Unexpected VictoryRewardBadge text: '{rewardTxt}'.");
                    Assert.IsTrue(E2ETestHelpers.FindUIElement("VictoryModalCard/NextStageButton").activeSelf, "NextStageButton must be visible on Stage 1.");
                    Assert.GreaterOrEqual(PlayerPrefs.GetInt("Stickman_UnlockedStage", 1), 2);

                    // Stage 4 Boss Victory (index 3 -> stageNumber 4 / isBossStage)
                    PlayerPrefs.SetInt("SelectedStageIndex", 3);
                    PlayerPrefs.Save();
                    yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                    levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                    PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                    object stage4Data = PlayModeTierSupport.GetInstanceProperty(levelMgr, "CurrentStageData");
                    Assert.AreEqual(4, (int)PlayModeTierSupport.GetInstanceField(stage4Data, "stageNumber"));

                    Tier34Support.CompleteCurrentStage(levelMgr);
                    yield return null;
                    yield return null;

                    Assert.IsTrue(E2ETestHelpers.FindUIElement("GameCanvas/VictoryPanel").activeSelf);
                    Assert.IsFalse(E2ETestHelpers.FindUIElement("VictoryModalCard/NextStageButton").activeSelf, "NextStageButton must be hidden on Stage 4 Boss.");

                    // Defeat Flow -> DefeatPanel & DefeatSummaryPill
                    PlayerPrefs.SetInt("SelectedStageIndex", 0);
                    PlayerPrefs.Save();
                    yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                    levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                    PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                    cm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Economy.CurrencyManager");
                    Tier34Support.AddCoinsExact(cm, 15);
                    yield return null;

                    Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                    Component gameUi = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.UI.GameUIManager");
                    PlayModeTierSupport.SetInstanceProperty(player, "TrainingInvincible", false);
                    PlayModeTierSupport.ApplyFatalDamageToPlayer(player, 10000f);

                    yield return E2ETestHelpers.WaitForCondition(
                        () => (bool)PlayModeTierSupport.GetInstanceProperty(gameUi, "IsEndScreenOpen"),
                        4f,
                        "Expected DefeatPanel to open after fatal damage.");
                    yield return null;

                    Assert.IsTrue(E2ETestHelpers.FindUIElement("GameCanvas/DefeatPanel").activeSelf);
                    string defeatSummary = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("DefeatModalCard/DefeatSummaryPill/SummaryText"));
                    Assert.IsTrue(defeatSummary.Contains("Màn 1") && defeatSummary.Contains("+15 Xu"), $"Unexpected DefeatSummaryPill text: '{defeatSummary}'.");

                    E2ETestHelpers.ClickButton("DefeatModalCard/DefeatMenuButton");
                    yield return Tier34Support.WaitForScene("MainMenu");
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // Journey 5 — Endless Mode Wave Progression & Defeat Summary Journey
        [UnityTest]
        [Category("Tier4")]
        [Category("F3")]
        [Category("F8")]
        [Category("F9")]
        [Category("F11")]
        public IEnumerator T4_J05_EndlessMode_WaveProgression_Coins_DefeatSummary_And_MenuPersistence()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                    Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                    Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "EndlessHero", "pass1234", "pass1234", out string err), err);

                    E2ETestHelpers.ClickButton("Screen1_MainMenu/PlayButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Endless");
                    yield return null;
                    E2ETestHelpers.ClickButton("Screen2_CharacterSelect/CharacterNextButton");
                    yield return Tier34Support.WaitForScene("Stickman_DemoScene");

                    GameObject banner = E2ETestHelpers.FindUIElement("GameCanvas/EndlessWaveBanner");
                    Assert.IsTrue(banner.activeInHierarchy);
                    Assert.IsTrue(PlayModeTierSupport.ReadAnyText(banner).Contains("VÔ TẬN — WAVE 1"));
                    Assert.IsFalse(E2ETestHelpers.FindUIElement("GameCanvas/TrainingTopBar").activeInHierarchy);

                    Component endlessMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.GameModes.EndlessModeManager");
                    PlayModeTierSupport.InvokeMethod(endlessMgr, "StartNextWave");
                    yield return null;
                    Assert.AreEqual(2, (int)PlayModeTierSupport.GetInstanceProperty(endlessMgr, "CurrentWave"));

                    Component cm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Economy.CurrencyManager");
                    Tier34Support.AddCoinsExact(cm, 50);
                    yield return E2ETestHelpers.WaitForCondition(
                        () => string.Equals(PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("GameCanvas/BattleCoinBadge/CoinsText")).Trim(), "50 Xu", StringComparison.Ordinal),
                        3f,
                        "BattleCoinBadge did not reach '50 Xu'");
                    Assert.AreEqual("50 Xu", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("GameCanvas/BattleCoinBadge/CoinsText")).Trim());

                    Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                    Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                    Component gameUi = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.UI.GameUIManager");
                    PlayModeTierSupport.SetInstanceProperty(player, "TrainingInvincible", false);
                    PlayModeTierSupport.ApplyFatalDamageToPlayer(player, 10000f);

                    yield return E2ETestHelpers.WaitForCondition(
                        () => (bool)PlayModeTierSupport.GetInstanceProperty(gameUi, "IsEndScreenOpen"),
                        4f,
                        "Expected Endless DefeatPanel.");
                    yield return null;

                    string summary = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("DefeatModalCard/DefeatSummaryPill/SummaryText"));
                    Assert.IsTrue(summary.Contains("Đạt Wave 2") && summary.Contains("+50 Xu"), $"Unexpected Endless DefeatSummaryPill: '{summary}'.");

                    E2ETestHelpers.ClickButton("DefeatModalCard/DefeatMenuButton");
                    yield return Tier34Support.WaitForScene("MainMenu");
                    Assert.AreEqual(50, PlayModeTierSupport.GetCoins());
                    Assert.AreEqual("50 Xu", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("MainMenuCanvas/PersistentCoinBadge/CoinsText")).Trim());
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // Journey 6 — Training Mode Sandbox Dock, 4 Option Pills, Free-Use Q/E & ClearAll
        [UnityTest]
        [Category("Tier4")]
        [Category("F3")]
        [Category("F8")]
        [Category("F9")]
        [Category("F10")]
        [Category("F11")]
        public IEnumerator T4_J06_TrainingMode_SpawnerDock_4OptionPills_FreeUseQE_And_ClearAll()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                    Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                    Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "TrainSandboxHero", "pass1234", "pass1234", out string err), err);

                    PlayModeTierSupport.AddOwnedItem("SenzuBean", 1);
                    PlayModeTierSupport.AddOwnedItem("SpinalFluid", 1);
                    PlayerPrefs.SetInt("MDS_EquippedSlot_0", 1);
                    PlayerPrefs.SetInt("MDS_EquippedSlot_1", 2);
                    PlayerPrefs.Save();
                    Tier34Support.SyncActiveProfile(acctMgr);

                    E2ETestHelpers.ClickButton("Screen1_MainMenu/PlayButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Training");
                    yield return null;
                    E2ETestHelpers.ClickButton("Screen2_CharacterSelect/CharacterNextButton");
                    yield return Tier34Support.WaitForScene("Stickman_DemoScene");

                    GameObject trainingBar = E2ETestHelpers.FindUIElement("GameCanvas/TrainingTopBar");
                    Assert.IsTrue(trainingBar.activeInHierarchy);
                    Canvas rootCanvas = E2ETestHelpers.FindUIElement("GameCanvas").GetComponent<Canvas>();
                    Rect dockBounds = E2ETestHelpers.GetRectBoundsIn1920x1080(trainingBar.GetComponent<RectTransform>(), rootCanvas);
                    Assert.GreaterOrEqual(dockBounds.xMin, 464f - 2f, $"TrainingTopBar xMin={dockBounds.xMin} intrudes into left HUD.");
                    Assert.LessOrEqual(dockBounds.xMax, 1432f + 2f, $"TrainingTopBar xMax={dockBounds.xMax} intrudes into right HUD.");

                    Assert.AreEqual("FREE", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("HudItemSlotsPanel/SlotQ/CountPill/CountText")).Trim());
                    Assert.AreEqual("FREE", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("HudItemSlotsPanel/SlotE/CountPill/CountText")).Trim());

                    // Spawn units from SpawnUnit_0..12
                    Component tm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.GameModes.TrainingModeManager");
                    int coinsBefore = PlayModeTierSupport.GetCoins();
                    E2ETestHelpers.ClickButton("Group3_SpawnAndClear/SpawnListScroll/Viewport/Content/SpawnUnit_0");
                    E2ETestHelpers.ClickButton("Group3_SpawnAndClear/SpawnListScroll/Viewport/Content/SpawnUnit_12");
                    yield return null;

                    List<GameObject> spawned = PlayModeTierSupport.GetInstanceField(tm, "spawnedUnits") as List<GameObject>;
                    Assert.IsNotNull(spawned);
                    Assert.AreEqual(2, spawned.Count, "Expected 2 spawned units from SpawnUnit_0 and SpawnUnit_12.");

                    // Toggle all 4 option pills
                    bool initInv = (bool)PlayModeTierSupport.GetInstanceProperty(tm, "PlayerInvincible");
                    bool initRage = (bool)PlayModeTierSupport.GetInstanceProperty(tm, "AutoMaxRage");
                    bool initAi = (bool)PlayModeTierSupport.GetInstanceProperty(tm, "EnableEnemyAI");
                    bool initEnemyInv = (bool)PlayModeTierSupport.GetInstanceProperty(tm, "EnemyInvincible");

                    E2ETestHelpers.ClickButton("Group1_PlayerControls/PlayerInvincibleButton");
                    E2ETestHelpers.ClickButton("Group1_PlayerControls/AutoMaxRageButton");
                    E2ETestHelpers.ClickButton("Group2_EnemyControls/EnemyAiButton");
                    E2ETestHelpers.ClickButton("Group2_EnemyControls/EnemyInvincibleButton");
                    yield return null;

                    Assert.AreNotEqual(initInv, (bool)PlayModeTierSupport.GetInstanceProperty(tm, "PlayerInvincible"));
                    Assert.AreNotEqual(initRage, (bool)PlayModeTierSupport.GetInstanceProperty(tm, "AutoMaxRage"));
                    Assert.AreNotEqual(initAi, (bool)PlayModeTierSupport.GetInstanceProperty(tm, "EnableEnemyAI"));
                    Assert.AreNotEqual(initEnemyInv, (bool)PlayModeTierSupport.GetInstanceProperty(tm, "EnemyInvincible"));

                    // Free-use Q & E slots must NOT decrement owned items
                    Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                    Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                    Component itemCtrl = Tier34Support.RequirePlayerItemController(player);
                    Assert.IsTrue((bool)PlayModeTierSupport.InvokeMethod(itemCtrl, "TryUseSlotQ"));
                    Assert.IsTrue((bool)PlayModeTierSupport.InvokeMethod(itemCtrl, "TryUseSlotE"));
                    yield return null;
                    Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SenzuBean"));
                    Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SpinalFluid"));

                    // ClearAllEnemies -> 0 enemies & 0 Xu gained
                    E2ETestHelpers.ClickButton("Group3_SpawnAndClear/ClearAllEnemiesButton");
                    yield return null;
                    yield return null;

                    int alive = 0;
                    for (int i = 0; i < spawned.Count; i++)
                    {
                        if (spawned[i] != null) alive++;
                    }
                    Assert.AreEqual(0, alive, "ClearAllEnemiesButton must remove all spawned units.");
                    Assert.AreEqual(coinsBefore, PlayModeTierSupport.GetCoins(), "Training mode must award 0 Xu.");

                    // ExitMatch to MainMenu -> verify clean flag reset
                    E2ETestHelpers.ClickButton("GameCanvas/HudMenuButton");
                    yield return Tier34Support.WaitForScene("MainMenu");
                    Component cm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Economy.CurrencyManager");
                    Component inv = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Items.InventoryManager");
                    Assert.AreEqual(1f, Time.timeScale, 0.001f);
                    Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(cm, "SuppressEnemyRewards"));
                    Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(inv, "IsTrainingFreeUseMode"));
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }
    }
}
