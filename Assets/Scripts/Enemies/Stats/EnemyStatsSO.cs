using UnityEngine;

namespace StickmanBeatEmUp.Enemies.Stats
{
    /// <summary>
    /// ScriptableObject chứa chỉ số quái vật và Boss
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemyStats", menuName = "Stickman BeatEmUp/Enemy Stats")]
    public class EnemyStatsSO : ScriptableObject
    {
        [Header("Identity & HP")]
        public string enemyName = "Enemy";
        public float maxHealth = 50f;
        public float moveSpeed = 3.5f;

        [Header("Combat Settings")]
        public float attackDamage = 10f;
        public float attackRange = 1.2f;
        public float attackCooldown = 1.5f;
        public float telegraphDuration = 0.5f; // Thời gian nhấp nháy đỏ cảnh báo trước khi ra đòn
        public Vector2 knockbackOnAttack = new Vector2(3f, 1f);

        [Header("Super Armor & Stagger")]
        [Tooltip("Nếu true: Đòn đánh thường J không làm khựng (stagger), chỉ có U, I, Combo mới làm khựng")]
        public bool hasSuperArmor = false;
        public float staggerDuration = 0.4f;

        [Header("Ranger Specific (Optional)")]
        public float preferredDistance = 6f;
        public float fleeDistance = 3f;

        [Header("Kamikaze Specific (Optional)")]
        public float explosionRadius = 3.5f;
        public float explosionCountdown = 1.5f;
    }
}
