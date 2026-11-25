using System;
using UnityEngine;

namespace StickmanBeatEmUp.Audio
{
    [Serializable]
    public class NamedAudioClip
    {
        public string id;
        public AudioClip clip;
    }

    /// <summary>
    /// Kho ánh xạ trackId/sfxId -> AudioClip đặt tại Assets/Resources/Audio/AudioLibrarySO.asset
    /// để Unity đóng gói sạch toàn bộ tệp Assets/Audio/{BGM,SFX}/ vào bản build độc lập.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLibrarySO", menuName = "Stickman BeatEmUp/Audio Library SO")]
    public class AudioLibrarySO : ScriptableObject
    {
        [Header("Background Music Tracks")]
        public NamedAudioClip[] bgmTracks;

        [Header("Sound Effects (SFX)")]
        public NamedAudioClip[] sfxClips;

        public AudioClip FindBgm(string trackId)
        {
            return FindClip(bgmTracks, trackId);
        }

        public AudioClip FindSfx(string sfxId)
        {
            return FindClip(sfxClips, sfxId);
        }

        private static AudioClip FindClip(NamedAudioClip[] entries, string id)
        {
            if (entries == null || string.IsNullOrWhiteSpace(id)) return null;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null &&
                    string.Equals(entries[i].id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return entries[i].clip;
                }
            }
            return null;
        }
    }
}
