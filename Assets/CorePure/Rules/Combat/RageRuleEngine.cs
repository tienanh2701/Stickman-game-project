using System;
using StickmanBeatEmUp.CorePure.Entities.Combat;
using StickmanBeatEmUp.CorePure.Exceptions;

namespace StickmanBeatEmUp.CorePure.Rules.Combat
{
    /// <summary>
    /// Động cơ quy tắc Tích Nộ (Rage System) thuần C# (0 phụ thuộc UnityEngine).
    /// Bảo toàn bất biến miền: 0 <= CurrentRageFloat <= MAX_RAGE (30), chia 3 mốc x10.
    /// </summary>
    public sealed class RageRuleEngine
    {
        public const int MAX_RAGE = 30;
        public const int RAGE_PER_SEGMENT = 10;
        public const int MAX_SEGMENTS = MAX_RAGE / RAGE_PER_SEGMENT;
        public const int SKILL_I_COST = 10;
        public const int TRANSFORM_COST = 30;
        public const int SPECIAL_COMBO_COST = 30;
        public const float RAGE_PER_HIT = 1.5f;
        public const float RAGE_PER_KILL = 2.0f;

        private float currentRageExact;

        public event Action<int, int> OnRageChanged;
        public event Action<int> OnSegmentFilled;

        public int CurrentRage => (int)Math.Floor(currentRageExact);
        public float CurrentRageFloat => currentRageExact;
        public bool IsMaxRage => currentRageExact >= MAX_RAGE;
        public int FilledSegments => CurrentRage / RAGE_PER_SEGMENT;

        public RageRuleEngine(float initialRage = 0f)
        {
            currentRageExact = ClampRage(initialRage);
        }

        public static bool CanGainRageFromHit(CombatAttackKind attackKind)
        {
            return attackKind == CombatAttackKind.NormalAttackJ ||
                   attackKind == CombatAttackKind.SkillU ||
                   attackKind == CombatAttackKind.ComboDashJ;
        }

        public float AddRageFromHit(CombatAttackKind attackKind)
        {
            return AddRageFromHit(attackKind, RAGE_PER_HIT);
        }

        public float AddRageFromHit(CombatAttackKind attackKind, float hitRageAmount)
        {
            if (!CanGainRageFromHit(attackKind))
            {
                return 0f;
            }

            return AddRageFloat(hitRageAmount);
        }

        public float AddRageFromKill()
        {
            return AddRageFloat(RAGE_PER_KILL);
        }

        public float AddRageFloat(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f || currentRageExact >= MAX_RAGE)
            {
                return 0f;
            }

            int prevSegments = FilledSegments;
            float prevExact = currentRageExact;

            currentRageExact = ClampRage(currentRageExact + amount);

            OnRageChanged?.Invoke(CurrentRage, MAX_RAGE);

            int newSegments = FilledSegments;
            if (newSegments > prevSegments)
            {
                for (int i = prevSegments + 1; i <= newSegments; i++)
                {
                    OnSegmentFilled?.Invoke(i);
                }
            }

            return currentRageExact - prevExact;
        }

        public float AddRage(int amount)
        {
            return AddRageFloat(amount);
        }

        public bool CanUseSkillI()
        {
            return CurrentRage >= SKILL_I_COST;
        }

        public bool CanTransform()
        {
            return CurrentRage >= TRANSFORM_COST;
        }

        public bool CanUseSpecialCombo(bool isTransformed)
        {
            return isTransformed && CurrentRage >= SPECIAL_COMBO_COST;
        }

        public bool TryConsumeRage(int amount)
        {
            if (amount < 0)
            {
                return false;
            }

            if (currentRageExact >= amount)
            {
                currentRageExact = ClampRage(currentRageExact - amount);
                OnRageChanged?.Invoke(CurrentRage, MAX_RAGE);
                return true;
            }

            return false;
        }

        public void ConsumeRageOrThrow(int amount, string currentStateName = "")
        {
            if (amount < 0)
            {
                throw new InvalidCombatStateException(
                    currentStateName,
                    $"Rage cost cannot be negative ({amount}).");
            }

            if (!TryConsumeRage(amount))
            {
                throw new InvalidCombatStateException(
                    currentStateName,
                    $"Insufficient rage: required {amount}, current {CurrentRage}/{MAX_RAGE}.");
            }
        }

        public void FillMaxRage()
        {
            int prevSegments = FilledSegments;
            currentRageExact = MAX_RAGE;
            OnRageChanged?.Invoke(CurrentRage, MAX_RAGE);

            for (int i = prevSegments + 1; i <= MAX_SEGMENTS; i++)
            {
                OnSegmentFilled?.Invoke(i);
            }
        }

        public void ResetRage()
        {
            currentRageExact = 0f;
            OnRageChanged?.Invoke(0, MAX_RAGE);
        }

        public void SetRageExact(float value, bool notify = true)
        {
            int prevSegments = FilledSegments;
            currentRageExact = ClampRage(value);

            if (!notify)
            {
                return;
            }

            OnRageChanged?.Invoke(CurrentRage, MAX_RAGE);

            int newSegments = FilledSegments;
            if (newSegments > prevSegments)
            {
                for (int i = prevSegments + 1; i <= newSegments; i++)
                {
                    OnSegmentFilled?.Invoke(i);
                }
            }
        }

        private static float ClampRage(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return 0f;
            }

            if (value >= MAX_RAGE)
            {
                return MAX_RAGE;
            }

            return value;
        }
    }
}
