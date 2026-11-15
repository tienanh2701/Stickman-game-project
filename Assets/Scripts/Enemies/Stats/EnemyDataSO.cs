using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Enemies.Stats
{
    /// <summary>
    /// Modular ScriptableObject defining stats, World exclusivity, and role tier (Creep / Elite / Boss).
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemyData", menuName = "Stickman BeatEmUp/Enemy Data SO")]
    public class EnemyDataSO : ScriptableObject
    {
        [Header("Identity & World")]
        public string enemyName = "Creep";
        public WorldType worldTheme = WorldType.World1_RuinedCity;
        public bool isElite = false;
        public bool isBoss = false;

        [Header("Core Vitals")]
        public float maxHealth = 60f;
        public float moveSpeed = 3.5f;

        [Header("Combat")]
        public float attackDamage = 10f;
        public float heavyAttackDamage = 20f;
        public float attackRange = 1.4f;
        public float attackCooldown = 1.5f;
        public float telegraphDuration = 0.5f;
        public Vector2 knockbackForce = new Vector2(3.5f, 1.5f);

        [Header("Defense & Stagger")]
        [Tooltip("True for Elites/Bosses: light NormalAttack_J does not interrupt their action.")]
        public bool hasSuperArmor = false;
        public float staggerDuration = 0.4f;

        [Header("Ranged Behavior (Optional)")]
        public float preferredRange = 6.5f;
        public float fleeDistance = 2.6f;
        public float projectileSpeed = 11f;
    }
}
