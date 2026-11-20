namespace StickmanBeatEmUp.CorePure.Interfaces
{
    /// <summary>
    /// Hợp đồng GoF Decorator cho phép TV2 cộng dồn buff/hiệu ứng chỉ số lên nhân vật TV1 mà không sửa BaseCharacter.
    /// </summary>
    public interface IStatSource
    {
        float AttackDamageMultiplier { get; }
        float MoveSpeedMultiplier { get; }
        float MaxShieldCapacity { get; }
        float DamageReflectAmount { get; }
        float RagePerSecondBonus { get; }
    }
}
