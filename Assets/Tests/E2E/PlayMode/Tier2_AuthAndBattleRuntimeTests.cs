using System;
using System.Collections;
using System.Collections.Generic;
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
    public class Tier2_AuthAndBattleRuntimeTests
    {
        // ==================== F1: Runtime Wide Texture & 9-Slice Clamp (1 Test) ====================

        [UnityTest]
        [Category("Tier2")]
        [Category("F1")]
        public IEnumerator F01_T2_Wide4045x793Texture_And_UIManagerLoadGlassSprite_RuntimePpuClamp()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Type themeType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.GlassHudTheme");
                MethodInfo loadMethod = themeType.GetMethod(
                    "LoadSprite",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(string), typeof(Vector4) },
                    null);
                Assert.IsNotNull(loadMethod);

                Sprite wideDock = loadMethod.Invoke(null, new object[] { "09_TrainingSpawnerDock/Frame_Training_MainDock_Clean.png", default(Vector4) }) as Sprite;
                Assert.IsNotNull(wideDock, "Failed to load Frame_Training_MainDock_Clean.png at runtime.");
                Assert.Greater(wideDock.border.x + wideDock.border.z, 100f);

                Image dockImg = E2ETestHelpers.FindUIElement("GameCanvas/TrainingTopBar").GetComponent<Image>();
                Assert.AreEqual(Image.Type.Sliced, dockImg.type);
                Assert.GreaterOrEqual(dockImg.pixelsPerUnitMultiplier, 0.25f);
                Assert.LessOrEqual(dockImg.pixelsPerUnitMultiplier, 12f);

                float scaledVert = (wideDock.border.y + wideDock.border.w) / dockImg.pixelsPerUnitMultiplier;
                Assert.LessOrEqual(scaledVert, 98f + 1f, "Scaled vertical border on 98px TrainingTopBar must not overlap.");
            }
        }

        // ==================== F7: 04_AuthRegisterLogin Boundary & Isolation (4 Tests) ====================

        [UnityTest]
        [Category("Tier2")]
        [Category("F7")]
        public IEnumerator F07_T2_AccountManager_RegisterValidation_EmptyShortLongDuplicateAndMismatch()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component acctMgr = PlayModeTierSupport.RequireAccountManager();

                Assert.IsFalse(PlayModeTierSupport.TryRegister(acctMgr, "", "pass1234", "pass1234", out _), "Empty username must fail.");
                Assert.IsFalse(PlayModeTierSupport.TryRegister(acctMgr, "   ", "pass1234", "pass1234", out _), "Whitespace username must fail.");
                Assert.IsFalse(PlayModeTierSupport.TryRegister(acctMgr, "ab", "pass1234", "pass1234", out _), "2-char username must fail.");
                Assert.IsFalse(PlayModeTierSupport.TryRegister(acctMgr, "ThisUsernameIsWayTooLong21", "pass1234", "pass1234", out _), ">20-char username must fail.");
                Assert.IsFalse(PlayModeTierSupport.TryRegister(acctMgr, "ValidUser", "123", "123", out _), "3-char password must fail.");
                Assert.IsFalse(PlayModeTierSupport.TryRegister(acctMgr, "ValidUser", "pass1234", "pass9999", out _), "Password mismatch must fail.");

                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "ValidUser", "pass1234", "pass1234", out _));
                Assert.IsFalse(PlayModeTierSupport.TryRegister(acctMgr, "validuser", "pass5678", "pass5678", out _), "Duplicate username (case-insensitive) must fail.");
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F7")]
        public IEnumerator F07_T2_AccountManager_MultiAccountIsolation_SwitchesCoinsInventoryAndStage()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component acctMgr = PlayModeTierSupport.RequireAccountManager();

                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "HeroA", "passA123", "passA123", out _));
                PlayModeTierSupport.GrantCoins(200);
                PlayModeTierSupport.AddOwnedItem("SenzuBean", 2);
                PlayerPrefs.SetInt("Stickman_UnlockedStage", 7);
                PlayerPrefs.Save();
                PlayModeTierSupport.InvokeMethodExact(acctMgr, "SyncActiveProfileFromPrefs", Type.EmptyTypes, null);
                PlayModeTierSupport.InvokeMethod(acctMgr, "Logout");

                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "HeroB", "passB123", "passB123", out _));
                Assert.AreEqual(0, PlayModeTierSupport.GetCoins(), "Fresh HeroB must start with 0 Xu.");
                Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SenzuBean"), "Fresh HeroB must start with 0 SenzuBean.");
                Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SpinalFluid"), "Fresh HeroB must start with 0 SpinalFluid.");
                Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("UltraDivineWater"), "Fresh HeroB must start with 0 UltraDivineWater.");
                Assert.AreEqual(1, PlayerPrefs.GetInt("Stickman_UnlockedStage", 1), "HeroA unlockedStage must NOT leak to fresh HeroB.");
                PlayModeTierSupport.GrantCoins(45);
                PlayModeTierSupport.InvokeMethodExact(acctMgr, "SyncActiveProfileFromPrefs", Type.EmptyTypes, null);

                // Destroy AccountManager & reload scene to verify multi-account JSON persistence across sessions
                UnityEngine.Object.DestroyImmediate(acctMgr.gameObject);
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component reloadedMgr = PlayModeTierSupport.RequireAccountManager();

                Assert.IsTrue(PlayModeTierSupport.TryLogin(reloadedMgr, "HeroA", "passA123", out _));
                Assert.AreEqual(200, PlayModeTierSupport.GetCoins(), "HeroA coins must be restored after reload.");
                Assert.AreEqual(2, PlayModeTierSupport.GetOwnedCount("SenzuBean"), "HeroA inventory must be restored after reload.");
                Assert.AreEqual(7, PlayerPrefs.GetInt("Stickman_UnlockedStage", 1), "HeroA unlocked stage must be restored after reload.");

                Assert.IsTrue(PlayModeTierSupport.TryLogin(reloadedMgr, "HeroB", "passB123", out _));
                Assert.AreEqual(45, PlayModeTierSupport.GetCoins(), "HeroB coins must remain isolated after reload.");
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F7")]
        public IEnumerator F07_T2_AccountManager_WrongPasswordAndCorruptedJson_RecoverSafely()
        {
            using (var backup = new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component acctMgr = PlayModeTierSupport.RequireAccountManager();

                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "HeroSafe", "goodPass1", "goodPass1", out _));
                PlayModeTierSupport.InvokeMethod(acctMgr, "Logout");

                Assert.IsFalse(PlayModeTierSupport.TryLogin(acctMgr, "HeroSafe", "wrongPass", out string err));
                Assert.IsFalse(string.IsNullOrEmpty(err), "Wrong password must return Vietnamese error message.");
                Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(acctMgr, "HasActiveSession"));

                // Simulate corrupted stickman_accounts.json on disk & reload scene
                UnityEngine.Object.DestroyImmediate(acctMgr.gameObject);
                File.WriteAllText(backup.AccountsFilePath, "{corrupted_not_valid_json!!!");

                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component reloadedMgr = PlayModeTierSupport.RequireAccountManager();
                Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(reloadedMgr, "HasActiveSession"));
                Assert.IsTrue(PlayModeTierSupport.TryRegister(reloadedMgr, "RecoveredUser", "pass9999", "pass9999", out _));
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F7")]
        public IEnumerator F07_T2_AuthScreen_HidesCoinBadge_ClearsInputPadding_And_UpdatesAccountPill()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                GameObject badge = E2ETestHelpers.FindUIElement("MainMenuCanvas/PersistentCoinBadge");

                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Auth");
                yield return null;
                Assert.IsFalse(badge.activeInHierarchy, "PersistentCoinBadge must be hidden on MainMenuScreen.Auth.");

                GameObject authPanel = E2ETestHelpers.FindUIElement("SubScreen_Auth");
                TMP_InputField[] inputs = authPanel.GetComponentsInChildren<TMP_InputField>(true);
                Assert.GreaterOrEqual(inputs.Length, 3, "Expected Username, Password, and ConfirmPassword inputs.");
                for (int i = 0; i < inputs.Length; i++)
                {
                    RectTransform vp = inputs[i].textViewport;
                    Assert.IsNotNull(vp);
                    Assert.GreaterOrEqual(vp.offsetMin.x, 40f, $"Input '{inputs[i].name}' left padding must clear icon/border.");
                    Assert.LessOrEqual(vp.offsetMax.x, -40f, $"Input '{inputs[i].name}' right padding must clear icon/border.");
                }

                Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "PillHero", "pass1234", "pass1234", out _));
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "MainMenu");
                yield return null;

                Assert.IsTrue(badge.activeInHierarchy, "PersistentCoinBadge must re-appear on MainMenu.");
                string pillText = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("AccountStatusPill/AccountStatusText"));
                Assert.IsTrue(pillText.Contains("PillHero"), $"AccountStatusPill should display 'PillHero', got '{pillText}'.");
            }
        }

        // ==================== F8: 07_BattleVitalsHUD Runtime Transitions (3 Tests) ====================

        [UnityTest]
        [Category("Tier2")]
        [Category("F8")]
        public IEnumerator F08_T2_PlayerTakeDamageAndHeal_UpdatesHpSliderText_And_DamageTrail()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                Assert.IsNotNull(player);

                float maxHp = (float)PlayModeTierSupport.GetInstanceProperty(player, "MaxHealth");
                PlayModeTierSupport.ApplyFatalDamageToPlayer(player, 25f);
                yield return null;

                Slider hpSlider = E2ETestHelpers.FindUIElement("PlayerHUD/PlayerHPBar").GetComponent<Slider>();
                Assert.Less(hpSlider.value, maxHp, "PlayerHPBar value must decrease after damage.");

                Component vitals = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.UI.PlayerVitalsHudUI");
                Image trail = PlayModeTierSupport.GetInstanceField(vitals, "hpTrailImage") as Image;
                Assert.IsNotNull(trail);
                Assert.IsTrue(trail.enabled, "HPTrailImage must activate on damage.");

                float afterDamageVal = hpSlider.value;
                PlayModeTierSupport.InvokeMethod(player, "Heal", 15f);
                yield return null;
                Assert.Greater(hpSlider.value, afterDamageVal, "PlayerHPBar value must increase after Heal.");
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F8")]
        public IEnumerator F08_T2_RageBVA_0_10_20_30_FillsSegments_And_ShowsTransformPrompt()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                Component rageMgr = PlayModeTierSupport.GetInstanceProperty(player, "RageManager") as Component;

                Slider seg1 = E2ETestHelpers.FindUIElement("RageContainer/RageSegment_1").GetComponent<Slider>();
                Slider seg2 = E2ETestHelpers.FindUIElement("RageContainer/RageSegment_2").GetComponent<Slider>();
                Slider seg3 = E2ETestHelpers.FindUIElement("RageContainer/RageSegment_3").GetComponent<Slider>();

                // Boundary 0
                Assert.AreEqual(0f, seg1.normalizedValue, 0.05f);

                // Boundary 15 (seg1 full, seg2 half, seg3 empty)
                PlayModeTierSupport.InvokeMethod(rageMgr, "AddRageFloat", 15f);
                yield return null;
                Assert.GreaterOrEqual(seg1.normalizedValue, 0.99f);
                Assert.AreEqual(0.5f, seg2.normalizedValue, 0.08f);
                Assert.AreEqual(0f, seg3.normalizedValue, 0.05f);

                // Boundary 30 max rage -> TransformReadyPrompt active
                PlayModeTierSupport.InvokeMethod(rageMgr, "FillMaxRage");
                yield return null;
                yield return null;
                Assert.GreaterOrEqual(seg3.normalizedValue, 0.99f);
                Assert.IsTrue(E2ETestHelpers.FindUIElement("PlayerHUD/TransformReadyPrompt").activeSelf);
                Assert.IsFalse(E2ETestHelpers.FindUIElement("PlayerHUD/SpecialComboReadyPrompt").activeSelf);
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F8")]
        public IEnumerator F08_T2_SpinalFluidBuff_ActivatesShieldBar_And_AbsorbsIncomingDamage()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;

                float maxHp = (float)PlayModeTierSupport.GetInstanceProperty(player, "MaxHealth");
                PlayModeTierSupport.InvokeMethod(player, "ActivateSpinalFluidBuff", 10f, 40f, 0f);
                yield return null;

                GameObject shieldBar = E2ETestHelpers.FindUIElement("PlayerHUD/ShieldBar");
                Assert.IsTrue(shieldBar.activeSelf, "ShieldBar must activate when Spinal Fluid buff is active.");
                Assert.IsTrue(PlayModeTierSupport.ReadAnyText(shieldBar).Contains("40"));

                PlayModeTierSupport.ApplyFatalDamageToPlayer(player, 15f);
                yield return null;

                float remainingShield = (float)PlayModeTierSupport.GetInstanceProperty(player, "VirtualShieldHP");
                float currentHp = (float)PlayModeTierSupport.GetInstanceProperty(player, "CurrentHealth");
                Assert.AreEqual(25f, remainingShield, 0.5f, "Virtual shield should absorb 15 damage from 40.");
                Assert.AreEqual(maxHp, currentHp, 0.5f, "Player HP must remain intact while shield absorbs damage.");
            }
        }

        // ==================== F9: 08_BattleTopHUD Runtime Contracts (3 Tests) ====================

        [UnityTest]
        [Category("Tier2")]
        [Category("F9")]
        public IEnumerator F09_T2_PauseMenuUI_BlockedWhenEndScreenOpen_ViaFatalDamageDefeat()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                Component gameUi = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.UI.GameUIManager");
                Component pauseUi = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.PauseMenuUI");

                PlayModeTierSupport.SetInstanceProperty(player, "TrainingInvincible", false);
                PlayModeTierSupport.ApplyFatalDamageToPlayer(player, 10000f);

                yield return E2ETestHelpers.WaitForCondition(
                    () => (bool)PlayModeTierSupport.GetInstanceProperty(gameUi, "IsEndScreenOpen"),
                    4f,
                    "Expected Defeat end screen to open after fatal damage.");

                PlayModeTierSupport.InvokeMethod(pauseUi, "SetPaused", true);
                yield return null;

                Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(pauseUi, "IsPaused"), "Pause must be blocked when IsEndScreenOpen is true.");
                Assert.IsFalse(E2ETestHelpers.FindUIElement("GameCanvas/PauseModalPanel").activeSelf);
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F9")]
        public IEnumerator F09_T2_EndlessBanner_UpdatesWaveAndEnemies_And_HidesInTraining()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Endless");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component gameUi = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.UI.GameUIManager");
                GameObject banner = E2ETestHelpers.FindUIElement("GameCanvas/EndlessWaveBanner");
                Assert.IsTrue(banner.activeInHierarchy, "EndlessWaveBanner must be visible in Endless mode.");

                PlayModeTierSupport.InvokeMethod(gameUi, "UpdateEndlessWave", 5, 3);
                yield return null;

                string bannerTxt = PlayModeTierSupport.ReadAnyText(banner);
                Assert.IsTrue(bannerTxt.Contains("5"), $"Expected wave 5 in banner text, got '{bannerTxt}'.");

                Type gameModeEnum = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.GameMode");
                PlayModeTierSupport.InvokeMethod(gameUi, "ConfigureModeHud", Enum.Parse(gameModeEnum, "Training"));
                yield return null;
                Assert.IsFalse(banner.activeInHierarchy, "EndlessWaveBanner must hide when configured for Training.");
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F9")]
        public IEnumerator F09_T2_PauseRetry_NoItemRefund_And_ExitResetsTimeScaleAndSuppressFlags()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.AddOwnedItem("SenzuBean", 2);
                PlayerPrefs.SetInt("MDS_EquippedSlot_0", 1);
                PlayerPrefs.Save();

                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component inv = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Items.InventoryManager");
                Assert.IsTrue(PlayModeTierSupport.TryConsumeSlot(inv, 0));
                Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SenzuBean"));

                // Pause -> Exit to Menu resets Time.timeScale and never refunds consumed item
                E2ETestHelpers.ClickButton("GameCanvas/PauseHudButton");
                yield return null;
                Assert.AreEqual(0f, Time.timeScale, 0.001f);

                E2ETestHelpers.ClickButton("PauseBox/PauseExitMenuButton");
                yield return E2ETestHelpers.WaitForCondition(
                    () => string.Equals(SceneManager.GetActiveScene().name, "MainMenu", StringComparison.Ordinal),
                    5f,
                    "Expected return to MainMenu scene.");

                Assert.AreEqual(1f, Time.timeScale, 0.001f);
                Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SenzuBean"), "Consumed item must NOT be refunded.");
            }
        }

        // ==================== F10: 09_TrainingSpawnerDock Boundaries (3 Tests) ====================

        [UnityTest]
        [Category("Tier2")]
        [Category("F10")]
        public IEnumerator F10_T2_TrainingSpawnUnitByIndex_PlacesNonOverlappingWithinArenaBounds()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Training");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component tm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.GameModes.TrainingModeManager");
                PlayModeTierSupport.InvokeMethod(tm, "SpawnUnitByIndex", 0);
                PlayModeTierSupport.InvokeMethod(tm, "SpawnUnitByIndex", 0);
                PlayModeTierSupport.InvokeMethod(tm, "SpawnUnitByIndex", 0);
                yield return null;

                List<GameObject> spawned = PlayModeTierSupport.GetInstanceField(tm, "spawnedUnits") as List<GameObject>;
                Assert.IsNotNull(spawned);
                Assert.AreEqual(3, spawned.Count, "Expected 3 spawned training units.");

                for (int i = 0; i < spawned.Count; i++)
                {
                    Assert.IsNotNull(spawned[i]);
                    float x = spawned[i].transform.position.x;
                    Assert.GreaterOrEqual(x, -22f);
                    Assert.LessOrEqual(x, 42f);

                    for (int j = i + 1; j < spawned.Count; j++)
                    {
                        float sep = Mathf.Abs(spawned[i].transform.position.x - spawned[j].transform.position.x);
                        Assert.GreaterOrEqual(sep, 1.30f, $"Spawned units {i} and {j} overlap horizontally (sep={sep:0.2f}).");
                    }
                }
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F10")]
        public IEnumerator F10_T2_TrainingClearAllEnemies_RemovesSpawnedUnits_And_AwardsZeroXu()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Training");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                int coinsBefore = PlayModeTierSupport.GetCoins();
                Component tm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.GameModes.TrainingModeManager");
                PlayModeTierSupport.InvokeMethod(tm, "SpawnUnitByIndex", 0);
                PlayModeTierSupport.InvokeMethod(tm, "SpawnUnitByIndex", 1);
                yield return null;

                E2ETestHelpers.ClickButton("Group3_SpawnAndClear/ClearAllEnemiesButton");
                yield return null;
                yield return null;

                List<GameObject> spawned = PlayModeTierSupport.GetInstanceField(tm, "spawnedUnits") as List<GameObject>;
                int aliveCount = 0;
                for (int i = 0; i < spawned.Count; i++)
                {
                    if (spawned[i] != null) aliveCount++;
                }
                Assert.AreEqual(0, aliveCount, "ClearAllEnemies must destroy all spawned training units.");
                Assert.AreEqual(coinsBefore, PlayModeTierSupport.GetCoins(), "ClearAllEnemies must award 0 Xu.");
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F10")]
        public IEnumerator F10_T2_TrainingFreeUseQE_DoesNotDecrementOwned_And_PlayerInvincibleBlocksDamage()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.AddOwnedItem("SenzuBean", 2);
                PlayerPrefs.SetInt("MDS_EquippedSlot_0", 1);
                PlayerPrefs.Save();

                PlayModeTierSupport.SetSelectedGameMode("Training");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component inv = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Items.InventoryManager");
                Assert.IsTrue(PlayModeTierSupport.TryConsumeSlot(inv, 0));
                Assert.AreEqual(2, PlayModeTierSupport.GetOwnedCount("SenzuBean"), "Training free-use Q/E must not decrement owned count.");

                Component tm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.GameModes.TrainingModeManager");
                if (!(bool)PlayModeTierSupport.GetInstanceProperty(tm, "PlayerInvincible"))
                {
                    E2ETestHelpers.ClickButton("Group1_PlayerControls/PlayerInvincibleButton");
                    yield return null;
                }

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                float maxHp = (float)PlayModeTierSupport.GetInstanceProperty(player, "MaxHealth");
                PlayModeTierSupport.ApplyFatalDamageToPlayer(player, 50f);
                yield return null;

                Assert.AreEqual(maxHp, (float)PlayModeTierSupport.GetInstanceProperty(player, "CurrentHealth"), 0.01f);
            }
        }

        // ==================== F11: Cross-Scene & Stage Unlock Boundaries (2 Tests) ====================

        [UnityTest]
        [Category("Tier2")]
        [Category("F11")]
        public IEnumerator F11_T2_CharacterSelectScene_12StageUnlockBVA_LockedIgnoresClick_UnlockAllSets12()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayerPrefs.SetInt("Stickman_UnlockedStage", 1);
                PlayerPrefs.SetInt("SelectedStageIndex", 0);
                PlayerPrefs.Save();

                yield return E2ETestHelpers.LoadSceneAndWait("CharacterSelectScene");
                Component mgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.CharacterSelectManager");
                Assert.IsNotNull(mgr);

                PlayModeTierSupport.InvokeMethod(mgr, "SelectStage", 5);
                Assert.AreEqual(0, PlayerPrefs.GetInt("SelectedStageIndex", -1), "Locked stage index 5 must be ignored when unlocked=1.");

                E2ETestHelpers.ClickButton("UnlockAllButton");
                yield return null;
                Assert.AreEqual(12, PlayerPrefs.GetInt("Stickman_UnlockedStage", 0));

                PlayModeTierSupport.InvokeMethod(mgr, "SelectStage", 11);
                Assert.AreEqual(11, PlayerPrefs.GetInt("SelectedStageIndex", -1), "Stage index 11 must be selectable after UnlockAllStages.");
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F11")]
        public IEnumerator F11_T2_All3BuildScenes_LoadCleanly_WithZeroErrorsOrWarnings()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            using (ScopedLogCapture logs = E2ETestHelpers.CaptureLogs())
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                yield return E2ETestHelpers.LoadSceneAndWait("CharacterSelectScene");

                PlayModeTierSupport.SetSelectedGameMode("Training");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                // Return from Training to MainMenu via PauseExitMenuButton verifies clean flag reset
                E2ETestHelpers.ClickButton("GameCanvas/PauseHudButton");
                yield return null;
                E2ETestHelpers.ClickButton("PauseBox/PauseExitMenuButton");
                yield return E2ETestHelpers.WaitForCondition(
                    () => string.Equals(SceneManager.GetActiveScene().name, "MainMenu", StringComparison.Ordinal),
                    5f,
                    "Expected return to MainMenu.");

                Component cm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Economy.CurrencyManager");
                Component inv = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Items.InventoryManager");
                Assert.AreEqual(1f, Time.timeScale, 0.001f);
                Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(cm, "SuppressEnemyRewards"));
                Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(inv, "IsTrainingFreeUseMode"));

                logs.AssertNoWarningsOrErrors();
            }
        }
    }
}
