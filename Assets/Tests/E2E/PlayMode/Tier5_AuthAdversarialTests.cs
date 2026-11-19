using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StickmanBeatEmUp.Tests.E2E.PlayMode
{
    public class Tier5_AuthAdversarialTests
    {
        [UnityTest]
        [Category("Tier5")]
        [Category("F7")]
        public IEnumerator T5_A01_AuthValidation_ExactBoundaries_CharacterWhitelist_Trimming_And_CaseInsensitiveLogin()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component acctMgr = PlayModeTierSupport.RequireAccountManager();

                // Whitelist rejections
                Assert.IsFalse(PlayModeTierSupport.TryRegister(acctMgr, "ab cd", "pass1234", "pass1234", out _), "Internal spaces must be rejected.");
                Assert.IsFalse(PlayModeTierSupport.TryRegister(acctMgr, "hero@1", "pass1234", "pass1234", out _), "'@' must be rejected.");
                Assert.IsFalse(PlayModeTierSupport.TryRegister(acctMgr, "hero-1", "pass1234", "pass1234", out _), "'-' must be rejected.");
                Assert.IsFalse(PlayModeTierSupport.TryRegister(acctMgr, "AnhTuấn", "pass1234", "pass1234", out _), "Vietnamese accents in username must be rejected.");

                // Whitespace-only password (length 4) must be rejected
                Assert.IsFalse(PlayModeTierSupport.TryRegister(acctMgr, "ValidName", "    ", "    ", out _), "Whitespace-only password must be rejected.");

                // Exact min boundary (3 chars user, 4 chars pass)
                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "abc", "abcd", "abcd", out string errMin), errMin);
                PlayModeTierSupport.InvokeMethod(acctMgr, "Logout");

                // Exact max boundary (20 chars user) + Unicode/Vietnamese password
                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "Abc_123.def_456.7890", "MậtKhẩu@123", "MậtKhẩu@123", out string errMax), errMax);
                PlayModeTierSupport.InvokeMethod(acctMgr, "Logout");
                Assert.IsTrue(PlayModeTierSupport.TryLogin(acctMgr, "Abc_123.def_456.7890", "MậtKhẩu@123", out string errUni), errUni);
                PlayModeTierSupport.InvokeMethod(acctMgr, "Logout");

                // Trimming on register + case-insensitive login with spaces
                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "  TrimHero  ", "pass1234", "pass1234", out string errTrim), errTrim);
                object activeRec = PlayModeTierSupport.GetInstanceProperty(acctMgr, "ActiveAccount");
                Assert.AreEqual("TrimHero", PlayModeTierSupport.GetInstanceField(activeRec, "username"));
                PlayModeTierSupport.InvokeMethod(acctMgr, "Logout");

                Assert.IsTrue(PlayModeTierSupport.TryLogin(acctMgr, "  trimhero  ", "pass1234", out string errLower), errLower);
                PlayModeTierSupport.InvokeMethod(acctMgr, "Logout");
                Assert.IsTrue(PlayModeTierSupport.TryLogin(acctMgr, "TRIMHERO", "pass1234", out string errUpper), errUpper);

                System.Collections.IList accounts = PlayModeTierSupport.GetInstanceProperty(acctMgr, "Accounts") as System.Collections.IList;
                Assert.IsNotNull(accounts);
                Assert.AreEqual(3, accounts.Count, "Expected exactly 3 registered accounts.");
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F7")]
        public IEnumerator T5_A02_AuthUI_LoginErrors_DuplicateRegister_StayOpen_And_DoNotClobberExistingAccount()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component acctMgr = PlayModeTierSupport.RequireAccountManager();

                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "SeedUser", "seedPass1", "seedPass1", out string err), err);
                PlayModeTierSupport.GrantCoins(150);
                PlayModeTierSupport.AddOwnedItem("SenzuBean", 2);
                Tier34Support.SyncActiveProfile(acctMgr);
                PlayModeTierSupport.InvokeMethod(acctMgr, "Logout");

                E2ETestHelpers.ClickButton("AccountStatusPill/AccountActionButton");
                yield return null;

                GameObject authPanel = E2ETestHelpers.FindUIElement("SubScreen_Auth");
                Assert.IsTrue(authPanel.activeInHierarchy);
                Type authCtrlType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Auth.AuthUIController");
                Component ctrl = authPanel.GetComponent(authCtrlType);
                Assert.AreEqual("Login", PlayModeTierSupport.GetInstanceProperty(ctrl, "CurrentTab").ToString(), "With existing accounts, Auth modal must default to Login tab.");

                // 1. Empty username submit
                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputUsername", "");
                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputPassword", "seedPass1");
                E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthSubmitButton");
                yield return null;
                Assert.IsTrue(authPanel.activeInHierarchy);
                Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(acctMgr, "HasActiveSession"));
                Assert.IsFalse(string.IsNullOrEmpty(PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("SubScreen_Auth/AuthModalContainer/AuthFeedbackText"))));

                // 2. Empty password submit
                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputUsername", "SeedUser");
                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputPassword", "");
                E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthSubmitButton");
                yield return null;
                Assert.IsTrue(authPanel.activeInHierarchy);
                Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(acctMgr, "HasActiveSession"));
                Assert.IsFalse(string.IsNullOrEmpty(PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("SubScreen_Auth/AuthModalContainer/AuthFeedbackText"))));

                // 3. Wrong password submit
                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputUsername", "SeedUser");
                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputPassword", "wrongPass");
                E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthSubmitButton");
                yield return null;
                Assert.IsTrue(authPanel.activeInHierarchy);
                Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(acctMgr, "HasActiveSession"));
                string wrongErr = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("SubScreen_Auth/AuthModalContainer/AuthFeedbackText"));
                Assert.IsTrue(wrongErr.Contains("không đúng"), $"Expected wrong password error, got '{wrongErr}'.");

                // 4. Duplicate register (case-insensitive)
                E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/TabRegisterButton");
                yield return null;
                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputUsername", "seeduser");
                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputPassword", "otherPass");
                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputConfirmPassword", "otherPass");
                E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthSubmitButton");
                yield return null;
                Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(acctMgr, "HasActiveSession"));
                string dupErr = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("SubScreen_Auth/AuthModalContainer/AuthFeedbackText"));
                Assert.IsTrue(dupErr.Contains("tồn tại"), $"Expected duplicate username error, got '{dupErr}'.");

                // 5. Valid login succeeds & original account untouched
                E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/TabLoginButton");
                yield return null;
                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputUsername", "SeedUser");
                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputPassword", "seedPass1");
                E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthSubmitButton");
                yield return null;

                Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen1_MainMenu").activeInHierarchy);
                Assert.AreEqual(150, PlayModeTierSupport.GetCoins());
                Assert.AreEqual(2, PlayModeTierSupport.GetOwnedCount("SenzuBean"));
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F7")]
        [Category("F2")]
        public IEnumerator T5_A03_AuthNavigation_PlayGateBackResetsAutoAdvance_And_LoggedInPlayBypassesAuth()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");

                // Play while logged out -> Auth (autoAdvance = true) -> Back -> MainMenu
                E2ETestHelpers.ClickButton("Screen1_MainMenu/PlayButton");
                yield return null;
                Assert.IsTrue(E2ETestHelpers.FindUIElement("SubScreen_Auth").activeInHierarchy);

                E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthBackButton");
                yield return null;
                Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen1_MainMenu").activeInHierarchy);

                // Direct ShowScreen("Auth") should NOT inherit stale autoAdvance=true (Bug 4)
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                E2ETestHelpers.ClickButton("Screen1_MainMenu/PlayButton");
                yield return null;
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "MainMenu");
                yield return null;
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Auth");
                yield return null;

                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputUsername", "GateHero");
                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputPassword", "pass1234");
                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputConfirmPassword", "pass1234");
                E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthSubmitButton");
                yield return null;

                Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen1_MainMenu").activeInHierarchy, "Must land on MainMenu rather than auto-advancing to ModeSelect.");
                Assert.IsFalse(E2ETestHelpers.FindUIElement("SubScreen_ModeSelect").activeInHierarchy);

                string statusTxt = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("AccountStatusPill/AccountStatusText"));
                Assert.IsTrue(statusTxt.Contains("GateHero"));
                Assert.AreEqual("ĐỔI TK", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("AccountStatusPill/AccountActionButton")).Trim());
                Assert.IsTrue(E2ETestHelpers.FindUIElement("AccountStatusPill/AccountLogoutButton").activeInHierarchy);

                // Logged-in PlayButton bypasses Auth directly to ModeSelect
                E2ETestHelpers.ClickButton("Screen1_MainMenu/PlayButton");
                yield return null;
                Assert.IsTrue(E2ETestHelpers.FindUIElement("SubScreen_ModeSelect").activeInHierarchy);
                Assert.IsFalse(E2ETestHelpers.FindUIElement("SubScreen_Auth").activeInHierarchy);
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F7")]
        public IEnumerator T5_A04_AuthUI_ActiveSessionBanner_ModalLogout_And_AccountPillNonOverlap()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "BannerUser", "pass1234", "pass1234", out string err), err);
                yield return null;

                Canvas rootCanvas = E2ETestHelpers.FindUIElement("MainMenuCanvas").GetComponent<Canvas>();
                Rect pillRect = E2ETestHelpers.GetRectBoundsIn1920x1080(E2ETestHelpers.FindUIElement("Screen1_MainMenu/AccountStatusPill").GetComponent<RectTransform>(), rootCanvas);
                Rect statusRect = E2ETestHelpers.GetRectBoundsIn1920x1080(E2ETestHelpers.FindUIElement("AccountStatusPill/AccountStatusText").GetComponent<RectTransform>(), rootCanvas);
                Rect actionRect = E2ETestHelpers.GetRectBoundsIn1920x1080(E2ETestHelpers.FindUIElement("AccountStatusPill/AccountActionButton").GetComponent<RectTransform>(), rootCanvas);
                Rect logoutRect = E2ETestHelpers.GetRectBoundsIn1920x1080(E2ETestHelpers.FindUIElement("AccountStatusPill/AccountLogoutButton").GetComponent<RectTransform>(), rootCanvas);

                Assert.GreaterOrEqual(statusRect.xMin, pillRect.xMin - 2f);
                Assert.LessOrEqual(statusRect.xMax, actionRect.xMin + 0.5f, "AccountStatusText overlaps AccountActionButton.");
                Assert.LessOrEqual(actionRect.xMax, logoutRect.xMin + 0.5f, "AccountActionButton overlaps AccountLogoutButton.");
                Assert.LessOrEqual(logoutRect.xMax, pillRect.xMax + 2f);

                E2ETestHelpers.ClickButton("AccountStatusPill/AccountActionButton");
                yield return null;

                GameObject banner = E2ETestHelpers.FindUIElement("AuthModalContainer/ActiveSessionBanner");
                Assert.IsTrue(banner.activeInHierarchy, "ActiveSessionBanner must be visible when opening Auth while logged in.");
                string bannerTxt = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("ActiveSessionBanner/ActiveSessionText"));
                Assert.IsTrue(bannerTxt.Contains("Đang đăng nhập: BannerUser"), $"Unexpected session banner text '{bannerTxt}'.");
                Assert.AreEqual("BannerUser", E2ETestHelpers.FindUIElement("AuthModalContainer/InputUsername").GetComponent<TMP_InputField>().text);

                E2ETestHelpers.ClickButton("ActiveSessionBanner/AuthLogoutButton");
                yield return null;

                Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(acctMgr, "HasActiveSession"));
                Assert.IsFalse(banner.activeSelf);
                Assert.AreEqual(string.Empty, E2ETestHelpers.FindUIElement("AuthModalContainer/InputUsername").GetComponent<TMP_InputField>().text);
                Assert.AreEqual(string.Empty, E2ETestHelpers.FindUIElement("AuthModalContainer/InputPassword").GetComponent<TMP_InputField>().text);
                Assert.AreEqual(string.Empty, E2ETestHelpers.FindUIElement("AuthModalContainer/InputConfirmPassword").GetComponent<TMP_InputField>().text);
                string fb = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("AuthModalContainer/AuthFeedbackText"));
                Assert.IsTrue(fb.Contains("đăng xuất"), $"Expected logout feedback, got '{fb}'.");

                E2ETestHelpers.ClickButton("AuthModalContainer/AuthBackButton");
                yield return null;
                Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen1_MainMenu").activeInHierarchy);
                Assert.AreEqual("Chưa đăng nhập", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("AccountStatusPill/AccountStatusText")).Trim());
                Assert.IsFalse(E2ETestHelpers.FindUIElement("AccountStatusPill/AccountLogoutButton").activeSelf);
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F7")]
        [Category("F4")]
        [Category("F5")]
        public IEnumerator T5_A05_AccountIsolation_GuestQuarantine_And_DirectSwitchWithoutLogout()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");

                // Dirty Guest state prior to registering AccountA
                PlayModeTierSupport.GrantCoins(500);
                PlayModeTierSupport.AddOwnedItem("UltraDivineWater", 3);
                PlayerPrefs.SetInt("MDS_EquippedSlot_0", 3);
                PlayerPrefs.SetInt("SelectedCharacter", 2);
                PlayerPrefs.SetInt("SelectedWorld", 2);
                PlayerPrefs.SetInt("Stickman_UnlockedStage", 9);
                PlayerPrefs.Save();

                E2ETestHelpers.ClickButton("Screen1_MainMenu/PlayButton");
                yield return null;
                Tier34Support.SetInputFieldText("AuthModalContainer/InputUsername", "AccountA");
                Tier34Support.SetInputFieldText("AuthModalContainer/InputPassword", "passA123");
                Tier34Support.SetInputFieldText("AuthModalContainer/InputConfirmPassword", "passA123");
                E2ETestHelpers.ClickButton("AuthModalContainer/AuthSubmitButton");
                yield return null;

                // Guest quarantine verified
                Assert.AreEqual(0, PlayModeTierSupport.GetCoins());
                Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SenzuBean"));
                Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SpinalFluid"));
                Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("UltraDivineWater"));
                Assert.AreEqual("SenzuBean", PlayModeTierSupport.GetEquippedItemName(0));
                Assert.AreEqual("SpinalFluid", PlayModeTierSupport.GetEquippedItemName(1));
                Assert.AreEqual(0, PlayerPrefs.GetInt("SelectedCharacter", -1));
                Assert.AreEqual(0, PlayerPrefs.GetInt("SelectedWorld", -1));
                Assert.AreEqual(1, PlayerPrefs.GetInt("Stickman_UnlockedStage", 0));

                // Populate AccountA state
                PlayModeTierSupport.GrantCoins(200);
                PlayModeTierSupport.AddOwnedItem("UltraDivineWater", 2);
                Component invMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Items.InventoryManager");
                Type itemEnum = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.ConsumableItemType");
                PlayModeTierSupport.InvokeMethod(invMgr, "SetEquippedItem", 0, Enum.Parse(itemEnum, "UltraDivineWater"));
                PlayModeTierSupport.InvokeMethod(uiMgr, "SelectCharacter", 2);
                PlayModeTierSupport.InvokeMethod(uiMgr, "SelectWorld", 2);
                PlayerPrefs.SetInt("Stickman_UnlockedStage", 6);
                PlayerPrefs.Save();
                Tier34Support.SyncActiveProfile(PlayModeTierSupport.RequireAccountManager());

                // Direct switch (without Logout) -> Register AccountB via UI
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "MainMenu");
                yield return null;
                E2ETestHelpers.ClickButton("AccountStatusPill/AccountActionButton");
                yield return null;
                E2ETestHelpers.ClickButton("AuthModalContainer/TabRegisterButton");
                yield return null;
                Tier34Support.SetInputFieldText("AuthModalContainer/InputUsername", "AccountB");
                Tier34Support.SetInputFieldText("AuthModalContainer/InputPassword", "passB123");
                Tier34Support.SetInputFieldText("AuthModalContainer/InputConfirmPassword", "passB123");
                E2ETestHelpers.ClickButton("AuthModalContainer/AuthSubmitButton");
                yield return null;

                Assert.AreEqual(0, PlayModeTierSupport.GetCoins());
                Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("UltraDivineWater"));
                Assert.AreEqual(0, PlayerPrefs.GetInt("SelectedCharacter", -1));
                Assert.AreEqual(0, PlayerPrefs.GetInt("SelectedWorld", -1));
                Assert.AreEqual(1, PlayerPrefs.GetInt("Stickman_UnlockedStage", 0));
                Assert.AreEqual("0 Xu", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("MainMenuCanvas/PersistentCoinBadge")).Trim());

                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "CharacterSelect");
                yield return null;
                Assert.AreEqual("GOKU", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("Screen2_CharacterSelect/SelectedCharName")).Trim());

                // Populate AccountB state
                PlayModeTierSupport.GrantCoins(40);
                PlayModeTierSupport.InvokeMethod(uiMgr, "SelectCharacter", 1);

                // Direct switch (without Logout) -> Login AccountA via UI
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "MainMenu");
                yield return null;
                E2ETestHelpers.ClickButton("AccountStatusPill/AccountActionButton");
                yield return null;
                E2ETestHelpers.ClickButton("AuthModalContainer/TabLoginButton");
                yield return null;
                Tier34Support.SetInputFieldText("AuthModalContainer/InputUsername", "AccountA");
                Tier34Support.SetInputFieldText("AuthModalContainer/InputPassword", "passA123");
                E2ETestHelpers.ClickButton("AuthModalContainer/AuthSubmitButton");
                yield return null;

                Assert.AreEqual(200, PlayModeTierSupport.GetCoins());
                Assert.AreEqual(2, PlayModeTierSupport.GetOwnedCount("UltraDivineWater"));
                Assert.AreEqual("UltraDivineWater", PlayModeTierSupport.GetEquippedItemName(0));
                Assert.AreEqual(2, PlayerPrefs.GetInt("SelectedCharacter", -1));
                Assert.AreEqual(2, PlayerPrefs.GetInt("SelectedWorld", -1));
                Assert.AreEqual(6, PlayerPrefs.GetInt("Stickman_UnlockedStage", 0));

                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "CharacterSelect");
                yield return null;
                Assert.AreEqual("DIEND", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("Screen2_CharacterSelect/SelectedCharName")).Trim());

                // Switch back to AccountB
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "MainMenu");
                yield return null;
                E2ETestHelpers.ClickButton("AccountStatusPill/AccountActionButton");
                yield return null;
                E2ETestHelpers.ClickButton("AuthModalContainer/TabLoginButton");
                yield return null;
                Tier34Support.SetInputFieldText("AuthModalContainer/InputUsername", "AccountB");
                Tier34Support.SetInputFieldText("AuthModalContainer/InputPassword", "passB123");
                E2ETestHelpers.ClickButton("AuthModalContainer/AuthSubmitButton");
                yield return null;

                Assert.AreEqual(40, PlayModeTierSupport.GetCoins());
                Assert.AreEqual(1, PlayerPrefs.GetInt("SelectedCharacter", -1));
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F7")]
        public IEnumerator T5_A06_AccountPersistence_UniqueSalts_MalformedJsonVariants_And_TmpBakCleanup()
        {
            using (var backup = new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component acctMgr = PlayModeTierSupport.RequireAccountManager();

                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "SaltUser1", "SameSecret99", "SameSecret99", out string err1), err1);
                PlayModeTierSupport.InvokeMethod(acctMgr, "Logout");
                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "SaltUser2", "SameSecret99", "SameSecret99", out string err2), err2);

                System.Collections.IList accounts = PlayModeTierSupport.GetInstanceProperty(acctMgr, "Accounts") as System.Collections.IList;
                Assert.AreEqual(2, accounts.Count);
                string salt1 = PlayModeTierSupport.GetInstanceField(accounts[0], "saltBase64") as string;
                string salt2 = PlayModeTierSupport.GetInstanceField(accounts[1], "saltBase64") as string;
                string hash1 = PlayModeTierSupport.GetInstanceField(accounts[0], "passwordHashHex") as string;
                string hash2 = PlayModeTierSupport.GetInstanceField(accounts[1], "passwordHashHex") as string;
                Assert.AreNotEqual(salt1, salt2, "Each account must use a unique cryptographic salt.");
                Assert.AreNotEqual(hash1, hash2, "Identical passwords with unique salts must yield distinct hashes.");

                string diskJson = File.ReadAllText(backup.AccountsFilePath);
                Assert.IsFalse(diskJson.Contains("SameSecret99"), "Plaintext password must never appear in stickman_accounts.json.");

                string[] badPayloads =
                {
                    "",
                    "null",
                    "{}",
                    "[1,2,3]",
                    "{\"version\":1,\"accounts\":[null,{\"username\":\"\"},{\"username\":\"   \"}]}",
                };

                string tmpPath = backup.AccountsFilePath + ".tmp";
                string bakPath = backup.AccountsFilePath + ".bak";

                for (int i = 0; i < badPayloads.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(acctMgr.gameObject);
                    File.WriteAllText(backup.AccountsFilePath, badPayloads[i]);
                    File.WriteAllText(tmpPath, "stale_tmp");
                    File.WriteAllText(bakPath, "stale_bak");

                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                    acctMgr = PlayModeTierSupport.RequireAccountManager();
                    Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(acctMgr, "HasActiveSession"));
                    System.Collections.IList loadedList = PlayModeTierSupport.GetInstanceProperty(acctMgr, "Accounts") as System.Collections.IList;
                    Assert.AreEqual(0, loadedList.Count, $"Malformed payload #{i} should produce 0 valid accounts.");
                }

                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "CleanAfterBadJson", "pass1234", "pass1234", out string errClean), errClean);
                Assert.IsFalse(File.Exists(tmpPath), "Stale .tmp file must be cleaned up after save.");
                Assert.IsFalse(File.Exists(bakPath), "Stale .bak file must be cleaned up after save.");
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F7")]
        public IEnumerator T5_A07_AccountRecovery_StaleOrWhitespaceActiveUsername_ClearsGhostPlayerPrefs()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "RealUser", "pass1234", "pass1234", out string err), err);
                PlayModeTierSupport.InvokeMethod(acctMgr, "Logout");

                // Case 1: Missing/deleted active account pointer
                UnityEngine.Object.DestroyImmediate(acctMgr.gameObject);
                PlayerPrefs.SetString("MDS_ActiveAccountUsername", "GhostDeletedUser");
                PlayerPrefs.SetInt("MDS_PlayerCoins", 999);
                PlayerPrefs.SetInt("MDS_ItemOwned_SenzuBean", 4);
                PlayerPrefs.SetInt("Stickman_UnlockedStage", 8);
                PlayerPrefs.Save();

                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                acctMgr = PlayModeTierSupport.RequireAccountManager();
                Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(acctMgr, "HasActiveSession"));
                Assert.IsFalse(PlayerPrefs.HasKey("MDS_ActiveAccountUsername"));
                Assert.AreEqual(0, PlayModeTierSupport.GetCoins());
                Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SenzuBean"));
                Assert.AreEqual(1, PlayerPrefs.GetInt("Stickman_UnlockedStage", 1));

                // Case 2: Whitespace active account pointer (Bug 1 fix)
                UnityEngine.Object.DestroyImmediate(acctMgr.gameObject);
                PlayerPrefs.SetString("MDS_ActiveAccountUsername", "   ");
                PlayerPrefs.SetInt("MDS_PlayerCoins", 888);
                PlayerPrefs.SetInt("MDS_ItemOwned_SpinalFluid", 3);
                PlayerPrefs.SetInt("Stickman_UnlockedStage", 7);
                PlayerPrefs.Save();

                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                acctMgr = PlayModeTierSupport.RequireAccountManager();
                Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(acctMgr, "HasActiveSession"));
                Assert.IsFalse(PlayerPrefs.HasKey("MDS_ActiveAccountUsername"));
                Assert.AreEqual(0, PlayModeTierSupport.GetCoins());
                Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SpinalFluid"));
                Assert.AreEqual(1, PlayerPrefs.GetInt("Stickman_UnlockedStage", 1));
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F7")]
        public IEnumerator T5_A08_AccountDatabase_CorruptedCredentialRecord_DoesNotLockoutOrAutoLogin()
        {
            using (var backup = new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                string craftedJson =
                    "{\"version\":1,\"accounts\":[" +
                    "{\"username\":\"CorruptHero\",\"coins\":777}," +
                    "{\"username\":\"Bad@Name\",\"saltBase64\":\"AAAA\",\"passwordHashHex\":\"bbbb\"}," +
                    "{\"username\":\"DupUser\",\"saltBase64\":\"AAAA\",\"passwordHashHex\":\"1111\",\"coins\":10}," +
                    "{\"username\":\"dupuser\",\"saltBase64\":\"BBBB\",\"passwordHashHex\":\"2222\",\"coins\":99}" +
                    "]}";
                File.WriteAllText(backup.AccountsFilePath, craftedJson);
                PlayerPrefs.SetString("MDS_ActiveAccountUsername", "CorruptHero");
                PlayerPrefs.SetInt("MDS_PlayerCoins", 777);
                PlayerPrefs.Save();

                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component acctMgr = PlayModeTierSupport.RequireAccountManager();

                Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(acctMgr, "HasActiveSession"), "Credential-less record must never auto-authenticate.");
                Assert.AreEqual(0, PlayModeTierSupport.GetCoins(), "Residual coins must reset when active session is invalid.");

                System.Collections.IList accounts = PlayModeTierSupport.GetInstanceProperty(acctMgr, "Accounts") as System.Collections.IList;
                Assert.AreEqual(1, accounts.Count, "Corrupt, invalid-username, and duplicate-case records must be pruned on load.");
                Assert.AreEqual("DupUser", PlayModeTierSupport.GetInstanceField(accounts[0], "username"));

                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "CorruptHero", "newPass1", "newPass1", out string err), err);
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F7")]
        public IEnumerator T5_A09_AuthUI_InputProperties_VietnameseFontAsset_And_RuntimeFallbackBuilder()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Auth");
                yield return null;

                VerifyAuthInputPropertiesAndFonts();

                // Destroy serialized SubScreen_Auth and AccountStatusPill, then trigger runtime fallback builder
                GameObject authPanel = E2ETestHelpers.FindUIElement("SubScreen_Auth");
                GameObject pill = E2ETestHelpers.FindUIElement("AccountStatusPill");
                UnityEngine.Object.DestroyImmediate(authPanel);
                UnityEngine.Object.DestroyImmediate(pill);

                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "MainMenu");
                yield return null;
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Auth");
                yield return null;

                GameObject rebuiltAuth = E2ETestHelpers.FindUIElement("SubScreen_Auth");
                Assert.IsNotNull(rebuiltAuth, "EnsureAuthUIInitialized must rebuild SubScreen_Auth at runtime.");
                Assert.IsTrue(rebuiltAuth.activeInHierarchy);
                VerifyAuthInputPropertiesAndFonts();

                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputUsername", "RuntimeUser");
                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputPassword", "pass1234");
                Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputConfirmPassword", "pass1234");
                E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthSubmitButton");
                yield return null;

                Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(acctMgr, "HasActiveSession"));
                Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen1_MainMenu").activeInHierarchy);
                Assert.IsNotNull(E2ETestHelpers.FindUIElement("AccountStatusPill"));
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F7")]
        [Category("F4")]
        [Category("F11")]
        public IEnumerator T5_A10_CrossScene_AccountProgressionSync_BattleVictory_And_CharacterSelectScene()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "CampaignHero", "pass1234", "pass1234", out string err), err);

                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.InvokeMethod(uiMgr, "SelectCharacter", 1); // Eren
                PlayerPrefs.SetInt("SelectedStageIndex", 0);
                PlayerPrefs.SetInt("Stickman_UnlockedStage", 1);
                PlayerPrefs.Save();
                Tier34Support.SyncActiveProfile(acctMgr);

                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                Component cm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Economy.CurrencyManager");
                Tier34Support.AddCoinsExact(cm, 30);
                Tier34Support.CompleteCurrentStage(levelMgr);
                yield return null;

                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                acctMgr = PlayModeTierSupport.RequireAccountManager();
                object rec = PlayModeTierSupport.GetInstanceProperty(acctMgr, "ActiveAccount");
                Assert.AreEqual(30, (int)PlayModeTierSupport.GetInstanceField(rec, "coins"));
                Assert.GreaterOrEqual((int)PlayModeTierSupport.GetInstanceField(rec, "unlockedStage"), 2);
                Assert.AreEqual(1, (int)PlayModeTierSupport.GetInstanceField(rec, "selectedCharacter"));

                // Bug 3 / B-02: CharacterSelectScene must preserve Eren (1) and sync Diend (2) to AccountManager
                yield return E2ETestHelpers.LoadSceneAndWait("CharacterSelectScene");
                Assert.AreEqual(1, PlayerPrefs.GetInt("SelectedCharacter", -1), "CharacterSelectManager.Start() must preserve saved Eren selection.");

                Component csMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.CharacterSelectManager");
                PlayModeTierSupport.InvokeMethod(csMgr, "SelectCharacter", 2); // Diend
                Assert.AreEqual(2, PlayerPrefs.GetInt("SelectedCharacter", -1));

                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                acctMgr = PlayModeTierSupport.RequireAccountManager();
                PlayModeTierSupport.InvokeMethod(acctMgr, "Logout");
                Assert.AreEqual(0, PlayerPrefs.GetInt("SelectedCharacter", -1), "Guest logout must reset SelectedCharacter to 0.");

                Assert.IsTrue(PlayModeTierSupport.TryLogin(acctMgr, "CampaignHero", "pass1234", out string errLogin), errLogin);
                Assert.AreEqual(2, PlayerPrefs.GetInt("SelectedCharacter", -1), "Account must restore Diend (2) saved via CharacterSelectManager.");
                Assert.GreaterOrEqual(PlayerPrefs.GetInt("Stickman_UnlockedStage", 1), 2);
                Assert.AreEqual(30, PlayModeTierSupport.GetCoins());
            }
        }

        private static void VerifyAuthInputPropertiesAndFonts()
        {
            TMP_InputField userInput = E2ETestHelpers.FindUIElement("SubScreen_Auth/AuthModalContainer/InputUsername").GetComponent<TMP_InputField>();
            TMP_InputField passInput = E2ETestHelpers.FindUIElement("SubScreen_Auth/AuthModalContainer/InputPassword").GetComponent<TMP_InputField>();
            TMP_InputField confirmInput = E2ETestHelpers.FindUIElement("SubScreen_Auth/AuthModalContainer/InputConfirmPassword").GetComponent<TMP_InputField>();

            Assert.AreEqual(TMP_InputField.ContentType.Standard, userInput.contentType);
            Assert.AreEqual(20, userInput.characterLimit);

            Assert.AreEqual(TMP_InputField.ContentType.Password, passInput.contentType);
            Assert.AreEqual('*', passInput.asteriskChar);
            Assert.AreEqual(32, passInput.characterLimit);

            Assert.AreEqual(TMP_InputField.ContentType.Password, confirmInput.contentType);
            Assert.AreEqual('*', confirmInput.asteriskChar);
            Assert.AreEqual(32, confirmInput.characterLimit);

            TMP_InputField[] all = { userInput, passInput, confirmInput };
            for (int i = 0; i < all.Length; i++)
            {
                Assert.IsNotNull(all[i].fontAsset, $"{all[i].name}.fontAsset is null.");
                Assert.IsFalse(all[i].fontAsset.name.Contains("LiberationSans"));
                Assert.IsNotNull(all[i].textComponent.font, $"{all[i].name}.textComponent.font is null.");
                Assert.IsFalse(all[i].textComponent.font.name.Contains("LiberationSans"));
                TMP_Text ph = all[i].placeholder as TMP_Text;
                Assert.IsNotNull(ph, $"{all[i].name}.placeholder is not TMP_Text.");
                Assert.IsNotNull(ph.font, $"{all[i].name}.placeholder.font is null.");
                Assert.IsFalse(ph.font.name.Contains("LiberationSans"));
            }
        }
    }
}
