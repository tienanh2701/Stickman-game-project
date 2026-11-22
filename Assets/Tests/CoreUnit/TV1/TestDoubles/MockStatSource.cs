using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.Tests.CoreUnit.TV1.TestDoubles
{
    /// <summary>
    /// Mock IStatSource thuần C# dùng để kiểm thử tương tác chỉ số và đếm lượt truy vấn từ DamageCalculator.
    /// </summary>
    public sealed class MockStatSource : IStatSource
    {
        private float attackDamageMultiplier = 1f;
        private float moveSpeedMultiplier = 1f;
        private float maxShieldCapacity = 0f;
        private float damageReflectAmount = 0f;
        private float ragePerSecondBonus = 0f;

        public int AttackMultiplierReadCount { get; private set; }
        public int MoveSpeedReadCount { get; private set; }
        public int MaxShieldReadCount { get; private set; }
        public int DamageReflectReadCount { get; private set; }
        public int RagePerSecondReadCount { get; private set; }

        public float AttackDamageMultiplier
        {
            get
            {
                AttackMultiplierReadCount++;
                return attackDamageMultiplier;
            }
            set => attackDamageMultiplier = value;
        }

        public float MoveSpeedMultiplier
        {
            get
            {
                MoveSpeedReadCount++;
                return moveSpeedMultiplier;
            }
            set => moveSpeedMultiplier = value;
        }

        public float MaxShieldCapacity
        {
            get
            {
                MaxShieldReadCount++;
                return maxShieldCapacity;
            }
            set => maxShieldCapacity = value;
        }

        public float DamageReflectAmount
        {
            get
            {
                DamageReflectReadCount++;
                return damageReflectAmount;
            }
            set => damageReflectAmount = value;
        }

        public float RagePerSecondBonus
        {
            get
            {
                RagePerSecondReadCount++;
                return ragePerSecondBonus;
            }
            set => ragePerSecondBonus = value;
        }
    }
}
