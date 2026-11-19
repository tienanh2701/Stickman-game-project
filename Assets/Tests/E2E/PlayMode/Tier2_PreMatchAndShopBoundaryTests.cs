using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StickmanBeatEmUp.Tests.E2E.PlayMode
{
    public class Tier2_PreMatchAndShopBoundaryTests
    {
        // ==================== F2: 01_MainMenu Boundaries (3 Tests) ====================

        [UnityTest]
        [Category("Tier2")]
        [Category("F2")]
        public IEnumerator F02_T2_MainMenuButtons_ExactSizes_Sliced_And_WhiteTint()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");

                string[] names =
                {
                    "Screen1_MainMenu/PlayButton",
                    "Screen1_MainMenu/InstructionsButton",
                    "Screen1_MainMenu/ExitButton",
                };
                float[] expectedW = { 460f, 430f, 430f };
                float[] expectedH = { 106f, 98f, 98f };

                for (int i = 0; i < names.Length; i++)
                {
                    GameObject go = E2ETestHelpers.FindUIElement(names[i]);
                    RectTransform rt = go.GetComponent<RectTransform>();
                    Image img = go.GetComponent<Image>();

                    Assert.AreEqual(expectedW[i], rt.sizeDelta.x, 1f, $"Width mismatch on '{names[i]}'.");
                    Assert.AreEqual(expectedH[i], rt.sizeDelta.y, 1f, $"Height mismatch on '{names[i]}'.");
                    Assert.AreEqual(Image.Type.Sliced, img.type, $"'{names[i]}' must be Sliced.");
                    Assert.IsFalse(img.preserveAspect, $"'{names[i]}' Sliced button must disable preserveAspect.");
                    Assert.AreEqual(1f, img.color.a, 0.01f, $"'{names[i]}' must be opaque.");
                    Assert.LessOrEqual(img.pixelsPerUnitMultiplier, 12f);
                }
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F2")]
        public IEnumerator F02_T2_MainMenuButtons_HaveModeCardHoverEffect_And_VietnameseLabels()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Type hoverType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.ModeCardHoverEffect");

                string[] names =
                {
                    "Screen1_MainMenu/PlayButton",
                    "Screen1_MainMenu/InstructionsButton",
                    "Screen1_MainMenu/ExitButton",
                };
                string[] expectedLabels = { "CHƠI", "HƯỚNG DẪN", "THOÁT" };

                for (int i = 0; i < names.Length; i++)
                {
                    GameObject go = E2ETestHelpers.FindUIElement(names[i]);
                    Assert.IsNotNull(go.GetComponent(hoverType), $"ModeCardHoverEffect missing on '{names[i]}'.");
                    string label = PlayModeTierSupport.ReadAnyText(go).Trim();
                    Assert.AreEqual(expectedLabels[i], label, $"Label mismatch on '{names[i]}'.");
                }
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F2")]
        public IEnumerator F02_T2_BackButtonFixedTopLeft_HiddenOnMainMenu_VisibleOnSubScreens()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                GameObject backBtn = E2ETestHelpers.FindUIElement("MainMenuCanvas/BackButton_FixedTopLeft");

                Assert.IsFalse(backBtn.activeInHierarchy, "BackButton must be hidden on MainMenu root.");

                string[] subScreens = { "Instructions", "ModeSelect", "CharacterSelect", "Shop" };
                for (int i = 0; i < subScreens.Length; i++)
                {
                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, subScreens[i]);
                    yield return null;
                    Assert.IsTrue(backBtn.activeInHierarchy, $"BackButton must be visible on {subScreens[i]}.");
                }

                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "MainMenu");
                yield return null;
                Assert.IsFalse(backBtn.activeInHierarchy, "BackButton must hide again on returning to MainMenu.");
            }
        }

        // ==================== F3: 02_ModeSelect Boundaries (3 Tests) ====================

        [UnityTest]
        [Category("Tier2")]
        [Category("F3")]
        public IEnumerator F03_T2_ModeSelect_EndlessAndTraining_SetGameMode_And_OpenCharSelect()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");

                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "ModeSelect");
                yield return null;
                E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Endless");
                yield return null;
                Assert.AreEqual("Endless", PlayModeTierSupport.GetSelectedGameModeName());
                Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen2_CharacterSelect").activeInHierarchy);

                E2ETestHelpers.ClickButton("MainMenuCanvas/BackButton_FixedTopLeft");
                yield return null;
                Assert.IsTrue(E2ETestHelpers.FindUIElement("SubScreen_ModeSelect").activeInHierarchy);

                E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Training");
                yield return null;
                Assert.AreEqual("Training", PlayModeTierSupport.GetSelectedGameModeName());
                Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen2_CharacterSelect").activeInHierarchy);
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F3")]
        public IEnumerator F03_T2_ModeSelect_4Cards_HaveBlankFrameAndModeIcons_WithPreserveAspect()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "ModeSelect");
                yield return null;

                string[][] cardSpecs =
                {
                    new[] { "ModeCard_Campaign", "Card_Mode_Campaign_BlankFrame", "Icon_Mode_Campaign_ShieldSwords" },
                    new[] { "ModeCard_Endless", "Card_Mode_Endless_BlankFrame", "Icon_Mode_Endless_InfinityGear" },
                    new[] { "ModeCard_Training", "Card_Mode_Training_BlankFrame", "Icon_Mode_Training_CyberFist" },
                    new[] { "ModeCard_Shop", "Card_Mode_Shop_BlankFrame", "Icon_Mode_Shop_CyberCart" },
                };

                for (int i = 0; i < cardSpecs.Length; i++)
                {
                    GameObject card = E2ETestHelpers.FindUIElement("SubScreen_ModeSelect/" + cardSpecs[i][0]);
                    Image cardImg = card.GetComponent<Image>();
                    Assert.IsNotNull(cardImg.sprite);
                    Assert.AreEqual(cardSpecs[i][1], cardImg.sprite.name);
                    Assert.AreEqual(Image.Type.Sliced, cardImg.type);

                    Transform iconTr = card.transform.Find("ModeIcon");
                    Assert.IsNotNull(iconTr, $"ModeIcon child missing on '{cardSpecs[i][0]}'.");
                    Assert.IsTrue(iconTr.gameObject.activeInHierarchy);
                    Image iconImg = iconTr.GetComponent<Image>();
                    Assert.IsNotNull(iconImg.sprite);
                    Assert.AreEqual(cardSpecs[i][2], iconImg.sprite.name);
                    Assert.AreEqual(Image.Type.Simple, iconImg.type);
                    Assert.IsTrue(iconImg.preserveAspect);
                }
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F3")]
        public IEnumerator F03_T2_PersistentCoinBadge_UpdatesOnCurrencyChange_WithCoinXuIcon()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                PlayModeTierSupport.GrantCoins(125);

                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "ModeSelect");
                yield return null;

                GameObject badge = E2ETestHelpers.FindUIElement("MainMenuCanvas/PersistentCoinBadge");
                Assert.IsTrue(badge.activeInHierarchy);
                Assert.AreEqual("Badge_ModeSelect_CoinTopRight_Blank", badge.GetComponent<Image>().sprite.name);
                Assert.AreEqual("Icon_Currency_CoinXu", E2ETestHelpers.FindUIElement("PersistentCoinBadge/CoinIcon").GetComponent<Image>().sprite.name);
                Assert.AreEqual("125 Xu", PlayModeTierSupport.ReadAnyText(badge).Trim());
            }
        }

        // ==================== F4: 03_CharacterAndItemSelect Boundaries (3 Tests) ====================

        [UnityTest]
        [Category("Tier2")]
        [Category("F4")]
        public IEnumerator F04_T2_LockedCharacterSlots_NotInteractable_And_SelectCharacterFeedback()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "CharacterSelect");
                yield return null;

                Button lock1 = E2ETestHelpers.FindUIElement("CharCard_Locked1/CardInner").GetComponent<Button>();
                Button lock2 = E2ETestHelpers.FindUIElement("CharCard_Locked2/CardInner").GetComponent<Button>();
                Assert.IsFalse(lock1.interactable, "CharCard_Locked1 must not be interactable.");
                Assert.IsFalse(lock2.interactable, "CharCard_Locked2 must not be interactable.");

                PlayModeTierSupport.InvokeMethod(uiMgr, "SelectCharacter", 0);
                PlayModeTierSupport.InvokeMethod(uiMgr, "SelectCharacter", 3);
                yield return null;

                Assert.AreEqual(0, PlayerPrefs.GetInt("SelectedCharacter", -1), "Selecting locked character must not mutate SelectedCharacter.");
                string fb = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("Screen2_CharacterSelect/CharSelectFeedback"));
                Assert.IsTrue(fb.Contains("đang phát triển"), $"Expected locked feedback, got '{fb}'.");
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F4")]
        public IEnumerator F04_T2_LoadoutQECycle_EmptyInventoryFeedback_And_OwnedItemCycling()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "CharacterSelect");
                yield return null;

                // With 0 owned items, clicking Q displays empty-inventory feedback
                E2ETestHelpers.ClickButton("LoadoutSlotQ/CircleSlot");
                yield return null;
                string emptyFb = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("Screen2_CharacterSelect/CharSelectFeedback"));
                Assert.IsTrue(emptyFb.Contains("Chưa sở hữu"), $"Expected empty inventory feedback, got '{emptyFb}'.");

                // Grant SenzuBean and SpinalFluid, then cycle Q & E
                PlayModeTierSupport.AddOwnedItem("SenzuBean", 2);
                PlayModeTierSupport.AddOwnedItem("SpinalFluid", 1);
                string qBefore = PlayModeTierSupport.GetEquippedItemName(0);
                E2ETestHelpers.ClickButton("LoadoutSlotQ/CircleSlot");
                yield return null;
                string qAfter = PlayModeTierSupport.GetEquippedItemName(0);
                Assert.AreNotEqual(qBefore, qAfter, "Clicking LoadoutSlotQ with owned items must cycle equipped item.");
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F4")]
        public IEnumerator F04_T2_CharacterSelectScene_ResolvesManagerButtons_And_SelectsEren()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("CharacterSelectScene");
                Component mgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.CharacterSelectManager");
                Assert.IsNotNull(mgr, "CharacterSelectManager missing in CharacterSelectScene.");

                Button[] charButtons = PlayModeTierSupport.GetInstanceField(mgr, "characterButtons") as Button[];
                Assert.IsNotNull(charButtons);
                Assert.AreEqual(3, charButtons.Length);

                E2ETestHelpers.ClickButton(charButtons[1]);
                yield return null;
                Assert.AreEqual(1, PlayerPrefs.GetInt("SelectedCharacter", -1));

                E2ETestHelpers.ClickButton("ToggleFormPreviewButton");
                yield return null;
                Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceField(mgr, "showTransformedPortrait"));
            }
        }

        // ==================== F5: 05_Shop Boundaries (3 Tests) ====================

        [UnityTest]
        [Category("Tier2")]
        [Category("F5")]
        public IEnumerator F05_T2_ShopPurchase_InsufficientCoins29Vs30_And_ZeroCoins_FailsCleanly()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Shop");
                yield return null;

                Component shopMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.Economy.ShopManager");
                Assert.IsNotNull(shopMgr);

                // Boundary 0 coins
                bool okZero = PlayModeTierSupport.TryPurchaseItem(shopMgr, "SenzuBean", out string fbZero);
                Assert.IsFalse(okZero);
                Assert.AreEqual("Không đủ Xu!", fbZero);
                Assert.AreEqual(0, PlayModeTierSupport.GetCoins());
                Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SenzuBean"));

                // Boundary 29 coins (< 30 price)
                PlayModeTierSupport.GrantCoins(29);
                bool ok29 = PlayModeTierSupport.TryPurchaseItem(shopMgr, "SenzuBean", out string fb29);
                Assert.IsFalse(ok29);
                Assert.AreEqual("Không đủ Xu!", fb29);
                Assert.AreEqual(29, PlayModeTierSupport.GetCoins());
                Assert.AreEqual(0, PlayModeTierSupport.GetOwnedCount("SenzuBean"));
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F5")]
        public IEnumerator F05_T2_ShopPurchase_ExactPrice30Equals30_Succeeds_And_NoneTypeRejected()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                PlayModeTierSupport.GrantCoins(30);
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Shop");
                yield return null;

                Component shopMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.Economy.ShopManager");
                Assert.IsFalse(PlayModeTierSupport.TryPurchaseItem(shopMgr, "None", out _), "None item type must be rejected.");

                bool okExact = PlayModeTierSupport.TryPurchaseItem(shopMgr, "SenzuBean", out string fbExact);
                Assert.IsTrue(okExact, $"Purchase at exact price 30==30 failed: {fbExact}");
                Assert.AreEqual(0, PlayModeTierSupport.GetCoins());
                Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SenzuBean"));
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F5")]
        public IEnumerator F05_T2_ShopPurchase_AutoEquipsEmptyOrZeroOwnedSlot_And_PersistsAcrossReload()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                PlayModeTierSupport.GrantCoins(110);
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Shop");
                yield return null;

                E2ETestHelpers.ClickButton("ShopRow_UltraDivineWater/BuyButton");
                yield return null;
                Assert.AreEqual("UltraDivineWater", PlayModeTierSupport.GetEquippedItemName(0), "First purchase with 0-owned defaults should auto-equip Slot Q.");

                E2ETestHelpers.ClickButton("ShopRow_SpinalFluid/BuyButton");
                yield return null;
                Assert.AreEqual("SpinalFluid", PlayModeTierSupport.GetEquippedItemName(1), "Second distinct purchase should keep Slot E.");
                Assert.AreEqual(0, PlayModeTierSupport.GetCoins());

                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Assert.AreEqual(0, PlayModeTierSupport.GetCoins());
                Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("UltraDivineWater"));
                Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SpinalFluid"));
            }
        }

        // ==================== F6: 06_InstructionsGuide Boundaries (3 Tests) ====================

        [UnityTest]
        [Category("Tier2")]
        [Category("F6")]
        public IEnumerator F06_T2_Instructions_Row9Ultimate_HasWarmAccentTint_DistinctFromRow0()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                E2ETestHelpers.ClickButton("Screen1_MainMenu/InstructionsButton");
                yield return null;

                Image row0 = E2ETestHelpers.FindUIElement("InstrGridContainer/InstrRow_0").GetComponent<Image>();
                Image row9 = E2ETestHelpers.FindUIElement("InstrGridContainer/InstrRow_9").GetComponent<Image>();

                Assert.Greater(row0.color.b, 0.98f, "Row 0 should use neutral white tint.");
                Assert.Less(row9.color.b, 0.90f, "Row 9 ('O' Ultimate) should have warm gold accent tint.");
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F6")]
        public IEnumerator F06_T2_Instructions_All10CardsFitWithinInstrGridContainer_InPlayMode()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                E2ETestHelpers.ClickButton("Screen1_MainMenu/InstructionsButton");
                yield return null;

                Canvas canvas = E2ETestHelpers.FindUIElement("MainMenuCanvas").GetComponent<Canvas>();
                Rect container = E2ETestHelpers.GetRectBoundsIn1920x1080(
                    E2ETestHelpers.FindUIElement("SubScreen_Instructions/InstrGridContainer").GetComponent<RectTransform>(),
                    canvas);

                for (int i = 0; i < 10; i++)
                {
                    Rect card = E2ETestHelpers.GetRectBoundsIn1920x1080(
                        E2ETestHelpers.FindUIElement($"InstrGridContainer/InstrRow_{i}").GetComponent<RectTransform>(),
                        canvas);
                    Assert.GreaterOrEqual(card.xMin, container.xMin - 6f);
                    Assert.LessOrEqual(card.xMax, container.xMax + 6f);
                    Assert.GreaterOrEqual(card.yMin, container.yMin - 6f);
                    Assert.LessOrEqual(card.yMax, container.yMax + 6f);
                }
            }
        }

        [UnityTest]
        [Category("Tier2")]
        [Category("F6")]
        public IEnumerator F06_T2_Instructions_EnsureCoreKeybindsPresent_IsIdempotentAt10Items()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                E2ETestHelpers.ClickButton("Screen1_MainMenu/InstructionsButton");
                yield return null;

                Component instrUi = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.InstructionsUI");
                Assert.IsNotNull(instrUi);

                PlayModeTierSupport.InvokeMethod(instrUi, "EnsureCoreKeybindsPresent", null, null);
                PlayModeTierSupport.InvokeMethod(instrUi, "EnsureCoreKeybindsPresent", null, null);

                Array items = PlayModeTierSupport.GetInstanceProperty(instrUi, "Items") as Array;
                Assert.IsNotNull(items);
                Assert.AreEqual(10, items.Length, "EnsureCoreKeybindsPresent must remain idempotent at 10 items.");
            }
        }
    }
}
