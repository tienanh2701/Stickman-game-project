using System;
using UnityEngine;

namespace StickmanBeatEmUp.Core
{
    /// <summary>
    /// Quản lý cơ chế Tích Nộ (Rage System):
    /// - Tối đa 30 điểm (3 mốc, mỗi mốc 10 điểm)
    /// - Đánh trúng bằng J hoặc U: +1 điểm nộ
    /// - Kỹ năng U: 0 nộ
    /// - Kỹ năng I: 10 nộ (1 mốc)
    /// - Biến hình (Transform): 30 nộ (đầy cả 3 mốc)
    /// - Combo Đặc Biệt (Dash + I): Phải biến hình & Đầy 30 nộ
    /// </summary>
    public class RageManager : MonoBehaviour
    {
        public const int MAX_RAGE = 30;
        public const int RAGE_PER_SEGMENT = 10;
        public const int SKILL_I_COST = 10;
        public const int TRANSFORM_COST = 30;
        public const int SPECIAL_COMBO_COST = 30;
        public const float RAGE_PER_HIT = 1.5f;
        public const float RAGE_PER_KILL = 2.0f;

        [Header("Runtime State")]
        [SerializeField] private int currentRage = 0;
        private float currentRageExact = 0f;

        // Sự kiện thông báo thay đổi Nộ cho UI và FX
        public event Action<int, int> OnRageChanged; // (current, max)
        public event Action<int> OnSegmentFilled;     // (segmentIndex: 1, 2, 3)

        public int CurrentRage => currentRage;
        public float CurrentRageFloat => currentRageExact;
        public bool IsMaxRage => currentRageExact >= MAX_RAGE;
        public int FilledSegments => Mathf.FloorToInt(currentRageExact) / RAGE_PER_SEGMENT;

        private void Awake()
        {
            currentRageExact = 0f;
            currentRage = 0;
        }

        private void Start()
        {
            OnRageChanged?.Invoke(currentRage, MAX_RAGE);
        }

        /// <summary>
        /// Cộng +1.5 Rage khi đánh trúng địch bằng J, U hoặc combo Dash+J
        /// </summary>
        public void AddRageFromHit(AttackType attackType)
        {
            AddRageFromHit(attackType, RAGE_PER_HIT);
        }

        /// <summary>
        /// Cộng Rage tùy biến theo nhân vật khi đánh trúng địch bằng J, U hoặc combo Dash+J
        /// </summary>
        public void AddRageFromHit(AttackType attackType, float hitRageAmount)
        {
            if (attackType == AttackType.NormalAttack_J ||
                attackType == AttackType.Skill_U ||
                attackType == AttackType.Combo_DashJ)
            {
                AddRageFloat(hitRageAmount);
            }
        }

        /// <summary>
        /// Cộng +2.0 Rage thưởng khi tiêu diệt thành công 1 Enemy
        /// </summary>
        public void AddRageFromKill()
        {
            AddRageFloat(RAGE_PER_KILL);
        }

        /// <summary>
        /// Tăng nộ theo giá trị thực (+1.5/hit, +2.0/kill)
        /// </summary>
        public void AddRageFloat(float amount)
        {
            if (amount <= 0f || currentRageExact >= MAX_RAGE) return;

            int prevSegments = FilledSegments;
            currentRageExact = Mathf.Clamp(currentRageExact + amount, 0f, MAX_RAGE);
            currentRage = Mathf.FloorToInt(currentRageExact);

            OnRageChanged?.Invoke(currentRage, MAX_RAGE);

            int newSegments = FilledSegments;
            if (newSegments > prevSegments)
            {
                for (int i = prevSegments + 1; i <= newSegments; i++)
                {
                    OnSegmentFilled?.Invoke(i);
                }
            }
        }

        /// <summary>
        /// Tăng nộ với giá trị nguyên tùy chỉnh
        /// </summary>
        public void AddRage(int amount)
        {
            AddRageFloat(amount);
        }

        /// <summary>
        /// Kiểm tra có đủ nộ để dùng Kỹ năng I (10 nộ)
        /// </summary>
        public bool CanUseSkillI()
        {
            return currentRage >= SKILL_I_COST;
        }

        /// <summary>
        /// Kiểm tra có đủ nộ để Biến Hình (30 nộ)
        /// </summary>
        public bool CanTransform()
        {
            return currentRage >= TRANSFORM_COST;
        }

        /// <summary>
        /// Kiểm tra có đủ điều kiện dùng Combo Đặc Biệt (Dash + I)
        /// </summary>
        public bool CanUseSpecialCombo(bool isTransformed)
        {
            return isTransformed && currentRage >= SPECIAL_COMBO_COST;
        }

        /// <summary>
        /// Trừ nộ khi thi triển chiêu
        /// </summary>
        public bool TryConsumeRage(int amount)
        {
            if (currentRageExact >= amount)
            {
                currentRageExact = Mathf.Clamp(currentRageExact - amount, 0f, MAX_RAGE);
                currentRage = Mathf.FloorToInt(currentRageExact);
                OnRageChanged?.Invoke(currentRage, MAX_RAGE);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Nạp đầy 30/30 nộ ngay lập tức (phục vụ Sandbox/Debug/Power-up)
        /// </summary>
        public void FillMaxRage()
        {
            int prevSegments = FilledSegments;
            currentRageExact = MAX_RAGE;
            currentRage = MAX_RAGE;
            OnRageChanged?.Invoke(currentRage, MAX_RAGE);
            for (int i = prevSegments + 1; i <= 3; i++)
            {
                OnSegmentFilled?.Invoke(i);
            }
        }

        /// <summary>
        /// Reset nộ về 0 (ví dụ khi hồi sinh)
        /// </summary>
        public void ResetRage()
        {
            currentRageExact = 0f;
            currentRage = 0;
            OnRageChanged?.Invoke(currentRage, MAX_RAGE);
        }

#if UNITY_EDITOR
        [ContextMenu("Add 10 Rage (Debug)")]
        private void DebugAdd10Rage() => AddRage(10);

        [ContextMenu("Full Rage (Debug)")]
        private void DebugFullRage() => AddRage(30);
#endif
    }
}
