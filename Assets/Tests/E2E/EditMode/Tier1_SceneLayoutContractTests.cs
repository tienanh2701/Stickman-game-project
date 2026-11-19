using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StickmanBeatEmUp.Tests.E2E.EditMode
{
    public class Tier1_SceneLayoutContractTests
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string CharSelectScenePath = "Assets/Scenes/CharacterSelectScene.unity";
        private const string DemoScenePath = "Assets/Scenes/Stickman_DemoScene.unity";

        // ==================== F2: 01_MainMenu Integration (2 Tests) ====================

        [Test]
        [Category("Tier1")]
        [Category("F2")]
        public void F02_T1_MainMenuScene_HasScreen1Hierarchy_And_ActiveDefaults()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            GameObject screen1 = EditModeTierSupport.RequireUIElement(scene, "MainMenuCanvas/Screen1_MainMenu");
            Assert.IsTrue(screen1.activeSelf, "Screen1_MainMenu must be active by default.");

            string[] requiredChildren =
            {
                "Screen1_MainMenu/GameTitle",
                "Screen1_MainMenu/PlayButton",
                "Screen1_MainMenu/InstructionsButton",
                "Screen1_MainMenu/ExitButton",
            };
            for (int i = 0; i < requiredChildren.Length; i++)
            {
                GameObject child = EditModeTierSupport.RequireUIElement(scene, requiredChildren[i]);
                Assert.IsTrue(child.activeSelf, $"'{requiredChildren[i]}' should be active on Screen1_MainMenu.");
            }

            string[] inactivePanels =
            {
                "SubScreen_ModeSelect",
                "SubScreen_Instructions",
                "SubScreen_Shop",
                "Screen2_CharacterSelect",
                "Screen3_WorldSelect",
                "Screen4_StageSelect",
            };
            for (int i = 0; i < inactivePanels.Length; i++)
            {
                GameObject panel = EditModeTierSupport.RequireUIElement(scene, inactivePanels[i]);
                Assert.IsFalse(panel.activeSelf, $"Sub-screen '{inactivePanels[i]}' should start inactive.");
            }
        }

        [Test]
        [Category("Tier1")]
        [Category("F2")]
        public void F02_T1_MainMenuScene_ButtonsHaveHoverEffect_And_ProtectedBackground()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Type hoverType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.ModeCardHoverEffect");

            string[] buttonNames =
            {
                "Screen1_MainMenu/PlayButton",
                "Screen1_MainMenu/InstructionsButton",
                "Screen1_MainMenu/ExitButton",
            };
            for (int i = 0; i < buttonNames.Length; i++)
            {
                GameObject btnGo = EditModeTierSupport.RequireUIElement(scene, buttonNames[i]);
                Assert.IsNotNull(btnGo.GetComponent<Button>(), $"Button component missing on '{buttonNames[i]}'.");
                Assert.IsNotNull(btnGo.GetComponent(hoverType), $"ModeCardHoverEffect missing on '{buttonNames[i]}'.");
            }

            GameObject bgGo = EditModeTierSupport.RequireUIElement(scene, "MainMenuCanvas/MenuBackground");
            Image bgImg = bgGo.GetComponent<Image>();
            Assert.IsNotNull(bgImg, "MenuBackground Image missing.");
            Assert.IsNotNull(bgImg.sprite, "MenuBackground sprite missing.");
            Assert.IsTrue(
                string.Equals(bgImg.sprite.name, "MenuGlobalBackground", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(bgImg.sprite.name, "MainMenu_ThreeHeroes_Thumb", StringComparison.OrdinalIgnoreCase),
                $"MenuBackground unexpected sprite '{bgImg.sprite.name}'.");
        }

        // ==================== F3: 02_ModeSelect Integration (2 Tests) ====================

        [Test]
        [Category("Tier1")]
        [Category("F3")]
        public void F03_T1_ModeSelect_Has4ModeCards_BackButton_And_CoinBadge()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            EditModeTierSupport.RequireUIElement(scene, "MainMenuCanvas/SubScreen_ModeSelect");
            EditModeTierSupport.RequireUIElement(scene, "SubScreen_ModeSelect/ModeSelectTitle");

            string[] cards =
            {
                "SubScreen_ModeSelect/ModeCard_Campaign",
                "SubScreen_ModeSelect/ModeCard_Endless",
                "SubScreen_ModeSelect/ModeCard_Training",
                "SubScreen_ModeSelect/ModeCard_Shop",
            };
            for (int i = 0; i < cards.Length; i++)
            {
                GameObject card = EditModeTierSupport.RequireUIElement(scene, cards[i]);
                Assert.IsNotNull(card.GetComponent<Button>(), $"Button missing on '{cards[i]}'.");
            }

            EditModeTierSupport.RequireUIElement(scene, "MainMenuCanvas/BackButton_FixedTopLeft");
            EditModeTierSupport.RequireUIElement(scene, "MainMenuCanvas/PersistentCoinBadge");
        }

        [Test]
        [Category("Tier1")]
        [Category("F3")]
        public void F03_T1_ModeSelect_TopBarAnchors_And_CardHoverComponents()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Type hoverType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.ModeCardHoverEffect");
            Type coinCounterType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.CoinCounterUI");

            string[] cards =
            {
                "SubScreen_ModeSelect/ModeCard_Campaign",
                "SubScreen_ModeSelect/ModeCard_Endless",
                "SubScreen_ModeSelect/ModeCard_Training",
                "SubScreen_ModeSelect/ModeCard_Shop",
            };
            for (int i = 0; i < cards.Length; i++)
            {
                GameObject card = EditModeTierSupport.RequireUIElement(scene, cards[i]);
                Assert.IsNotNull(card.GetComponent(hoverType), $"ModeCardHoverEffect missing on '{cards[i]}'.");
            }

            RectTransform backRt = EditModeTierSupport.RequireUIElement(scene, "MainMenuCanvas/BackButton_FixedTopLeft").GetComponent<RectTransform>();
            Assert.AreEqual(0f, backRt.anchorMin.x, 0.01f, "BackButton_FixedTopLeft must be left-anchored.");
            Assert.AreEqual(1f, backRt.anchorMin.y, 0.01f, "BackButton_FixedTopLeft must be top-anchored.");

            GameObject badgeGo = EditModeTierSupport.RequireUIElement(scene, "MainMenuCanvas/PersistentCoinBadge");
            RectTransform badgeRt = badgeGo.GetComponent<RectTransform>();
            Assert.AreEqual(1f, badgeRt.anchorMin.x, 0.01f, "PersistentCoinBadge must be right-anchored.");
            Assert.AreEqual(1f, badgeRt.anchorMin.y, 0.01f, "PersistentCoinBadge must be top-anchored.");

            Component coinCounter = badgeGo.GetComponent(coinCounterType);
            Assert.IsNotNull(coinCounter, "CoinCounterUI missing on PersistentCoinBadge.");
            Assert.IsNotNull(EditModeTierSupport.GetInstanceFieldValue(coinCounter, "coinIcon"), "CoinCounterUI.coinIcon missing.");
        }

        // ==================== F4: 03_CharacterAndItemSelect Integration (2 Tests) ====================

        [Test]
        [Category("Tier1")]
        [Category("F4")]
        public void F04_T1_MainMenuCharSelect_Has5Slots_LoadoutQE_And_PreviewControls()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            string[] elements =
            {
                "MainMenuCanvas/Screen2_CharacterSelect",
                "Screen2_CharacterSelect/CharacterSelectLeftFrame",
                "CharacterSelectLeftFrame/CharCard_Goku",
                "CharacterSelectLeftFrame/CharCard_Eren",
                "CharacterSelectLeftFrame/CharCard_Diend",
                "CharacterSelectLeftFrame/CharCard_Locked1",
                "CharacterSelectLeftFrame/CharCard_Locked2",
                "Screen2_CharacterSelect/LoadoutBar",
                "LoadoutBar/LoadoutSlotQ",
                "LoadoutBar/LoadoutSlotE",
                "Screen2_CharacterSelect/BgCharacterPreview",
                "Screen2_CharacterSelect/TogglePreviewFormButton",
                "Screen2_CharacterSelect/CharacterNextButton",
            };
            for (int i = 0; i < elements.Length; i++)
            {
                Assert.IsNotNull(EditModeTierSupport.RequireUIElement(scene, elements[i]));
            }
        }

        [Test]
        [Category("Tier1")]
        [Category("F4")]
        public void F04_T1_CharacterSelectScene_Has3Cards_12Stages_And_Manager()
        {
            Scene scene = EditorSceneManager.OpenScene(CharSelectScenePath, OpenSceneMode.Single);
            Type managerType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.CharacterSelectManager");

            Component managerComp = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                managerComp = roots[i].GetComponentInChildren(managerType, true);
                if (managerComp != null)
                {
                    break;
                }
            }
            Assert.IsNotNull(managerComp, "CharacterSelectManager not found in CharacterSelectScene.unity.");

            Button[] charButtons = EditModeTierSupport.GetInstanceField<Button[]>(managerComp, "characterButtons");
            Assert.IsNotNull(charButtons, "CharacterSelectManager.characterButtons is null.");
            Assert.AreEqual(3, charButtons.Length, "Expected 3 characterButtons in CharacterSelectManager.");

            Button[] stageButtons = EditModeTierSupport.GetInstanceField<Button[]>(managerComp, "stageButtons");
            Assert.IsNotNull(stageButtons, "CharacterSelectManager.stageButtons is null.");
            Assert.AreEqual(12, stageButtons.Length, "Expected 12 stageButtons in CharacterSelectManager.");

            for (int w = 1; w <= 3; w++)
            {
                for (int s = 1; s <= 4; s++)
                {
                    EditModeTierSupport.RequireUIElement(scene, $"StageButton_W{w}_S{s}");
                }
            }
            EditModeTierSupport.RequireUIElement(scene, "StageSelectionPanel");
            EditModeTierSupport.RequireUIElement(scene, "PlayButton");
            EditModeTierSupport.RequireUIElement(scene, "UnlockAllButton");
        }

        // ==================== F5: 05_Shop Integration (2 Tests) ====================

        [Test]
        [Category("Tier1")]
        [Category("F5")]
        public void F05_T1_ShopPanel_HasSummaryBar_RowsContainer_And_3ItemRows()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            EditModeTierSupport.RequireUIElement(scene, "MainMenuCanvas/SubScreen_Shop");
            EditModeTierSupport.RequireUIElement(scene, "SubScreen_Shop/ShopSummaryBar");
            EditModeTierSupport.RequireUIElement(scene, "ShopSummaryBar/ShopBalanceText");
            EditModeTierSupport.RequireUIElement(scene, "ShopSummaryBar/SlotQSummary");
            EditModeTierSupport.RequireUIElement(scene, "ShopSummaryBar/SlotESummary");

            GameObject rowsContainer = EditModeTierSupport.RequireUIElement(scene, "SubScreen_Shop/ShopRowsContainer");
            Assert.IsNotNull(rowsContainer.GetComponent<VerticalLayoutGroup>(), "ShopRowsContainer must have VerticalLayoutGroup.");

            string[] rows =
            {
                "ShopRowsContainer/ShopRow_SenzuBean",
                "ShopRowsContainer/ShopRow_SpinalFluid",
                "ShopRowsContainer/ShopRow_UltraDivineWater",
            };
            for (int i = 0; i < rows.Length; i++)
            {
                EditModeTierSupport.RequireUIElement(scene, rows[i]);
                EditModeTierSupport.RequireUIElement(scene, rows[i] + "/BuyButton");
                EditModeTierSupport.RequireUIElement(scene, rows[i] + "/IconFrame/ItemIcon");
                EditModeTierSupport.RequireUIElement(scene, rows[i] + "/NameText");
                EditModeTierSupport.RequireUIElement(scene, rows[i] + "/PriceText");
                EditModeTierSupport.RequireUIElement(scene, rows[i] + "/OwnedText");
            }
        }

        [Test]
        [Category("Tier1")]
        [Category("F5")]
        public void F05_T1_ShopPanel_ProtectedItemIcons_And_CanonicalSOConsumablePrices()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            string[][] expectedRowIcons =
            {
                new[] { "ShopRow_SenzuBean/IconFrame/ItemIcon", "Icon_SenzuBean" },
                new[] { "ShopRow_SpinalFluid/IconFrame/ItemIcon", "Icon_SpinalFluid" },
                new[] { "ShopRow_UltraDivineWater/IconFrame/ItemIcon", "Icon_UltraDivineWater" },
            };

            for (int i = 0; i < expectedRowIcons.Length; i++)
            {
                GameObject iconGo = EditModeTierSupport.RequireUIElement(scene, expectedRowIcons[i][0]);
                Image img = iconGo.GetComponent<Image>();
                Assert.IsNotNull(img, $"Image missing on '{expectedRowIcons[i][0]}'.");
                Assert.IsNotNull(img.sprite, $"Sprite missing on '{expectedRowIcons[i][0]}'.");
                Assert.AreEqual(expectedRowIcons[i][1], img.sprite.name, $"Protected icon mismatch on '{expectedRowIcons[i][0]}'.");
            }

            string[] assetPaths =
            {
                "Assets/Resources/Items/SenzuBeanSO.asset",
                "Assets/Resources/Items/SpinalFluidSO.asset",
                "Assets/Resources/Items/UltraDivineWaterSO.asset",
            };
            int[] expectedPrices = { 30, 60, 50 };

            for (int i = 0; i < assetPaths.Length; i++)
            {
                ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPaths[i]);
                Assert.IsNotNull(so, $"Missing ConsumableItemSO asset '{assetPaths[i]}'.");
                int price = (int)EditModeTierSupport.GetInstanceFieldValue(so, "price");
                Assert.AreEqual(expectedPrices[i], price, $"Price mismatch in '{assetPaths[i]}'.");
            }
        }

        // ==================== F6: 06_InstructionsGuide Integration (2 Tests) ====================

        [Test]
        [Category("Tier1")]
        [Category("F6")]
        public void F06_T1_InstructionsPanel_Has10RowsInGridContainer()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            EditModeTierSupport.RequireUIElement(scene, "MainMenuCanvas/SubScreen_Instructions");
            GameObject container = EditModeTierSupport.RequireUIElement(scene, "SubScreen_Instructions/InstrGridContainer");

            Type instrType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.InstructionsUI");
            Assert.IsNotNull(container.GetComponent(instrType), "InstructionsUI component missing on InstrGridContainer.");

            for (int i = 0; i < 10; i++)
            {
                string rowPath = $"InstrGridContainer/InstrRow_{i}";
                EditModeTierSupport.RequireUIElement(scene, rowPath);
                EditModeTierSupport.RequireUIElement(scene, rowPath + "/SpriteFrame/ActionSprite");
                GameObject keyTextGo = E2EEditModeHelpers.FindUIElement(scene, rowPath + "/KeyPill/KeyText");
                if (keyTextGo == null)
                {
                    keyTextGo = EditModeTierSupport.RequireUIElement(scene, rowPath + "/KeyBadge/KeyText");
                }
                Text keyText = keyTextGo.GetComponent<Text>();
                Assert.IsNotNull(keyText, $"KeyText missing on '{rowPath}'.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(keyText.text), $"KeyText empty on '{rowPath}'.");

                GameObject descGo = EditModeTierSupport.RequireUIElement(scene, rowPath + "/ActionDesc");
                Text descText = descGo.GetComponent<Text>();
                Assert.IsNotNull(descText, $"ActionDesc Text missing on '{rowPath}'.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(descText.text), $"ActionDesc empty on '{rowPath}'.");
            }
        }

        [Test]
        [Category("Tier1")]
        [Category("F6")]
        public void F06_T1_InstructionsUI_DefaultKeybindRows_MapsAll10GuideIcons()
        {
            Type instrType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.InstructionsUI");
            Array rows = EditModeTierSupport.GetStaticFieldValue(instrType, "DefaultKeybindRows") as Array;
            Assert.IsNotNull(rows, "InstructionsUI.DefaultKeybindRows missing.");
            Assert.AreEqual(10, rows.Length, "Expected 10 entries in InstructionsUI.DefaultKeybindRows.");

            HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows.Length; i++)
            {
                object tupleObj = rows.GetValue(i);
                FieldInfo pathField = tupleObj.GetType().GetField("Item3");
                Assert.IsNotNull(pathField, "Tuple Item3 missing on DefaultKeybindRows entry.");
                string path = pathField.GetValue(tupleObj) as string;
                Assert.IsFalse(string.IsNullOrEmpty(path), $"Empty defaultSpritePath at index {i}.");
                Assert.IsTrue(path.Contains("06_InstructionsGuide/Icon_Guide_"), $"Row {i} path '{path}' must reference 06_InstructionsGuide/Icon_Guide_*.");
                Assert.IsTrue(File.Exists(path), $"Guide icon PNG missing on disk: '{path}'.");
                Assert.IsTrue(seenPaths.Add(path), $"Duplicate guide icon path '{path}'.");
            }
        }

        // ==================== F7: 04_AuthRegisterLogin (M3 Contract, 2 Tests) ====================

        [Test]
        [Category("Tier1")]
        [Category("F7")]
        public void F07_T1_AuthTypesAndReloadProfileMethods_ExistInAssemblyCSharp()
        {
            Type accountManagerType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.Auth.AccountManager");
            E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.Auth.AuthUIController");
            E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.Auth.UserAccountRecord");
            E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.Auth.AccountSaveDatabase");

            Type screenEnum = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.MainMenuScreen");
            Assert.IsTrue(Enum.IsDefined(screenEnum, "Auth"), "MainMenuScreen enum must define 'Auth' (M3).");

            Type currencyType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.Economy.CurrencyManager");
            Assert.IsNotNull(
                currencyType.GetMethod("ReloadProfileState", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null),
                "CurrencyManager.ReloadProfileState() missing (M3).");

            Type inventoryType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.Items.InventoryManager");
            Assert.IsNotNull(
                inventoryType.GetMethod("ReloadProfileState", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null),
                "InventoryManager.ReloadProfileState() missing (M3).");

            Assert.IsNotNull(
                accountManagerType.GetMethod(
                    "TryRegister",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(string), typeof(string), typeof(string), typeof(string).MakeByRefType() },
                    null),
                "AccountManager.TryRegister missing.");
            Assert.IsNotNull(
                accountManagerType.GetMethod(
                    "TryLogin",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(string), typeof(string), typeof(string).MakeByRefType() },
                    null),
                "AccountManager.TryLogin missing.");
            Assert.IsNotNull(
                accountManagerType.GetMethod("Logout", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null),
                "AccountManager.Logout missing.");
        }

        [Test]
        [Category("Tier1")]
        [Category("F7")]
        public void F07_T1_MainMenuScene_HasSubScreenAuth_AccountPill_And_All14AuthSprites()
        {
            const string authDir = "Assets/Sprites/UI/SlicedUI/04_AuthRegisterLogin";
            Assert.IsTrue(Directory.Exists(authDir), $"Missing {authDir}.");
            string[] authPngs = Directory.GetFiles(authDir, "*.png", SearchOption.TopDirectoryOnly);
            Assert.AreEqual(14, authPngs.Length, $"Expected 14 PNGs in {authDir}, found {authPngs.Length}.");

            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            GameObject subScreenAuth = E2EEditModeHelpers.FindUIElement(scene, "SubScreen_Auth");
            Assert.IsNotNull(subScreenAuth, "SubScreen_Auth GameObject missing in MainMenu.unity (awaiting M3).");

            Type authCtrlType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.Auth.AuthUIController");
            Assert.IsNotNull(subScreenAuth.GetComponentInChildren(authCtrlType, true), "AuthUIController missing under SubScreen_Auth.");
        }

        // ==================== F8: 07_BattleVitalsHUD (2 Tests) ====================

        [Test]
        [Category("Tier1")]
        [Category("F8")]
        public void F08_T1_DemoScene_PlayerHud_HasVitalsHierarchy_And_07Guids()
        {
            Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            string[] elements =
            {
                "PlayerHUD",
                "PlayerHUD/VitalsBackplate",
                "PlayerHUD/HeartIcon",
                "PlayerHUD/PlayerHPBar",
                "PlayerHUD/ShieldBar",
                "PlayerHUD/RageContainer",
                "RageContainer/RageSegment_1",
                "RageContainer/RageSegment_2",
                "RageContainer/RageSegment_3",
                "PlayerHUD/BlockStatusBadge",
            };
            for (int i = 0; i < elements.Length; i++)
            {
                Assert.IsNotNull(EditModeTierSupport.RequireUIElement(scene, elements[i]));
            }

            Assert.AreEqual("Frame_HUD_VitalsBackplate_Clean", EditModeTierSupport.RequireUIElement(scene, "PlayerHUD/VitalsBackplate").GetComponent<Image>().sprite.name);
            Assert.AreEqual("Icon_HUD_HeartCrystal", EditModeTierSupport.RequireUIElement(scene, "PlayerHUD/HeartIcon").GetComponent<Image>().sprite.name);
            Assert.AreEqual("Bar_HUD_HP_Frame_Clean", EditModeTierSupport.RequireUIElement(scene, "PlayerHPBar/Background").GetComponent<Image>().sprite.name);
            Assert.AreEqual("Bar_HUD_HP_Fill_RedOrange_Clean", EditModeTierSupport.RequireUIElement(scene, "PlayerHPBar/Fill Area/Fill").GetComponent<Image>().sprite.name);
            Assert.AreEqual("Pill_HUD_ShieldReady_Clean", EditModeTierSupport.RequireUIElement(scene, "PlayerHUD/BlockStatusBadge").GetComponent<Image>().sprite.name);
        }

        [Test]
        [Category("Tier1")]
        [Category("F8")]
        public void F08_T1_DemoScene_RageFrameSerialized_And_PromptsStartInactive()
        {
            Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            Type vitalsType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.PlayerVitalsHudUI");

            GameObject canvas = EditModeTierSupport.RequireUIElement(scene, "GameCanvas");
            Component vitals = canvas.GetComponent(vitalsType);
            Assert.IsNotNull(vitals, "PlayerVitalsHudUI component missing on GameCanvas.");

            Sprite rageFrame = EditModeTierSupport.GetInstanceField<Sprite>(vitals, "rage3SegFrameSprite");
            Assert.IsNotNull(rageFrame, "PlayerVitalsHudUI.rage3SegFrameSprite is null.");
            Assert.AreEqual("Bar_HUD_Rage_3SegFrame_Clean", rageFrame.name, "PlayerVitalsHudUI.rage3SegFrameSprite mismatch.");

            Assert.IsFalse(EditModeTierSupport.RequireUIElement(scene, "PlayerHUD/ShieldBar").activeSelf, "ShieldBar should start inactive.");
            Assert.IsFalse(EditModeTierSupport.RequireUIElement(scene, "PlayerHUD/TransformReadyPrompt").activeSelf, "TransformReadyPrompt should start inactive.");
            Assert.IsFalse(EditModeTierSupport.RequireUIElement(scene, "PlayerHUD/SpecialComboReadyPrompt").activeSelf, "SpecialComboReadyPrompt should start inactive.");
        }

        // ==================== F9: 08_BattleTopHUD (2 Tests) ====================

        [Test]
        [Category("Tier1")]
        [Category("F9")]
        public void F09_T1_DemoScene_TopRightCluster_And_EndlessBanner_Have08Sprites()
        {
            Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);

            Assert.AreEqual("Frame_HUD_TopRightCluster_Clean", EditModeTierSupport.RequireUIElement(scene, "GameCanvas/TopRightClusterBg").GetComponent<Image>().sprite.name);
            Assert.AreEqual("Frame_HUD_StageWaveBanner_Clean", EditModeTierSupport.RequireUIElement(scene, "GameCanvas/EndlessWaveBanner").GetComponent<Image>().sprite.name);
            Assert.AreEqual("Badge_HUD_InGameCoin_Clean", EditModeTierSupport.RequireUIElement(scene, "GameCanvas/BattleCoinBadge").GetComponent<Image>().sprite.name);
            Assert.AreEqual("Icon_Currency_CoinXu", EditModeTierSupport.RequireUIElement(scene, "BattleCoinBadge/CoinIcon").GetComponent<Image>().sprite.name);
            Assert.AreEqual("Btn_HUD_Pause_Clean", EditModeTierSupport.RequireUIElement(scene, "GameCanvas/PauseHudButton").GetComponent<Image>().sprite.name);
            Assert.AreEqual("Btn_HUD_ExitMatch_Clean", EditModeTierSupport.RequireUIElement(scene, "GameCanvas/HudMenuButton").GetComponent<Image>().sprite.name);
        }

        [Test]
        [Category("Tier1")]
        [Category("F9")]
        public void F09_T1_DemoScene_ModalsAndBossPanel_ExistAndStartInactive()
        {
            Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            string[] inactiveModals =
            {
                "GameCanvas/PauseModalPanel",
                "GameCanvas/VictoryPanel",
                "GameCanvas/DefeatPanel",
                "GameCanvas/BossHealthPanel",
            };
            for (int i = 0; i < inactiveModals.Length; i++)
            {
                GameObject go = EditModeTierSupport.RequireUIElement(scene, inactiveModals[i]);
                Assert.IsFalse(go.activeSelf, $"Modal/Panel '{inactiveModals[i]}' should start inactive.");
            }

            EditModeTierSupport.RequireUIElement(scene, "PauseModalPanel/PauseBox");
            EditModeTierSupport.RequireUIElement(scene, "VictoryPanel/VictoryModalCard");
            EditModeTierSupport.RequireUIElement(scene, "DefeatPanel/DefeatModalCard");
        }

        // ==================== F10: 09_TrainingSpawnerDock (2 Tests) ====================

        [Test]
        [Category("Tier1")]
        [Category("F10")]
        public void F10_T1_DemoScene_TrainingTopBar_StartsInactive_With13SpawnCards()
        {
            Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            GameObject topBar = EditModeTierSupport.RequireUIElement(scene, "GameCanvas/TrainingTopBar");
            Assert.IsFalse(topBar.activeSelf, "TrainingTopBar should start inactive in Stickman_DemoScene.unity.");

            string[] controls =
            {
                "TrainingTopBar/Group1_PlayerControls/PlayerInvincibleButton",
                "TrainingTopBar/Group1_PlayerControls/AutoMaxRageButton",
                "TrainingTopBar/Group2_EnemyControls/EnemyAiButton",
                "TrainingTopBar/Group2_EnemyControls/EnemyInvincibleButton",
                "TrainingTopBar/Group3_SpawnAndClear/ScrollLeftButton",
                "TrainingTopBar/Group3_SpawnAndClear/ScrollRightButton",
                "TrainingTopBar/Group3_SpawnAndClear/SpawnListScroll",
                "TrainingTopBar/Group3_SpawnAndClear/ClearAllEnemiesButton",
            };
            for (int i = 0; i < controls.Length; i++)
            {
                EditModeTierSupport.RequireUIElement(scene, controls[i]);
            }

            for (int i = 0; i < 13; i++)
            {
                GameObject card = EditModeTierSupport.RequireUIElement(scene, $"SpawnListScroll/Viewport/Content/SpawnUnit_{i}");
                Image cardImg = card.GetComponent<Image>();
                Assert.IsNotNull(cardImg, $"Image missing on SpawnUnit_{i}.");
                Assert.IsNotNull(cardImg.sprite, $"Sprite missing on SpawnUnit_{i}.");
                Assert.AreEqual("Slot_Training_EnemySpawnCard_Clean", cardImg.sprite.name, $"Sprite mismatch on SpawnUnit_{i}.");
            }
        }

        [Test]
        [Category("Tier1")]
        [Category("F10")]
        public void F10_T1_DemoScene_TrainingDock_Has09Sprites_And_ManagerReferences()
        {
            Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            Assert.AreEqual("Frame_Training_MainDock_Clean", EditModeTierSupport.RequireUIElement(scene, "GameCanvas/TrainingTopBar").GetComponent<Image>().sprite.name);
            Assert.AreEqual("Toggle_Training_OptionPill_Blank", EditModeTierSupport.RequireUIElement(scene, "PlayerInvincibleButton").GetComponent<Image>().sprite.name);
            Assert.AreEqual("Toggle_Training_OptionPill_BlueLED_Clean", EditModeTierSupport.RequireUIElement(scene, "AutoMaxRageButton").GetComponent<Image>().sprite.name);
            Assert.AreEqual("Toggle_Training_OptionPill_BlueLED_Clean", EditModeTierSupport.RequireUIElement(scene, "EnemyAiButton").GetComponent<Image>().sprite.name);
            Assert.AreEqual("Toggle_Training_OptionPill_Blank", EditModeTierSupport.RequireUIElement(scene, "EnemyInvincibleButton").GetComponent<Image>().sprite.name);
            Assert.AreEqual("Btn_Training_ClearAll_RedRing_Clean", EditModeTierSupport.RequireUIElement(scene, "ClearAllEnemiesButton").GetComponent<Image>().sprite.name);

            Type tmType = E2EEditModeHelpers.RequireRuntimeType("StickmanBeatEmUp.GameModes.TrainingModeManager");
            Component tm = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                tm = roots[i].GetComponentInChildren(tmType, true);
                if (tm != null)
                {
                    break;
                }
            }
            Assert.IsNotNull(tm, "TrainingModeManager not found in Stickman_DemoScene.unity.");
            Assert.AreEqual("Icon_Training_BlueLEDOrb", EditModeTierSupport.GetInstanceField<Sprite>(tm, "blueLedOrbSprite").name);
            Assert.AreEqual("Divider_Training_PlasmaTube_Violet", EditModeTierSupport.GetInstanceField<Sprite>(tm, "plasmaDividerSprite").name);

            Array roster = EditModeTierSupport.GetInstanceField<Array>(tm, "unitRoster");
            Assert.IsNotNull(roster, "TrainingModeManager.unitRoster is null.");
            Assert.GreaterOrEqual(roster.Length, 12, "TrainingModeManager.unitRoster must have >= 12 entries.");
        }
    }
}
