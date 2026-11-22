using System;
using System.Collections.Generic;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Patterns.Decorators
{
    /// <summary>
    /// Thuật toán quản lý cộng dồn, làm mới và đếm ngược hết hạn buff theo deltaTime,
    /// đồng thời lắp chuỗi GoF Decorator IStatSource thuần C# cho nhân vật.
    /// </summary>
    public sealed class BuffStackResolver
    {
        private sealed class ActiveBuffEntry
        {
            public string BuffId;
            public float RemainingSeconds;
            public float RemainingShield;
            public bool ExpiresWhenShieldDepleted;
            public Func<IStatSource, IStatSource> DecoratorFactory;
        }

        private readonly List<ActiveBuffEntry> activeBuffs = new List<ActiveBuffEntry>();

        public int ActiveCount => activeBuffs.Count;

        /// <summary>
        /// Kích hoạt buff mới hoặc làm mới thời lượng/giáp nếu buff cùng mã đang tồn tại (không nhân đôi vô hạn).
        /// </summary>
        public void ApplyOrRefresh(
            string buffId,
            float durationSeconds,
            float shieldAmount,
            bool expiresWhenShieldDepleted,
            Func<IStatSource, IStatSource> decoratorFactory)
        {
            if (string.IsNullOrWhiteSpace(buffId))
            {
                throw new ArgumentException("buffId must not be null or empty.", nameof(buffId));
            }

            if (decoratorFactory == null)
            {
                throw new ArgumentNullException(nameof(decoratorFactory));
            }

            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds <= 0f)
            {
                return;
            }

            float sanitizedShield = SanitizeNonNegative(shieldAmount);
            if (expiresWhenShieldDepleted && sanitizedShield <= 0f)
            {
                return;
            }

            int existingIndex = FindBuffIndex(buffId);
            if (existingIndex >= 0)
            {
                ActiveBuffEntry existing = activeBuffs[existingIndex];
                existing.RemainingSeconds = Math.Max(existing.RemainingSeconds, durationSeconds);
                existing.RemainingShield = Math.Max(existing.RemainingShield, sanitizedShield);
                existing.ExpiresWhenShieldDepleted = expiresWhenShieldDepleted;
                existing.DecoratorFactory = decoratorFactory;
                return;
            }

            activeBuffs.Add(new ActiveBuffEntry
            {
                BuffId = buffId,
                RemainingSeconds = durationSeconds,
                RemainingShield = sanitizedShield,
                ExpiresWhenShieldDepleted = expiresWhenShieldDepleted,
                DecoratorFactory = decoratorFactory
            });
        }

        /// <summary>
        /// Cập nhật đồng hồ đếm ngược cho toàn bộ buff và tự động gỡ bỏ các buff đã hết hạn hoặc vỡ giáp.
        /// Độ phức tạp thời gian: O(n) với n là số buff đang kích hoạt.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f || activeBuffs.Count == 0)
            {
                return;
            }

            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                ActiveBuffEntry entry = activeBuffs[i];
                entry.RemainingSeconds -= deltaTime;

                bool expiredByTime = entry.RemainingSeconds <= 0f;
                bool expiredByShield = entry.ExpiresWhenShieldDepleted && entry.RemainingShield <= 0f;
                if (expiredByTime || expiredByShield)
                {
                    activeBuffs.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Đồng bộ lượng giáp ảo còn lại của một buff; nếu giáp về 0 và buff phụ thuộc giáp thì tự gỡ bỏ.
        /// </summary>
        public void UpdateShield(string buffId, float remainingShield)
        {
            int index = FindBuffIndex(buffId);
            if (index < 0)
            {
                return;
            }

            ActiveBuffEntry entry = activeBuffs[index];
            entry.RemainingShield = SanitizeNonNegative(remainingShield);
            if (entry.ExpiresWhenShieldDepleted && entry.RemainingShield <= 0f)
            {
                activeBuffs.RemoveAt(index);
            }
        }

        public bool HasBuff(string buffId)
        {
            return FindBuffIndex(buffId) >= 0;
        }

        public float GetRemainingSeconds(string buffId)
        {
            int index = FindBuffIndex(buffId);
            return index >= 0 ? activeBuffs[index].RemainingSeconds : 0f;
        }

        public bool RemoveBuff(string buffId)
        {
            int index = FindBuffIndex(buffId);
            if (index < 0)
            {
                return false;
            }

            activeBuffs.RemoveAt(index);
            return true;
        }

        public void ClearAll()
        {
            activeBuffs.Clear();
        }

        /// <summary>
        /// Lắp chuỗi Decorator tuần tự quanh nguồn chỉ số nền theo danh sách buff đang còn hạn.
        /// Độ phức tạp thời gian: O(n).
        /// </summary>
        public IStatSource BuildStatChain(IStatSource baseSource = null)
        {
            IStatSource current = baseSource ?? DefaultStatSource.Default;
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                current = activeBuffs[i].DecoratorFactory(current);
            }
            return current;
        }

        /// <summary>
        /// Xuất trạng thái các buff hiện có sang danh sách BuffSnapshotDTO phục vụ Save/Load giữa trận.
        /// </summary>
        public List<BuffSnapshotDTO> ExportSnapshots()
        {
            List<BuffSnapshotDTO> snapshots = new List<BuffSnapshotDTO>(activeBuffs.Count);
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                ActiveBuffEntry entry = activeBuffs[i];
                snapshots.Add(new BuffSnapshotDTO
                {
                    buffId = entry.BuffId,
                    remainingSeconds = entry.RemainingSeconds,
                    remainingShield = entry.RemainingShield
                });
            }
            return snapshots;
        }

        /// <summary>
        /// Khôi phục danh sách buff từ dữ liệu Save thông qua hàm ánh xạ buffId sang DecoratorFactory.
        /// </summary>
        public void RestoreSnapshots(
            IReadOnlyList<BuffSnapshotDTO> snapshots,
            Func<BuffSnapshotDTO, Func<IStatSource, IStatSource>> factoryResolver,
            Func<string, bool> expiresWhenShieldDepletedResolver = null)
        {
            ClearAll();
            if (snapshots == null || factoryResolver == null)
            {
                return;
            }

            for (int i = 0; i < snapshots.Count; i++)
            {
                BuffSnapshotDTO dto = snapshots[i];
                if (dto == null || string.IsNullOrWhiteSpace(dto.buffId) || dto.remainingSeconds <= 0f)
                {
                    continue;
                }

                Func<IStatSource, IStatSource> factory = factoryResolver(dto);
                if (factory == null)
                {
                    continue;
                }

                bool expiresOnShieldZero = expiresWhenShieldDepletedResolver != null &&
                                           expiresWhenShieldDepletedResolver(dto.buffId);
                ApplyOrRefresh(
                    dto.buffId,
                    dto.remainingSeconds,
                    dto.remainingShield,
                    expiresOnShieldZero,
                    factory);
            }
        }

        private static float SanitizeNonNegative(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return 0f;
            }
            return value;
        }

        private int FindBuffIndex(string buffId)
        {
            if (string.IsNullOrWhiteSpace(buffId))
            {
                return -1;
            }

            for (int i = 0; i < activeBuffs.Count; i++)
            {
                if (string.Equals(activeBuffs[i].BuffId, buffId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
