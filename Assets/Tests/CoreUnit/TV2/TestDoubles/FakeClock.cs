using StickmanBeatEmUp.CorePure.Rules.Persistence;

namespace StickmanBeatEmUp.Tests.CoreUnit.TV2.TestDoubles
{
    /// <summary>
    /// Stub/Fake đồng hồ cố định thời gian để kiểm thử xác định (deterministic) không phụ thuộc đồng hồ hệ thống.
    /// </summary>
    public sealed class FakeClock : IGameClock
    {
        public string UtcNowIso { get; set; }
        public float ElapsedSeconds { get; set; }

        public FakeClock(string utcNowIso = "2026-11-22T10:00:00Z", float elapsedSeconds = 42.5f)
        {
            UtcNowIso = utcNowIso;
            ElapsedSeconds = elapsedSeconds;
        }

        public void Advance(float seconds)
        {
            ElapsedSeconds += seconds;
        }
    }
}
