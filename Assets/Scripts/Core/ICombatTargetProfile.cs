namespace StickmanBeatEmUp.Core
{
    /// <summary>
    /// Interface đa hình cung cấp phân loại mục tiêu chiến đấu (ví dụ Elite/Boss kích nổ Titan Boulder),
    /// thay thế chuỗi ép kiểu is/as trong Projectile.
    /// </summary>
    public interface ICombatTargetProfile
    {
        bool IsEliteOrBoss { get; }
    }
}
