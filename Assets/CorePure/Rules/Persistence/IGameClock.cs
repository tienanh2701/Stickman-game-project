using System;

namespace StickmanBeatEmUp.CorePure.Rules.Persistence
{
    /// <summary>
    /// Hợp đồng đồng hồ trừu tượng (DIP) cho phép bơm FakeClock vào Unit Tests xác định (deterministic).
    /// </summary>
    public interface IGameClock
    {
        string UtcNowIso { get; }
        float ElapsedSeconds { get; }
    }

    public sealed class SystemGameClock : IGameClock
    {
        private readonly DateTime startedUtc;

        public SystemGameClock()
        {
            startedUtc = DateTime.UtcNow;
        }

        public string UtcNowIso => DateTime.UtcNow.ToString("o");
        public float ElapsedSeconds => (float)Math.Max(0.0, (DateTime.UtcNow - startedUtc).TotalSeconds);
    }
}
