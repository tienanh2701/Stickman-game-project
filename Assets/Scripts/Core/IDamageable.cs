using UnityEngine;

namespace StickmanBeatEmUp.Core
{
    /// <summary>
    /// Interface dành cho tất cả đối tượng có thể nhận sát thương (Player, Quái, Boss, Vật thể nổ)
    /// Đảm bảo tính Đa hình (Polymorphism) và Loose Coupling
    /// </summary>
    public interface IDamageable
    {
        Transform Transform { get; }
        bool IsDead { get; }
        void TakeDamage(DamageData damageData);
    }
}
