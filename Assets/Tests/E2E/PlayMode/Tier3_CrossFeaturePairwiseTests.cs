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
    internal static class Tier34Support
    {
        public static void SetInputFieldText(string elementPath, string value)
        {
            GameObject go = E2ETestHelpers.FindUIElement(elementPath);
            Assert.IsNotNull(go, $"TMP_InputField GameObject '{elementPath}' not found.");
            Assert.IsTrue(go.activeInHierarchy, $"TMP_InputField '{elementPath}' is not activeInHierarchy.");
            TMP_InputField input = go.GetComponent<TMP_InputField>();
            Assert.IsNotNull(input, $"GameObject '{elementPath}' has no TMP_InputField component.");
            input.text = value ?? string.Empty;
        }

        public static IEnumerator WaitForScene(string sceneName, float timeoutSeconds = 8f)
        {
            yield return E2ETestHelpers.WaitForCondition(
                () => string.Equals(SceneManager.GetActiveScene().name, sceneName, StringComparison.Ordinal),
                timeoutSeconds,
                $"Timed out waiting for active scene '{sceneName}'.");
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        public static void SyncActiveProfile(Component acctMgr)
        {
            PlayModeTierSupport.InvokeMethodExact(acctMgr, "SyncActiveProfileFromPrefs", Type.EmptyTypes, null);
        }

        public static void AddCoinsExact(Component cm, int amount)
        {
            PlayModeTierSupport.InvokeMethodExact(
                cm,
                "AddCoins",
                new[] { typeof(int), typeof(Vector3?) },
                new object[] { amount, null });
        }

        public static void CompleteCurrentStage(Component levelMgr)
        {
            PlayModeTierSupport.InvokeMethodExact(levelMgr, "CompleteStage", Type.EmptyTypes, null);
        }

        public static Component RequirePlayerItemController(Component player)
        {
            Assert.IsNotNull(player, "ActivePlayer must not be null.");
            Type ctrlType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Items.PlayerItemController");
            Component ctrl = player.GetComponent(ctrlType);
            Assert.IsNotNull(ctrl, "PlayerItemController must be attached to ActivePlayer.");
            return ctrl;
        }

        public static void AssertSlicedImageNonSquashed(string elementPath, string expectedSpriteName)
        {
            GameObject go = E2ETestHelpers.FindUIElement(elementPath);
            Assert.IsNotNull(go, $"UI Image GameObject '{elementPath}' not found.");
            Image img = go.GetComponent<Image>();
            Assert.IsNotNull(img, $"Image component missing on '{elementPath}'.");
            Assert.IsNotNull(img.sprite, $"Sprite missing on '{elementPath}'.");
            if (!string.IsNullOrEmpty(expectedSpriteName))
            {
                Assert.AreEqual(expectedSpriteName, img.sprite.name, $"Unexpected sprite on '{elementPath}'.");
            }

            Vector4 b = img.sprite.border;
            Assert.Greater(b.x + b.y + b.z + b.w, 0f, $"Expected non-zero 9-slice border on '{elementPath}' ({img.sprite.name}).");
            Assert.AreEqual(Image.Type.Sliced, img.type, $"Expected Sliced Image.Type on '{elementPath}'.");
            Assert.GreaterOrEqual(img.pixelsPerUnitMultiplier, 0.25f, $"ppuMultiplier below 0.25f on '{elementPath}'.");
            Assert.LessOrEqual(img.pixelsPerUnitMultiplier, 12f, $"ppuMultiplier exceeded 12f on '{elementPath}'.");

            RectTransform rt = img.rectTransform;
            float w = rt.rect.width > 1f ? rt.rect.width : rt.sizeDelta.x;
            float h = rt.rect.height > 1f ? rt.rect.height : rt.sizeDelta.y;
            if (img.pixelsPerUnitMultiplier < 11.99f)
            {
                if (w > 1f && (b.x + b.z) > 0f)
                {
                    float scaledW = (b.x + b.z) / img.pixelsPerUnitMultiplier;
                    Assert.LessOrEqual(scaledW, w * 0.72f + 1.5f, $"Horizontal 9-slice border squashed on '{elementPath}'.");
                }
                if (h > 1f && (b.y + b.w) > 0f)
                {
                    float scaledH = (b.y + b.w) / img.pixelsPerUnitMultiplier;
                    Assert.LessOrEqual(scaledH, h * 0.72f + 1.5f, $"Vertical 9-slice border squashed on '{elementPath}'.");
                }
            }
        }

        public static void AssertTmpFontsNotLiberationSans(GameObject root)
        {
            Assert.IsNotNull(root, "Root GameObject for TMP font audit must not be null.");
            TMP_Text[] tmps = root.GetComponentsInChildren<TMP_Text>(true);
            Assert.Greater(tmps.Length, 0, $"Expected at least 1 TMP_Text under '{root.name}'.");
            for (int i = 0; i < tmps.Length; i++)
            {
                TMP_Text t = tmps[i];
                if (t == null) continue;
                Assert.IsNotNull(t.font, $"TMP_Text '{t.name}' under '{root.name}' has null font.");
                Assert.IsFalse(
                    t.font.name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) >= 0,
                    $"TMP_Text '{t.name}' under '{root.name}' must not use LiberationSans SDF (got '{t.font.name}').");
            }
        }
    }

    public class Tier3_CrossFeaturePairwiseTests
    {
        // 1. Auth <-> Currency / Inventory / StageProgress per-account isolation
        [UnityTest]
        [Category("Tier3")]
        [Category("F7")]
        [Category("F5")]
        [Category("F11")]
        public IEnumerator T3_01_Auth_And_CurrencyInventoryStage_PerAccountIsolation()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                    Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                    Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");

                    Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "AlphaHero", "passA123", "passA123", out string errA), errA);
                    PlayModeTierSupport.GrantCoins(180);
                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Shop");
                    yield return null;

                    E2ETestHelpers.ClickButton("ShopRow_SenzuBean/BuyButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("ShopRow_SpinalFluid/BuyButton");
                    yield return null;

                    Assert.AreEqual(90, PlayModeTierSupport.GetCoins());
                    Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SenzuBean"));
                    Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SpinalFluid"));

                    PlayerPrefs.SetInt("Stickman_UnlockedStage", 5);
                    PlayerPrefs.Save();
                    Tier34Support.SyncActiveProfile(acctMgr);
                    PlayModeTierSupport.InvokeMethod(acctMgr, "Logout");
                    yield return null;

                    Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "BetaHero", "passB123", "passB123", out string errB), errB);
                    Assert.AreEqual(0, PlayModeTierSupport.GetCoins(), "Account B must start with 0 Xu.");
                    Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SenzuBean"), "Account B must not inherit SenzuBean.");
                    Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SpinalFluid"), "Account B must not inherit SpinalFluid.");
                    Assert.AreEqual(1, PlayerPrefs.GetInt("Stickman_UnlockedStage", 1), "Account B must start at unlockedStage 1.");
                    Assert.AreEqual("0 Xu", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("ShopSummaryBar/ShopBalanceText")).Trim());

                    Assert.IsTrue(PlayModeTierSupport.TryLogin(acctMgr, "AlphaHero", "passA123", out string errLogin), errLogin);
                    yield return null;
                    Assert.AreEqual(90, PlayModeTierSupport.GetCoins(), "Account A coins must be restored.");
                    Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SenzuBean"));
                    Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SpinalFluid"));
                    Assert.AreEqual(5, PlayerPrefs.GetInt("Stickman_UnlockedStage", 1));
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // 2. Auth <-> MainMenu / ModeSelect / Shop / CharSelect navigation & PersistentCoinBadge visibility
        [UnityTest]
        [Category("Tier3")]
        [Category("F7")]
        [Category("F2")]
        [Category("F3")]
        [Category("F5")]
        public IEnumerator T3_02_Auth_And_MainMenuModeShopCharNavigation_CoinBadgeRouting()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                    GameObject coinBadge = E2ETestHelpers.FindUIElement("MainMenuCanvas/PersistentCoinBadge");
                    Assert.IsTrue(coinBadge.activeInHierarchy);
                    Assert.AreEqual("Chưa đăng nhập", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("AccountStatusPill/AccountStatusText")).Trim());

                    E2ETestHelpers.ClickButton("Screen1_MainMenu/PlayButton");
                    yield return null;

                    GameObject authPanel = E2ETestHelpers.FindUIElement("SubScreen_Auth");
                    Assert.IsTrue(authPanel.activeInHierarchy, "Unauthenticated PlayButton must route to SubScreen_Auth.");
                    Assert.IsFalse(coinBadge.activeInHierarchy, "PersistentCoinBadge must hide on SubScreen_Auth.");

                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputUsername", "NavHero");
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputPassword", "navpass1");
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputConfirmPassword", "navpass1");
                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthSubmitButton");
                    yield return null;

                    Assert.IsFalse(authPanel.activeInHierarchy);
                    Assert.IsTrue(E2ETestHelpers.FindUIElement("SubScreen_ModeSelect").activeInHierarchy, "Auth via PlayButton must auto-advance to ModeSelect.");
                    Assert.IsTrue(coinBadge.activeInHierarchy, "PersistentCoinBadge must show on ModeSelect.");

                    E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Shop");
                    yield return null;
                    Assert.IsTrue(E2ETestHelpers.FindUIElement("SubScreen_Shop").activeInHierarchy);
                    Assert.IsTrue(coinBadge.activeInHierarchy, "PersistentCoinBadge must show on Shop.");

                    E2ETestHelpers.ClickButton("MainMenuCanvas/BackButton_FixedTopLeft");
                    yield return null;
                    E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Campaign");
                    yield return null;
                    Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen2_CharacterSelect").activeInHierarchy);
                    Assert.IsTrue(coinBadge.activeInHierarchy, "PersistentCoinBadge must show on CharacterSelect.");

                    Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "MainMenu");
                    yield return null;
                    string pillTxt = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("AccountStatusPill/AccountStatusText"));
                    Assert.IsTrue(pillTxt.Contains("NavHero"), $"Expected NavHero in AccountStatusPill, got '{pillTxt}'.");
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // 3. Auth <-> Tab Switch (Register/Login), ConfirmPassword visibility, input/error reset & Back navigation
        [UnityTest]
        [Category("Tier3")]
        [Category("F7")]
        [Category("F2")]
        public IEnumerator T3_03_Auth_TabSwitch_ConfirmPasswordVisibility_ResetAndBackNoSoftLock()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                    E2ETestHelpers.ClickButton("AccountStatusPill/AccountActionButton");
                    yield return null;

                    GameObject authPanel = E2ETestHelpers.FindUIElement("SubScreen_Auth");
                    Assert.IsTrue(authPanel.activeInHierarchy);

                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/TabRegisterButton");
                    yield return null;

                    GameObject confirmObj = E2ETestHelpers.FindUIElement("SubScreen_Auth/AuthModalContainer/InputConfirmPassword");
                    Assert.IsTrue(confirmObj.activeSelf, "InputConfirmPassword must be active on Register tab.");
                    Assert.AreEqual("ĐĂNG KÝ", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("SubScreen_Auth/AuthModalContainer/AuthSubmitButton")).Trim());

                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputUsername", "TabTester");
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputPassword", "pass1234");
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputConfirmPassword", "mismatch");
                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthSubmitButton");
                    yield return null;

                    string errMsg = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("SubScreen_Auth/AuthModalContainer/AuthFeedbackText"));
                    Assert.IsFalse(string.IsNullOrEmpty(errMsg), "Mismatched confirm password must display feedback error.");

                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/TabLoginButton");
                    yield return null;

                    Assert.IsFalse(confirmObj.activeSelf, "InputConfirmPassword must hide on Login tab.");
                    Assert.AreEqual("ĐĂNG NHẬP", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("SubScreen_Auth/AuthModalContainer/AuthSubmitButton")).Trim());
                    Assert.AreEqual(string.Empty, E2ETestHelpers.FindUIElement("SubScreen_Auth/AuthModalContainer/InputPassword").GetComponent<TMP_InputField>().text);
                    Assert.AreEqual(string.Empty, PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("SubScreen_Auth/AuthModalContainer/AuthFeedbackText")));

                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthBackButton");
                    yield return null;

                    Assert.IsFalse(authPanel.activeInHierarchy);
                    Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen1_MainMenu").activeInHierarchy, "AuthBackButton must return cleanly to MainMenu without soft-lock.");
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // 4. Shop <-> CurrencyManager <-> InventoryManager <-> CharSelect Q/E loadout & ResetIcon sync
        [UnityTest]
        [Category("Tier3")]
        [Category("F5")]
        [Category("F4")]
        public IEnumerator T3_04_Shop_Currency_Inventory_CharSelectQE_And_ResetIconSync()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                    Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                    Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "ShopUser", "pass1234", "pass1234", out string err), err);

                    PlayModeTierSupport.GrantCoins(150);
                    Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Shop");
                    yield return null;

                    E2ETestHelpers.ClickButton("ShopRow_SenzuBean/BuyButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("ShopRow_UltraDivineWater/BuyButton");
                    yield return null;

                    Assert.AreEqual(70, PlayModeTierSupport.GetCoins());
                    Assert.AreEqual("SenzuBean", PlayModeTierSupport.GetEquippedItemName(0));
                    Assert.AreEqual("UltraDivineWater", PlayModeTierSupport.GetEquippedItemName(1));

                    E2ETestHelpers.ClickButton("MainMenuCanvas/BackButton_FixedTopLeft");
                    yield return null;
                    E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Campaign");
                    yield return null;

                    GameObject slotQCountObj = E2ETestHelpers.FindUIElement("LoadoutBar/LoadoutSlotQ/CircleSlot/CountBadge/ItemCountTxt");
                    GameObject slotECountObj = E2ETestHelpers.FindUIElement("LoadoutBar/LoadoutSlotE/CircleSlot/CountBadge/ItemCountTxt");
                    Assert.IsNotNull(slotQCountObj, "Expected GameObject at 'LoadoutBar/LoadoutSlotQ/CircleSlot/CountBadge/ItemCountTxt'.");
                    Assert.IsNotNull(slotECountObj, "Expected GameObject at 'LoadoutBar/LoadoutSlotE/CircleSlot/CountBadge/ItemCountTxt'.");
                    string slotQCount = PlayModeTierSupport.ReadAnyText(slotQCountObj);
                    string slotECount = PlayModeTierSupport.ReadAnyText(slotECountObj);
                    Assert.IsTrue(slotQCount.Contains("1"), $"Expected Q count 1 on CharSelect, got '{slotQCount}'.");
                    Assert.IsTrue(slotECount.Contains("1"), $"Expected E count 1 on CharSelect, got '{slotECount}'.");

                    E2ETestHelpers.ClickButton("LoadoutBar/LoadoutSlotQ/CircleSlot");
                    yield return null;
                    Assert.AreEqual("UltraDivineWater", PlayModeTierSupport.GetEquippedItemName(0), "Cycling Q slot among 2 owned items must advance to UltraDivineWater.");

                    GameObject resetBtn = E2ETestHelpers.FindUIElement("Screen2_CharacterSelect/TogglePreviewFormButton");
                    Image resetImg = resetBtn.GetComponent<Image>();
                    Assert.IsNotNull(resetImg.sprite);
                    Assert.AreEqual("Btn_CharSelect_ResetIcon", resetImg.sprite.name);
                    Assert.AreEqual(Image.Type.Simple, resetImg.type);
                    Assert.IsTrue(resetImg.preserveAspect);

                    Image previewImg = E2ETestHelpers.FindUIElement("Screen2_CharacterSelect/BgCharacterPreview").GetComponent<Image>();
                    Sprite normalSprite = previewImg.sprite;
                    E2ETestHelpers.ClickButton("Screen2_CharacterSelect/TogglePreviewFormButton");
                    yield return null;
                    Assert.AreNotEqual(normalSprite, previewImg.sprite);
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // 5. CharSelect <-> Shop <-> Battle HUD (HudItemSlotsPanel & BattleCoinBadge) cross-scene sync
        [UnityTest]
        [Category("Tier3")]
        [Category("F4")]
        [Category("F5")]
        [Category("F9")]
        [Category("F11")]
        public IEnumerator T3_05_CharSelect_Shop_BattleHudLoadoutAndCoinBadge_CrossSceneSync()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                    Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                    Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "SyncHero", "pass1234", "pass1234", out string err), err);

                    PlayModeTierSupport.GrantCoins(120);
                    Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Shop");
                    yield return null;

                    E2ETestHelpers.ClickButton("ShopRow_SenzuBean/BuyButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("ShopRow_SpinalFluid/BuyButton");
                    yield return null;

                    E2ETestHelpers.ClickButton("MainMenuCanvas/BackButton_FixedTopLeft");
                    yield return null;
                    E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Campaign");
                    yield return null;

                    E2ETestHelpers.ClickButton("CharCard_Eren/CardInner");
                    yield return null;
                    E2ETestHelpers.ClickButton("Screen2_CharacterSelect/CharacterNextButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("Screen3_WorldSelect/WorldNextButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("Screen4_StageSelect/StageCard_STAGE 1");

                    yield return Tier34Support.WaitForScene("Stickman_DemoScene");

                    Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                    Assert.IsNotNull(PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer"));
                    Assert.AreEqual(1, PlayerPrefs.GetInt("SelectedCharacter", -1));
                    Assert.AreEqual("30 Xu", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("GameCanvas/BattleCoinBadge/CoinsText")).Trim());
                    Assert.AreEqual("1", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("HudItemSlotsPanel/SlotQ/CountPill/CountText")).Trim());
                    Assert.AreEqual("1", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("HudItemSlotsPanel/SlotE/CountPill/CountText")).Trim());
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // 6. ModeSelect <-> WorldSelect / StageSelect <-> StageManager / LevelManager stage index & Campaign launch
        [UnityTest]
        [Category("Tier3")]
        [Category("F3")]
        [Category("F11")]
        public IEnumerator T3_06_ModeSelect_WorldSelect_StageSelect_StageIndexAndCampaignLaunch()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                    Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                    Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "WorldHero", "pass1234", "pass1234", out string err), err);

                    Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "ModeSelect");
                    yield return null;

                    E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Campaign");
                    yield return null;
                    E2ETestHelpers.ClickButton("Screen2_CharacterSelect/CharacterNextButton");
                    yield return null;

                    Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen3_WorldSelect").activeInHierarchy);
                    E2ETestHelpers.ClickButton("WorldCard_THẾ GIỚI 2_DRAGON BALL/CardInner");
                    yield return null;
                    Assert.AreEqual(1, PlayerPrefs.GetInt("SelectedWorld", -1));

                    E2ETestHelpers.ClickButton("Screen3_WorldSelect/WorldNextButton");
                    yield return null;
                    Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen4_StageSelect").activeInHierarchy);

                    E2ETestHelpers.ClickButton("Screen4_StageSelect/StageCard_STAGE 3");
                    yield return Tier34Support.WaitForScene("Stickman_DemoScene");

                    Assert.AreEqual(6, PlayerPrefs.GetInt("SelectedStageIndex", -1), "World 2 Stage 3 must map to global index 6.");
                    Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                    Assert.AreEqual("Campaign", PlayModeTierSupport.GetInstanceProperty(levelMgr, "CurrentGameMode").ToString());
                    object stageData = PlayModeTierSupport.GetInstanceProperty(levelMgr, "CurrentStageData");
                    Assert.IsNotNull(stageData);
                    Assert.AreEqual(3, (int)PlayModeTierSupport.GetInstanceField(stageData, "stageNumber"));
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // 7. ModeSelect <-> TrainingModeManager <-> HudItemSlotsPanel ("FREE") & BattleCoinBadge (0 gain)
        [UnityTest]
        [Category("Tier3")]
        [Category("F3")]
        [Category("F10")]
        [Category("F9")]
        public IEnumerator T3_07_ModeSelect_TrainingModeManager_HudFreeUseAndZeroReward()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                    Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                    Assert.IsTrue(PlayModeTierSupport.TryRegister(acctMgr, "TrainHero", "pass1234", "pass1234", out string err), err);

                    Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "ModeSelect");
                    yield return null;

                    E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Training");
                    yield return null;
                    E2ETestHelpers.ClickButton("Screen2_CharacterSelect/CharacterNextButton");
                    yield return Tier34Support.WaitForScene("Stickman_DemoScene");

                    GameObject trainingBar = E2ETestHelpers.FindUIElement("GameCanvas/TrainingTopBar");
                    Assert.IsTrue(trainingBar.activeInHierarchy, "TrainingTopBar must be active in Training mode.");

                    Component tm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.GameModes.TrainingModeManager");
                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(tm, "IsActive"));

                    Component inv = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Items.InventoryManager");
                    Component cm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Economy.CurrencyManager");
                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(inv, "IsTrainingFreeUseMode"));
                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(cm, "SuppressEnemyRewards"));

                    Assert.AreEqual("FREE", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("HudItemSlotsPanel/SlotQ/CountPill/CountText")).Trim());

                    int coinsBefore = PlayModeTierSupport.GetCoins();
                    Type cmType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Economy.CurrencyManager");
                    PlayModeTierSupport.InvokeStatic(cmType, "RewardEnemyKill", 25, Vector3.zero);
                    yield return null;
                    Assert.AreEqual(coinsBefore, PlayModeTierSupport.GetCoins(), "Training mode must suppress enemy kill coin rewards.");
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // 8. PlayerVitalsHudUI <-> PlayerItemController Q/E consumable heal/shield & GameUIManager
        [UnityTest]
        [Category("Tier3")]
        [Category("F8")]
        public IEnumerator T3_08_PlayerVitalsHud_PlayerItemController_QEHealShield_And_GameUIManager()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    PlayModeTierSupport.AddOwnedItem("SenzuBean", 1);
                    PlayModeTierSupport.AddOwnedItem("SpinalFluid", 1);
                    PlayerPrefs.SetInt("MDS_EquippedSlot_0", 1);
                    PlayerPrefs.SetInt("MDS_EquippedSlot_1", 2);
                    PlayerPrefs.Save();

                    PlayModeTierSupport.SetSelectedGameMode("Campaign");
                    yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                    Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                    PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                    Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                    Component itemCtrl = Tier34Support.RequirePlayerItemController(player);

                    Slider hpSlider = E2ETestHelpers.FindUIElement("PlayerHUD/PlayerHPBar").GetComponent<Slider>();
                    float maxHp = (float)PlayModeTierSupport.GetInstanceProperty(player, "MaxHealth");
                    PlayModeTierSupport.ApplyFatalDamageToPlayer(player, 35f);
                    yield return null;

                    float damagedHp = (float)PlayModeTierSupport.GetInstanceProperty(player, "CurrentHealth");
                    Assert.Less(damagedHp, maxHp);
                    Assert.Less(hpSlider.value, maxHp);

                    Assert.IsTrue((bool)PlayModeTierSupport.InvokeMethod(itemCtrl, "TryUseSlotQ"));
                    yield return null;
                    Assert.Greater((float)PlayModeTierSupport.GetInstanceProperty(player, "CurrentHealth"), damagedHp);
                    Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SenzuBean"));
                    Assert.AreEqual("0", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("HudItemSlotsPanel/SlotQ/CountPill/CountText")).Trim());

                    Assert.IsTrue((bool)PlayModeTierSupport.InvokeMethod(itemCtrl, "TryUseSlotE"));
                    yield return null;
                    GameObject shieldBar = E2ETestHelpers.FindUIElement("PlayerHUD/ShieldBar");
                    Assert.IsTrue(shieldBar.activeSelf, "ShieldBar must activate after consuming SpinalFluid on E.");
                    Assert.Greater((float)PlayModeTierSupport.GetInstanceProperty(player, "VirtualShieldHP"), 0f);
                    Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SpinalFluid"));
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // 9. EndlessModeManager (EndlessWaveBanner) <-> BattleCoinBadge <-> GameOverModalUI (DefeatSummaryPill)
        [UnityTest]
        [Category("Tier3")]
        [Category("F9")]
        [Category("F3")]
        public IEnumerator T3_09_EndlessModeManager_WaveBanner_BattleCoinBadge_And_DefeatSummaryPill()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    PlayModeTierSupport.SetSelectedGameMode("Endless");
                    yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                    Component endlessMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.GameModes.EndlessModeManager");
                    Assert.IsNotNull(endlessMgr);
                    GameObject banner = E2ETestHelpers.FindUIElement("GameCanvas/EndlessWaveBanner");
                    Assert.IsTrue(banner.activeInHierarchy);
                    Assert.IsTrue(PlayModeTierSupport.ReadAnyText(banner).Contains("WAVE 1"));

                    PlayModeTierSupport.InvokeMethod(endlessMgr, "StartNextWave");
                    yield return null;
                    Assert.AreEqual(2, (int)PlayModeTierSupport.GetInstanceProperty(endlessMgr, "CurrentWave"));
                    Assert.IsTrue(PlayModeTierSupport.ReadAnyText(banner).Contains("WAVE 2"));

                    Component cm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Economy.CurrencyManager");
                    Tier34Support.AddCoinsExact(cm, 35);
                    yield return E2ETestHelpers.WaitForCondition(
                        () => string.Equals(PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("GameCanvas/BattleCoinBadge/CoinsText")).Trim(), "35 Xu", StringComparison.Ordinal),
                        3f,
                        "BattleCoinBadge did not reach '35 Xu'");
                    Assert.AreEqual("35 Xu", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("GameCanvas/BattleCoinBadge/CoinsText")).Trim());

                    Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                    Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                    Component gameUi = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.UI.GameUIManager");
                    PlayModeTierSupport.SetInstanceProperty(player, "TrainingInvincible", false);
                    PlayModeTierSupport.ApplyFatalDamageToPlayer(player, 10000f);

                    yield return E2ETestHelpers.WaitForCondition(
                        () => (bool)PlayModeTierSupport.GetInstanceProperty(gameUi, "IsEndScreenOpen"),
                        4f,
                        "Expected DefeatPanel to open in Endless mode.");
                    yield return null;

                    string summaryTxt = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("DefeatModalCard/DefeatSummaryPill/SummaryText"));
                    Assert.IsTrue(summaryTxt.Contains("Đạt Wave 2"), $"Expected 'Đạt Wave 2' in summary, got '{summaryTxt}'.");
                    Assert.IsTrue(summaryTxt.Contains("+35 Xu"), $"Expected '+35 Xu' in summary, got '{summaryTxt}'.");
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // 10. PauseMenuUI <-> GameUIManager (IsEndScreenOpen pause lock, Time.timeScale 0 -> 1, ExitMatch -> MainMenu)
        [UnityTest]
        [Category("Tier3")]
        [Category("F9")]
        [Category("F11")]
        public IEnumerator T3_10_PauseMenuUI_GameUIManager_EndScreenLock_And_TimeScaleRestore()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    PlayModeTierSupport.SetSelectedGameMode("Campaign");
                    yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                    Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                    PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                    Component gameUi = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.UI.GameUIManager");
                    Component pauseUi = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.PauseMenuUI");

                    E2ETestHelpers.ClickButton("GameCanvas/PauseHudButton");
                    yield return null;
                    Assert.AreEqual(0f, Time.timeScale, 0.001f);
                    Assert.IsTrue(E2ETestHelpers.FindUIElement("GameCanvas/PauseModalPanel").activeSelf);

                    E2ETestHelpers.ClickButton("PauseBox/ResumeButton");
                    yield return null;
                    Assert.AreEqual(1f, Time.timeScale, 0.001f);
                    Assert.IsFalse(E2ETestHelpers.FindUIElement("GameCanvas/PauseModalPanel").activeSelf);

                    Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                    PlayModeTierSupport.SetInstanceProperty(player, "TrainingInvincible", false);
                    PlayModeTierSupport.ApplyFatalDamageToPlayer(player, 10000f);

                    yield return E2ETestHelpers.WaitForCondition(
                        () => (bool)PlayModeTierSupport.GetInstanceProperty(gameUi, "IsEndScreenOpen"),
                        4f,
                        "Expected Defeat end screen to open.");

                    PlayModeTierSupport.InvokeMethod(pauseUi, "SetPaused", true);
                    yield return null;
                    Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(pauseUi, "IsPaused"));
                    Assert.AreEqual(1f, Time.timeScale, 0.001f);

                    E2ETestHelpers.ClickButton("DefeatModalCard/DefeatMenuButton");
                    yield return Tier34Support.WaitForScene("MainMenu");
                    Assert.AreEqual(1f, Time.timeScale, 0.001f);
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // 11. GlassHudTheme 9-slice (F1) <-> Runtime theme overrides across MainMenu (01..06) & Stickman_DemoScene (07..09)
        [UnityTest]
        [Category("Tier3")]
        [Category("F1")]
        [Category("F2")]
        [Category("F3")]
        [Category("F4")]
        [Category("F5")]
        [Category("F6")]
        [Category("F7")]
        [Category("F8")]
        [Category("F9")]
        [Category("F10")]
        public IEnumerator T3_11_GlassHudTheme_9SliceNonSquash_And_VietnameseTmpFont_AcrossScenes()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                    Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");

                    Tier34Support.AssertSlicedImageNonSquashed("Screen1_MainMenu/PlayButton", "Btn_MainMenu_Play_Clean");
                    Tier34Support.AssertTmpFontsNotLiberationSans(E2ETestHelpers.FindUIElement("MainMenuCanvas/PersistentCoinBadge"));

                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "ModeSelect");
                    yield return null;
                    Tier34Support.AssertSlicedImageNonSquashed("SubScreen_ModeSelect/ModeCard_Campaign", "Card_Mode_Campaign_BlankFrame");

                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "CharacterSelect");
                    yield return null;
                    Tier34Support.AssertSlicedImageNonSquashed("Screen2_CharacterSelect/CharacterSelectLeftFrame", "Frame_CharSelect_MainPanel_Clean");

                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Auth");
                    yield return null;
                    Tier34Support.AssertSlicedImageNonSquashed("SubScreen_Auth/AuthModalContainer", "Frame_Auth_ModalPanel_Clean");
                    Tier34Support.AssertTmpFontsNotLiberationSans(E2ETestHelpers.FindUIElement("SubScreen_Auth"));

                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Shop");
                    yield return null;
                    Tier34Support.AssertSlicedImageNonSquashed("SubScreen_Shop/ShopSummaryBar", "Bar_Shop_InventorySummary_Clean");

                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Instructions");
                    yield return null;
                    Tier34Support.AssertSlicedImageNonSquashed("InstrGridContainer/InstrRow_0", "Row_Guide_ControlCard_WithIconBox_Clean");

                    PlayModeTierSupport.SetSelectedGameMode("Training");
                    yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                    Tier34Support.AssertSlicedImageNonSquashed("GameCanvas/PlayerHUD/VitalsBackplate", "Frame_HUD_VitalsBackplate_Clean");
                    Tier34Support.AssertSlicedImageNonSquashed("GameCanvas/TopRightClusterBg", "Frame_HUD_TopRightCluster_Clean");
                    Tier34Support.AssertSlicedImageNonSquashed("GameCanvas/TrainingTopBar", "Frame_Training_MainDock_Clean");
                    Tier34Support.AssertTmpFontsNotLiberationSans(E2ETestHelpers.FindUIElement("GameCanvas/PlayerHUD"));
                    Tier34Support.AssertTmpFontsNotLiberationSans(E2ETestHelpers.FindUIElement("GameCanvas/BattleCoinBadge"));
                    Tier34Support.AssertTmpFontsNotLiberationSans(E2ETestHelpers.FindUIElement("GameCanvas/TrainingTopBar"));
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        // 12. Multi-account Logout -> Register B -> Login A -> Enter Shop & Battle -> Return to Menu -> JSON persistence
        [UnityTest]
        [Category("Tier3")]
        [Category("F7")]
        [Category("F5")]
        [Category("F11")]
        public IEnumerator T3_12_MultiAccount_Logout_RegisterB_LoginA_ShopAndBattle_JsonPersistence()
        {
            using (var backup = new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");

                    // Register Account A via UI
                    E2ETestHelpers.ClickButton("AccountStatusPill/AccountActionButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/TabRegisterButton");
                    yield return null;
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputUsername", "HeroOne");
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputPassword", "pass1111");
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputConfirmPassword", "pass1111");
                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthSubmitButton");
                    yield return null;

                    PlayModeTierSupport.GrantCoins(100);
                    Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Shop");
                    yield return null;
                    E2ETestHelpers.ClickButton("ShopRow_SenzuBean/BuyButton");
                    yield return null;
                    Assert.AreEqual(70, PlayModeTierSupport.GetCoins());

                    // Logout via pill -> Register B via UI
                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "MainMenu");
                    yield return null;
                    E2ETestHelpers.ClickButton("AccountStatusPill/AccountLogoutButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("AccountStatusPill/AccountActionButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/TabRegisterButton");
                    yield return null;
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputUsername", "HeroTwo");
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputPassword", "pass2222");
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputConfirmPassword", "pass2222");
                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthSubmitButton");
                    yield return null;

                    Assert.AreEqual(0, PlayModeTierSupport.GetCoins(), "HeroTwo must have 0 Xu.");
                    Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SenzuBean"));

                    // Switch back to HeroOne via UI Login
                    E2ETestHelpers.ClickButton("AccountStatusPill/AccountActionButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/TabLoginButton");
                    yield return null;
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputUsername", "HeroOne");
                    Tier34Support.SetInputFieldText("SubScreen_Auth/AuthModalContainer/InputPassword", "pass1111");
                    E2ETestHelpers.ClickButton("SubScreen_Auth/AuthModalContainer/AuthSubmitButton");
                    yield return null;

                    Assert.AreEqual(70, PlayModeTierSupport.GetCoins());
                    Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SenzuBean"));

                    // Enter Battle, earn +25 Xu, return to MainMenu
                    E2ETestHelpers.ClickButton("Screen1_MainMenu/PlayButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Campaign");
                    yield return null;
                    E2ETestHelpers.ClickButton("Screen2_CharacterSelect/CharacterNextButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("Screen3_WorldSelect/WorldNextButton");
                    yield return null;
                    E2ETestHelpers.ClickButton("Screen4_StageSelect/StageCard_STAGE 1");
                    yield return Tier34Support.WaitForScene("Stickman_DemoScene");

                    Component cm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Economy.CurrencyManager");
                    Tier34Support.AddCoinsExact(cm, 25);
                    yield return null;
                    Assert.AreEqual(95, PlayModeTierSupport.GetCoins());

                    E2ETestHelpers.ClickButton("GameCanvas/HudMenuButton");
                    yield return Tier34Support.WaitForScene("MainMenu");

                    Assert.IsTrue(File.Exists(backup.AccountsFilePath), "stickman_accounts.json must exist on disk.");
                    string jsonText = File.ReadAllText(backup.AccountsFilePath);
                    Assert.IsTrue(jsonText.Contains("HeroOne") && jsonText.Contains("HeroTwo"), "Both accounts must be persisted in stickman_accounts.json.");

                    // Force cold reload of AccountManager from disk
                    Component acctMgr = PlayModeTierSupport.RequireAccountManager();
                    UnityEngine.Object.DestroyImmediate(acctMgr.gameObject);
                    yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                    Component reloadedMgr = PlayModeTierSupport.RequireAccountManager();

                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(reloadedMgr, "HasActiveSession"));
                    Assert.AreEqual(95, PlayModeTierSupport.GetCoins(), "HeroOne's 95 Xu must persist across cold AccountManager reload.");
                    Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SenzuBean"));
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }
    }
}
