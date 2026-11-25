using System;
using System.Collections.Generic;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.Tests.CoreUnit.TV1.TestDoubles
{
    /// <summary>
    /// Spy triển khai IAudioService thuần C# ghi lại mọi yêu cầu phát BGM/SFX và thay đổi âm lượng.
    /// Hoàn thiện đủ bộ 3 Test Doubles của TV1: StubInputProvider, MockStatSource và SpyAudioService.
    /// </summary>
    public sealed class SpyAudioService : IAudioService
    {
        private readonly List<string> playedBgmTracks = new List<string>();
        private readonly List<string> playedSfxIds = new List<string>();

        public float MasterVolume { get; private set; } = 1f;
        public bool IsMuted { get; private set; }
        public int StopBgmCallCount { get; private set; }
        public IReadOnlyList<string> PlayedBgmTracks => playedBgmTracks;
        public IReadOnlyList<string> PlayedSfxIds => playedSfxIds;
        public string LastBgmTrack => playedBgmTracks.Count > 0 ? playedBgmTracks[playedBgmTracks.Count - 1] : string.Empty;
        public string LastSfxId => playedSfxIds.Count > 0 ? playedSfxIds[playedSfxIds.Count - 1] : string.Empty;

        public void PlayBgm(string trackId)
        {
            if (!string.IsNullOrWhiteSpace(trackId))
            {
                playedBgmTracks.Add(trackId.Trim());
            }
        }

        public void StopBgm()
        {
            StopBgmCallCount++;
        }

        public void PlaySfx(string sfxId)
        {
            if (!string.IsNullOrWhiteSpace(sfxId))
            {
                playedSfxIds.Add(sfxId.Trim());
            }
        }

        public void SetMasterVolume(float volume)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume))
            {
                return;
            }

            MasterVolume = Math.Max(0f, Math.Min(1f, volume));
        }

        public void SetMuted(bool muted)
        {
            IsMuted = muted;
        }

        public void Reset()
        {
            playedBgmTracks.Clear();
            playedSfxIds.Clear();
            StopBgmCallCount = 0;
            MasterVolume = 1f;
            IsMuted = false;
        }
    }
}
