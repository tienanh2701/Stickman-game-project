using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StickmanBeatEmUp.Tests.E2E.PlayMode
{
    public class AudioAndSettingsPlayModeTests
    {
        private static readonly string[] All45SfxSubPaths =
        {
            "Goku/goku_J_normal_attack_swing",
            "Goku/goku_J_normal_attack_hit",
            "Goku/goku_J_ssj_attack_hit",
            "Goku/goku_U_normal_ki_blast",
            "Goku/goku_U_ssj_multihit_flurry",
            "Goku/goku_I_normal_kaioken",
            "Goku/goku_I_ssj_kamehameha",
            "Goku/goku_dashJ_ki_explosion",
            "Goku/goku_O_transform_ssj",
            "Goku/goku_dashI_genkidama_charge",
            "Goku/goku_dashI_genkidama_launch",
            "Goku/goku_dashI_genkidama_explode",
            "Goku/goku_state_jump",
            "Goku/goku_state_dash",
            "Goku/goku_state_hurt",
            "Goku/goku_state_hurt_heavy",
            "Goku/goku_state_die",
            "Eren/eren_J_normal_slash_swing",
            "Eren/eren_J_normal_slash_hit",
            "Eren/eren_J_titan_punch_hit",
            "Eren/eren_U_normal_dash_slash",
            "Eren/eren_U_titan_ground_slam",
            "Eren/eren_I_normal_thunder_spear_fire",
            "Eren/eren_I_normal_thunder_spear_explode",
            "Eren/eren_I_titan_rock_throw",
            "Eren/eren_I_titan_rock_smash",
            "Eren/eren_dashJ_spin_slash",
            "Eren/eren_O_transform_titan",
            "Eren/eren_dashI_rumbling_roar",
            "Eren/eren_dashI_rumbling_stomp",
            "Eren/eren_state_jump",
            "Eren/eren_state_dash",
            "Eren/eren_state_hurt",
            "Eren/eren_state_die",
            "Diend/diend_J_normal_gun_shot",
            "Diend/diend_J_bullet_hit",
            "Diend/diend_U_phantom_slash",
            "Diend/diend_I_piercing_shot",
            "Diend/diend_dashJ_fan_spread_shot",
            "Diend/diend_O_transform_summon_kiva",
            "Diend/diend_dashI_dark_kiva_freeze",
            "Diend/diend_state_jump",
            "Diend/diend_state_dash",
            "Diend/diend_state_hurt",
            "Diend/diend_state_die",
            "UI/ui_hover",
            "UI/ui_click",
            "UI/ui_confirm",
            "UI/ui_back",
            "UI/ui_error",
        };

        private sealed class ScopedSettingsAndAudioBackup : IDisposable
        {
            private enum PrefType { Float, Int, String }

            private struct Entry
            {
                public string Key;
                public PrefType Kind;
                public bool Existed;
                public float FloatVal;
                public int IntVal;
                public string StringVal;
            }

            private static readonly (string Key, PrefType Kind)[] SettingsKeys =
            {
                ("Stickman_Settings_MasterVol", PrefType.Float),
                ("Stickman_Settings_BgmVol", PrefType.Float),
                ("Stickman_Settings_SfxVol", PrefType.Float),
                ("Stickman_Settings_MasterMute", PrefType.Int),
                ("Stickman_Settings_BgmMute", PrefType.Int),
                ("Stickman_Settings_SfxMute", PrefType.Int),
                ("Stickman_Settings_Fullscreen", PrefType.Int),
                ("Stickman_Settings_ResIndex", PrefType.Int),
                ("Stickman_Settings_VSync", PrefType.Int),
                ("Stickman_Settings_TargetFps", PrefType.Int),
                ("Stickman_Settings_ShakeOn", PrefType.Int),
                ("Stickman_Settings_ShakeScale", PrefType.Float),
                ("Stickman_Settings_DamagePopups", PrefType.Int),
                ("Stickman_Settings_HitSparks", PrefType.Int),
                ("Stickman_Settings_ControlsHint", PrefType.Int),
                ("Stickman_Settings_DoubleTap", PrefType.Float),
                ("Stickman_Settings_AutoSwapKeys", PrefType.Int),
                ("Stickman_Settings_KeyBindingsCsv", PrefType.String),
            };

            private readonly Entry[] snapshots;
            private bool disposed;

            public ScopedSettingsAndAudioBackup()
            {
                snapshots = new Entry[SettingsKeys.Length];
                for (int i = 0; i < SettingsKeys.Length; i++)
                {
                    string key = SettingsKeys[i].Key;
                    PrefType kind = SettingsKeys[i].Kind;
                    bool existed = PlayerPrefs.HasKey(key);
                    snapshots[i] = new Entry
                    {
                        Key = key,
                        Kind = kind,
                        Existed = existed,
                        FloatVal = (existed && kind == PrefType.Float) ? PlayerPrefs.GetFloat(key) : 0f,
                        IntVal = (existed && kind == PrefType.Int) ? PlayerPrefs.GetInt(key) : 0,
                        StringVal = (existed && kind == PrefType.String) ? PlayerPrefs.GetString(key) : string.Empty,
                    };
                }

                ClearSettingsPrefsAndDestroySingletons();
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;

                Time.timeScale = 1f;
                DestroySingleton("StickmanBeatEmUp.Audio.AudioManager");
                DestroySingleton("StickmanBeatEmUp.Core.GameSettingsManager");

                for (int i = 0; i < snapshots.Length; i++)
                {
                    Entry e = snapshots[i];
                    if (!e.Existed)
                    {
                        PlayerPrefs.DeleteKey(e.Key);
                    }
                    else if (e.Kind == PrefType.Float)
                    {
                        PlayerPrefs.SetFloat(e.Key, e.FloatVal);
                    }
                    else if (e.Kind == PrefType.Int)
                    {
                        PlayerPrefs.SetInt(e.Key, e.IntVal);
                    }
                    else
                    {
                        PlayerPrefs.SetString(e.Key, e.StringVal ?? string.Empty);
                    }
                }
                PlayerPrefs.Save();
            }

            private static void ClearSettingsPrefsAndDestroySingletons()
            {
                DestroySingleton("StickmanBeatEmUp.Audio.AudioManager");
                DestroySingleton("StickmanBeatEmUp.Core.GameSettingsManager");
                for (int i = 0; i < SettingsKeys.Length; i++)
                {
                    PlayerPrefs.DeleteKey(SettingsKeys[i].Key);
                }
                PlayerPrefs.Save();
            }

            private static void DestroySingleton(string typeFullName)
            {
                Type t = E2ETestHelpers.FindRuntimeType(typeFullName);
                if (t == null) return;

                PropertyInfo hasProp = t.GetProperty("HasInstance", BindingFlags.Static | BindingFlags.Public);
                if (hasProp != null && hasProp.GetValue(null) is bool hasInst && !hasInst)
                {
                    return;
                }

                PropertyInfo instProp = t.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
                object inst = instProp != null ? instProp.GetValue(null) : UnityEngine.Object.FindAnyObjectByType(t);
                if (inst is Component comp && comp != null)
                {
                    UnityEngine.Object.DestroyImmediate(comp.gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator MainMenu_SettingsModal_AudioSlidersAndMutes_UpdateAudioManagerLive()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            using (new ScopedSettingsAndAudioBackup())
            using (ScopedLogCapture logs = E2ETestHelpers.CaptureLogs())
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");
                yield return new WaitForSecondsRealtime(0.35f);

                Type audioType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Audio.AudioManager");
                Type settingsType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.GameSettingsManager");
                object audioMgr = GetSingletonInstance(audioType);
                object settingsMgr = GetSingletonInstance(settingsType);

                AudioClip bgmClip = GetProp<AudioClip>(audioMgr, "CurrentBgmClip");
                Assert.IsNotNull(bgmClip, "MainMenu should start with a non-null BGM clip.");
                Assert.AreEqual("bgm_world1_aot_stage", bgmClip.name);
                Assert.IsTrue(GetProp<bool>(audioMgr, "IsBgmPlaying"), "MainMenu BGM should be playing.");

                GameObject settingsBtnGo = E2ETestHelpers.FindUIElement("MainMenuSettingsButton");
                Assert.IsNotNull(settingsBtnGo, "MainMenuSettingsButton not found in MainMenu.");
                Button settingsBtn = settingsBtnGo.GetComponent<Button>();
                Assert.IsNotNull(settingsBtn);
                settingsBtn.onClick.Invoke();
                yield return null;

                GameObject modalGo = E2ETestHelpers.FindUIElement("SettingsModalPanel");
                Assert.IsNotNull(modalGo, "SettingsModalPanel was not created when opening MainMenu settings.");
                Assert.IsTrue(modalGo.activeInHierarchy, "SettingsModalPanel should be active after clicking settings button.");

                InvokeMethod(settingsMgr, "SetMasterVolume", 0.5f);
                InvokeMethod(settingsMgr, "SetBgmVolume", 0.4f);
                InvokeMethod(settingsMgr, "SetSfxVolume", 0.75f);
                yield return null;

                float effectiveBgm = GetProp<float>(audioMgr, "CurrentBgmVolume");
                Assert.AreEqual(0.20f, effectiveBgm, 0.02f, "AudioManager BGM volume should match MasterVolume * BgmVolume.");

                InvokeMethod(settingsMgr, "SetBgmMuted", true);
                yield return null;
                Assert.IsTrue(GetProp<bool>(audioMgr, "IsBgmMuted"), "AudioManager should mute BGM channel live.");

                InvokeMethod(settingsMgr, "SetSfxMuted", true);
                yield return null;
                Assert.IsTrue(GetProp<bool>(audioMgr, "IsSfxMuted"), "AudioManager should mute SFX channel live.");

                InvokeMethod(settingsMgr, "SetBgmMuted", false);
                InvokeMethod(settingsMgr, "SetSfxMuted", false);
                yield return null;
                Assert.IsFalse(GetProp<bool>(audioMgr, "IsBgmMuted"));
                Assert.IsFalse(GetProp<bool>(audioMgr, "IsSfxMuted"));

                InvokeMethod(audioMgr, "PlayTestSfx");
                InvokeMethod(audioMgr, "PlayUiSfx", new object[] { null, 1f });

                Type uiSfxEnum = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Audio.UiSfxType");
                MethodInfo playTypedUi = audioType.GetMethod("PlayUiSfx", new[] { uiSfxEnum, typeof(float) });
                Assert.IsNotNull(playTypedUi, "AudioManager.PlayUiSfx(UiSfxType, float) not found.");
                foreach (object enumVal in Enum.GetValues(uiSfxEnum))
                {
                    playTypedUi.Invoke(audioMgr, new[] { enumVal, (object)0.6f });
                }

                Type hoverEffectType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.ModeCardHoverEffect");
                Component settingsHover = settingsBtnGo.GetComponent(hoverEffectType);
                Assert.IsNotNull(settingsHover, "ModeCardHoverEffect should exist on MainMenuSettingsButton.");

                PointerEventData ped = new PointerEventData(EventSystem.current);
                (settingsHover as IPointerEnterHandler)?.OnPointerEnter(ped);
                (settingsHover as IPointerDownHandler)?.OnPointerDown(ped);
                (settingsHover as IPointerUpHandler)?.OnPointerUp(ped);

                Type juiceType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.UIButtonJuice");
                MethodInfo ensureBoot = juiceType.GetMethod("EnsureBootstrapper", BindingFlags.Static | BindingFlags.Public);
                ensureBoot?.Invoke(null, null);

                // Close modal cleanly as a user would, verifying UIButtonJuice on modal CloseSettingsButton
                GameObject closeBtnGo = E2ETestHelpers.FindUIElement("SettingsModalPanel/SettingsBox/CloseSettingsButton")
                    ?? modalGo.transform.Find("SettingsBox/CloseSettingsButton")?.gameObject;
                Assert.IsNotNull(closeBtnGo, "SettingsModalPanel CloseSettingsButton not found.");
                Component juice = closeBtnGo.GetComponent(juiceType);
                Assert.IsNotNull(juice, "UIButtonJuice should attach to SettingsModalPanel CloseSettingsButton.");

                (juice as IPointerEnterHandler)?.OnPointerEnter(ped);
                (juice as IPointerDownHandler)?.OnPointerDown(ped);
                (juice as IPointerUpHandler)?.OnPointerUp(ped);
                closeBtnGo.GetComponent<Button>()?.onClick.Invoke();
                yield return null;
                Assert.IsFalse(modalGo.activeInHierarchy, "SettingsModalPanel should close after clicking CloseSettingsButton.");

                logs.AssertNoWarningsOrErrors();
            }
        }

        [UnityTest]
        public IEnumerator AudioManager_All3Worlds_StageAndBossBgm_AndAll45Sfx_PlayWithoutError()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            using (new ScopedSettingsAndAudioBackup())
            using (ScopedLogCapture logs = E2ETestHelpers.CaptureLogs())
            {
                yield return E2ETestHelpers.LoadSceneAndWait("MainMenu");

                Type audioType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Audio.AudioManager");
                Type worldTypeEnum = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.WorldType");
                object audioMgr = GetSingletonInstance(audioType);
                MethodInfo playWorldImmediate = audioType.GetMethod(
                    "PlayWorldBgm",
                    new[] { worldTypeEnum, typeof(bool), typeof(bool) });
                Assert.IsNotNull(playWorldImmediate, "AudioManager.PlayWorldBgm(WorldType, bool, bool) not found.");

                (string EnumName, bool Boss, string ExpectedClip)[] matrix =
                {
                    ("World1_RuinedCity", false, "bgm_world1_aot_stage"),
                    ("World1_RuinedCity", true, "bgm_world1_aot_boss"),
                    ("World2_Wasteland", false, "bgm_world2_db_stage"),
                    ("World2_Wasteland", true, "bgm_world2_db_boss"),
                    ("World3_NeoCity", false, "bgm_world3_diend_stage"),
                    ("World3_NeoCity", true, "bgm_world3_diend_boss"),
                };

                for (int i = 0; i < matrix.Length; i++)
                {
                    object worldVal = Enum.Parse(worldTypeEnum, matrix[i].EnumName);
                    playWorldImmediate.Invoke(audioMgr, new[] { worldVal, matrix[i].Boss, (object)true });
                    yield return null;

                    AudioClip active = GetProp<AudioClip>(audioMgr, "CurrentBgmClip");
                    Assert.IsNotNull(active, $"BGM clip was null for {matrix[i].EnumName} boss={matrix[i].Boss}.");
                    Assert.AreEqual(matrix[i].ExpectedClip, active.name);
                }

                // Also verify crossfade transition cleanly completes with zero warnings/errors.
                object w2 = Enum.Parse(worldTypeEnum, "World2_Wasteland");
                playWorldImmediate.Invoke(audioMgr, new[] { w2, false, (object)false });
                yield return new WaitForSecondsRealtime(0.55f);
                AudioClip crossfaded = GetProp<AudioClip>(audioMgr, "CurrentBgmClip");
                Assert.IsNotNull(crossfaded);
                Assert.AreEqual("bgm_world2_db_stage", crossfaded.name);

                for (int i = 0; i < All45SfxSubPaths.Length; i++)
                {
                    string subPath = All45SfxSubPaths[i];
                    AudioClip clip = InvokeMethod(audioMgr, "ResolveClip", "Audio/SFX/" + subPath) as AudioClip;
                    Assert.IsNotNull(clip, $"Failed to resolve SFX clip '{subPath}' at runtime.");
                }

                InvokeMethod(audioMgr, "PlaySfxByResourceName", new object[] { "Goku/goku_I_ssj_kamehameha", 0.5f, 0f });
                InvokeMethod(audioMgr, "PlaySfxByResourceName", new object[] { "Eren/eren_dashI_rumbling_roar", 0.5f, 0f });
                InvokeMethod(audioMgr, "PlaySfxByResourceName", new object[] { "Diend/diend_O_transform_summon_kiva", 0.5f, 0f });
                yield return null;

                logs.AssertNoWarningsOrErrors();
            }
        }

        [UnityTest]
        public IEnumerator Battle_PauseSettings_And_KeybindConflictPrevention_WorkEndToEnd()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            using (new ScopedSettingsAndAudioBackup())
            using (ScopedLogCapture logs = E2ETestHelpers.CaptureLogs())
            {
                PlayerPrefs.SetInt("SelectedGameMode", 0);
                PlayerPrefs.SetInt("SelectedStageIndex", 0);
                PlayerPrefs.SetInt("SelectedCharacter", 0);
                PlayerPrefs.Save();

                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");
                yield return null;
                yield return new WaitForSecondsRealtime(0.55f);

                Type audioType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Audio.AudioManager");
                object audioMgr = GetSingletonInstance(audioType);
                AudioClip battleBgm = GetProp<AudioClip>(audioMgr, "CurrentBgmClip");
                Assert.IsNotNull(battleBgm, "Battle BGM clip must be active in Stickman_DemoScene.");
                Assert.AreEqual("bgm_world1_aot_stage", battleBgm.name);

                Type pauseType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.PauseMenuUI");
                object pauseUi = UnityEngine.Object.FindAnyObjectByType(pauseType);
                Assert.IsNotNull(pauseUi, "PauseMenuUI not found in Stickman_DemoScene.");

                InvokeMethod(pauseUi, "SetPaused", true);
                yield return null;
                Assert.AreEqual(0f, Time.timeScale, 0.001f, "Time.timeScale must be 0 when paused.");

                GameObject pauseSettingsGo = E2ETestHelpers.FindUIElement("PauseSettingsButton");
                Assert.IsNotNull(pauseSettingsGo, "PauseSettingsButton not found in PauseMenuUI.");
                Button pauseSettingsBtn = pauseSettingsGo.GetComponent<Button>();
                Assert.IsNotNull(pauseSettingsBtn);
                pauseSettingsBtn.onClick.Invoke();
                yield return null;

                GameObject settingsModalGo = E2ETestHelpers.FindUIElement("SettingsModalPanel");
                Assert.IsNotNull(settingsModalGo, "SettingsModalPanel not found when opened from PauseMenu.");
                Assert.IsTrue(settingsModalGo.activeInHierarchy, "SettingsModalPanel should be active over PauseMenu.");

                Type settingsType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.GameSettingsManager");
                Type presetEnum = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.CorePure.Settings.ControlPresetId");
                Type actionEnum = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.CorePure.Settings.GameInputAction");
                Type statusEnum = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.CorePure.Settings.KeyBindChangeStatus");
                Type inputType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.InputHandler");
                Type gameUiType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.UI.GameUIManager");

                object settingsMgr = GetSingletonInstance(settingsType);
                object inputHandler = UnityEngine.Object.FindAnyObjectByType(inputType);
                object gameUi = UnityEngine.Object.FindAnyObjectByType(gameUiType);
                Assert.IsNotNull(inputHandler, "InputHandler not found on player in Stickman_DemoScene.");
                Assert.IsNotNull(gameUi, "GameUIManager not found in Stickman_DemoScene.");

                // Apply ArrowKeysZx preset -> InputHandler and ControlsHint update live without conflict
                object arrowPreset = Enum.Parse(presetEnum, "ArrowKeysZx");
                InvokeMethod(settingsMgr, "ApplyControlPreset", arrowPreset);
                yield return null;

                object moveLeftAction = Enum.Parse(actionEnum, "MoveLeft");
                object attackAction = Enum.Parse(actionEnum, "Attack");
                Assert.AreEqual(KeyCode.LeftArrow, InvokeMethod(settingsMgr, "GetKeyCode", moveLeftAction));

                FieldInfo leftKeyField = inputType.GetField("leftKey", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(leftKeyField);
                Assert.AreEqual(KeyCode.LeftArrow, (KeyCode)leftKeyField.GetValue(inputHandler), "InputHandler leftKey did not update from preset.");

                FieldInfo hintTextField = gameUiType.GetField("controlsHintText", BindingFlags.Instance | BindingFlags.NonPublic);
                TMP_Text hintTmp = hintTextField != null ? hintTextField.GetValue(gameUi) as TMP_Text : null;
                if (hintTmp != null)
                {
                    Assert.IsTrue(hintTmp.text.Contains("←/→"), $"Expected ControlsHint to contain '←/→', got: {hintTmp.text}");
                }

                // Reject duplicate key when AutoSwapOnConflict is false
                InvokeMethod(settingsMgr, "SetAutoSwapOnConflict", false);
                object rejectResult = InvokeMethod(settingsMgr, "TryRebindKey", new[] { attackAction, (object)KeyCode.LeftArrow });
                object rejectStatus = rejectResult.GetType().GetProperty("Status").GetValue(rejectResult);
                Assert.AreEqual(Enum.Parse(statusEnum, "ConflictRejected"), rejectStatus, "Duplicate key binding must be rejected when auto-swap is disabled.");
                Assert.AreEqual(KeyCode.Z, InvokeMethod(settingsMgr, "GetKeyCode", attackAction));

                // Swap cleanly when AutoSwapOnConflict is true
                InvokeMethod(settingsMgr, "SetAutoSwapOnConflict", true);
                object swapResult = InvokeMethod(settingsMgr, "TryRebindKey", new[] { attackAction, (object)KeyCode.LeftArrow });
                object swapStatus = swapResult.GetType().GetProperty("Status").GetValue(swapResult);
                Assert.AreEqual(Enum.Parse(statusEnum, "Swapped"), swapStatus);
                Assert.AreEqual(KeyCode.LeftArrow, InvokeMethod(settingsMgr, "GetKeyCode", attackAction));
                Assert.AreEqual(KeyCode.Z, InvokeMethod(settingsMgr, "GetKeyCode", moveLeftAction));

                InvokeMethod(settingsMgr, "ResetToDefaults");
                Assert.AreEqual(KeyCode.A, InvokeMethod(settingsMgr, "GetKeyCode", moveLeftAction));
                Assert.AreEqual(KeyCode.J, InvokeMethod(settingsMgr, "GetKeyCode", attackAction));

                // Close Settings and unpause cleanly
                InvokeMethod(pauseUi, "SetPaused", false);
                yield return null;
                Assert.IsFalse(settingsModalGo.activeInHierarchy, "SettingsModalPanel must close on unpause.");
                Assert.AreEqual(1f, Time.timeScale, 0.001f, "Time.timeScale must restore to 1 on unpause.");

                logs.AssertNoWarningsOrErrors();
            }
        }

        private static object GetSingletonInstance(Type managerType)
        {
            PropertyInfo instProp = managerType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(instProp, $"{managerType.Name}.Instance property not found.");
            object inst = instProp.GetValue(null);
            Assert.IsNotNull(inst, $"{managerType.Name}.Instance returned null.");
            return inst;
        }

        private static T GetProp<T>(object target, string propName)
        {
            PropertyInfo prop = target.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(prop, $"{target.GetType().Name}.{propName} property not found.");
            return (T)prop.GetValue(target);
        }

        private static object InvokeMethod(object target, string methodName, params object[] args)
        {
            MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                if (!string.Equals(methods[i].Name, methodName, StringComparison.Ordinal)) continue;
                ParameterInfo[] ps = methods[i].GetParameters();
                if (ps.Length != args.Length) continue;
                return methods[i].Invoke(target, args);
            }
            Assert.Fail($"Method '{target.GetType().Name}.{methodName}' with {args.Length} arg(s) not found.");
            return null;
        }
    }
}
