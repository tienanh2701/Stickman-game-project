using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StickmanBeatEmUp.Tests.E2E.EditMode
{
    public class Tier2_SceneGeometryAndGuidParityTests
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string CharSelectScenePath = "Assets/Scenes/CharacterSelectScene.unity";
        private const string DemoScenePath = "Assets/Scenes/Stickman_DemoScene.unity";

        // ==================== F2: 01_MainMenu Geometry & Serialized GUID Parity (2 Tests) ====================

        [Test]
        [Category("Tier2")]
        [Category("F2")]
        public void F02_T2_MainMenuButtons_NoVerticalOverlap_And_Inside1920x1080()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Canvas canvas = EditModeTierSupport.FindRootCanvas(scene);
            Assert.IsNotNull(canvas);

            Rect title = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "Screen1_MainMenu/GameTitle");
            Rect play = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "Screen1_MainMenu/PlayButton");
            Rect guide = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "Screen1_MainMenu/InstructionsButton");
            Rect exit = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "Screen1_MainMenu/ExitButton");

            EditModeTierSupport.AssertRectInside1920x1080(title, "GameTitle");
            EditModeTierSupport.AssertRectInside1920x1080(play, "PlayButton");
            EditModeTierSupport.AssertRectInside1920x1080(guide, "InstructionsButton");
            EditModeTierSupport.AssertRectInside1920x1080(exit, "ExitButton");

            Assert.Greater(title.yMin, play.yMax, "GameTitle must sit above PlayButton.");
            Assert.Greater(play.yMin, guide.yMax, "PlayButton must sit above InstructionsButton.");
            Assert.Greater(guide.yMin, exit.yMax, "InstructionsButton must sit above ExitButton.");
        }

        [Test]
        [Category("Tier2")]
        [Category("F2")]
        public void F02_T2_MainMenuScene_Serialized01SlicedUISprites_MatchContract()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            string[][] expected =
            {
                new[] { "Screen1_MainMenu/PlayButton", "Btn_MainMenu_Play_Clean" },
                new[] { "Screen1_MainMenu/InstructionsButton", "Btn_MainMenu_Guide_Clean" },
                new[] { "Screen1_MainMenu/ExitButton", "Btn_MainMenu_Quit_Clean" },
            };

            for (int i = 0; i < expected.Length; i++)
            {
                Image img = EditModeTierSupport.RequireUIElement(scene, expected[i][0]).GetComponent<Image>();
                Assert.IsNotNull(img, $"Image missing on '{expected[i][0]}'.");
                Assert.IsNotNull(img.sprite, $"Sprite missing on '{expected[i][0]}'.");
                Assert.AreEqual(
                    expected[i][1],
                    img.sprite.name,
                    $"MainMenu.unity '{expected[i][0]}' awaits M5 scene rebuild for SlicedUI sprite '{expected[i][1]}' (currently '{img.sprite.name}').");
                Assert.AreEqual(Image.Type.Sliced, img.type, $"Expected Sliced type on '{expected[i][0]}'.");
            }
        }

        // ==================== F3: 02_ModeSelect Geometry & Serialized GUID Parity (2 Tests) ====================

        [Test]
        [Category("Tier2")]
        [Category("F3")]
        public void F03_T2_ModeSelect_4CardsAndTopBar_OrderedNonOverlapping_In1920x1080()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Canvas canvas = EditModeTierSupport.FindRootCanvas(scene);

            Rect campaign = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "SubScreen_ModeSelect/ModeCard_Campaign");
            Rect endless = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "SubScreen_ModeSelect/ModeCard_Endless");
            Rect training = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "SubScreen_ModeSelect/ModeCard_Training");
            Rect shop = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "SubScreen_ModeSelect/ModeCard_Shop");
            Rect back = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "MainMenuCanvas/BackButton_FixedTopLeft");
            Rect coin = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "MainMenuCanvas/PersistentCoinBadge");

            Rect[] all = { campaign, endless, training, shop, back, coin };
            string[] names = { "Campaign", "Endless", "Training", "Shop", "Back", "CoinBadge" };
            for (int i = 0; i < all.Length; i++)
            {
                EditModeTierSupport.AssertRectInside1920x1080(all[i], names[i]);
            }

            Assert.Less(campaign.xMax, endless.xMin, "Campaign must sit left of Endless.");
            Assert.Less(endless.xMax, training.xMin, "Endless must sit left of Training.");
            Assert.Less(training.xMax, shop.xMin, "Training must sit left of Shop.");

            EditModeTierSupport.AssertRectsDoNotOverlap(back, coin, "BackButton_FixedTopLeft", "PersistentCoinBadge");
            EditModeTierSupport.AssertRectsDoNotOverlap(back, campaign, "BackButton_FixedTopLeft", "ModeCard_Campaign");
        }

        [Test]
        [Category("Tier2")]
        [Category("F3")]
        public void F03_T2_ModeSelect_Serialized02SlicedUISprites_MatchContract()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            string[][] expected =
            {
                new[] { "SubScreen_ModeSelect/ModeCard_Campaign", "Card_Mode_Campaign_BlankFrame" },
                new[] { "SubScreen_ModeSelect/ModeCard_Endless", "Card_Mode_Endless_BlankFrame" },
                new[] { "SubScreen_ModeSelect/ModeCard_Training", "Card_Mode_Training_BlankFrame" },
                new[] { "SubScreen_ModeSelect/ModeCard_Shop", "Card_Mode_Shop_BlankFrame" },
                new[] { "MainMenuCanvas/BackButton_FixedTopLeft", "Btn_ModeSelect_Back_Clean" },
                new[] { "MainMenuCanvas/PersistentCoinBadge", "Badge_ModeSelect_CoinTopRight_Blank" },
                new[] { "MainMenuCanvas/PersistentCoinBadge/CoinIcon", "Icon_Currency_CoinXu" },
            };

            for (int i = 0; i < expected.Length; i++)
            {
                Image img = EditModeTierSupport.RequireUIElement(scene, expected[i][0]).GetComponent<Image>();
                Assert.IsNotNull(img, $"Image missing on '{expected[i][0]}'.");
                Assert.IsNotNull(img.sprite, $"Sprite missing on '{expected[i][0]}'.");
                Assert.AreEqual(
                    expected[i][1],
                    img.sprite.name,
                    $"MainMenu.unity '{expected[i][0]}' awaits M5 scene rebuild for '{expected[i][1]}' (currently '{img.sprite.name}').");
            }
        }

        // ==================== F4: 03_CharacterAndItemSelect Geometry & Parity (2 Tests) ====================

        [Test]
        [Category("Tier2")]
        [Category("F4")]
        public void F04_T2_MainMenuCharSelect_5SlotsAndLoadoutQE_NonOverlapping_InBounds()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Canvas canvas = EditModeTierSupport.FindRootCanvas(scene);
            Assert.IsNotNull(canvas, "Root Canvas missing in MainMenu.unity.");

            string[] slots =
            {
                "CharacterSelectLeftFrame/CharCard_Goku",
                "CharacterSelectLeftFrame/CharCard_Eren",
                "CharacterSelectLeftFrame/CharCard_Diend",
                "CharacterSelectLeftFrame/CharCard_Locked1",
                "CharacterSelectLeftFrame/CharCard_Locked2",
            };
            Rect leftFrame = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "Screen2_CharacterSelect/CharacterSelectLeftFrame");
            Rect loadoutBar = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "Screen2_CharacterSelect/LoadoutBar");
            Rect slotQ = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "LoadoutBar/LoadoutSlotQ");
            Rect slotE = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "LoadoutBar/LoadoutSlotE");

            Rect[] slotRects = new Rect[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                slotRects[i] = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, slots[i]);
                EditModeTierSupport.AssertRectContainedIn(slotRects[i], leftFrame, slots[i], "CharacterSelectLeftFrame", 12f);
                EditModeTierSupport.AssertRectsDoNotOverlap(slotRects[i], loadoutBar, slots[i], "LoadoutBar");
            }

            for (int i = 0; i < slotRects.Length; i++)
            {
                for (int j = i + 1; j < slotRects.Length; j++)
                {
                    EditModeTierSupport.AssertRectsDoNotOverlap(slotRects[i], slotRects[j], slots[i], slots[j]);
                }
            }

            EditModeTierSupport.AssertRectContainedIn(slotQ, loadoutBar, "LoadoutSlotQ", "LoadoutBar");
            EditModeTierSupport.AssertRectContainedIn(slotE, loadoutBar, "LoadoutSlotE", "LoadoutBar");
            EditModeTierSupport.AssertRectsDoNotOverlap(slotQ, slotE, "LoadoutSlotQ", "LoadoutSlotE");
        }

        [Test]
        [Category("Tier2")]
        [Category("F4")]
        public void F04_T2_CharSelect_Serialized03SlicedUISprites_And_12StagesNonOverlapping()
        {
            Scene csScene = EditorSceneManager.OpenScene(CharSelectScenePath, OpenSceneMode.Single);
            Canvas csCanvas = EditModeTierSupport.FindRootCanvas(csScene);
            Rect[] stageRects = new Rect[12];
            int idx = 0;
            for (int w = 1; w <= 3; w++)
            {
                for (int s = 1; s <= 4; s++)
                {
                    stageRects[idx] = EditModeTierSupport.GetElementBounds1920x1080(csScene, csCanvas, $"StageButton_W{w}_S{s}");
                    EditModeTierSupport.AssertRectInside1920x1080(stageRects[idx], $"StageButton_W{w}_S{s}");
                    idx++;
                }
            }
            for (int i = 0; i < stageRects.Length; i++)
            {
                for (int j = i + 1; j < stageRects.Length; j++)
                {
                    EditModeTierSupport.AssertRectsDoNotOverlap(stageRects[i], stageRects[j], $"Stage_{i}", $"Stage_{j}");
                }
            }

            Scene mmScene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Image leftFrameImg = EditModeTierSupport.RequireUIElement(mmScene, "Screen2_CharacterSelect/CharacterSelectLeftFrame").GetComponent<Image>();
            Assert.IsNotNull(leftFrameImg.sprite);
            Assert.AreEqual(
                "Frame_CharSelect_MainPanel_Clean",
                leftFrameImg.sprite.name,
                $"MainMenu.unity CharacterSelectLeftFrame awaits M5 scene rebuild (currently '{leftFrameImg.sprite.name}').");
        }

        // ==================== F5: 05_Shop Geometry & Serialized GUID Parity (2 Tests) ====================

        [Test]
        [Category("Tier2")]
        [Category("F5")]
        public void F05_T2_ShopRows_NonOverlapping_And_Within1920x1080()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Canvas canvas = EditModeTierSupport.FindRootCanvas(scene);
            Assert.IsNotNull(canvas, "Root Canvas missing in MainMenu.unity.");

            Rect summary = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "SubScreen_Shop/ShopSummaryBar");
            Rect row1 = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "ShopRowsContainer/ShopRow_SenzuBean");
            Rect row2 = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "ShopRowsContainer/ShopRow_SpinalFluid");
            Rect row3 = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "ShopRowsContainer/ShopRow_UltraDivineWater");

            EditModeTierSupport.AssertRectInside1920x1080(summary, "ShopSummaryBar");
            EditModeTierSupport.AssertRectInside1920x1080(row1, "ShopRow_SenzuBean");
            EditModeTierSupport.AssertRectInside1920x1080(row2, "ShopRow_SpinalFluid");
            EditModeTierSupport.AssertRectInside1920x1080(row3, "ShopRow_UltraDivineWater");

            EditModeTierSupport.AssertRectsDoNotOverlap(summary, row1, "ShopSummaryBar", "ShopRow_SenzuBean");
            EditModeTierSupport.AssertRectsDoNotOverlap(row1, row2, "ShopRow_SenzuBean", "ShopRow_SpinalFluid");
            EditModeTierSupport.AssertRectsDoNotOverlap(row2, row3, "ShopRow_SpinalFluid", "ShopRow_UltraDivineWater");
        }

        [Test]
        [Category("Tier2")]
        [Category("F5")]
        public void F05_T2_ShopPanel_Serialized05SlicedUISprites_MatchContract()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            string[][] expected =
            {
                new[] { "SubScreen_Shop/ShopSummaryBar", "Bar_Shop_InventorySummary_Clean" },
                new[] { "ShopRowsContainer/ShopRow_SenzuBean", "Row_Shop_ItemStrip_Clean" },
                new[] { "ShopRowsContainer/ShopRow_SenzuBean/BuyButton", "Btn_Shop_Buy_Clean" },
            };

            for (int i = 0; i < expected.Length; i++)
            {
                Image img = EditModeTierSupport.RequireUIElement(scene, expected[i][0]).GetComponent<Image>();
                Assert.IsNotNull(img.sprite);
                Assert.AreEqual(
                    expected[i][1],
                    img.sprite.name,
                    $"MainMenu.unity '{expected[i][0]}' awaits M5 scene rebuild for '{expected[i][1]}' (currently '{img.sprite.name}').");
            }
        }

        // ==================== F6: 06_InstructionsGuide Geometry & Parity (2 Tests) ====================

        [Test]
        [Category("Tier2")]
        [Category("F6")]
        public void F06_T2_Instructions_2x5Grid_Aligned_NonOverlapping_InsideContainer()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Canvas canvas = EditModeTierSupport.FindRootCanvas(scene);

            Rect container = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "SubScreen_Instructions/InstrGridContainer");
            EditModeTierSupport.AssertRectInside1920x1080(container, "InstrGridContainer");

            Rect[] rows = new Rect[10];
            for (int i = 0; i < 10; i++)
            {
                rows[i] = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, $"InstrGridContainer/InstrRow_{i}");
                EditModeTierSupport.AssertRectContainedIn(rows[i], container, $"InstrRow_{i}", "InstrGridContainer", 6f);
            }

            for (int i = 0; i < 5; i++)
            {
                Assert.Less(rows[i].xMax, rows[i + 5].xMin, $"Left column InstrRow_{i} must sit left of InstrRow_{i + 5}.");
                if (i < 4)
                {
                    Assert.Greater(rows[i].yMin, rows[i + 1].yMax, $"InstrRow_{i} must sit above InstrRow_{i + 1}.");
                    Assert.Greater(rows[i + 5].yMin, rows[i + 6].yMax, $"InstrRow_{i + 5} must sit above InstrRow_{i + 6}.");
                }
            }

            for (int i = 0; i < 10; i++)
            {
                for (int j = i + 1; j < 10; j++)
                {
                    EditModeTierSupport.AssertRectsDoNotOverlap(rows[i], rows[j], $"InstrRow_{i}", $"InstrRow_{j}");
                }
            }
        }

        [Test]
        [Category("Tier2")]
        [Category("F6")]
        public void F06_T2_Instructions_Serialized06SlicedUISprites_MatchContract()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Image gridImg = EditModeTierSupport.RequireUIElement(scene, "SubScreen_Instructions/InstrGridContainer").GetComponent<Image>();
            Assert.IsNotNull(gridImg.sprite);
            Assert.AreEqual(
                "Frame_Guide_MainPanel_Clean",
                gridImg.sprite.name,
                $"MainMenu.unity InstrGridContainer awaits M5 scene rebuild (currently '{gridImg.sprite.name}').");

            Image row0Img = EditModeTierSupport.RequireUIElement(scene, "InstrGridContainer/InstrRow_0").GetComponent<Image>();
            Assert.IsNotNull(row0Img.sprite);
            Assert.AreEqual(
                "Row_Guide_ControlCard_WithIconBox_Clean",
                row0Img.sprite.name,
                $"MainMenu.unity InstrRow_0 awaits M5 scene rebuild (currently '{row0Img.sprite.name}').");
        }

        // ==================== F7: 04_AuthRegisterLogin Boundary Contracts (2 Tests) ====================

        [Test]
        [Category("Tier2")]
        [Category("F7")]
        public void F07_T2_AuthInputFields_TextAreaPadding_ClearsIconAndOrnamentBorders()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            GameObject subScreenAuth = E2EEditModeHelpers.FindUIElement(scene, "SubScreen_Auth");
            Assert.IsNotNull(subScreenAuth, "SubScreen_Auth missing in MainMenu.unity (awaiting M3).");

            TMP_InputField[] inputs = subScreenAuth.GetComponentsInChildren<TMP_InputField>(true);
            Assert.GreaterOrEqual(inputs.Length, 2, "SubScreen_Auth must contain at least Username and Password TMP_InputFields.");

            for (int i = 0; i < inputs.Length; i++)
            {
                TMP_InputField input = inputs[i];
                Assert.IsNotNull(input.textViewport, $"TMP_InputField '{input.name}' missing textViewport.");
                RectTransform vp = input.textViewport;
                Assert.GreaterOrEqual(vp.offsetMin.x, 40f, $"TMP_InputField '{input.name}' left TextArea padding too narrow for 04_AuthRegisterLogin icon.");
                Assert.LessOrEqual(vp.offsetMax.x, -40f, $"TMP_InputField '{input.name}' right TextArea padding too narrow for ornament border.");
            }
        }

        [Test]
        [Category("Tier2")]
        [Category("F7")]
        public void F07_T2_AuthPanel_Serialized04SlicedUISprites_And_TmpFonts()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            GameObject subScreenAuth = E2EEditModeHelpers.FindUIElement(scene, "SubScreen_Auth");
            Assert.IsNotNull(subScreenAuth, "SubScreen_Auth missing in MainMenu.unity (awaiting M3).");

            Image[] images = subScreenAuth.GetComponentsInChildren<Image>(true);
            bool hasModalFrame = false;
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].sprite != null &&
                    string.Equals(images[i].sprite.name, "Frame_Auth_ModalPanel_Clean", StringComparison.Ordinal))
                {
                    hasModalFrame = true;
                    break;
                }
            }
            Assert.IsTrue(hasModalFrame, "SubScreen_Auth must use Frame_Auth_ModalPanel_Clean.");

            TMP_InputField[] inputs = subScreenAuth.GetComponentsInChildren<TMP_InputField>(true);
            for (int i = 0; i < inputs.Length; i++)
            {
                Assert.IsNotNull(inputs[i].textComponent, $"Input '{inputs[i].name}' missing textComponent.");
                Assert.IsNotNull(inputs[i].textComponent.font, $"Input '{inputs[i].name}' textComponent.font is null.");
                Assert.IsFalse(
                    inputs[i].textComponent.font.name.Contains("LiberationSans"),
                    $"Input '{inputs[i].name}' must not use LiberationSans SDF (no Vietnamese glyphs).");
            }
        }

        // ==================== F8: 07_BattleVitalsHUD Geometry & Bounds (2 Tests) ====================

        [Test]
        [Category("Tier2")]
        [Category("F8")]
        public void F08_T2_PlayerHud_HorizontalBudget20to452_And_VerticalNonOverlap()
        {
            Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            Canvas canvas = EditModeTierSupport.FindRootCanvas(scene);

            Rect playerHud = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "GameCanvas/PlayerHUD");
            Assert.AreEqual(20f, playerHud.xMin, 1.5f, $"PlayerHUD xMin expected 20, got {playerHud.xMin:0.1}.");
            Assert.AreEqual(452f, playerHud.xMax, 1.5f, $"PlayerHUD xMax expected 452, got {playerHud.xMax:0.1}.");

            Rect hpBar = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "PlayerHUD/PlayerHPBar");
            Rect shieldBar = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "PlayerHUD/ShieldBar");
            Rect rage = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "PlayerHUD/RageContainer");
            Rect blockBadge = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "PlayerHUD/BlockStatusBadge");

            EditModeTierSupport.AssertRectContainedIn(hpBar, playerHud, "PlayerHPBar", "PlayerHUD");
            EditModeTierSupport.AssertRectContainedIn(rage, playerHud, "RageContainer", "PlayerHUD");
            EditModeTierSupport.AssertRectContainedIn(blockBadge, playerHud, "BlockStatusBadge", "PlayerHUD");

            EditModeTierSupport.AssertRectsDoNotOverlap(hpBar, shieldBar, "PlayerHPBar", "ShieldBar");
            EditModeTierSupport.AssertRectsDoNotOverlap(shieldBar, rage, "ShieldBar", "RageContainer");
            EditModeTierSupport.AssertRectsDoNotOverlap(rage, blockBadge, "RageContainer", "BlockStatusBadge");
        }

        [Test]
        [Category("Tier2")]
        [Category("F8")]
        public void F08_T2_DemoScene_VitalsImages_ValidSlicedAndSimpleTypes()
        {
            Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            Image backplate = EditModeTierSupport.RequireUIElement(scene, "PlayerHUD/VitalsBackplate").GetComponent<Image>();
            Image heart = EditModeTierSupport.RequireUIElement(scene, "PlayerHUD/HeartIcon").GetComponent<Image>();
            Image block = EditModeTierSupport.RequireUIElement(scene, "PlayerHUD/BlockStatusBadge").GetComponent<Image>();

            Assert.AreEqual(Image.Type.Sliced, backplate.type, "VitalsBackplate must be Sliced.");
            Assert.LessOrEqual(backplate.pixelsPerUnitMultiplier, 12f, "VitalsBackplate ppuMultiplier > 12.");
            Assert.AreEqual(Image.Type.Simple, heart.type, "HeartIcon must be Simple.");
            Assert.IsTrue(heart.preserveAspect, "HeartIcon must have preserveAspect=true.");
            Assert.AreEqual(Image.Type.Sliced, block.type, "BlockStatusBadge must be Sliced.");
        }

        // ==================== F9: 08_BattleTopHUD Geometry & Bounds (2 Tests) ====================

        [Test]
        [Category("Tier2")]
        [Category("F9")]
        public void F09_T2_TopRightCluster1440to1906_And_Banner690to1230_Disjoint()
        {
            Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            Canvas canvas = EditModeTierSupport.FindRootCanvas(scene);

            Rect topRight = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "GameCanvas/TopRightClusterBg");
            Rect banner = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "GameCanvas/EndlessWaveBanner");

            Assert.AreEqual(1440f, topRight.xMin, 1.5f, $"TopRightClusterBg xMin expected 1440, got {topRight.xMin:0.1}.");
            Assert.AreEqual(1906f, topRight.xMax, 1.5f, $"TopRightClusterBg xMax expected 1906, got {topRight.xMax:0.1}.");

            Assert.AreEqual(690f, banner.xMin, 2f, $"EndlessWaveBanner xMin expected 690, got {banner.xMin:0.1}.");
            Assert.AreEqual(1230f, banner.xMax, 2f, $"EndlessWaveBanner xMax expected 1230, got {banner.xMax:0.1}.");

            EditModeTierSupport.AssertRectsDoNotOverlap(banner, topRight, "EndlessWaveBanner", "TopRightClusterBg");
        }

        [Test]
        [Category("Tier2")]
        [Category("F9")]
        public void F09_T2_DemoScene_EndModalsCentered_And_TopButtonsInsideCluster()
        {
            Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            Canvas canvas = EditModeTierSupport.FindRootCanvas(scene);

            Rect topRight = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "GameCanvas/TopRightClusterBg");
            Rect coin = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "GameCanvas/BattleCoinBadge");
            Rect pauseBtn = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "GameCanvas/PauseHudButton");
            Rect menuBtn = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "GameCanvas/HudMenuButton");

            EditModeTierSupport.AssertRectContainedIn(coin, topRight, "BattleCoinBadge", "TopRightClusterBg");
            EditModeTierSupport.AssertRectContainedIn(pauseBtn, topRight, "PauseHudButton", "TopRightClusterBg");
            EditModeTierSupport.AssertRectContainedIn(menuBtn, topRight, "HudMenuButton", "TopRightClusterBg");
            EditModeTierSupport.AssertRectsDoNotOverlap(coin, pauseBtn, "BattleCoinBadge", "PauseHudButton");
            EditModeTierSupport.AssertRectsDoNotOverlap(pauseBtn, menuBtn, "PauseHudButton", "HudMenuButton");

            string[] modalCards = { "PauseModalPanel/PauseBox", "VictoryPanel/VictoryModalCard", "DefeatPanel/DefeatModalCard" };
            for (int i = 0; i < modalCards.Length; i++)
            {
                Rect card = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, modalCards[i]);
                Assert.AreEqual(960f, card.center.x, 5f, $"{modalCards[i]} not horizontally centered.");
                Assert.AreEqual(540f, card.center.y, 5f, $"{modalCards[i]} not vertically centered.");
            }
        }

        // ==================== F10: 09_TrainingSpawnerDock Geometry & Bounds (2 Tests) ====================

        [Test]
        [Category("Tier2")]
        [Category("F10")]
        public void F10_T2_TrainingTopBar_Budget464to1432_StrictlyDisjointFromLeftAndRightHud()
        {
            Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            Canvas canvas = EditModeTierSupport.FindRootCanvas(scene);

            Rect playerHud = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "GameCanvas/PlayerHUD");
            Rect trainingBar = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "GameCanvas/TrainingTopBar");
            Rect topRight = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "GameCanvas/TopRightClusterBg");

            Assert.AreEqual(464f, trainingBar.xMin, 1.5f, $"TrainingTopBar xMin expected 464, got {trainingBar.xMin:0.1}.");
            Assert.AreEqual(1432f, trainingBar.xMax, 1.5f, $"TrainingTopBar xMax expected 1432, got {trainingBar.xMax:0.1}.");

            Assert.GreaterOrEqual(trainingBar.xMin - playerHud.xMax, 8f, "Gap between PlayerHUD and TrainingTopBar must be >= 8px.");
            Assert.GreaterOrEqual(topRight.xMin - trainingBar.xMax, 8f, "Gap between TrainingTopBar and TopRightClusterBg must be >= 8px.");
        }

        [Test]
        [Category("Tier2")]
        [Category("F10")]
        public void F10_T2_ClearAllEnemiesButton_SimplePreserveAspect_And_ToggleSpritesMatch()
        {
            Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            Image clearImg = EditModeTierSupport.RequireUIElement(scene, "ClearAllEnemiesButton").GetComponent<Image>();
            Assert.IsNotNull(clearImg.sprite);
            Assert.AreEqual("Btn_Training_ClearAll_RedRing_Clean", clearImg.sprite.name);
            Assert.AreEqual(Image.Type.Simple, clearImg.type, "ClearAllEnemiesButton has zero border and must be Simple.");
            Assert.IsTrue(clearImg.preserveAspect, "ClearAllEnemiesButton must preserve aspect.");

            Image dockImg = EditModeTierSupport.RequireUIElement(scene, "GameCanvas/TrainingTopBar").GetComponent<Image>();
            Assert.AreEqual(Image.Type.Sliced, dockImg.type, "TrainingTopBar must be Sliced.");
            Assert.LessOrEqual(dockImg.pixelsPerUnitMultiplier, 12f);
        }
    }
}
