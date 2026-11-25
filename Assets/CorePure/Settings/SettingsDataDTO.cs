using System;

namespace StickmanBeatEmUp.CorePure.Settings
{
    /// <summary>
    /// Pure C# container for audio, display, gameplay/HUD, and control settings.
    /// </summary>
    [Serializable]
    public sealed class SettingsDataDTO
    {
        public const float MinDoubleTapThreshold = 0.15f;
        public const float MaxDoubleTapThreshold = 0.40f;
        public const float DefaultDoubleTapThreshold = 0.25f;

        // Audio
        public float MasterVolume = 1f;
        public float BgmVolume = 0.70f;
        public float SfxVolume = 1f;
        public bool MasterMuted = false;
        public bool BgmMuted = false;
        public bool SfxMuted = false;

        // Display & Performance
        public bool Fullscreen = true;
        public int ResolutionIndex = 0; // 0 = 1920x1080, 1 = 1600x900, 2 = 1280x720
        public bool VSyncEnabled = true;
        public int TargetFps = 60; // 60, 120, or -1 (Unlimited)

        // Gameplay & HUD
        public bool ScreenShakeEnabled = true;
        public float ScreenShakeIntensity = 1f;
        public bool ShowDamagePopups = true;
        public bool ShowHitSparks = true;
        public bool ShowControlsHint = true;

        // Controls
        public float DoubleTapThreshold = DefaultDoubleTapThreshold;
        public ControlPresetId ControlPreset = ControlPresetId.DefaultWasd;
        public bool AutoSwapOnConflict = false;
        public KeyBindingMap KeyBindings = new KeyBindingMap();

        public float EffectiveBgmVolume =>
            (MasterMuted || BgmMuted) ? 0f : Clamp01(MasterVolume) * Clamp01(BgmVolume);

        public float EffectiveSfxVolume =>
            (MasterMuted || SfxMuted) ? 0f : Clamp01(MasterVolume) * Clamp01(SfxVolume);

        public float EffectiveScreenShakeScale =>
            ScreenShakeEnabled ? Clamp01(ScreenShakeIntensity) : 0f;

        public static SettingsDataDTO CreateDefault()
        {
            SettingsDataDTO dto = new SettingsDataDTO();
            dto.Sanitize();
            return dto;
        }

        public void Sanitize()
        {
            MasterVolume = Clamp01(MasterVolume);
            BgmVolume = Clamp01(BgmVolume);
            SfxVolume = Clamp01(SfxVolume);

            if (ResolutionIndex < 0) ResolutionIndex = 0;
            if (ResolutionIndex > 2) ResolutionIndex = 2;

            if (TargetFps != 60 && TargetFps != 120 && TargetFps != -1)
            {
                TargetFps = 60;
            }

            ScreenShakeIntensity = Clamp01(ScreenShakeIntensity);

            if (float.IsNaN(DoubleTapThreshold) || float.IsInfinity(DoubleTapThreshold))
            {
                DoubleTapThreshold = DefaultDoubleTapThreshold;
            }
            if (DoubleTapThreshold < MinDoubleTapThreshold) DoubleTapThreshold = MinDoubleTapThreshold;
            if (DoubleTapThreshold > MaxDoubleTapThreshold) DoubleTapThreshold = MaxDoubleTapThreshold;

            if (KeyBindings == null)
            {
                KeyBindings = new KeyBindingMap();
            }
            else
            {
                KeyBindings.ValidateAndSanitize();
            }

            ControlPreset = KeyBindings.DetectPreset();
        }

        public static void GetResolutionSize(int index, out int width, out int height)
        {
            switch (index)
            {
                case 1:
                    width = 1600;
                    height = 900;
                    break;
                case 2:
                    width = 1280;
                    height = 720;
                    break;
                default:
                    width = 1920;
                    height = 1080;
                    break;
            }
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
