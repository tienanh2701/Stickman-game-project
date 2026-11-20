using StickmanBeatEmUp.CorePure.Entities.DTOs;

namespace StickmanBeatEmUp.CorePure.Interfaces
{
    /// <summary>
    /// Hợp đồng GoF Strategy cho hành vi AI kẻ thù/Boss, cho phép hoán đổi chiến thuật tại runtime (OCP).
    /// </summary>
    public interface IAIStrategy
    {
        string StrategyId { get; }

        AIActionDecision ComputeAction(EnemyAIContext context);
    }
}
