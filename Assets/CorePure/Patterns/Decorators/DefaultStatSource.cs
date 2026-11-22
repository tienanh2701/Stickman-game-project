using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Patterns.Decorators
{
    /// <summary>
    /// Nguồn chỉ số nền mặc định của nhân vật khi chưa có buff/decorator nào được kích hoạt.
    /// </summary>
    public sealed class DefaultStatSource : IStatSource
    {
        public float AttackDamageMultiplier => 1f;
        public float MoveSpeedMultiplier => 1f;
        public float MaxShieldCapacity => 0f;
        public float DamageReflectAmount => 0f;
        public float RagePerSecondBonus => 0f;
    }
}
