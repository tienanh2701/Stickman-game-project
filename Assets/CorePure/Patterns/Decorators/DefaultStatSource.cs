using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Patterns.Decorators
{
    /// <summary>
    /// Nguồn chỉ số nền mặc định của nhân vật khi chưa có buff/decorator nào được kích hoạt.
    /// Đóng vai trò ConcreteComponent gốc trên nhánh TV2 (tương thích DRY với BaseStatSource.Default bên nhánh TV1).
    /// </summary>
    public sealed class DefaultStatSource : IStatSource
    {
        public static DefaultStatSource Default { get; } = new DefaultStatSource();

        public float AttackDamageMultiplier => 1f;
        public float MoveSpeedMultiplier => 1f;
        public float MaxShieldCapacity => 0f;
        public float DamageReflectAmount => 0f;
        public float RagePerSecondBonus => 0f;
    }
}
