using System;
using UnityEngine;
using StickmanBeatEmUp.CorePure.Entities.Combat;
using StickmanBeatEmUp.CorePure.Rules.Combat;

namespace StickmanBeatEmUp.Core
{
    /// <summary>
    /// Unity Adapter mỏng quản lý cơ chế Tích Nộ (Rage System), ủy quyền toàn bộ luật sang RageRuleEngine (CorePure):
    /// - Tối đa 30 điểm (3 mốc, mỗi mốc 10 điểm)
    /// - Đánh trúng bằng J, U hoặc Dash+J: +1.5 điểm nộ (hoặc theo cấu hình nhân vật)
    /// - Kỹ năng U: 0 nộ | Kỹ năng I: 10 nộ | Biến hình: 30 nộ | Combo Đặc Biệt (Dash + I): Biến hình & 30 nộ
    /// </summary>
    public class RageManager : MonoBehaviour
    {
        public const int MAX_RAGE = RageRuleEngine.MAX_RAGE;
        public const int RAGE_PER_SEGMENT = RageRuleEngine.RAGE_PER_SEGMENT;
        public const int SKILL_I_COST = RageRuleEngine.SKILL_I_COST;
        public const int TRANSFORM_COST = RageRuleEngine.TRANSFORM_COST;
        public const int SPECIAL_COMBO_COST = RageRuleEngine.SPECIAL_COMBO_COST;
        public const float RAGE_PER_HIT = RageRuleEngine.RAGE_PER_HIT;
        public const float RAGE_PER_KILL = RageRuleEngine.RAGE_PER_KILL;

        [Header("Runtime State")]
        [SerializeField] private int currentRage = 0;

        private RageRuleEngine ruleEngine;

        // Sự kiện thông báo thay đổi Nộ cho UI và FX
        public event Action<int, int> OnRageChanged; // (current, max)
        public event Action<int> OnSegmentFilled;     // (segmentIndex: 1, 2, 3)

        public RageRuleEngine RuleEngine => EnsureRuleEngine();
        public int CurrentRage => EnsureRuleEngine().CurrentRage;
        public float CurrentRageFloat => EnsureRuleEngine().CurrentRageFloat;
        public bool IsMaxRage => EnsureRuleEngine().IsMaxRage;
        public int FilledSegments => EnsureRuleEngine().FilledSegments;

        private RageRuleEngine EnsureRuleEngine()
        {
            if (ruleEngine == null)
            {
                ruleEngine = new RageRuleEngine();
                ruleEngine.OnRageChanged += HandleEngineRageChanged;
                ruleEngine.OnSegmentFilled += HandleEngineSegmentFilled;
            }
            return ruleEngine;
        }

        private void Awake()
        {
            RageRuleEngine engine = EnsureRuleEngine();
            engine.SetRageExact(0f, notify: false);
            currentRage = 0;
        }

        private void Start()
        {
            currentRage = EnsureRuleEngine().CurrentRage;
            OnRageChanged?.Invoke(currentRage, MAX_RAGE);
        }

        private void OnDestroy()
        {
            if (ruleEngine != null)
            {
                ruleEngine.OnRageChanged -= HandleEngineRageChanged;
                ruleEngine.OnSegmentFilled -= HandleEngineSegmentFilled;
            }
        }

        private void HandleEngineRageChanged(int current, int max)
        {
            currentRage = current;
            OnRageChanged?.Invoke(current, max);
        }

        private void HandleEngineSegmentFilled(int segmentIndex)
        {
            OnSegmentFilled?.Invoke(segmentIndex);
        }

        private static CombatAttackKind ToCombatAttackKind(AttackType attackType)
        {
            switch (attackType)
            {
                case AttackType.NormalAttack_J: return CombatAttackKind.NormalAttackJ;
                case AttackType.Skill_U: return CombatAttackKind.SkillU;
                case AttackType.Skill_I: return CombatAttackKind.SkillI;
                case AttackType.Combo_DashJ: return CombatAttackKind.ComboDashJ;
                case AttackType.Combo_DashI: return CombatAttackKind.ComboDashI;
                case AttackType.TransformBurst: return CombatAttackKind.TransformBurst;
                case AttackType.EnemyAttack: return CombatAttackKind.EnemyAttack;
                case AttackType.EnemyHeavyAttack: return CombatAttackKind.EnemyHeavyAttack;
                default: return CombatAttackKind.NormalAttackJ;
            }
        }

        /// <summary>
        /// Cộng +1.5 Rage khi đánh trúng địch bằng J, U hoặc combo Dash+J
        /// </summary>
        public void AddRageFromHit(AttackType attackType)
        {
            EnsureRuleEngine().AddRageFromHit(ToCombatAttackKind(attackType));
        }

        /// <summary>
        /// Cộng Rage tùy biến theo nhân vật khi đánh trúng địch bằng J, U hoặc combo Dash+J
        /// </summary>
        public void AddRageFromHit(AttackType attackType, float hitRageAmount)
        {
            EnsureRuleEngine().AddRageFromHit(ToCombatAttackKind(attackType), hitRageAmount);
        }

        /// <summary>
        /// Cộng +2.0 Rage thưởng khi tiêu diệt thành công 1 Enemy
        /// </summary>
        public void AddRageFromKill()
        {
            EnsureRuleEngine().AddRageFromKill();
        }

        /// <summary>
        /// Tăng nộ theo giá trị thực (+1.5/hit, +2.0/kill)
        /// </summary>
        public void AddRageFloat(float amount)
        {
            EnsureRuleEngine().AddRageFloat(amount);
        }

        /// <summary>
        /// Tăng nộ với giá trị nguyên tùy chỉnh
        /// </summary>
        public void AddRage(int amount)
        {
            EnsureRuleEngine().AddRage(amount);
        }

        /// <summary>
        /// Kiểm tra có đủ nộ để dùng Kỹ năng I (10 nộ)
        /// </summary>
        public bool CanUseSkillI()
        {
            return EnsureRuleEngine().CanUseSkillI();
        }

        /// <summary>
        /// Kiểm tra có đủ nộ để Biến Hình (30 nộ)
        /// </summary>
        public bool CanTransform()
        {
            return EnsureRuleEngine().CanTransform();
        }

        /// <summary>
        /// Kiểm tra có đủ điều kiện dùng Combo Đặc Biệt (Dash + I)
        /// </summary>
        public bool CanUseSpecialCombo(bool isTransformed)
        {
            return EnsureRuleEngine().CanUseSpecialCombo(isTransformed);
        }

        /// <summary>
        /// Trừ nộ khi thi triển chiêu
        /// </summary>
        public bool TryConsumeRage(int amount)
        {
            return EnsureRuleEngine().TryConsumeRage(amount);
        }

        /// <summary>
        /// Nạp đầy 30/30 nộ ngay lập tức (phục vụ Sandbox/Debug/Power-up)
        /// </summary>
        public void FillMaxRage()
        {
            EnsureRuleEngine().FillMaxRage();
        }

        /// <summary>
        /// Reset nộ về 0 (ví dụ khi hồi sinh)
        /// </summary>
        public void ResetRage()
        {
            EnsureRuleEngine().ResetRage();
        }

#if UNITY_EDITOR
        [ContextMenu("Add 10 Rage (Debug)")]
        private void DebugAdd10Rage() => AddRage(10);

        [ContextMenu("Full Rage (Debug)")]
        private void DebugFullRage() => AddRage(30);
#endif
    }
}
