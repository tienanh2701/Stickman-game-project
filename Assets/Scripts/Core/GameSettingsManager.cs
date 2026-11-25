using System;
using UnityEngine;
using StickmanBeatEmUp.CorePure.Settings;

namespace StickmanBeatEmUp.Core
{
    /// <summary>
    /// Persistent runtime coordinator for Audio, Display, Gameplay/HUD toggles,
    /// and conflict-free Key Bindings backed by PlayerPrefs.
    /// </summary>
    public class GameSettingsManager : MonoBehaviour
    {
        public const string PrefPrefix = "Stickman_Settings_";
        private const string MasterVolKey = PrefPrefix + "MasterVol";
        private const string BgmVolKey = PrefPrefix + "BgmVol";
        private const string SfxVolKey = PrefPrefix + "SfxVol";
        private const string MasterMuteKey = PrefPrefix + "MasterMute";
        private const string BgmMuteKey = PrefPrefix + "BgmMute";
        private const string SfxMuteKey = PrefPrefix + "SfxMute";
        private const string FullscreenKey = PrefPrefix + "Fullscreen";
        private const string ResIndexKey = PrefPrefix + "ResIndex";
        private const string VSyncKey = PrefPrefix + "VSync";
        private const string TargetFpsKey = PrefPrefix + "TargetFps";
        private const string ShakeOnKey = PrefPrefix + "ShakeOn";
        private const string ShakeScaleKey = PrefPrefix + "ShakeScale";
        private const string DamagePopupsKey = PrefPrefix + "DamagePopups";
        private const string HitSparksKey = PrefPrefix + "HitSparks";
        private const string ControlsHintKey = PrefPrefix + "ControlsHint";
        private const string DoubleTapKey = PrefPrefix + "DoubleTap";
        private const string AutoSwapKeysKey = PrefPrefix + "AutoSwapKeys";
        private const string KeyBindingsCsvKey = PrefPrefix + "KeyBindingsCsv";

        private static readonly string[] AllPrefKeys =
        {
            MasterVolKey,
            BgmVolKey,
            SfxVolKey,
            MasterMuteKey,
            BgmMuteKey,
            SfxMuteKey,
            FullscreenKey,
            ResIndexKey,
            VSyncKey,
            TargetFpsKey,
            ShakeOnKey,
            ShakeScaleKey,
            DamagePopupsKey,
            HitSparksKey,
            ControlsHintKey,
            DoubleTapKey,
            AutoSwapKeysKey,
            KeyBindingsCsvKey
        };

        private static GameSettingsManager instance;
        private static bool isQuitting;

        public static bool HasInstance => instance != null;
        public static bool IsCapturingKeyRebind { get; set; }

        public static GameSettingsManager Instance
        {
            get
            {
                if (instance == null && Application.isPlaying && !isQuitting)
                {
                    instance = FindAnyObjectByType<GameSettingsManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("GameSettingsManager");
                        DontDestroyOnLoad(go);
                        instance = go.AddComponent<GameSettingsManager>();
                    }
                }
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            isQuitting = false;
            IsCapturingKeyRebind = false;
        }

        private SettingsDataDTO data = SettingsDataDTO.CreateDefault();

        public event Action OnSettingsChanged;
        public event Action OnAudioSettingsChanged;
        public event Action OnKeyBindingsChanged;
        public event Action OnHudSettingsChanged;

        // Audio
        public float MasterVolume => data.MasterVolume;
        public float BgmVolume => data.BgmVolume;
        public float SfxVolume => data.SfxVolume;
        public bool MasterMuted => data.MasterMuted;
        public bool BgmMuted => data.BgmMuted;
        public bool SfxMuted => data.SfxMuted;
        public float EffectiveBgmVolume => data.EffectiveBgmVolume;
        public float EffectiveSfxVolume => data.EffectiveSfxVolume;

        // Display & Performance
        public bool Fullscreen => data.Fullscreen;
        public int ResolutionIndex => data.ResolutionIndex;
        public bool VSyncEnabled => data.VSyncEnabled;
        public int TargetFps => data.TargetFps;

        // Gameplay & HUD
        public bool ScreenShakeEnabled => data.ScreenShakeEnabled;
        public float ScreenShakeIntensity => data.ScreenShakeIntensity;
        public float EffectiveScreenShakeScale => data.EffectiveScreenShakeScale;
        public bool ShowDamagePopups => data.ShowDamagePopups;
        public bool ShowHitSparks => data.ShowHitSparks;
        public bool ShowControlsHint => data.ShowControlsHint;

        // Controls
        public float DoubleTapThreshold => data.DoubleTapThreshold;
        public ControlPresetId ControlPreset => data.ControlPreset;
        public bool AutoSwapOnConflict => data.AutoSwapOnConflict;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (transform.parent == null && Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            LoadFromPrefs();
            ApplyDisplaySettings();
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
            IsCapturingKeyRebind = false;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                IsCapturingKeyRebind = false;
            }
        }

        public void SetMasterVolume(float volume)
        {
            data.MasterVolume = Mathf.Clamp01(volume);
            SaveAndNotifyAudio();
        }

        public void SetBgmVolume(float volume)
        {
            data.BgmVolume = Mathf.Clamp01(volume);
            SaveAndNotifyAudio();
        }

        public void SetSfxVolume(float volume)
        {
            data.SfxVolume = Mathf.Clamp01(volume);
            SaveAndNotifyAudio();
        }

        public void SetMasterMuted(bool muted)
        {
            data.MasterMuted = muted;
            SaveAndNotifyAudio();
        }

        public void SetBgmMuted(bool muted)
        {
            data.BgmMuted = muted;
            SaveAndNotifyAudio();
        }

        public void SetSfxMuted(bool muted)
        {
            data.SfxMuted = muted;
            SaveAndNotifyAudio();
        }

        public void SetFullscreen(bool fullscreen)
        {
            data.Fullscreen = fullscreen;
            SaveAndNotifyDisplay();
        }

        public void SetResolutionIndex(int index)
        {
            data.ResolutionIndex = Mathf.Clamp(index, 0, 2);
            SaveAndNotifyDisplay();
        }

        public void SetVSyncEnabled(bool enabled)
        {
            data.VSyncEnabled = enabled;
            SaveAndNotifyDisplay();
        }

        public void SetTargetFps(int fps)
        {
            data.TargetFps = fps;
            data.Sanitize();
            SaveAndNotifyDisplay();
        }

        public void SetScreenShakeEnabled(bool enabled)
        {
            data.ScreenShakeEnabled = enabled;
            SaveAndNotifyGeneral();
        }

        public void SetScreenShakeIntensity(float intensity)
        {
            data.ScreenShakeIntensity = Mathf.Clamp01(intensity);
            SaveAndNotifyGeneral();
        }

        public void SetShowDamagePopups(bool show)
        {
            data.ShowDamagePopups = show;
            SaveAndNotifyGeneral();
        }

        public void SetShowHitSparks(bool show)
        {
            data.ShowHitSparks = show;
            SaveAndNotifyGeneral();
        }

        public void SetShowControlsHint(bool show)
        {
            if (data.ShowControlsHint == show) return;
            data.ShowControlsHint = show;
            SaveToPrefs();
            OnHudSettingsChanged?.Invoke();
            OnSettingsChanged?.Invoke();
        }

        public void SetDoubleTapThreshold(float seconds)
        {
            data.DoubleTapThreshold = Mathf.Clamp(
                seconds,
                SettingsDataDTO.MinDoubleTapThreshold,
                SettingsDataDTO.MaxDoubleTapThreshold);
            SaveToPrefs();
            OnKeyBindingsChanged?.Invoke();
            OnSettingsChanged?.Invoke();
        }

        public void SetAutoSwapOnConflict(bool autoSwap)
        {
            data.AutoSwapOnConflict = autoSwap;
            SaveToPrefs();
            OnSettingsChanged?.Invoke();
        }

        public KeyCode GetKeyCode(GameInputAction action)
        {
            return (KeyCode)data.KeyBindings.GetKeyCode(action);
        }

        public KeyBindChangeResult TryRebindKey(GameInputAction action, KeyCode newKey)
        {
            KeyBindConflictPolicy policy = data.AutoSwapOnConflict
                ? KeyBindConflictPolicy.Swap
                : KeyBindConflictPolicy.Reject;

            KeyBindChangeResult result = data.KeyBindings.TrySetBinding(action, (int)newKey, policy);
            if (result.IsSuccess && result.Status != KeyBindChangeStatus.Unchanged)
            {
                data.ControlPreset = data.KeyBindings.DetectPreset();
                SaveToPrefs();
                OnKeyBindingsChanged?.Invoke();
                OnSettingsChanged?.Invoke();
            }
            return result;
        }

        public void ApplyControlPreset(ControlPresetId preset)
        {
            data.KeyBindings.ApplyPreset(preset);
            data.ControlPreset = data.KeyBindings.DetectPreset();
            SaveToPrefs();
            OnKeyBindingsChanged?.Invoke();
            OnSettingsChanged?.Invoke();
        }

        public void ApplyDisplaySettings()
        {
            QualitySettings.vSyncCount = data.VSyncEnabled ? 1 : 0;
            Application.targetFrameRate = data.VSyncEnabled ? -1 : data.TargetFps;

            if (!Application.isBatchMode && !Application.isEditor)
            {
                SettingsDataDTO.GetResolutionSize(data.ResolutionIndex, out int w, out int h);
                FullScreenMode mode = data.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                Screen.SetResolution(w, h, mode);
            }
            else if (!Application.isBatchMode)
            {
                Screen.fullScreen = data.Fullscreen;
            }
        }

        public void ResetToDefaults()
        {
            for (int i = 0; i < AllPrefKeys.Length; i++)
            {
                PlayerPrefs.DeleteKey(AllPrefKeys[i]);
            }

            data = SettingsDataDTO.CreateDefault();
            SaveToPrefs();
            ApplyDisplaySettings();

            OnAudioSettingsChanged?.Invoke();
            OnKeyBindingsChanged?.Invoke();
            OnHudSettingsChanged?.Invoke();
            OnSettingsChanged?.Invoke();
        }

        public string BuildControlsHintRichText()
        {
            string leftLbl = FormatKeyLabel(GetKeyCode(GameInputAction.MoveLeft));
            string rightLbl = FormatKeyLabel(GetKeyCode(GameInputAction.MoveRight));
            string jumpLbl = FormatKeyLabel(GetKeyCode(GameInputAction.Jump));
            string altJumpLbl = FormatKeyLabel(GetKeyCode(GameInputAction.AltJump));
            string blockLbl = FormatKeyLabel(GetKeyCode(GameInputAction.Block));
            string attackLbl = FormatKeyLabel(GetKeyCode(GameInputAction.Attack));
            string skill1Lbl = FormatKeyLabel(GetKeyCode(GameInputAction.Skill1));
            string skill2Lbl = FormatKeyLabel(GetKeyCode(GameInputAction.Skill2));
            string transformLbl = FormatKeyLabel(GetKeyCode(GameInputAction.Transform));
            string itemQLbl = FormatKeyLabel(GetKeyCode(GameInputAction.ItemQ));
            string itemELbl = FormatKeyLabel(GetKeyCode(GameInputAction.ItemE));

            return $"<color=#FFD84D>[{leftLbl}/{rightLbl}]</color> Di chuyển   •   " +
                   $"<color=#FFD84D>[2x {leftLbl}/{rightLbl}]</color> Lướt   •   " +
                   $"<color=#FFD84D>[{altJumpLbl}/{jumpLbl}]</color> Nhảy   •   " +
                   $"<color=#FFD84D>[{blockLbl}+{jumpLbl}]</color> Rơi thềm   •   " +
                   $"<color=#FFD84D>[{attackLbl}]</color> Đánh   •   " +
                   $"<color=#FFD84D>[{blockLbl}]</color> Khiên   •   " +
                   $"<color=#FFD84D>[{skill1Lbl}/{skill2Lbl}]</color> Kỹ năng   •   " +
                   $"<color=#FFD84D>[{transformLbl}]</color> Biến hình   •   " +
                   $"<color=#FFD84D>[{itemQLbl}/{itemELbl}]</color> Vật phẩm";
        }

        public static string GetActionDisplayName(GameInputAction action)
        {
            switch (action)
            {
                case GameInputAction.MoveLeft: return "Di chuyển trái";
                case GameInputAction.MoveRight: return "Di chuyển phải";
                case GameInputAction.Jump: return "Nhảy chính";
                case GameInputAction.AltJump: return "Nhảy phụ";
                case GameInputAction.Attack: return "Đánh thường";
                case GameInputAction.Skill1: return "Kỹ năng 1 (U)";
                case GameInputAction.Skill2: return "Kỹ năng 2 (I)";
                case GameInputAction.Transform: return "Biến hình";
                case GameInputAction.Block: return "Khiên đỡ đòn";
                case GameInputAction.ItemQ: return "Vật phẩm ô 1";
                case GameInputAction.ItemE: return "Vật phẩm ô 2";
                case GameInputAction.Pause: return "Tạm dừng";
                default: return action.ToString();
            }
        }

        public static string FormatKeyLabel(KeyCode key)
        {
            if (key >= KeyCode.A && key <= KeyCode.Z)
            {
                return ((char)('A' + (key - KeyCode.A))).ToString();
            }
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            {
                return ((char)('0' + (key - KeyCode.Alpha0))).ToString();
            }

            switch (key)
            {
                case KeyCode.Space: return "Space";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftShift: return "LShift";
                case KeyCode.RightShift: return "RShift";
                case KeyCode.LeftControl: return "LCtrl";
                case KeyCode.RightControl: return "RCtrl";
                case KeyCode.Escape: return "ESC";
                case KeyCode.Return: return "Enter";
                case KeyCode.Tab: return "Tab";
                default: return key.ToString();
            }
        }

        private void SaveAndNotifyAudio()
        {
            data.Sanitize();
            SaveToPrefs();
            OnAudioSettingsChanged?.Invoke();
            OnSettingsChanged?.Invoke();
        }

        private void SaveAndNotifyDisplay()
        {
            data.Sanitize();
            SaveToPrefs();
            ApplyDisplaySettings();
            OnSettingsChanged?.Invoke();
        }

        private void SaveAndNotifyGeneral()
        {
            data.Sanitize();
            SaveToPrefs();
            OnSettingsChanged?.Invoke();
        }

        private void LoadFromPrefs()
        {
            SettingsDataDTO loaded = SettingsDataDTO.CreateDefault();
            loaded.MasterVolume = PlayerPrefs.GetFloat(MasterVolKey, loaded.MasterVolume);
            loaded.BgmVolume = PlayerPrefs.GetFloat(BgmVolKey, loaded.BgmVolume);
            loaded.SfxVolume = PlayerPrefs.GetFloat(SfxVolKey, loaded.SfxVolume);
            loaded.MasterMuted = PlayerPrefs.GetInt(MasterMuteKey, 0) != 0;
            loaded.BgmMuted = PlayerPrefs.GetInt(BgmMuteKey, 0) != 0;
            loaded.SfxMuted = PlayerPrefs.GetInt(SfxMuteKey, 0) != 0;

            loaded.Fullscreen = PlayerPrefs.GetInt(FullscreenKey, 1) != 0;
            loaded.ResolutionIndex = PlayerPrefs.GetInt(ResIndexKey, loaded.ResolutionIndex);
            loaded.VSyncEnabled = PlayerPrefs.GetInt(VSyncKey, 1) != 0;
            loaded.TargetFps = PlayerPrefs.GetInt(TargetFpsKey, loaded.TargetFps);

            loaded.ScreenShakeEnabled = PlayerPrefs.GetInt(ShakeOnKey, 1) != 0;
            loaded.ScreenShakeIntensity = PlayerPrefs.GetFloat(ShakeScaleKey, loaded.ScreenShakeIntensity);
            loaded.ShowDamagePopups = PlayerPrefs.GetInt(DamagePopupsKey, 1) != 0;
            loaded.ShowHitSparks = PlayerPrefs.GetInt(HitSparksKey, 1) != 0;
            loaded.ShowControlsHint = PlayerPrefs.GetInt(ControlsHintKey, 1) != 0;

            loaded.DoubleTapThreshold = PlayerPrefs.GetFloat(DoubleTapKey, loaded.DoubleTapThreshold);
            loaded.AutoSwapOnConflict = PlayerPrefs.GetInt(AutoSwapKeysKey, 0) != 0;

            string csv = PlayerPrefs.GetString(KeyBindingsCsvKey, string.Empty);
            loaded.KeyBindings = KeyBindingMap.DeserializeFromCsv(csv);
            loaded.Sanitize();
            data = loaded;
        }

        private void SaveToPrefs()
        {
            PlayerPrefs.SetFloat(MasterVolKey, data.MasterVolume);
            PlayerPrefs.SetFloat(BgmVolKey, data.BgmVolume);
            PlayerPrefs.SetFloat(SfxVolKey, data.SfxVolume);
            PlayerPrefs.SetInt(MasterMuteKey, data.MasterMuted ? 1 : 0);
            PlayerPrefs.SetInt(BgmMuteKey, data.BgmMuted ? 1 : 0);
            PlayerPrefs.SetInt(SfxMuteKey, data.SfxMuted ? 1 : 0);

            PlayerPrefs.SetInt(FullscreenKey, data.Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(ResIndexKey, data.ResolutionIndex);
            PlayerPrefs.SetInt(VSyncKey, data.VSyncEnabled ? 1 : 0);
            PlayerPrefs.SetInt(TargetFpsKey, data.TargetFps);

            PlayerPrefs.SetInt(ShakeOnKey, data.ScreenShakeEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(ShakeScaleKey, data.ScreenShakeIntensity);
            PlayerPrefs.SetInt(DamagePopupsKey, data.ShowDamagePopups ? 1 : 0);
            PlayerPrefs.SetInt(HitSparksKey, data.ShowHitSparks ? 1 : 0);
            PlayerPrefs.SetInt(ControlsHintKey, data.ShowControlsHint ? 1 : 0);

            PlayerPrefs.SetFloat(DoubleTapKey, data.DoubleTapThreshold);
            PlayerPrefs.SetInt(AutoSwapKeysKey, data.AutoSwapOnConflict ? 1 : 0);
            PlayerPrefs.SetString(KeyBindingsCsvKey, data.KeyBindings.SerializeToCsv());
            PlayerPrefs.Save();
        }
    }
}
