using System;

namespace StickmanBeatEmUp.CorePure.Entities.DTOs
{
    /// <summary>
    /// Bản thiết kế chỉ số và chiến lược AI của một kẻ địch do IEnemyFactory tạo ra từ cấu hình ngoài.
    /// </summary>
    [Serializable]
    public class EnemyBlueprint
    {
        public string enemyId;
        public int worldIndex;
        public string strategyId;
        public float maxHealth;
        public float attackDamage;
        public float heavyAttackDamage;
        public float moveSpeed;
        public float attackCooldown;
        public bool hasSuperArmor;
        public bool isBoss;
        public float healthMultiplier = 1f;
        public float damageMultiplier = 1f;
    }
}
