using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StickmanBeatEmUp.Tests.E2E.PlayMode
{
    public class Tier5_AdversarialUiHardeningTests
    {
        [UnityTest]
        [Category("Tier5")]
        [Category("F1")]
        public IEnumerator T5_B01_GlassHudTheme_ZeroBorderAndExtremeRectPpuBoundaries()
        {
            Type themeType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.GlassHudTheme");
            MethodInfo loadSpriteMi = themeType.GetMethod(
                "LoadSprite",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(Vector4) },
                null);
            MethodInfo applySlicedMi = themeType.GetMethod(
                "ApplySlicedSprite",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(Image), typeof(Sprite), typeof(float), typeof(float), typeof(Color?) },
                null);
            MethodInfo applySpriteObjMi = themeType.GetMethod(
                "ApplySprite",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(Image), typeof(Sprite), typeof(bool), typeof(bool) },
                null);
            MethodInfo applySpriteNameMi = themeType.GetMethod(
                "ApplySprite",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(Image), typeof(string), typeof(Vector4), typeof(bool), typeof(bool) },
                null);
            Assert.IsNotNull(loadSpriteMi);
            Assert.IsNotNull(applySlicedMi);
            Assert.IsNotNull(applySpriteObjMi);
            Assert.IsNotNull(applySpriteNameMi);

            GameObject canvasGo = new GameObject("TempB01Canvas", typeof(Canvas), typeof(CanvasScaler));
            GameObject imgGo = new GameObject("TempB01Image", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(canvasGo.transform, false);

            Texture2D zeroTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Sprite zeroBorderSprite = Sprite.Create(zeroTex, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.zero);

            try
            {
                Image img = imgGo.GetComponent<Image>();
                RectTransform rt = imgGo.GetComponent<RectTransform>();

                // 1. Zero-border sprite via ApplySlicedSprite must switch to Simple + preserveAspect + ppu=1
                applySlicedMi.Invoke(null, new object[] { img, zeroBorderSprite, 300f, 80f, null });
                Assert.AreEqual(Image.Type.Simple, img.type);
                Assert.IsTrue(img.preserveAspect);
                Assert.AreEqual(1f, img.pixelsPerUnitMultiplier, 0.001f);

                // 2. Zero-border sprite via ApplySprite(sliced:true, preserveAspect:false) still enforces Simple + preserveAspect
                applySpriteObjMi.Invoke(null, new object[] { img, zeroBorderSprite, true, false });
                Assert.AreEqual(Image.Type.Simple, img.type);
                Assert.IsTrue(img.preserveAspect);

                // 3. Tiny 32x32 target with large border sprite must raise ppu above 12 so borders stay <= 72% (Bug B-05)
                Sprite largeBorderSprite = loadSpriteMi.Invoke(null, new object[] { "08_BattleTopHUD/Frame_HUD_StageWaveBanner_Clean.png", default(Vector4) }) as Sprite;
                Assert.IsNotNull(largeBorderSprite);
                Vector4 b = largeBorderSprite.border;
                Assert.Greater(b.x + b.z, 100f);

                rt.sizeDelta = new Vector2(32f, 32f);
                applySlicedMi.Invoke(null, new object[] { img, largeBorderSprite, 32f, 32f, null });
                Assert.AreEqual(Image.Type.Sliced, img.type);
                float scaledW32 = (b.x + b.z) / img.pixelsPerUnitMultiplier;
                float scaledH32 = (b.y + b.w) / img.pixelsPerUnitMultiplier;
                Assert.LessOrEqual(scaledW32, 32f * 0.72f + 0.05f, $"Horizontal border overflow at 32x32: {scaledW32:0.2} (ppu={img.pixelsPerUnitMultiplier:0.2}).");
                Assert.LessOrEqual(scaledH32, 32f * 0.72f + 0.05f, $"Vertical border overflow at 32x32: {scaledH32:0.2} (ppu={img.pixelsPerUnitMultiplier:0.2}).");

                // 4. Ultra-wide, narrow, and stretched zero/negative sizeDelta rects produce finite valid ppu >= 0.25f
                applySlicedMi.Invoke(null, new object[] { img, largeBorderSprite, 1400f, 24f, null });
                Assert.IsFalse(float.IsNaN(img.pixelsPerUnitMultiplier) || float.IsInfinity(img.pixelsPerUnitMultiplier));
                Assert.GreaterOrEqual(img.pixelsPerUnitMultiplier, 0.25f);

                applySlicedMi.Invoke(null, new object[] { img, largeBorderSprite, 60f, 300f, null });
                Assert.IsFalse(float.IsNaN(img.pixelsPerUnitMultiplier) || float.IsInfinity(img.pixelsPerUnitMultiplier));
                Assert.GreaterOrEqual(img.pixelsPerUnitMultiplier, 0.25f);

                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = new Vector2(-40f, -40f);
                applySpriteObjMi.Invoke(null, new object[] { img, largeBorderSprite, true, false });
                Assert.IsFalse(float.IsNaN(img.pixelsPerUnitMultiplier) || float.IsInfinity(img.pixelsPerUnitMultiplier));
                Assert.GreaterOrEqual(img.pixelsPerUnitMultiplier, 0.25f);

                // 5. Missing sprite path leaves existing sprite untouched without exceptions
                Sprite beforeMissing = img.sprite;
                applySpriteNameMi.Invoke(null, new object[] { img, "NonExistent_Sprite_XYZ_999.png", default(Vector4), true, false });
                Assert.AreEqual(beforeMissing, img.sprite);

                yield return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasGo);
                UnityEngine.Object.DestroyImmediate(zeroBorderSprite);
                UnityEngine.Object.DestroyImmediate(zeroTex);
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F5")]
        [Category("F8")]
        [Category("F9")]
        [Category("F10")]
        public IEnumerator T5_B02_RuntimeDestroy_UnsubscribesManagerEvents_NoMissingReference()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            using (ScopedLogCapture logs = E2ETestHelpers.CaptureLogs())
            {
                // Phase A: MainMenu UI destruction
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Shop");
                yield return null;

                Component shopMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.Economy.ShopManager");
                Component coinCounter = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.CoinCounterUI");
                UnityEngine.Object.DestroyImmediate(shopMgr.gameObject);
                UnityEngine.Object.DestroyImmediate(coinCounter.gameObject);
                yield return null;

                PlayModeTierSupport.GrantCoins(25);
                PlayModeTierSupport.AddOwnedItem("SenzuBean", 1);
                yield return null;

                // Phase B: Stickman_DemoScene HUD destruction while managers emit events
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;

                Component gameUi = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.GameUIManager");
                Component pauseUi = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.PauseMenuUI");
                Component vitalsUi = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.PlayerVitalsHudUI");
                Component itemSlots = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.HudItemSlotsUI");
                Component tm = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.GameModes.TrainingModeManager");

                if (pauseUi != null) UnityEngine.Object.DestroyImmediate(pauseUi);
                if (vitalsUi != null) UnityEngine.Object.DestroyImmediate(vitalsUi);
                if (gameUi != null) UnityEngine.Object.DestroyImmediate(gameUi);
                if (itemSlots != null) UnityEngine.Object.DestroyImmediate(itemSlots.gameObject);
                if (tm != null) UnityEngine.Object.DestroyImmediate(tm.gameObject);
                yield return null;

                PlayModeTierSupport.GrantCoins(30);
                Component inv = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Items.InventoryManager");
                PlayModeTierSupport.TryConsumeSlot(inv, 0);
                PlayModeTierSupport.ApplyFatalDamageToPlayer(player, 15f);
                yield return null;

                Assert.AreEqual(1f, Time.timeScale, 0.001f);
                logs.AssertNoWarningsOrErrors();
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F5")]
        public IEnumerator T5_B03_ShopManager_EquipSlots_InvalidIndex_And_ReEnableRefresh()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                PlayModeTierSupport.GrantCoins(500);

                // Mutate while Shop is inactive
                PlayModeTierSupport.GrantCoins(150);
                PlayModeTierSupport.AddOwnedItem("SenzuBean", 2);

                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Shop");
                yield return null;

                Assert.AreEqual("650 Xu", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("ShopSummaryBar/ShopBalanceText")).Trim());
                Assert.IsTrue(PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("ShopRow_SenzuBean/OwnedText")).Contains("2"));

                Component shopMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.Economy.ShopManager");
                Type itemEnum = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.ConsumableItemType");
                object senzuVal = Enum.Parse(itemEnum, "SenzuBean");
                object noneVal = Enum.Parse(itemEnum, "None");

                PlayModeTierSupport.InvokeMethod(shopMgr, "EquipSlotQ", senzuVal);
                PlayModeTierSupport.InvokeMethod(shopMgr, "EquipSlotE", senzuVal);
                Assert.AreEqual("SenzuBean", PlayModeTierSupport.GetEquippedItemName(0));
                Assert.AreEqual("SenzuBean", PlayModeTierSupport.GetEquippedItemName(1));
                Assert.IsTrue(PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("ShopSummaryBar/SlotQSummary")).Contains("Senzu") ||
                              PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("ShopSummaryBar/SlotQSummary")).Contains("Đậu"));
                Assert.IsTrue(PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("ShopSummaryBar/SlotESummary")).Contains("Senzu") ||
                              PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("ShopSummaryBar/SlotESummary")).Contains("Đậu"));

                PlayModeTierSupport.InvokeMethod(shopMgr, "EquipSlotQ", noneVal);
                Assert.AreEqual("None", PlayModeTierSupport.GetEquippedItemName(0));
                Assert.IsTrue(PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("ShopSummaryBar/SlotQSummary")).Contains("Trống"));

                // Invalid slotIndex must be rejected cleanly without false-positive "vào ô E" feedback (Bug B-06)
                PlayModeTierSupport.InvokeMethod(shopMgr, "EquipToSlot", 99, senzuVal);
                string fb = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("SubScreen_Shop/ShopFeedbackText"));
                Assert.IsFalse(fb.Contains("vào ô E"), $"Invalid slotIndex 99 must not report false-positive slot E equip: '{fb}'.");

                // Spend until coins < 30, then verify failure
                for (int i = 0; i < 21; i++)
                {
                    Assert.IsTrue(PlayModeTierSupport.TryPurchaseItem(shopMgr, "SenzuBean", out _));
                }
                Assert.AreEqual(20, PlayModeTierSupport.GetCoins());
                Assert.IsFalse(PlayModeTierSupport.TryPurchaseItem(shopMgr, "SenzuBean", out string failMsg));
                Assert.AreEqual("Không đủ Xu!", failMsg);
                Assert.AreEqual(20, PlayModeTierSupport.GetCoins());
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F4")]
        public IEnumerator T5_B04_UIManager_CycleLoadoutSlot_AllowsUnequipWithMultipleOwnedItems()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                PlayModeTierSupport.AddOwnedItem("SenzuBean", 2);
                PlayModeTierSupport.AddOwnedItem("SpinalFluid", 2);

                Component invMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Items.InventoryManager");
                Type itemEnum = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.ConsumableItemType");
                PlayModeTierSupport.InvokeMethod(invMgr, "SetEquippedItem", 0, Enum.Parse(itemEnum, "SenzuBean"));
                PlayModeTierSupport.InvokeMethod(invMgr, "SetEquippedItem", 1, Enum.Parse(itemEnum, "None"));

                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "CharacterSelect");
                yield return null;

                List<string> visited = new List<string>();
                for (int i = 0; i < 3; i++)
                {
                    E2ETestHelpers.ClickButton("LoadoutBar/LoadoutSlotQ/CircleSlot");
                    yield return null;
                    visited.Add(PlayModeTierSupport.GetEquippedItemName(0));
                }

                Assert.IsTrue(visited.Contains("SpinalFluid"), "CycleLoadoutSlot must visit SpinalFluid.");
                Assert.IsTrue(visited.Contains("None"), "CycleLoadoutSlot must visit ConsumableItemType.None when >= 2 items are owned (Bug B-03).");
                Assert.IsTrue(visited.Contains("SenzuBean"), "CycleLoadoutSlot must wrap back to SenzuBean.");
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F4")]
        public IEnumerator T5_B05_CharacterSelectScene_PreservesSavedSelection_And_LockedSlot()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayerPrefs.SetInt("SelectedCharacter", 1);
                PlayerPrefs.SetInt("Stickman_UnlockedStage", 1);
                PlayerPrefs.SetInt("SelectedStageIndex", 0);
                PlayerPrefs.Save();

                yield return E2ETestHelpers.LoadSceneAndWait("CharacterSelectScene");
                Assert.AreEqual(1, PlayerPrefs.GetInt("SelectedCharacter", -1), "CharacterSelectManager.Start() must preserve saved Eren selection (Bug B-02).");

                Component mgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.CharacterSelectManager");
                Assert.IsNotNull(mgr);

                // Locked stage remains ignored, then UnlockAll enables selection & moves GoldSelectionFrame exclusively
                PlayModeTierSupport.InvokeMethod(mgr, "SelectStage", 5);
                Assert.AreEqual(0, PlayerPrefs.GetInt("SelectedStageIndex", -1));

                E2ETestHelpers.ClickButton("UnlockAllButton");
                yield return null;

                E2ETestHelpers.ClickButton("StageButton_W1_S2");
                yield return null;
                GameObject s2Frame = E2ETestHelpers.FindUIElement("StageButton_W1_S2/GoldSelectionFrame");
                GameObject s1Frame = E2ETestHelpers.FindUIElement("StageButton_W1_S1/GoldSelectionFrame");
                Assert.IsTrue(s2Frame.activeSelf, "Selected stage GoldSelectionFrame must be active.");
                Assert.IsFalse(s1Frame.activeSelf, "Previous stage GoldSelectionFrame must deactivate.");

                E2ETestHelpers.ClickButton("StageButton_W2_S1");
                yield return null;
                Assert.IsTrue(E2ETestHelpers.FindUIElement("StageButton_W2_S1/GoldSelectionFrame").activeSelf);
                Assert.IsFalse(s2Frame.activeSelf);

                // Calling WireButtons twice must not double-fire ToggleFormPreviewButton (Bug B-02)
                PlayModeTierSupport.InvokeMethod(mgr, "WireButtons");
                PlayModeTierSupport.InvokeMethod(mgr, "WireButtons");
                bool beforeForm = (bool)PlayModeTierSupport.GetInstanceField(mgr, "showTransformedPortrait");
                E2ETestHelpers.ClickButton("ToggleFormPreviewButton");
                yield return null;
                bool afterForm = (bool)PlayModeTierSupport.GetInstanceField(mgr, "showTransformedPortrait");
                Assert.AreEqual(!beforeForm, afterForm, "WireButtons must deduplicate listeners so single click toggles once.");
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F6")]
        public IEnumerator T5_B06_InstructionsUI_RepeatedOpenClose_ZeroDuplicateChildren_And_OverflowRows()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                GameObject container = E2ETestHelpers.FindUIElement("SubScreen_Instructions/InstrGridContainer");

                for (int i = 0; i < 5; i++)
                {
                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Instructions");
                    yield return null;
                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "MainMenu");
                    yield return null;
                }

                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Instructions");
                yield return null;
                Assert.AreEqual(10, container.transform.childCount, "Repeated open/close must not duplicate row children.");

                Canvas canvas = E2ETestHelpers.FindUIElement("MainMenuCanvas").GetComponent<Canvas>();
                Rect containerRect = E2ETestHelpers.GetRectBoundsIn1920x1080(container.GetComponent<RectTransform>(), canvas);
                Rect[] rowRects = new Rect[10];
                for (int i = 0; i < 10; i++)
                {
                    GameObject row = E2ETestHelpers.FindUIElement($"InstrGridContainer/InstrRow_{i}");
                    rowRects[i] = E2ETestHelpers.GetRectBoundsIn1920x1080(row.GetComponent<RectTransform>(), canvas);
                    Assert.GreaterOrEqual(rowRects[i].xMin, containerRect.xMin - 6f);
                    Assert.LessOrEqual(rowRects[i].xMax, containerRect.xMax + 6f);

                    GameObject descGo = E2ETestHelpers.FindUIElement($"InstrGridContainer/InstrRow_{i}/ActionDesc");
                    Assert.LessOrEqual(descGo.GetComponents<Outline>().Length, 1, $"Duplicate Outline on InstrRow_{i}/ActionDesc.");
                }
                for (int i = 0; i < 10; i++)
                {
                    for (int j = i + 1; j < 10; j++)
                    {
                        bool sepX = rowRects[i].xMax <= rowRects[j].xMin + 0.5f || rowRects[j].xMax <= rowRects[i].xMin + 0.5f;
                        bool sepY = rowRects[i].yMax <= rowRects[j].yMin + 0.5f || rowRects[j].yMax <= rowRects[i].yMin + 0.5f;
                        Assert.IsTrue(sepX || sepY, $"Overlap between InstrRow_{i} and InstrRow_{j}.");
                    }
                }

                // Overflow layout check for >= 10 items (Bug B-07)
                Type instrType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.InstructionsUI");
                Type itemType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.InstructionItem");
                MethodInfo createDefaultsMi = instrType.GetMethod("CreateDefaultInstructionItems", BindingFlags.Static | BindingFlags.Public);
                Array defaults = createDefaultsMi.Invoke(null, null) as Array;
                Assert.AreEqual(10, defaults.Length);

                GameObject tempRoot = new GameObject("TempInstrRoot", typeof(RectTransform), typeof(Image), instrType);
                try
                {
                    Component tempInstr = tempRoot.GetComponent(instrType);
                    Array items12 = Array.CreateInstance(itemType, 12);
                    for (int i = 0; i < 12; i++)
                    {
                        GameObject rowGo = new GameObject($"InstrRow_{i}", typeof(RectTransform), typeof(Image));
                        rowGo.transform.SetParent(tempRoot.transform, false);
                        object itemInst = Activator.CreateInstance(itemType);
                        itemType.GetField("keyCombo").SetValue(itemInst, $"K{i}");
                        itemType.GetField("actionDescription").SetValue(itemInst, $"Desc{i}");
                        items12.SetValue(itemInst, i);
                    }

                    PlayModeTierSupport.InvokeMethod(tempInstr, "ConfigureItems", items12);
                    RectTransform rt0 = tempRoot.transform.Find("InstrRow_0").GetComponent<RectTransform>();
                    RectTransform rt10 = tempRoot.transform.Find("InstrRow_10").GetComponent<RectTransform>();
                    RectTransform rt11 = tempRoot.transform.Find("InstrRow_11").GetComponent<RectTransform>();

                    Assert.AreEqual(new Vector2(752f, 120f), rt10.sizeDelta);
                    Assert.AreEqual(new Vector2(752f, 120f), rt11.sizeDelta);
                    Assert.AreNotEqual(rt0.anchoredPosition, rt10.anchoredPosition, "Overflow row 10 must not overlap row 0.");
                    Assert.AreNotEqual(rt10.anchoredPosition, rt11.anchoredPosition, "Overflow row 11 must not overlap row 10.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tempRoot);
                }
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F8")]
        public IEnumerator T5_B07_PlayerVitalsHudUI_HpClamp_And_DelayedHealthTrail_HoldDecayHealSnap()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                Component vitals = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.UI.PlayerVitalsHudUI");

                Image hpFill = PlayModeTierSupport.GetInstanceField(vitals, "hpFillImage") as Image;
                Image hpTrail = PlayModeTierSupport.GetInstanceField(vitals, "hpTrailImage") as Image;
                Assert.IsNotNull(hpFill);
                Assert.IsNotNull(hpTrail);
                Assert.AreEqual(1f, hpFill.fillAmount, 0.01f);

                float maxHp = (float)PlayModeTierSupport.GetInstanceProperty(player, "MaxHealth");
                PlayModeTierSupport.ApplyFatalDamageToPlayer(player, maxHp * 0.4f);
                yield return null;

                Assert.AreEqual(0.6f, hpFill.fillAmount, 0.05f);
                Assert.IsTrue(hpTrail.enabled, "Damage trail must activate immediately after damage.");
                Assert.GreaterOrEqual(hpTrail.fillAmount, hpFill.fillAmount + 0.30f, "Trail must hold above fill during hold window.");

                yield return new WaitForSecondsRealtime(1.1f);
                Assert.AreEqual(hpFill.fillAmount, hpTrail.fillAmount, 0.03f, "Trail must decay down to HP fill after hold window.");
                Assert.IsFalse(hpTrail.enabled, "Trail must disable once converged.");

                // Damage again then over-heal -> instant upward snap + max clamp
                PlayModeTierSupport.ApplyFatalDamageToPlayer(player, maxHp * 0.3f);
                yield return null;
                Assert.IsTrue(hpTrail.enabled);

                PlayModeTierSupport.InvokeMethod(player, "Heal", 9999f);
                yield return null;

                Assert.LessOrEqual((float)PlayModeTierSupport.GetInstanceProperty(player, "CurrentHealth"), maxHp + 0.01f);
                Assert.AreEqual(1f, hpFill.fillAmount, 0.01f);
                Assert.AreEqual(1f, hpTrail.fillAmount, 0.01f);
                Assert.IsFalse(hpTrail.enabled, "Heal must snap trail upward and hide it.");
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F8")]
        public IEnumerator T5_B08_PlayerVitalsHudUI_RageBoundarySegments_And_PromptMutualExclusion()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                Component rageMgr = PlayModeTierSupport.GetInstanceProperty(player, "RageManager") as Component;
                Component vitals = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.UI.PlayerVitalsHudUI");

                Image[] rims = PlayModeTierSupport.GetInstanceField(vitals, "rageSegmentGlowRims") as Image[];
                Assert.IsNotNull(rims);
                Assert.AreEqual(3, rims.Length);
                GameObject transformPrompt = E2ETestHelpers.FindUIElement("PlayerHUD/TransformReadyPrompt");
                GameObject specialPrompt = E2ETestHelpers.FindUIElement("PlayerHUD/SpecialComboReadyPrompt");

                // 0 / 30
                PlayModeTierSupport.InvokeMethod(rageMgr, "ResetRage");
                yield return null;
                Assert.IsFalse(rims[0].enabled);
                Assert.IsFalse(rims[1].enabled);
                Assert.IsFalse(rims[2].enabled);
                Assert.IsFalse(transformPrompt.activeSelf);
                Assert.IsFalse(specialPrompt.activeSelf);

                // 10 / 30
                PlayModeTierSupport.InvokeMethod(rageMgr, "AddRageFloat", 10f);
                yield return null;
                Assert.IsTrue(rims[0].enabled);
                Assert.IsFalse(rims[1].enabled);
                Assert.IsFalse(rims[2].enabled);
                Assert.IsFalse(transformPrompt.activeSelf);

                // 20 / 30
                PlayModeTierSupport.InvokeMethod(rageMgr, "AddRageFloat", 10f);
                yield return null;
                Assert.IsTrue(rims[0].enabled);
                Assert.IsTrue(rims[1].enabled);
                Assert.IsFalse(rims[2].enabled);
                Assert.IsFalse(transformPrompt.activeSelf);

                // 30 / 30 (untransformed)
                PlayModeTierSupport.InvokeMethod(rageMgr, "AddRageFloat", 10f);
                yield return null;
                Assert.IsTrue(rims[0].enabled);
                Assert.IsTrue(rims[1].enabled);
                Assert.IsTrue(rims[2].enabled);
                Assert.IsTrue(transformPrompt.activeSelf);
                Assert.IsFalse(specialPrompt.activeSelf);

                // Transformed with 30 / 30 rage -> SpecialComboReadyPrompt active, TransformReadyPrompt hidden
                PlayModeTierSupport.InvokeMethod(player, "HandleTransformInput");
                PlayModeTierSupport.InvokeMethod(rageMgr, "FillMaxRage");
                yield return null;
                Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(player, "IsTransformed"));
                Assert.IsFalse(transformPrompt.activeSelf);
                Assert.IsTrue(specialPrompt.activeSelf);

                // Consume 10 rage -> 20/30 -> SpecialComboReadyPrompt hides
                PlayModeTierSupport.InvokeMethod(rageMgr, "TryConsumeRage", 10);
                yield return null;
                Assert.IsFalse(specialPrompt.activeSelf);
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F8")]
        public IEnumerator T5_B09_PlayerVitalsHudUI_ShieldBarDepletion_And_BlockStatusOwnership()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                float maxHp = (float)PlayModeTierSupport.GetInstanceProperty(player, "MaxHealth");

                GameObject shieldBar = E2ETestHelpers.FindUIElement("PlayerHUD/ShieldBar");
                TMP_Text blockTxt = E2ETestHelpers.FindUIElement("BlockStatusBadge/BlockStatusText").GetComponent<TMP_Text>();
                Image badgeBg = E2ETestHelpers.FindUIElement("PlayerHUD/BlockStatusBadge").GetComponent<Image>();
                Image shieldIcon = E2ETestHelpers.FindUIElement("BlockStatusBadge/ShieldIcon").GetComponent<Image>();

                // 1. Virtual shield activation & depletion
                PlayModeTierSupport.InvokeMethod(player, "ActivateSpinalFluidBuff", 10f, 40f, 0f);
                yield return null;
                Assert.IsTrue(shieldBar.activeSelf);

                PlayModeTierSupport.ApplyFatalDamageToPlayer(player, 25f);
                yield return null;
                Assert.AreEqual(15f, (float)PlayModeTierSupport.GetInstanceProperty(player, "VirtualShieldHP"), 0.5f);
                Assert.AreEqual(maxHp, (float)PlayModeTierSupport.GetInstanceProperty(player, "CurrentHealth"), 0.5f);
                Assert.IsTrue(shieldBar.activeSelf);

                PlayModeTierSupport.ApplyFatalDamageToPlayer(player, 30f);
                yield return null;
                Assert.AreEqual(0f, (float)PlayModeTierSupport.GetInstanceProperty(player, "VirtualShieldHP"), 0.01f);
                Assert.AreEqual(maxHp - 15f, (float)PlayModeTierSupport.GetInstanceProperty(player, "CurrentHealth"), 0.5f);
                Assert.IsFalse(shieldBar.activeSelf, "ShieldBar must hide when shield depletes to 0.");

                // 2. Ownership check: GameUIManager must NOT overwrite PlayerVitalsHudUI's ready color (Bug B-01)
                yield return null;
                Assert.AreEqual(0.72f, blockTxt.color.r, 0.03f, "BlockStatusText color must be owned by PlayerVitalsHudUI (0.72r), not GameUIManager (0.45r).");
                Assert.AreEqual("Pill_HUD_ShieldReady_Clean", badgeBg.sprite.name);
                Assert.IsFalse(shieldIcon.enabled, "Clean pill embeds the green shield icon so overlay ShieldIcon stays hidden.");

                // 3. Blocking state
                PlayModeTierSupport.InvokeMethod(player, "HandleBlockInput");
                yield return null;
                yield return null;
                Assert.IsTrue(blockTxt.text.Contains("ĐANG ĐỠ"));
                Assert.AreEqual(0.35f, blockTxt.color.r, 0.03f);
                Assert.AreEqual(0.95f, blockTxt.color.g, 0.03f);

                // 4. Cooldown pill swap -> Pill_HUD_ShieldReady_Blank + ShieldIcon enabled
                Type baseCharType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Characters.BaseCharacter");
                FieldInfo cdField = baseCharType.GetField("blockCooldown", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo lastBlockField = baseCharType.GetField("lastBlockTime", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo blockTimerField = baseCharType.GetField("blockTimer", BindingFlags.Instance | BindingFlags.NonPublic);
                Type stateEnum = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.CharacterState");
                PlayModeTierSupport.InvokeMethod(player, "ChangeState", Enum.Parse(stateEnum, "Idle"));
                blockTimerField.SetValue(player, 0f);
                cdField.SetValue(player, 2.0f);
                lastBlockField.SetValue(player, Time.time);
                yield return null;

                Assert.AreEqual("Pill_HUD_ShieldReady_Blank", badgeBg.sprite.name);
                Assert.IsTrue(shieldIcon.enabled);

                cdField.SetValue(player, 0f);
                yield return null;
                Assert.AreEqual("Pill_HUD_ShieldReady_Clean", badgeBg.sprite.name);
                Assert.IsFalse(shieldIcon.enabled);
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F8")]
        public IEnumerator T5_B10_PlayerVitalsHudUI_MissingChildResilience()
        {
            using (ScopedLogCapture logs = E2ETestHelpers.CaptureLogs())
            {
                Type vitalsType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.PlayerVitalsHudUI");
                GameObject canvasGo = new GameObject("TempMinimalCanvas", typeof(Canvas), typeof(CanvasScaler));
                GameObject hudGo = new GameObject("PlayerHUD", typeof(RectTransform));
                hudGo.transform.SetParent(canvasGo.transform, false);
                GameObject hpObj = new GameObject("PlayerHPBar", typeof(RectTransform), typeof(Slider));
                hpObj.transform.SetParent(hudGo.transform, false);

                try
                {
                    Component vitals = canvasGo.AddComponent(vitalsType);
                    yield return null;
                    yield return null;
                    yield return null;
                    Assert.IsNotNull(vitals);
                    logs.AssertNoWarningsOrErrors();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(canvasGo);
                }
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F9")]
        public IEnumerator T5_B11_PauseMenuUI_RapidToggle_And_DisableWhilePaused_RestoresTimeScale()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                try
                {
                    PlayModeTierSupport.SetSelectedGameMode("Campaign");
                    yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                    Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                    PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);
                    Component pauseUi = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.PauseMenuUI");
                    GameObject pauseModal = E2ETestHelpers.FindUIElement("GameCanvas/PauseModalPanel");

                    for (int i = 0; i < 6; i++)
                    {
                        PlayModeTierSupport.InvokeMethod(pauseUi, "TogglePause");
                        yield return null;
                    }

                    Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(pauseUi, "IsPaused"));
                    Assert.AreEqual(1f, Time.timeScale, 0.001f);
                    Assert.IsFalse(pauseModal.activeSelf);

                    // Pause then disable PauseMenuUI's GameObject directly -> OnDisable must restore Time.timeScale = 1f (Bug B-04)
                    PlayModeTierSupport.InvokeMethod(pauseUi, "SetPaused", true);
                    yield return null;
                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(pauseUi, "IsPaused"));
                    Assert.AreEqual(0f, Time.timeScale, 0.001f);

                    pauseUi.gameObject.SetActive(false);
                    yield return null;

                    Assert.AreEqual(1f, Time.timeScale, 0.001f, "Disabling PauseMenuUI while paused must restore Time.timeScale = 1f.");
                    Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(pauseUi, "IsPaused"));
                }
                finally
                {
                    Time.timeScale = 1f;
                }
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F8")]
        [Category("F9")]
        public IEnumerator T5_B12_HudItemSlotsUI_Depletion_ZeroDimsBorder_And_BlockFallback()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                Component invSetup = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Items.InventoryManager");
                Type itemEnumType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.ConsumableItemType");
                PlayModeTierSupport.InvokeMethod(invSetup, "SetEquippedItem", 0, Enum.Parse(itemEnumType, "SenzuBean"));
                PlayModeTierSupport.InvokeMethod(invSetup, "SetEquippedItem", 1, Enum.Parse(itemEnumType, "None"));
                PlayModeTierSupport.AddOwnedItem("SenzuBean", 1);

                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);

                TMP_Text slotQCount = E2ETestHelpers.FindUIElement("HudItemSlotsPanel/SlotQ/CountPill/CountText").GetComponent<TMP_Text>();
                TMP_Text slotECount = E2ETestHelpers.FindUIElement("HudItemSlotsPanel/SlotE/CountPill/CountText").GetComponent<TMP_Text>();
                Image slotQBorder = E2ETestHelpers.FindUIElement("HudItemSlotsPanel/SlotQ").GetComponent<Image>();
                Image slotQIcon = E2ETestHelpers.FindUIElement("HudItemSlotsPanel/SlotQ/InnerBg/ItemIcon").GetComponent<Image>();

                Assert.AreEqual("1", slotQCount.text.Trim());
                Assert.AreEqual("-", slotECount.text.Trim());

                Component inv = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Items.InventoryManager");
                Assert.IsTrue(PlayModeTierSupport.TryConsumeSlot(inv, 0));
                yield return null;

                Assert.AreEqual("0", slotQCount.text.Trim());
                Assert.Greater(slotQCount.color.r, 0.85f);
                Assert.Less(slotQCount.color.g, 0.55f);
                Assert.LessOrEqual(slotQBorder.color.a, 0.65f);
                Assert.LessOrEqual(slotQIcon.color.a, 0.50f);

                // Fallback block indicator in HudItemSlotsUI when PlayerVitalsHudUI is disabled
                MonoBehaviour vitalsMb = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.PlayerVitalsHudUI") as MonoBehaviour;
                MonoBehaviour gameUiMb = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.GameUIManager") as MonoBehaviour;
                vitalsMb.enabled = false;
                gameUiMb.enabled = false;

                Component player = PlayModeTierSupport.GetInstanceProperty(levelMgr, "ActivePlayer") as Component;
                PlayModeTierSupport.InvokeMethod(player, "HandleBlockInput");
                yield return null;

                string blockTxt = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("BlockStatusBadge/BlockStatusText"));
                Assert.IsTrue(blockTxt.Contains("ĐANG BẬT"), $"Expected HudItemSlotsUI fallback block text, got '{blockTxt}'.");
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F2")]
        [Category("F3")]
        [Category("F4")]
        [Category("F5")]
        public IEnumerator T5_B13_CoinCounterUI_PerScreenBadgeSpriteSwapping_InMainMenu()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                GameObject badge = E2ETestHelpers.FindUIElement("MainMenuCanvas/PersistentCoinBadge");
                Image badgeImg = badge.GetComponent<Image>();

                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "ModeSelect");
                yield return null;
                Assert.IsTrue(badge.activeInHierarchy);
                Assert.AreEqual("Badge_ModeSelect_CoinTopRight_Blank", badgeImg.sprite.name);

                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Shop");
                yield return null;
                Assert.IsTrue(badge.activeInHierarchy);
                Assert.AreEqual("Badge_Shop_TopRightPill_Clean", badgeImg.sprite.name);

                string[] charFlowScreens = { "CharacterSelect", "WorldSelect", "StageSelect" };
                for (int i = 0; i < charFlowScreens.Length; i++)
                {
                    PlayModeTierSupport.ShowMainMenuScreen(uiMgr, charFlowScreens[i]);
                    yield return null;
                    Assert.IsTrue(badge.activeInHierarchy, $"CoinBadge must be active on {charFlowScreens[i]}.");
                    Assert.AreEqual("Badge_CharSelect_TopRightFrame_Clean", badgeImg.sprite.name, $"Sprite mismatch on {charFlowScreens[i]}.");
                }

                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Auth");
                yield return null;
                Assert.IsFalse(badge.activeInHierarchy, "PersistentCoinBadge must hide on Auth screen.");

                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "MainMenu");
                yield return null;
                Assert.IsTrue(badge.activeInHierarchy);
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F9")]
        public IEnumerator T5_B14_GameUIManager_EndlessWaveBanner_NoOverlap_TopRightCluster()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Endless");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Canvas rootCanvas = E2ETestHelpers.FindUIElement("GameCanvas").GetComponent<Canvas>();
                GameObject banner = E2ETestHelpers.FindUIElement("GameCanvas/EndlessWaveBanner");
                GameObject topRight = E2ETestHelpers.FindUIElement("GameCanvas/TopRightClusterBg");
                Assert.IsTrue(banner.activeInHierarchy);
                Assert.IsTrue(topRight.activeInHierarchy);

                Rect bannerRect = E2ETestHelpers.GetRectBoundsIn1920x1080(banner.GetComponent<RectTransform>(), rootCanvas);
                Rect topRightRect = E2ETestHelpers.GetRectBoundsIn1920x1080(topRight.GetComponent<RectTransform>(), rootCanvas);

                Assert.GreaterOrEqual(bannerRect.xMin, 0f);
                Assert.LessOrEqual(topRightRect.xMax, 1920f);
                Assert.IsFalse(bannerRect.Overlaps(topRightRect), "EndlessWaveBanner must not overlap TopRightClusterBg.");

                Component gameUi = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.UI.GameUIManager");
                PlayModeTierSupport.InvokeMethod(gameUi, "UpdateEndlessWave", 12, 8);
                yield return null;

                Assert.IsTrue(PlayModeTierSupport.ReadAnyText(banner).Contains("12"));
                Rect bannerAfter = E2ETestHelpers.GetRectBoundsIn1920x1080(banner.GetComponent<RectTransform>(), rootCanvas);
                Assert.IsFalse(bannerAfter.Overlaps(topRightRect), "Updated EndlessWaveBanner must not overlap TopRightClusterBg.");
            }
        }

        [UnityTest]
        [Category("Tier5")]
        [Category("F10")]
        public IEnumerator T5_B15_TrainingModeManager_ToggleLedSprites_CarouselScroll_DenseSpawnClamping_And_NonTrainingHide()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                // 1. Non-training exclusion (Campaign & Endless)
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");
                Assert.IsFalse(E2ETestHelpers.FindUIElement("GameCanvas/TrainingTopBar").activeInHierarchy, "TrainingTopBar must stay hidden in Campaign.");

                PlayModeTierSupport.SetSelectedGameMode("Endless");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");
                Assert.IsFalse(E2ETestHelpers.FindUIElement("GameCanvas/TrainingTopBar").activeInHierarchy, "TrainingTopBar must stay hidden in Endless.");

                // 2. Training Mode LED sprites & carousel scroll
                PlayModeTierSupport.SetSelectedGameMode("Training");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");
                Component tm = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.GameModes.TrainingModeManager");

                VerifyToggleVisual("Group1_PlayerControls/PlayerInvincibleButton", expectOn: false, "TẮT");
                VerifyToggleVisual("Group1_PlayerControls/AutoMaxRageButton", expectOn: true, "BẬT");
                VerifyToggleVisual("Group2_EnemyControls/EnemyAiButton", expectOn: true, "BẬT");
                VerifyToggleVisual("Group2_EnemyControls/EnemyInvincibleButton", expectOn: false, "TẮT");

                E2ETestHelpers.ClickButton("Group1_PlayerControls/PlayerInvincibleButton");
                E2ETestHelpers.ClickButton("Group1_PlayerControls/AutoMaxRageButton");
                E2ETestHelpers.ClickButton("Group2_EnemyControls/EnemyAiButton");
                E2ETestHelpers.ClickButton("Group2_EnemyControls/EnemyInvincibleButton");
                yield return null;

                VerifyToggleVisual("Group1_PlayerControls/PlayerInvincibleButton", expectOn: true, "BẬT");
                VerifyToggleVisual("Group1_PlayerControls/AutoMaxRageButton", expectOn: false, "TẮT");
                VerifyToggleVisual("Group2_EnemyControls/EnemyAiButton", expectOn: false, "TẮT");
                VerifyToggleVisual("Group2_EnemyControls/EnemyInvincibleButton", expectOn: true, "BẬT");

                ScrollRect scroll = E2ETestHelpers.FindUIElement("Group3_SpawnAndClear/SpawnListScroll").GetComponent<ScrollRect>();
                float pos0 = scroll.horizontalNormalizedPosition;
                E2ETestHelpers.ClickButton("Group3_SpawnAndClear/ScrollRightButton");
                yield return new WaitForSecondsRealtime(0.25f);
                float posRight = scroll.horizontalNormalizedPosition;
                Assert.Greater(posRight, pos0, "ScrollRightButton must advance horizontalNormalizedPosition.");

                E2ETestHelpers.ClickButton("Group3_SpawnAndClear/ScrollLeftButton");
                yield return new WaitForSecondsRealtime(0.25f);
                float posLeft = scroll.horizontalNormalizedPosition;
                Assert.Less(posLeft, posRight, "ScrollLeftButton must move horizontalNormalizedPosition back left.");

                // 3. Dense spawn (15+ units) clamping, null pruning, and cap enforcement (Bug B-08)
                for (int i = 0; i < 15; i++)
                {
                    PlayModeTierSupport.InvokeMethod(tm, "SpawnUnitByIndex", 0);
                }
                yield return null;

                List<GameObject> spawned = PlayModeTierSupport.GetInstanceField(tm, "spawnedUnits") as List<GameObject>;
                Assert.AreEqual(15, spawned.Count);
                for (int i = 0; i < spawned.Count; i++)
                {
                    Assert.IsNotNull(spawned[i]);
                    float x = spawned[i].transform.position.x;
                    Assert.GreaterOrEqual(x, -22f);
                    Assert.LessOrEqual(x, 42f);
                }

                // Prune destroyed entries before spawning more
                UnityEngine.Object.DestroyImmediate(spawned[0]);
                UnityEngine.Object.DestroyImmediate(spawned[1]);
                PlayModeTierSupport.InvokeMethod(tm, "SpawnUnitByIndex", 0);
                Assert.AreEqual(14, spawned.Count, "Destroyed entries must be pruned before adding new units.");

                // Enforce cap at MaxSpawnedTrainingUnits (30)
                for (int i = 0; i < 25; i++)
                {
                    PlayModeTierSupport.InvokeMethod(tm, "SpawnUnitByIndex", 0);
                }
                Assert.AreEqual(30, spawned.Count, "TrainingModeManager must cap spawnedUnits at MaxSpawnedTrainingUnits.");

                // ClearAll removes everything
                E2ETestHelpers.ClickButton("Group3_SpawnAndClear/ClearAllEnemiesButton");
                yield return null;
                yield return null;
                Assert.AreEqual(0, spawned.Count);
            }
        }

        private static void VerifyToggleVisual(string buttonPath, bool expectOn, string expectedStateWord)
        {
            GameObject btnGo = E2ETestHelpers.FindUIElement(buttonPath);
            Assert.IsNotNull(btnGo, $"Toggle button '{buttonPath}' not found.");
            Image bg = btnGo.GetComponent<Image>();
            string expectedSprite = expectOn ? "Toggle_Training_OptionPill_BlueLED_Clean" : "Toggle_Training_OptionPill_Blank";
            Assert.AreEqual(expectedSprite, bg.sprite.name, $"Toggle '{buttonPath}' sprite mismatch.");

            Transform orbTf = btnGo.transform.Find("LedOrb");
            Assert.IsNotNull(orbTf, $"LedOrb missing on '{buttonPath}'.");
            Image orbImg = orbTf.GetComponent<Image>();
            Assert.AreEqual(!expectOn, orbImg.enabled, $"LedOrb.enabled mismatch on '{buttonPath}'.");

            string txt = PlayModeTierSupport.ReadAnyText(btnGo);
            Assert.IsTrue(txt.Contains(expectedStateWord), $"Expected '{expectedStateWord}' in '{txt}' on '{buttonPath}'.");
        }
    }
}
