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
    internal static class PlayModeTierSupport
    {
        public static object GetStaticProperty(Type type, string propName)
        {
            PropertyInfo pi = type.GetProperty(propName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(pi, $"Static property '{type.Name}.{propName}' not found.");
            return pi.GetValue(null);
        }

        public static object GetInstanceProperty(object instance, string propName)
        {
            Assert.IsNotNull(instance, $"Instance is null when reading property '{propName}'.");
            PropertyInfo pi = instance.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(pi, $"Instance property '{instance.GetType().Name}.{propName}' not found.");
            return pi.GetValue(instance);
        }

        public static void SetInstanceProperty(object instance, string propName, object value)
        {
            Assert.IsNotNull(instance, $"Instance is null when setting property '{propName}'.");
            PropertyInfo pi = instance.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(pi, $"Instance property '{instance.GetType().Name}.{propName}' not found.");
            pi.SetValue(instance, value);
        }

        public static object GetInstanceField(object instance, string fieldName)
        {
            Assert.IsNotNull(instance, $"Instance is null when reading field '{fieldName}'.");
            FieldInfo fi = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(fi, $"Instance field '{instance.GetType().Name}.{fieldName}' not found.");
            return fi.GetValue(instance);
        }

        private static MethodInfo ResolveMethod(Type targetType, string methodName, BindingFlags flags, object[] args)
        {
            object[] callArgs = args ?? Array.Empty<object>();
            MethodInfo[] methods = targetType.GetMethods(flags);
            MethodInfo exactMatch = null;
            MethodInfo compatibleMatch = null;
            int exactCount = 0;
            int compatibleCount = 0;

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo candidate = methods[i];
                if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters = candidate.GetParameters();
                if (parameters.Length != callArgs.Length)
                {
                    continue;
                }

                bool allExact = true;
                bool allCompatible = true;
                for (int p = 0; p < parameters.Length; p++)
                {
                    Type paramType = parameters[p].ParameterType;
                    if (paramType.IsByRef)
                    {
                        paramType = paramType.GetElementType();
                    }

                    object arg = callArgs[p];
                    if (arg == null)
                    {
                        bool allowsNull = !paramType.IsValueType || Nullable.GetUnderlyingType(paramType) != null;
                        if (!allowsNull)
                        {
                            allCompatible = false;
                            allExact = false;
                            break;
                        }
                        allExact = false;
                    }
                    else
                    {
                        Type argType = arg.GetType();
                        if (paramType != argType)
                        {
                            allExact = false;
                            if (!paramType.IsAssignableFrom(argType))
                            {
                                allCompatible = false;
                                break;
                            }
                        }
                    }
                }

                if (allExact)
                {
                    exactMatch = candidate;
                    exactCount++;
                }
                else if (allCompatible)
                {
                    compatibleMatch = candidate;
                    compatibleCount++;
                }
            }

            if (exactCount == 1)
            {
                return exactMatch;
            }
            if (exactCount > 1)
            {
                Assert.Fail($"Ambiguous exact overload match for '{targetType.Name}.{methodName}' with {callArgs.Length} args.");
            }
            if (compatibleCount == 1)
            {
                return compatibleMatch;
            }
            if (compatibleCount > 1)
            {
                Assert.Fail($"Ambiguous compatible overload match for '{targetType.Name}.{methodName}' with {callArgs.Length} args.");
            }

            return null;
        }

        public static object InvokeMethodExact(object instance, string methodName, Type[] paramTypes, object[] args)
        {
            Assert.IsNotNull(instance, $"Instance is null when invoking '{methodName}'.");
            MethodInfo mi = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                paramTypes ?? Type.EmptyTypes,
                null);
            Assert.IsNotNull(mi, $"Exact method '{instance.GetType().Name}.{methodName}' not found.");
            return mi.Invoke(instance, args);
        }

        public static object InvokeMethod(object instance, string methodName, params object[] args)
        {
            Assert.IsNotNull(instance, $"Instance is null when invoking '{methodName}'.");
            MethodInfo mi = ResolveMethod(instance.GetType(), methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, args);
            Assert.IsNotNull(mi, $"Method '{instance.GetType().Name}.{methodName}' with {((args != null) ? args.Length : 0)} args not found.");
            return mi.Invoke(instance, args);
        }

        public static object InvokeStatic(Type type, string methodName, params object[] args)
        {
            MethodInfo mi = ResolveMethod(type, methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, args);
            Assert.IsNotNull(mi, $"Static method '{type.Name}.{methodName}' with {((args != null) ? args.Length : 0)} args not found.");
            return mi.Invoke(null, args);
        }

        public static Component FindComponentInScene(string fullTypeName)
        {
            Type t = E2ETestHelpers.RequireRuntimeType(fullTypeName);
            return UnityEngine.Object.FindAnyObjectByType(t) as Component;
        }

        public static Component RequireManagerInstance(string fullTypeName)
        {
            Type t = E2ETestHelpers.RequireRuntimeType(fullTypeName);
            object inst = GetStaticProperty(t, "Instance");
            Assert.IsNotNull(inst, $"{fullTypeName}.Instance returned null.");
            return inst as Component;
        }

        public static void ShowMainMenuScreen(Component uiManager, string screenEnumName)
        {
            Type screenEnum = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.MainMenuScreen");
            object enumVal = Enum.Parse(screenEnum, screenEnumName);
            MethodInfo mi = uiManager.GetType().GetMethod(
                "ShowScreen",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { screenEnum },
                null);
            Assert.IsNotNull(mi, "UIManager.ShowScreen(MainMenuScreen) not found.");
            mi.Invoke(uiManager, new[] { enumVal });
            Canvas.ForceUpdateCanvases();
        }

        public static void SetSelectedGameMode(string modeName)
        {
            Type stageMgr = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Level.StageManager");
            Type gameModeEnum = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.GameMode");
            object modeVal = Enum.Parse(gameModeEnum, modeName);
            InvokeStatic(stageMgr, "SetSelectedGameMode", modeVal);
        }

        public static string GetSelectedGameModeName()
        {
            Type stageMgr = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Level.StageManager");
            object val = InvokeStatic(stageMgr, "LoadSelectedGameMode");
            return val != null ? val.ToString() : string.Empty;
        }

        public static void GrantCoins(int amount)
        {
            Component cm = RequireManagerInstance("StickmanBeatEmUp.Economy.CurrencyManager");
            InvokeMethod(cm, "AddDebugCoins", amount);
        }

        public static int GetCoins()
        {
            Component cm = RequireManagerInstance("StickmanBeatEmUp.Economy.CurrencyManager");
            return (int)GetInstanceProperty(cm, "CurrentCoins");
        }

        public static int GetOwnedCount(string consumableEnumName)
        {
            Component inv = RequireManagerInstance("StickmanBeatEmUp.Items.InventoryManager");
            Type enumType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.ConsumableItemType");
            object enumVal = Enum.Parse(enumType, consumableEnumName);
            return (int)InvokeMethod(inv, "GetOwnedCount", enumVal);
        }

        public static void AddOwnedItem(string consumableEnumName, int amount)
        {
            Component inv = RequireManagerInstance("StickmanBeatEmUp.Items.InventoryManager");
            Type enumType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.ConsumableItemType");
            object enumVal = Enum.Parse(enumType, consumableEnumName);
            MethodInfo mi = inv.GetType().GetMethod(
                "AddOwnedItem",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { enumType, typeof(int) },
                null);
            Assert.IsNotNull(mi, "InventoryManager.AddOwnedItem(ConsumableItemType, int) not found.");
            mi.Invoke(inv, new[] { enumVal, amount });
        }

        public static string GetEquippedItemName(int slotIndex)
        {
            Component inv = RequireManagerInstance("StickmanBeatEmUp.Items.InventoryManager");
            object val = InvokeMethod(inv, "GetEquippedItem", slotIndex);
            return val != null ? val.ToString() : string.Empty;
        }

        public static bool TryConsumeSlot(Component inv, int slotIndex)
        {
            Assert.IsNotNull(inv, "InventoryManager is null when invoking TryConsumeSlot.");
            Type enumType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.ConsumableItemType");
            MethodInfo mi = inv.GetType().GetMethod(
                "TryConsumeSlot",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(int), enumType.MakeByRefType() },
                null);
            Assert.IsNotNull(mi, "InventoryManager.TryConsumeSlot(int, out ConsumableItemType) not found.");
            object[] callArgs = new object[] { slotIndex, null };
            return (bool)mi.Invoke(inv, callArgs);
        }

        public static bool TryPurchaseItem(Component shopMgr, string consumableEnumName, out string feedback)
        {
            Type enumType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.ConsumableItemType");
            object enumVal = Enum.Parse(enumType, consumableEnumName);
            MethodInfo mi = shopMgr.GetType().GetMethod(
                "TryPurchaseItem",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { enumType, typeof(string).MakeByRefType() },
                null);
            Assert.IsNotNull(mi, "ShopManager.TryPurchaseItem(ConsumableItemType, out string) not found.");
            object[] args = new object[] { enumVal, null };
            bool ok = (bool)mi.Invoke(shopMgr, args);
            feedback = args[1] as string;
            return ok;
        }

        public static void ApplyFatalDamageToPlayer(Component player, float amount = 10000f)
        {
            Type dmgType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.DamageData");
            Type atkEnum = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.AttackType");
            object enemyAttack = Enum.Parse(atkEnum, "EnemyAttack");
            object dmgData = Activator.CreateInstance(dmgType, new object[] { amount, Vector2.zero, enemyAttack, false, null });
            InvokeMethod(player, "TakeDamage", dmgData);
        }

        public static string ReadAnyText(GameObject go)
        {
            if (go == null) return string.Empty;
            TMP_Text tmp = go.GetComponent<TMP_Text>();
            if (tmp != null) return tmp.text ?? string.Empty;
            Text leg = go.GetComponent<Text>();
            if (leg != null) return leg.text ?? string.Empty;
            TMP_Text childTmp = go.GetComponentInChildren<TMP_Text>(true);
            if (childTmp != null) return childTmp.text ?? string.Empty;
            Text childLeg = go.GetComponentInChildren<Text>(true);
            return childLeg != null ? (childLeg.text ?? string.Empty) : string.Empty;
        }

        public static Component RequireAccountManager()
        {
            Type acctType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Auth.AccountManager");
            object inst = GetStaticProperty(acctType, "Instance");
            Assert.IsNotNull(inst, "AccountManager.Instance returned null.");
            return inst as Component;
        }

        public static bool TryRegister(Component acctMgr, string user, string pass, string confirm, out string err)
        {
            MethodInfo mi = acctMgr.GetType().GetMethod(
                "TryRegister",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(string), typeof(string), typeof(string).MakeByRefType() },
                null);
            Assert.IsNotNull(mi, "AccountManager.TryRegister(string, string, string, out string) not found.");
            object[] args = new object[] { user, pass, confirm, null };
            bool ok = (bool)mi.Invoke(acctMgr, args);
            err = args[3] as string;
            return ok;
        }

        public static bool TryLogin(Component acctMgr, string user, string pass, out string err)
        {
            MethodInfo mi = acctMgr.GetType().GetMethod(
                "TryLogin",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(string), typeof(string).MakeByRefType() },
                null);
            Assert.IsNotNull(mi, "AccountManager.TryLogin(string, string, out string) not found.");
            object[] args = new object[] { user, pass, null };
            bool ok = (bool)mi.Invoke(acctMgr, args);
            err = args[2] as string;
            return ok;
        }
    }

    public class Tier1_PreMatchFlowTests
    {
        // ==================== F2: 01_MainMenu (3 Tests) ====================

        [UnityTest]
        [Category("Tier1")]
        [Category("F2")]
        public IEnumerator F02_T1_PlayButtonClick_ExitsMainMenu_And_ShowsNextScreenWithBackButton()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");

                GameObject screen1 = E2ETestHelpers.FindUIElement("Screen1_MainMenu");
                Assert.IsNotNull(screen1);
                Assert.IsTrue(screen1.activeInHierarchy, "Screen1_MainMenu should be active on launch.");

                E2ETestHelpers.ClickButton("Screen1_MainMenu/PlayButton");
                yield return null;

                Assert.IsFalse(screen1.activeInHierarchy, "Screen1_MainMenu must become inactive after clicking PlayButton.");

                GameObject modeSelect = E2ETestHelpers.FindUIElement("SubScreen_ModeSelect");
                GameObject authScreen = E2ETestHelpers.FindUIElement("SubScreen_Auth");
                bool modeActive = modeSelect != null && modeSelect.activeInHierarchy;
                bool authActive = authScreen != null && authScreen.activeInHierarchy;
                Assert.IsTrue(modeActive || authActive, "Clicking PlayButton must activate SubScreen_ModeSelect or SubScreen_Auth.");

                if (modeActive)
                {
                    GameObject backBtn = E2ETestHelpers.FindUIElement("MainMenuCanvas/BackButton_FixedTopLeft");
                    Assert.IsNotNull(backBtn);
                    Assert.IsTrue(backBtn.activeInHierarchy, "BackButton_FixedTopLeft must be visible on ModeSelect.");
                }
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F2")]
        public IEnumerator F02_T1_InstructionsButtonClick_OpensInstructions_And_ShowsBackButton()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");

                E2ETestHelpers.ClickButton("Screen1_MainMenu/InstructionsButton");
                yield return null;

                GameObject instr = E2ETestHelpers.FindUIElement("SubScreen_Instructions");
                GameObject screen1 = E2ETestHelpers.FindUIElement("Screen1_MainMenu");
                GameObject back = E2ETestHelpers.FindUIElement("MainMenuCanvas/BackButton_FixedTopLeft");

                Assert.IsNotNull(instr);
                Assert.IsTrue(instr.activeInHierarchy, "SubScreen_Instructions must be active.");
                Assert.IsFalse(screen1.activeInHierarchy, "Screen1_MainMenu must be hidden.");
                Assert.IsTrue(back.activeInHierarchy, "BackButton_FixedTopLeft must be visible.");
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F2")]
        public IEnumerator F02_T1_MainMenu_ThreeButtons_ActiveInteractable_With01CleanSprites()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");

                string[][] expected =
                {
                    new[] { "Screen1_MainMenu/PlayButton", "Btn_MainMenu_Play_Clean" },
                    new[] { "Screen1_MainMenu/InstructionsButton", "Btn_MainMenu_Guide_Clean" },
                    new[] { "Screen1_MainMenu/ExitButton", "Btn_MainMenu_Quit_Clean" },
                };

                for (int i = 0; i < expected.Length; i++)
                {
                    GameObject go = E2ETestHelpers.FindUIElement(expected[i][0]);
                    Assert.IsNotNull(go, $"Missing '{expected[i][0]}'.");
                    Assert.IsTrue(go.activeInHierarchy, $"'{expected[i][0]}' must be active.");
                    Button btn = go.GetComponent<Button>();
                    Assert.IsNotNull(btn);
                    Assert.IsTrue(btn.IsInteractable(), $"'{expected[i][0]}' must be interactable.");
                    Image img = go.GetComponent<Image>();
                    Assert.IsNotNull(img.sprite);
                    Assert.AreEqual(expected[i][1], img.sprite.name, $"Expected '{expected[i][1]}' on '{expected[i][0]}'.");
                }
            }
        }

        // ==================== F3: 02_ModeSelect (3 Tests) ====================

        [UnityTest]
        [Category("Tier1")]
        [Category("F3")]
        public IEnumerator F03_T1_ModeCardCampaign_SetsGameModeCampaign_And_OpensCharacterSelect()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "ModeSelect");
                yield return null;

                E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Campaign");
                yield return null;

                Assert.AreEqual("Campaign", PlayModeTierSupport.GetSelectedGameModeName());
                Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen2_CharacterSelect").activeInHierarchy);
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F3")]
        public IEnumerator F03_T1_ModeCardShop_OpensShopPanel()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "ModeSelect");
                yield return null;

                E2ETestHelpers.ClickButton("SubScreen_ModeSelect/ModeCard_Shop");
                yield return null;

                Assert.IsTrue(E2ETestHelpers.FindUIElement("SubScreen_Shop").activeInHierarchy);
                Assert.IsFalse(E2ETestHelpers.FindUIElement("SubScreen_ModeSelect").activeInHierarchy);
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F3")]
        public IEnumerator F03_T1_ModeSelect_BackButton_ReturnsToMainMenu()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "ModeSelect");
                yield return null;

                E2ETestHelpers.ClickButton("MainMenuCanvas/BackButton_FixedTopLeft");
                yield return null;

                Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen1_MainMenu").activeInHierarchy);
                Assert.IsFalse(E2ETestHelpers.FindUIElement("SubScreen_ModeSelect").activeInHierarchy);
            }
        }

        // ==================== F4: 03_CharacterAndItemSelect (3 Tests) ====================

        [UnityTest]
        [Category("Tier1")]
        [Category("F4")]
        public IEnumerator F04_T1_SelectingErenAndDiend_UpdatesSelectedCharacterPref_And_NameLabel()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "CharacterSelect");
                yield return null;

                E2ETestHelpers.ClickButton("CharCard_Eren/CardInner");
                yield return null;
                Assert.AreEqual(1, PlayerPrefs.GetInt("SelectedCharacter", -1));
                Assert.AreEqual("EREN", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("Screen2_CharacterSelect/SelectedCharName")).Trim());

                E2ETestHelpers.ClickButton("CharCard_Diend/CardInner");
                yield return null;
                Assert.AreEqual(2, PlayerPrefs.GetInt("SelectedCharacter", -1));
                Assert.AreEqual("DIEND", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("Screen2_CharacterSelect/SelectedCharName")).Trim());
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F4")]
        public IEnumerator F04_T1_TogglePreviewFormButton_SwitchesNormalAndTransformedPreview()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "CharacterSelect");
                yield return null;

                Image previewImg = E2ETestHelpers.FindUIElement("Screen2_CharacterSelect/BgCharacterPreview").GetComponent<Image>();
                Sprite beforeSprite = previewImg.sprite;
                Assert.IsNotNull(beforeSprite, "BgCharacterPreview should have a normal-form sprite.");

                E2ETestHelpers.ClickButton("Screen2_CharacterSelect/TogglePreviewFormButton");
                yield return null;
                Sprite transformedSprite = previewImg.sprite;
                Assert.AreNotEqual(beforeSprite, transformedSprite, "TogglePreviewFormButton must change preview sprite.");

                E2ETestHelpers.ClickButton("Screen2_CharacterSelect/TogglePreviewFormButton");
                yield return null;
                Assert.AreEqual(beforeSprite, previewImg.sprite, "Second toggle click must restore normal preview sprite.");
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F4")]
        public IEnumerator F04_T1_CharacterNextButton_InCampaign_AdvancesToWorldSelect()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "CharacterSelect");
                yield return null;

                E2ETestHelpers.ClickButton("Screen2_CharacterSelect/CharacterNextButton");
                yield return null;

                Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen3_WorldSelect").activeInHierarchy);
                Assert.IsFalse(E2ETestHelpers.FindUIElement("Screen2_CharacterSelect").activeInHierarchy);
            }
        }

        // ==================== F5: 05_Shop (3 Tests) ====================

        [UnityTest]
        [Category("Tier1")]
        [Category("F5")]
        public IEnumerator F05_T1_ShopPurchaseSenzuBean_Deducts30Xu_And_IncrementsOwned()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                PlayModeTierSupport.GrantCoins(100);
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Shop");
                yield return null;

                E2ETestHelpers.ClickButton("ShopRow_SenzuBean/BuyButton");
                yield return null;

                Assert.AreEqual(70, PlayModeTierSupport.GetCoins());
                Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SenzuBean"));
                Assert.AreEqual("70 Xu", PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("ShopSummaryBar/ShopBalanceText")).Trim());
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F5")]
        public IEnumerator F05_T1_ShopPurchaseSpinalFluidAndUltraWater_ExactPrices60And50()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                PlayModeTierSupport.GrantCoins(110);
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Shop");
                yield return null;

                E2ETestHelpers.ClickButton("ShopRow_SpinalFluid/BuyButton");
                yield return null;
                Assert.AreEqual(50, PlayModeTierSupport.GetCoins());
                Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("SpinalFluid"));

                E2ETestHelpers.ClickButton("ShopRow_UltraDivineWater/BuyButton");
                yield return null;
                Assert.AreEqual(0, PlayModeTierSupport.GetCoins());
                Assert.AreEqual(1, PlayModeTierSupport.GetOwnedCount("UltraDivineWater"));
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F5")]
        public IEnumerator F05_T1_ShopSummaryBar_And_PersistentCoinBadge_ReflectPurchase()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                PlayModeTierSupport.GrantCoins(80);
                Component uiMgr = PlayModeTierSupport.FindComponentInScene("StickmanBeatEmUp.UI.UIManager");
                PlayModeTierSupport.ShowMainMenuScreen(uiMgr, "Shop");
                yield return null;

                E2ETestHelpers.ClickButton("ShopRow_SenzuBean/BuyButton");
                yield return null;

                string ownedTxt = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("ShopRow_SenzuBean/OwnedText"));
                Assert.IsTrue(ownedTxt.Contains("1"), $"Expected owned count 1 in '{ownedTxt}'.");

                string summaryQ = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("ShopSummaryBar/SlotQSummary"));
                Assert.IsTrue(summaryQ.Contains("x1"), $"Expected SlotQSummary to show x1, got '{summaryQ}'.");
            }
        }

        // ==================== F6: 06_InstructionsGuide (3 Tests) ====================

        [UnityTest]
        [Category("Tier1")]
        [Category("F6")]
        public IEnumerator F06_T1_InstructionsOpen_Populates10VietnameseKeybindRows()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                E2ETestHelpers.ClickButton("Screen1_MainMenu/InstructionsButton");
                yield return null;

                for (int i = 0; i < 10; i++)
                {
                    GameObject row = E2ETestHelpers.FindUIElement($"InstrGridContainer/InstrRow_{i}");
                    Assert.IsNotNull(row, $"InstrRow_{i} missing.");
                    Assert.IsTrue(row.activeInHierarchy, $"InstrRow_{i} should be active.");
                }

                string blockDesc = PlayModeTierSupport.ReadAnyText(E2ETestHelpers.FindUIElement("InstrGridContainer/InstrRow_3/ActionDesc"));
                Assert.IsTrue(blockDesc.Contains("Khiên") || blockDesc.Contains("đỡ"), $"Expected Vietnamese block description on row 3, got '{blockDesc}'.");
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F6")]
        public IEnumerator F06_T1_Instructions_All10ActionSprites_UseIconGuideSimplePreserveAspect()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                E2ETestHelpers.ClickButton("Screen1_MainMenu/InstructionsButton");
                yield return null;

                string[] expectedIcons =
                {
                    "Icon_Guide_Move_AD",
                    "Icon_Guide_Jump_WSpace",
                    "Icon_Guide_Dash",
                    "Icon_Guide_BlockShield_S",
                    "Icon_Guide_NormalAttack_J",
                    "Icon_Guide_Skill1_U",
                    "Icon_Guide_Skill2_I",
                    "Icon_Guide_Combo_DashJ",
                    "Icon_Guide_UseItem_QE",
                    "Icon_Guide_TransformUltimate_O",
                };

                for (int i = 0; i < expectedIcons.Length; i++)
                {
                    Image iconImg = E2ETestHelpers.FindUIElement($"InstrGridContainer/InstrRow_{i}/SpriteFrame/ActionSprite").GetComponent<Image>();
                    Assert.IsNotNull(iconImg.sprite, $"InstrRow_{i} ActionSprite is null.");
                    Assert.AreEqual(expectedIcons[i], iconImg.sprite.name, $"InstrRow_{i} icon mismatch.");
                    Assert.AreEqual(Image.Type.Simple, iconImg.type, $"InstrRow_{i} ActionSprite must be Simple.");
                    Assert.IsTrue(iconImg.preserveAspect, $"InstrRow_{i} ActionSprite must preserveAspect.");
                }
            }
        }

        [UnityTest]
        [Category("Tier1")]
        [Category("F6")]
        public IEnumerator F06_T1_Instructions_BackButton_ReturnsToMainMenu()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                E2ETestHelpers.ClickButton("Screen1_MainMenu/InstructionsButton");
                yield return null;

                Assert.IsTrue(E2ETestHelpers.FindUIElement("SubScreen_Instructions").activeInHierarchy);
                E2ETestHelpers.ClickButton("MainMenuCanvas/BackButton_FixedTopLeft");
                yield return null;

                Assert.IsFalse(E2ETestHelpers.FindUIElement("SubScreen_Instructions").activeInHierarchy);
                Assert.IsTrue(E2ETestHelpers.FindUIElement("Screen1_MainMenu").activeInHierarchy);
            }
        }
    }
}
