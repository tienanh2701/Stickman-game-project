using System.Collections.Generic;
using NUnit.Framework;
using StickmanBeatEmUp.CorePure.Settings;

namespace StickmanBeatEmUp.Tests.CoreUnit.TV2
{
    [TestFixture]
    public class SettingsAndKeyBindingTests
    {
        private static int CountDistinctBindings(KeyBindingMap map)
        {
            HashSet<int> set = new HashSet<int>();
            for (int i = 0; i < KeyBindingMap.ActionCount; i++)
            {
                set.Add(map.GetKeyCode((GameInputAction)i));
            }
            return set.Count;
        }

        [Test]
        public void DefaultWasdPreset_Has12DistinctKeys_AndZeroDuplicates()
        {
            KeyBindingMap map = new KeyBindingMap();

            Assert.IsFalse(map.HasAnyDuplicate());
            Assert.AreEqual(12, CountDistinctBindings(map));
            Assert.AreEqual(97, map.GetKeyCode(GameInputAction.MoveLeft));
            Assert.AreEqual(100, map.GetKeyCode(GameInputAction.MoveRight));
            Assert.AreEqual(32, map.GetKeyCode(GameInputAction.Jump));
            Assert.AreEqual(106, map.GetKeyCode(GameInputAction.Attack));
            Assert.AreEqual(KeyBindingMap.EscapeKeyCode, map.GetKeyCode(GameInputAction.Pause));
        }

        [Test]
        public void ArrowKeysZxPreset_Has12DistinctKeys_AndZeroDuplicates()
        {
            KeyBindingMap map = new KeyBindingMap();
            map.ApplyPreset(ControlPresetId.ArrowKeysZx);

            Assert.IsFalse(map.HasAnyDuplicate());
            Assert.AreEqual(12, CountDistinctBindings(map));
            Assert.AreEqual(276, map.GetKeyCode(GameInputAction.MoveLeft));
            Assert.AreEqual(275, map.GetKeyCode(GameInputAction.MoveRight));
            Assert.AreEqual(122, map.GetKeyCode(GameInputAction.Attack));
            Assert.AreEqual(27, map.GetKeyCode(GameInputAction.Pause));
        }

        [Test]
        public void TrySetBinding_ValidFreeKey_AppliesAndMaintainsUniqueness()
        {
            KeyBindingMap map = new KeyBindingMap();
            const int freeKeyK = 107; // K is unused in DefaultWasd

            KeyBindChangeResult result = map.TrySetBinding(
                GameInputAction.Attack,
                freeKeyK,
                KeyBindConflictPolicy.Reject);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(KeyBindChangeStatus.Applied, result.Status);
            Assert.AreEqual(106, result.PreviousKeyCode);
            Assert.AreEqual(freeKeyK, map.GetKeyCode(GameInputAction.Attack));
            Assert.IsFalse(map.HasAnyDuplicate());
        }

        [Test]
        public void TrySetBinding_ConflictWithRejectPolicy_BlocksDuplicateAndPreservesBothKeys()
        {
            KeyBindingMap map = new KeyBindingMap();
            int attackBefore = map.GetKeyCode(GameInputAction.Attack); // J (106)
            int skill1Before = map.GetKeyCode(GameInputAction.Skill1); // U (117)

            KeyBindChangeResult result = map.TrySetBinding(
                GameInputAction.Attack,
                skill1Before,
                KeyBindConflictPolicy.Reject);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(KeyBindChangeStatus.ConflictRejected, result.Status);
            Assert.AreEqual(GameInputAction.Skill1, result.ConflictAction);
            Assert.AreEqual(attackBefore, map.GetKeyCode(GameInputAction.Attack));
            Assert.AreEqual(skill1Before, map.GetKeyCode(GameInputAction.Skill1));
            Assert.IsFalse(map.HasAnyDuplicate());
        }

        [Test]
        public void TrySetBinding_ConflictWithSwapPolicy_SwapsAtomicallyAndMaintainsZeroDuplicates()
        {
            KeyBindingMap map = new KeyBindingMap();
            int attackBefore = map.GetKeyCode(GameInputAction.Attack); // J (106)
            int skill1Before = map.GetKeyCode(GameInputAction.Skill1); // U (117)

            KeyBindChangeResult result = map.TrySetBinding(
                GameInputAction.Attack,
                skill1Before,
                KeyBindConflictPolicy.Swap);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(KeyBindChangeStatus.Swapped, result.Status);
            Assert.AreEqual(GameInputAction.Skill1, result.ConflictAction);
            Assert.AreEqual(skill1Before, map.GetKeyCode(GameInputAction.Attack));
            Assert.AreEqual(attackBefore, map.GetKeyCode(GameInputAction.Skill1));
            Assert.IsFalse(map.HasAnyDuplicate());
            Assert.AreEqual(12, CountDistinctBindings(map));
        }

        [Test]
        public void TrySetBinding_EscapeOnCombatAction_RejectedAsReservedKey()
        {
            KeyBindingMap map = new KeyBindingMap();
            int before = map.GetKeyCode(GameInputAction.Skill2);

            KeyBindChangeResult rejectResult = map.TrySetBinding(
                GameInputAction.Skill2,
                KeyBindingMap.EscapeKeyCode,
                KeyBindConflictPolicy.Reject);
            KeyBindChangeResult swapResult = map.TrySetBinding(
                GameInputAction.Skill2,
                KeyBindingMap.EscapeKeyCode,
                KeyBindConflictPolicy.Swap);

            Assert.IsFalse(rejectResult.IsSuccess);
            Assert.AreEqual(KeyBindChangeStatus.ReservedKeyRejected, rejectResult.Status);
            Assert.IsFalse(swapResult.IsSuccess);
            Assert.AreEqual(KeyBindChangeStatus.ReservedKeyRejected, swapResult.Status);
            Assert.AreEqual(before, map.GetKeyCode(GameInputAction.Skill2));
            Assert.AreEqual(KeyBindingMap.EscapeKeyCode, map.GetKeyCode(GameInputAction.Pause));
        }

        [Test]
        public void TrySetBinding_RebindingPauseAction_RejectedAsLocked()
        {
            KeyBindingMap map = new KeyBindingMap();

            KeyBindChangeResult result = map.TrySetBinding(
                GameInputAction.Pause,
                112, // P
                KeyBindConflictPolicy.Swap);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(KeyBindChangeStatus.ReservedActionLocked, result.Status);
            Assert.AreEqual(KeyBindingMap.EscapeKeyCode, map.GetKeyCode(GameInputAction.Pause));
        }

        [Test]
        public void TrySetBinding_UnsupportedKeyCode_Rejected()
        {
            KeyBindingMap map = new KeyBindingMap();
            int before = map.GetKeyCode(GameInputAction.Jump);

            KeyBindChangeResult result = map.TrySetBinding(
                GameInputAction.Jump,
                999999,
                KeyBindConflictPolicy.Reject);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(KeyBindChangeStatus.UnsupportedKeyRejected, result.Status);
            Assert.AreEqual(before, map.GetKeyCode(GameInputAction.Jump));
        }

        [Test]
        public void TrySetBinding_SameKeyAsCurrent_ReturnsUnchanged()
        {
            KeyBindingMap map = new KeyBindingMap();
            int attackKey = map.GetKeyCode(GameInputAction.Attack);

            KeyBindChangeResult result = map.TrySetBinding(
                GameInputAction.Attack,
                attackKey,
                KeyBindConflictPolicy.Reject);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(KeyBindChangeStatus.Unchanged, result.Status);
            Assert.AreEqual(attackKey, map.GetKeyCode(GameInputAction.Attack));
        }

        [Test]
        public void DeserializeFromCsv_CorruptedDuplicateKeys_AutoSanitizesTo12UniqueKeys()
        {
            // All combat keys corrupted to 'J' (106), Pause corrupted to 100, plus invalid -99
            string corruptedCsv = "106,106,106,106,106,106,-99,27,106,106,106,100";

            KeyBindingMap map = KeyBindingMap.DeserializeFromCsv(corruptedCsv);

            Assert.IsFalse(map.HasAnyDuplicate());
            Assert.AreEqual(12, CountDistinctBindings(map));
            Assert.AreEqual(KeyBindingMap.EscapeKeyCode, map.GetKeyCode(GameInputAction.Pause));
            for (int i = 0; i < KeyBindingMap.ActionCount; i++)
            {
                Assert.IsTrue(KeyBindingMap.IsSupportedKeyCode(map.GetKeyCode((GameInputAction)i)));
            }
        }

        [Test]
        public void DetectPreset_RecognizesDefault_ArrowZx_AndCustomAfterRebind()
        {
            KeyBindingMap map = new KeyBindingMap();
            Assert.AreEqual(ControlPresetId.DefaultWasd, map.DetectPreset());

            map.ApplyPreset(ControlPresetId.ArrowKeysZx);
            Assert.AreEqual(ControlPresetId.ArrowKeysZx, map.DetectPreset());

            KeyBindChangeResult res = map.TrySetBinding(GameInputAction.Attack, 108, KeyBindConflictPolicy.Reject); // L
            Assert.IsTrue(res.IsSuccess);
            Assert.AreEqual(ControlPresetId.Custom, map.DetectPreset());
        }

        [Test]
        public void SettingsDataDTO_AudioMuteAndClamp_ComputesExactEffectiveVolumes()
        {
            SettingsDataDTO dto = SettingsDataDTO.CreateDefault();
            dto.MasterVolume = 1.8f; // Clamps to 1f
            dto.BgmVolume = 0.5f;
            dto.SfxVolume = -0.3f;   // Clamps to 0f
            dto.Sanitize();

            Assert.AreEqual(1f, dto.MasterVolume, 0.0001f);
            Assert.AreEqual(0f, dto.SfxVolume, 0.0001f);
            Assert.AreEqual(0.5f, dto.EffectiveBgmVolume, 0.0001f);

            dto.MasterMuted = true;
            Assert.AreEqual(0f, dto.EffectiveBgmVolume, 0.0001f);
            Assert.AreEqual(0f, dto.EffectiveSfxVolume, 0.0001f);

            dto.MasterMuted = false;
            dto.BgmMuted = true;
            dto.SfxVolume = 0.8f;
            dto.Sanitize();
            Assert.AreEqual(0f, dto.EffectiveBgmVolume, 0.0001f);
            Assert.AreEqual(0.8f, dto.EffectiveSfxVolume, 0.0001f);
        }

        [Test]
        public void SettingsDataDTO_DoubleTapAndShakeIntensity_ClampedAndGatedCorrectly()
        {
            SettingsDataDTO dto = SettingsDataDTO.CreateDefault();
            dto.DoubleTapThreshold = 0.01f;
            dto.ScreenShakeIntensity = 2.5f;
            dto.ScreenShakeEnabled = true;
            dto.Sanitize();

            Assert.AreEqual(SettingsDataDTO.MinDoubleTapThreshold, dto.DoubleTapThreshold, 0.0001f);
            Assert.AreEqual(1f, dto.EffectiveScreenShakeScale, 0.0001f);

            dto.DoubleTapThreshold = 5.0f;
            dto.ScreenShakeEnabled = false;
            dto.Sanitize();

            Assert.AreEqual(SettingsDataDTO.MaxDoubleTapThreshold, dto.DoubleTapThreshold, 0.0001f);
            Assert.AreEqual(0f, dto.EffectiveScreenShakeScale, 0.0001f);
        }

        [Test]
        public void SettingsDataDTO_CreateDefault_HasExpectedSafeDefaults()
        {
            SettingsDataDTO dto = SettingsDataDTO.CreateDefault();

            Assert.AreEqual(1f, dto.MasterVolume, 0.0001f);
            Assert.AreEqual(0.70f, dto.BgmVolume, 0.0001f);
            Assert.AreEqual(1f, dto.SfxVolume, 0.0001f);
            Assert.IsFalse(dto.MasterMuted);
            Assert.IsFalse(dto.BgmMuted);
            Assert.IsFalse(dto.SfxMuted);
            Assert.IsTrue(dto.Fullscreen);
            Assert.AreEqual(0, dto.ResolutionIndex);
            Assert.IsTrue(dto.VSyncEnabled);
            Assert.AreEqual(60, dto.TargetFps);
            Assert.IsTrue(dto.ScreenShakeEnabled);
            Assert.IsTrue(dto.ShowDamagePopups);
            Assert.IsTrue(dto.ShowHitSparks);
            Assert.IsTrue(dto.ShowControlsHint);
            Assert.AreEqual(SettingsDataDTO.DefaultDoubleTapThreshold, dto.DoubleTapThreshold, 0.0001f);
            Assert.AreEqual(ControlPresetId.DefaultWasd, dto.ControlPreset);
            Assert.IsFalse(dto.AutoSwapOnConflict);
            Assert.IsFalse(dto.KeyBindings.HasAnyDuplicate());
        }
    }
}
