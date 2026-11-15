using UnityEngine;

namespace StickmanBeatEmUp.Core
{
    /// <summary>
    /// Gói dữ liệu truyền tải sát thương, lực đẩy và thuộc tính đòn đánh
    /// Tuân thủ nguyên lý đóng gói (Encapsulation) trong OOP
    /// </summary>
    [System.Serializable]
    public struct DamageData
    {
        public float damageAmount;
        public Vector2 knockbackForce;
        public AttackType attackType;
        public bool breaksSuperArmor;
        public GameObject attacker;

        public DamageData(float damage, Vector2 knockback, AttackType type, bool breaksArmor, GameObject source)
        {
            damageAmount = damage;
            knockbackForce = knockback;
            attackType = type;
            breaksSuperArmor = breaksArmor;
            attacker = source;
        }
    }
}
