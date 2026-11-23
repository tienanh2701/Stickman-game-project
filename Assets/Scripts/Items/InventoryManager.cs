using System;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Rules.Economy;

namespace StickmanBeatEmUp.Items
{
    /// <summary>
    /// Unity Adapter quản lý kho vật phẩm dùng chung (Shared Quantities) và 2 ô trang bị (Slot Q = 0, Slot E = 1).
    /// Ủy quyền toàn bộ quy tắc miền (kẹp trần 99, kiểm tra slot, tiêu thụ) sang InventoryRuleEngine thuần C#.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public const string OWNED_PREF_PREFIX = "MDS_ItemOwned_";
        public const string EQUIPPED_SLOT_PREFIX = "MDS_EquippedSlot_";
        public const int SLOT_Q = InventoryRuleEngine.SLOT_Q;
        public const int SLOT_E = InventoryRuleEngine.SLOT_E;
        public const int SLOT_COUNT = InventoryRuleEngine.SLOT_COUNT;
        public const int MAX_STACK_PER_ITEM = InventoryRuleEngine.MAX_STACK_PER_ITEM;

        private static readonly ConsumableItemType[] DefaultCatalogOrder =
        {
            ConsumableItemType.SenzuBean,
            ConsumableItemType.SpinalFluid,
            ConsumableItemType.UltraDivineWater,
            ConsumableItemType.RageElixir
        };

        private static InventoryManager instance;
        private static bool isApplicationQuitting;

        [Header("Catalog (Tự động nạp Resources/Items nếu để trống)")]
        [SerializeField] private ConsumableItemSO[] itemCatalog;

        private readonly InventoryRuleEngine ruleEngine = new InventoryRuleEngine();
        private readonly Dictionary<ConsumableItemType, ConsumableItemSO> itemDataMap = new Dictionary<ConsumableItemType, ConsumableItemSO>();
        private readonly List<ConsumableItemSO> orderedCatalog = new List<ConsumableItemSO>(4);

        private bool isInitialized;

        public static bool HasInstance => !isApplicationQuitting && instance != null;

        public static InventoryManager Instance
        {
            get
            {
                if (isApplicationQuitting) return null;

                if (instance == null)
                {
                    instance = FindAnyObjectByType<InventoryManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject(nameof(InventoryManager));
                        instance = go.AddComponent<InventoryManager>();
                    }
                }
                return instance;
            }
        }

        public InventoryRuleEngine RuleEngine
        {
            get
            {
                EnsureInitialized();
                return ruleEngine;
            }
        }

        public bool IsTrainingFreeUseMode { get; set; }

        public event Action OnInventoryChanged;
        public event Action<int, ConsumableItemType> OnSlotConsumed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            isApplicationQuitting = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExistsOnLoad()
        {
            _ = Instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureInitialized();
        }

        private void OnApplicationQuit()
        {
            isApplicationQuitting = true;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void EnsureInitialized()
        {
            if (isInitialized) return;

            BuildCatalog();
            LoadInventoryAndSlots();
            isInitialized = true;
        }

        private void BuildCatalog()
        {
            itemDataMap.Clear();
            orderedCatalog.Clear();

            if (itemCatalog == null || itemCatalog.Length == 0)
            {
                itemCatalog = Resources.LoadAll<ConsumableItemSO>("Items");
            }

            if (itemCatalog != null)
            {
                for (int i = 0; i < itemCatalog.Length; i++)
                {
                    ConsumableItemSO item = itemCatalog[i];
                    if (item == null || item.itemType == ConsumableItemType.None) continue;
                    if (!itemDataMap.ContainsKey(item.itemType))
                    {
                        itemDataMap[item.itemType] = item;
                    }
                }
            }

            for (int i = 0; i < DefaultCatalogOrder.Length; i++)
            {
                ConsumableItemType type = DefaultCatalogOrder[i];
                if (!itemDataMap.TryGetValue(type, out ConsumableItemSO item) || item == null)
                {
                    item = ConsumableItemSO.CreateRuntimeDefault(type);
                    itemDataMap[type] = item;
                }
                orderedCatalog.Add(item);
            }

            foreach (KeyValuePair<ConsumableItemType, ConsumableItemSO> kvp in itemDataMap)
            {
                if (kvp.Value != null && !orderedCatalog.Contains(kvp.Value))
                {
                    orderedCatalog.Add(kvp.Value);
                }
            }
        }

        private void LoadInventoryAndSlots()
        {
            for (int i = 0; i < DefaultCatalogOrder.Length; i++)
            {
                ConsumableItemType type = DefaultCatalogOrder[i];
                int savedCount = Mathf.Max(0, PlayerPrefs.GetInt(GetOwnedPrefKey(type), 0));
                ruleEngine.SetOwnedCountExact((int)type, savedCount);
            }

            foreach (ConsumableItemType key in itemDataMap.Keys)
            {
                int savedCount = Mathf.Max(0, PlayerPrefs.GetInt(GetOwnedPrefKey(key), 0));
                ruleEngine.SetOwnedCountExact((int)key, savedCount);
            }

            ruleEngine.TrySetEquippedItem(SLOT_Q, (int)LoadEquippedSlot(SLOT_Q, ConsumableItemType.SenzuBean));
            ruleEngine.TrySetEquippedItem(SLOT_E, (int)LoadEquippedSlot(SLOT_E, ConsumableItemType.SpinalFluid));
        }

        private static ConsumableItemType LoadEquippedSlot(int slotIndex, ConsumableItemType defaultType)
        {
            int raw = PlayerPrefs.GetInt(GetSlotPrefKey(slotIndex), (int)defaultType);
            if (!Enum.IsDefined(typeof(ConsumableItemType), raw))
            {
                return defaultType;
            }
            return (ConsumableItemType)raw;
        }

        public int GetOwnedCount(ConsumableItemType type)
        {
            EnsureInitialized();
            if (type == ConsumableItemType.None) return 0;
            return ruleEngine.GetOwnedCount((int)type);
        }

        public void AddOwnedItem(ConsumableItemType type, int amount = 1)
        {
            EnsureInitialized();
            if (type == ConsumableItemType.None || amount <= 0) return;

            int added = ruleEngine.AddItem((int)type, amount);
            if (added <= 0) return;

            int updated = ruleEngine.GetOwnedCount((int)type);
            PlayerPrefs.SetInt(GetOwnedPrefKey(type), updated);
            PlayerPrefs.Save();

            OnInventoryChanged?.Invoke();
        }

        public void SetOwnedCountExact(ConsumableItemType type, int count)
        {
            EnsureInitialized();
            if (type == ConsumableItemType.None) return;

            ruleEngine.SetOwnedCountExact((int)type, count);
            PlayerPrefs.SetInt(GetOwnedPrefKey(type), ruleEngine.GetOwnedCount((int)type));
            PlayerPrefs.Save();

            OnInventoryChanged?.Invoke();
        }

        public ConsumableItemType GetEquippedItem(int slotIndex)
        {
            EnsureInitialized();
            int raw = ruleEngine.GetEquippedItem(slotIndex);
            return Enum.IsDefined(typeof(ConsumableItemType), raw)
                ? (ConsumableItemType)raw
                : ConsumableItemType.None;
        }

        public void SetEquippedItem(int slotIndex, ConsumableItemType type)
        {
            EnsureInitialized();
            if (!InventoryRuleEngine.IsValidSlotIndex(slotIndex)) return;

            if (!Enum.IsDefined(typeof(ConsumableItemType), type))
            {
                type = ConsumableItemType.None;
            }

            ruleEngine.TrySetEquippedItem(slotIndex, (int)type);
            PlayerPrefs.SetInt(GetSlotPrefKey(slotIndex), (int)type);
            PlayerPrefs.Save();

            OnInventoryChanged?.Invoke();
        }

        public bool CanUseSlot(int slotIndex)
        {
            EnsureInitialized();
            return ruleEngine.CanUseSlot(slotIndex, IsTrainingFreeUseMode);
        }

        public int GetSlotQuantity(int slotIndex)
        {
            return GetOwnedCount(GetEquippedItem(slotIndex));
        }

        public bool TryConsumeSlot(int slotIndex, out ConsumableItemType usedType)
        {
            EnsureInitialized();

            if (!ruleEngine.TryConsumeSlot(slotIndex, IsTrainingFreeUseMode, out int usedId) ||
                !Enum.IsDefined(typeof(ConsumableItemType), usedId))
            {
                usedType = ConsumableItemType.None;
                return false;
            }

            usedType = (ConsumableItemType)usedId;
            if (!IsTrainingFreeUseMode)
            {
                int remaining = ruleEngine.GetOwnedCount(usedId);
                PlayerPrefs.SetInt(GetOwnedPrefKey(usedType), remaining);
                PlayerPrefs.Save();
                OnInventoryChanged?.Invoke();
            }

            OnSlotConsumed?.Invoke(slotIndex, usedType);
            return true;
        }

        public ConsumableItemSO GetItemData(ConsumableItemType type)
        {
            EnsureInitialized();
            if (type == ConsumableItemType.None) return null;

            return itemDataMap.TryGetValue(type, out ConsumableItemSO data) ? data : null;
        }

        public ConsumableItemSO[] GetAllItems()
        {
            EnsureInitialized();
            return orderedCatalog.ToArray();
        }

        public void ApplySnapshot(GameStateDTO state)
        {
            EnsureInitialized();
            if (state == null) return;

            ruleEngine.SetOwnedCountExact((int)ConsumableItemType.SenzuBean, state.senzuCount);
            ruleEngine.SetOwnedCountExact((int)ConsumableItemType.SpinalFluid, state.spinalFluidCount);
            ruleEngine.SetOwnedCountExact((int)ConsumableItemType.UltraDivineWater, state.ultraWaterCount);
            ruleEngine.SetOwnedCountExact((int)ConsumableItemType.RageElixir, 0);

            if (state.extraItemCounts != null)
            {
                for (int i = 0; i < state.extraItemCounts.Count; i++)
                {
                    ItemCountEntryDTO entry = state.extraItemCounts[i];
                    if (entry != null && entry.itemTypeId > 0)
                    {
                        ruleEngine.SetOwnedCountExact(entry.itemTypeId, entry.count);
                    }
                }
            }

            ConsumableItemType slotQ = Enum.IsDefined(typeof(ConsumableItemType), state.equippedSlotQ)
                ? (ConsumableItemType)state.equippedSlotQ
                : ConsumableItemType.SenzuBean;
            ConsumableItemType slotE = Enum.IsDefined(typeof(ConsumableItemType), state.equippedSlotE)
                ? (ConsumableItemType)state.equippedSlotE
                : ConsumableItemType.SpinalFluid;

            ruleEngine.TrySetEquippedItem(SLOT_Q, (int)slotQ);
            ruleEngine.TrySetEquippedItem(SLOT_E, (int)slotE);

            for (int i = 0; i < DefaultCatalogOrder.Length; i++)
            {
                ConsumableItemType type = DefaultCatalogOrder[i];
                PlayerPrefs.SetInt(GetOwnedPrefKey(type), ruleEngine.GetOwnedCount((int)type));
            }
            PlayerPrefs.SetInt(GetSlotPrefKey(SLOT_Q), (int)slotQ);
            PlayerPrefs.SetInt(GetSlotPrefKey(SLOT_E), (int)slotE);
            PlayerPrefs.Save();

            OnInventoryChanged?.Invoke();
        }

        public void ResetInventory()
        {
            EnsureInitialized();

            int[] knownIds = new int[DefaultCatalogOrder.Length];
            for (int i = 0; i < DefaultCatalogOrder.Length; i++)
            {
                knownIds[i] = (int)DefaultCatalogOrder[i];
                PlayerPrefs.SetInt(GetOwnedPrefKey(DefaultCatalogOrder[i]), 0);
            }

            ruleEngine.ResetToDefaults(knownIds);
            PlayerPrefs.SetInt(GetSlotPrefKey(SLOT_Q), (int)ConsumableItemType.SenzuBean);
            PlayerPrefs.SetInt(GetSlotPrefKey(SLOT_E), (int)ConsumableItemType.SpinalFluid);
            PlayerPrefs.Save();

            OnInventoryChanged?.Invoke();
        }

        public void ReloadProfileState()
        {
            if (!isInitialized)
            {
                BuildCatalog();
                isInitialized = true;
            }
            LoadInventoryAndSlots();
            OnInventoryChanged?.Invoke();
        }

        private static string GetOwnedPrefKey(ConsumableItemType type) => OWNED_PREF_PREFIX + type;
        private static string GetSlotPrefKey(int slotIndex) => EQUIPPED_SLOT_PREFIX + slotIndex;
    }
}
