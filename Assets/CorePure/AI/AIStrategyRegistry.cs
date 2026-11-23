using System;
using System.Collections.Generic;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.AI
{
    /// <summary>
    /// Open/Closed (OCP) registry mapping strategy IDs (and optional per-difficulty overrides)
    /// to IAIStrategy implementations without modifying any existing strategy or enemy class.
    /// </summary>
    public sealed class AIStrategyRegistry
    {
        private readonly Dictionary<string, IAIStrategy> strategies =
            new Dictionary<string, IAIStrategy>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, IAIStrategy> difficultyOverrides =
            new Dictionary<string, IAIStrategy>(StringComparer.OrdinalIgnoreCase);

        public static AIStrategyRegistry Default { get; } = CreateDefault();

        public int Count => strategies.Count;

        public IReadOnlyCollection<string> RegisteredIds => strategies.Keys;

        public static AIStrategyRegistry CreateDefault()
        {
            AIStrategyRegistry registry = new AIStrategyRegistry();
            registry.Register(new MeleeChaseStrategy());
            registry.Register(new RangedKiteStrategy());
            registry.Register(new KamikazeRushStrategy());
            registry.Register(new BossPhaseStrategy());
            return registry;
        }

        public void Register(IAIStrategy strategy)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            if (string.IsNullOrWhiteSpace(strategy.StrategyId))
            {
                throw new ArgumentException("StrategyId must not be null or whitespace.", nameof(strategy));
            }

            strategies[strategy.StrategyId.Trim()] = strategy;
        }

        public void Register(string strategyKey, IAIStrategy strategy)
        {
            if (string.IsNullOrWhiteSpace(strategyKey))
            {
                throw new ArgumentException("strategyKey must not be null or whitespace.", nameof(strategyKey));
            }

            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            strategies[strategyKey.Trim()] = strategy;
        }

        public void RegisterForDifficulty(string strategyId, AIDifficultyPreset difficulty, IAIStrategy strategy)
        {
            if (string.IsNullOrWhiteSpace(strategyId))
            {
                throw new ArgumentException("strategyId must not be null or whitespace.", nameof(strategyId));
            }

            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            string compositeKey = BuildDifficultyKey(strategyId, (int)difficulty);
            difficultyOverrides[compositeKey] = strategy;
        }

        public bool Contains(string strategyId)
        {
            if (string.IsNullOrWhiteSpace(strategyId))
            {
                return false;
            }

            return strategies.ContainsKey(strategyId.Trim());
        }

        public bool TryResolve(string strategyId, out IAIStrategy strategy)
        {
            if (string.IsNullOrWhiteSpace(strategyId))
            {
                strategy = null;
                return false;
            }

            return strategies.TryGetValue(strategyId.Trim(), out strategy);
        }

        public bool TryResolve(string strategyId, int difficultyLevel, out IAIStrategy strategy)
        {
            if (string.IsNullOrWhiteSpace(strategyId))
            {
                strategy = null;
                return false;
            }

            string compositeKey = BuildDifficultyKey(strategyId, difficultyLevel);
            if (difficultyOverrides.TryGetValue(compositeKey, out strategy))
            {
                return true;
            }

            return strategies.TryGetValue(strategyId.Trim(), out strategy);
        }

        public IAIStrategy Resolve(string strategyId)
        {
            if (TryResolve(strategyId, out IAIStrategy found))
            {
                return found;
            }

            return ResolveFallback();
        }

        public IAIStrategy Resolve(string strategyId, int difficultyLevel)
        {
            if (TryResolve(strategyId, difficultyLevel, out IAIStrategy found))
            {
                return found;
            }

            return ResolveFallback();
        }

        public IAIStrategy Resolve(string strategyId, AIDifficultyPreset difficulty)
        {
            return Resolve(strategyId, (int)difficulty);
        }

        private IAIStrategy ResolveFallback()
        {
            if (strategies.TryGetValue(MeleeChaseStrategy.ID, out IAIStrategy fallback))
            {
                return fallback;
            }

            return new MeleeChaseStrategy();
        }

        private static string BuildDifficultyKey(string strategyId, int difficultyLevel)
        {
            int clamped = AIDifficultyTuning.ClampDifficulty(difficultyLevel);
            return strategyId.Trim() + "#" + clamped.ToString();
        }
    }
}
