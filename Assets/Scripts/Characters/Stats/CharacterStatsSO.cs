using UnityEngine;

namespace StickmanBeatEmUp.Characters.Stats
{
    /// <summary>
    /// ScriptableObject chứa chỉ số cấu hình của từng nhân vật
    /// Giúp Designer dễ dàng cân bằng chỉ số trực tiếp trong Unity Inspector mà không cần sửa code
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacterStats", menuName = "Stickman BeatEmUp/Character Stats")]
    public class CharacterStatsSO : ScriptableObject
    {
        [Header("Identity")]
        public string characterName = "Stickman";

        [Header("Health & Defense")]
        public float maxHealth = 100f;
        public float invulnerabilityDuration = 0.5f;

        [Header("Locomotion")]
        public float moveSpeed = 6f;
        public float jumpForce = 12.0f; // Nhảy cao ~1.83 units với gravityScale = 4.0 (từ -2.5 lên -0.67, qua đạn Y=-1.25 + 1 đầu)

        [Header("Dash Mechanic")]
        public float dashSpeed = 15f;
        public float dashDuration = 0.25f;
        public float dashCooldown = 0.5f;
        public bool isInvulnerableDuringDash = true;

        [Header("Normal Combat (Normal Form)")]
        public float normalAttackDamage = 2f;
        public Vector2 normalAttackKnockback = new Vector2(3f, 1f);
        public float skill1Damage = 3f;
        public Vector2 skill1Knockback = new Vector2(5f, 2f);
        public float skill2Damage = 10f;
        public Vector2 skill2Knockback = new Vector2(8f, 3f);
        public float dashAttackComboDamage = 6f;
        public Vector2 dashAttackKnockback = new Vector2(6f, 3f);

        [Header("Transformation Multipliers / Overrides")]
        public float transformBonusHealth = 0f;
        public float transformDamageMultiplier = 1.5f;
        public float transformSpeedMultiplier = 1.25f;
        public float transformedNormalAttackDamage = 4f;
        public float transformedSkill1Damage = 6f;
        public float transformedSkill2Damage = 15f;
        public float specialComboDamage = 35f;
        public Vector2 specialComboKnockback = new Vector2(12f, 5f);
    }
}
