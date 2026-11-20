namespace StickmanBeatEmUp.CorePure.Interfaces
{
    /// <summary>
    /// Hợp đồng dịch vụ âm thanh (DIP) tách logic chiến đấu/UI khỏi AudioSource thực tế của Unity.
    /// </summary>
    public interface IAudioService
    {
        float MasterVolume { get; }
        bool IsMuted { get; }

        void PlayBgm(string trackId);

        void StopBgm();

        void PlaySfx(string sfxId);

        void SetMasterVolume(float volume);

        void SetMuted(bool muted);
    }
}
