using System;
using System.Collections.Generic;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Patterns.Items;

namespace StickmanBeatEmUp.CorePure.Rules.Economy
{
    /// <summary>
    /// Động cơ quy tắc Kho vật phẩm & 2 ô trang bị Q (0) / E (1) thuần C#.
    /// Bảo toàn bất biến miền: 0 <= số lượng <= MAX_STACK_PER_ITEM (99).
    /// </summary>
    public sealed class InventoryRuleEngine
    {
        public const int SLOT_Q = 0;
        public const int SLOT_E = 1;
        public const int SLOT_COUNT = 2;
        public const int MAX_STACK_PER_ITEM = 99;

        private readonly Dictionary<int, int> ownedCounts = new Dictionary<int, int>();
        private readonly int[] equippedSlots = new int[SLOT_COUNT];

        public InventoryRuleEngine()
        {
            equippedSlots[SLOT_Q] = ConsumableItemIds.SENZU_BEAN;
            equippedSlots[SLOT_E] = ConsumableItemIds.SPINAL_FLUID;
        }

        public static bool IsValidSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < SLOT_COUNT;
        }

        public int GetOwnedCount(int itemTypeId)
        {
            if (itemTypeId <= ConsumableItemIds.NONE)
            {
                return 0;
            }

            return ownedCounts.TryGetValue(itemTypeId, out int count) ? count : 0;
        }

        /// <summary>
        /// Cộng vật phẩm vào kho, kẹp cứng biên [0, MAX_STACK_PER_ITEM (99)].
        /// Trả về số lượng thực tế đã cộng thêm.
        /// </summary>
        public int AddItem(int itemTypeId, int amount)
        {
            if (itemTypeId <= ConsumableItemIds.NONE || amount <= 0)
            {
                return 0;
            }

            int current = GetOwnedCount(itemTypeId);
            int remainingCapacity = MAX_STACK_PER_ITEM - current;
            if (remainingCapacity <= 0)
            {
                return 0;
            }

            int added = Math.Min(amount, remainingCapacity);
            ownedCounts[itemTypeId] = current + added;
            return added;
        }

        public void SetOwnedCountExact(int itemTypeId, int count)
        {
            if (itemTypeId <= ConsumableItemIds.NONE)
            {
                return;
            }

            int clamped = Math.Max(0, Math.Min(MAX_STACK_PER_ITEM, count));
            ownedCounts[itemTypeId] = clamped;
        }

        public int GetEquippedItem(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                return ConsumableItemIds.NONE;
            }

            return equippedSlots[slotIndex];
        }

        public bool TrySetEquippedItem(int slotIndex, int itemTypeId)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                return false;
            }

            equippedSlots[slotIndex] = itemTypeId < ConsumableItemIds.NONE
                ? ConsumableItemIds.NONE
                : itemTypeId;
            return true;
        }

        public void SetEquippedItemOrThrow(int slotIndex, int itemTypeId)
        {
            if (!TrySetEquippedItem(slotIndex, itemTypeId))
            {
                throw new InvalidPurchaseException(
                    itemTypeId.ToString(),
                    $"Invalid equipment slot index: {slotIndex}. Allowed slots: 0 (Q), 1 (E).");
            }
        }

        public bool CanUseSlot(int slotIndex, bool isFreeUseMode)
        {
            int itemTypeId = GetEquippedItem(slotIndex);
            if (itemTypeId <= ConsumableItemIds.NONE)
            {
                return false;
            }

            return isFreeUseMode || GetOwnedCount(itemTypeId) > 0;
        }

        public bool TryConsumeSlot(int slotIndex, bool isFreeUseMode, out int usedItemTypeId)
        {
            usedItemTypeId = GetEquippedItem(slotIndex);
            if (usedItemTypeId <= ConsumableItemIds.NONE)
            {
                return false;
            }

            if (isFreeUseMode)
            {
                return true;
            }

            int current = GetOwnedCount(usedItemTypeId);
            if (current <= 0)
            {
                return false;
            }

            ownedCounts[usedItemTypeId] = current - 1;
            return true;
        }

        public bool TryAutoEquipIfSlotAvailable(int itemTypeId, out int assignedSlot)
        {
            assignedSlot = -1;
            if (itemTypeId <= ConsumableItemIds.NONE)
            {
                return false;
            }

            int equippedQ = equippedSlots[SLOT_Q];
            int equippedE = equippedSlots[SLOT_E];
            if (equippedQ == itemTypeId || equippedE == itemTypeId)
            {
                return false;
            }

            if (equippedQ == ConsumableItemIds.NONE)
            {
                equippedSlots[SLOT_Q] = itemTypeId;
                assignedSlot = SLOT_Q;
                return true;
            }

            if (equippedE == ConsumableItemIds.NONE)
            {
                equippedSlots[SLOT_E] = itemTypeId;
                assignedSlot = SLOT_E;
                return true;
            }

            if (GetOwnedCount(equippedQ) <= 0)
            {
                equippedSlots[SLOT_Q] = itemTypeId;
                assignedSlot = SLOT_Q;
                return true;
            }

            if (GetOwnedCount(equippedE) <= 0)
            {
                equippedSlots[SLOT_E] = itemTypeId;
                assignedSlot = SLOT_E;
                return true;
            }

            return false;
        }

        public void ResetToDefaults(IReadOnlyList<int> knownItemTypeIds)
        {
            ownedCounts.Clear();
            if (knownItemTypeIds != null)
            {
                for (int i = 0; i < knownItemTypeIds.Count; i++)
                {
                    int id = knownItemTypeIds[i];
                    if (id > ConsumableItemIds.NONE)
                    {
                        ownedCounts[id] = 0;
                    }
                }
            }

            equippedSlots[SLOT_Q] = ConsumableItemIds.SENZU_BEAN;
            equippedSlots[SLOT_E] = ConsumableItemIds.SPINAL_FLUID;
        }
    }
}
