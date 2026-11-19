using System;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Items
{
    /// <summary>
    /// Quản lý kho vật phẩm dùng chung (Shared Quantities) và 2 ô trang bị (Slot Q = 0, Slot E = 1).
    /// Mọi thay đổi số lượng và trang bị được lưu tức thì qua PlayerPrefs và không bao giờ hoàn lại khi Retry/Exit.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public const string OWNED_PREF_PREFIX = "MDS_ItemOwned_";
        public const string EQUIPPED_SLOT_PREFIX = "MDS_EquippedSlot_";
        public const int SLOT_Q = 0;
        public const int SLOT_E = 1;
        public const int SLOT_COUNT = 2;

        private static readonly ConsumableItemType[] DefaultCatalogOrder =
        {
            ConsumableItemType.SenzuBean,
            ConsumableItemType.SpinalFluid,
            ConsumableItemType.UltraDivineWater
        };

        private static InventoryManager instance;
        private static bool isApplicationQuitting;

        [Header("Catalog (Tự động nạp Resources/Items nếu để trống)")]
        [SerializeField] private ConsumableItemSO[] itemCatalog;

        private readonly Dictionary<ConsumableItemType, ConsumableItemSO> itemDataMap = new Dictionary<ConsumableItemType, ConsumableItemSO>();
        private readonly Dictionary<ConsumableItemType, int> ownedCounts = new Dictionary<ConsumableItemType, int>();
        private readonly ConsumableItemType[] equippedSlots = new ConsumableItemType[SLOT_COUNT];
        private readonly List<ConsumableItemSO> orderedCatalog = new List<ConsumableItemSO>(3);

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

        /// <summary>
        /// Khi bật (Training Mode), dùng vật phẩm Q/E miễn phí và không trừ kho.
        /// </summary>
        public bool IsTrainingFreeUseMode { get; set; }

        /// <summary>
        /// Phát ra bất cứ khi nào số lượng trong kho hoặc trang bị ô Q/E thay đổi.
        /// </summary>
        public event Action OnInventoryChanged;

        /// <summary>
        /// Phát ra khi người chơi tiêu thụ thành công một ô vật phẩm (slotIndex, itemType).
        /// </summary>
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
            ownedCounts.Clear();

            for (int i = 0; i < DefaultCatalogOrder.Length; i++)
            {
                ConsumableItemType type = DefaultCatalogOrder[i];
                int savedCount = Mathf.Max(0, PlayerPrefs.GetInt(GetOwnedPrefKey(type), 0));
                ownedCounts[type] = savedCount;
            }

            foreach (ConsumableItemType key in itemDataMap.Keys)
            {
                if (!ownedCounts.ContainsKey(key))
                {
                    ownedCounts[key] = Mathf.Max(0, PlayerPrefs.GetInt(GetOwnedPrefKey(key), 0));
                }
            }

            equippedSlots[SLOT_Q] = LoadEquippedSlot(SLOT_Q, ConsumableItemType.SenzuBean);
            equippedSlots[SLOT_E] = LoadEquippedSlot(SLOT_E, ConsumableItemType.SpinalFluid);
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

        /// <summary>
        /// Trả về tổng số lượng đang sở hữu của một loại vật phẩm (dùng chung cho cả Q và E).
        /// </summary>
        public int GetOwnedCount(ConsumableItemType type)
        {
            EnsureInitialized();
            if (type == ConsumableItemType.None) return 0;
            return ownedCounts.TryGetValue(type, out int count) ? count : 0;
        }

        /// <summary>
        /// Cộng thêm vật phẩm vào kho dùng chung, lưu ngay PlayerPrefs và thông báo UI.
        /// </summary>
        public void AddOwnedItem(ConsumableItemType type, int amount = 1)
        {
            EnsureInitialized();
            if (type == ConsumableItemType.None || amount <= 0) return;

            int updated = GetOwnedCount(type) + amount;
            ownedCounts[type] = updated;

            PlayerPrefs.SetInt(GetOwnedPrefKey(type), updated);
            PlayerPrefs.Save();

            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Lấy loại vật phẩm đang trang bị ở ô 0 (Q) hoặc 1 (E).
        /// </summary>
        public ConsumableItemType GetEquippedItem(int slotIndex)
        {
            EnsureInitialized();
            if (slotIndex < 0 || slotIndex >= SLOT_COUNT) return ConsumableItemType.None;
            return equippedSlots[slotIndex];
        }

        /// <summary>
        /// Gán vật phẩm vào ô trang bị 0 (Q) hoặc 1 (E). Hỗ trợ gắn cùng 1 vật phẩm ở cả 2 ô.
        /// </summary>
        public void SetEquippedItem(int slotIndex, ConsumableItemType type)
        {
            EnsureInitialized();
            if (slotIndex < 0 || slotIndex >= SLOT_COUNT) return;

            if (!Enum.IsDefined(typeof(ConsumableItemType), type))
            {
                type = ConsumableItemType.None;
            }

            equippedSlots[slotIndex] = type;
            PlayerPrefs.SetInt(GetSlotPrefKey(slotIndex), (int)type);
            PlayerPrefs.Save();

            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Kiểm tra ô trang bị có thể kích hoạt ở thời điểm hiện tại hay không.
        /// </summary>
        public bool CanUseSlot(int slotIndex)
        {
            ConsumableItemType item = GetEquippedItem(slotIndex);
            if (item == ConsumableItemType.None) return false;
            return IsTrainingFreeUseMode || GetOwnedCount(item) > 0;
        }

        /// <summary>
        /// Lấy số lượng hiện có của vật phẩm đang gắn ở ô slotIndex.
        /// </summary>
        public int GetSlotQuantity(int slotIndex)
        {
            return GetOwnedCount(GetEquippedItem(slotIndex));
        }

        /// <summary>
        /// Tiêu thụ vật phẩm tại ô Q (0) hoặc E (1).
        /// Trong Campaign/Endless: trừ kho dùng chung và gọi PlayerPrefs.Save() ngay lập tức.
        /// Trong Training (IsTrainingFreeUseMode = true): cho dùng miễn phí mà không trừ kho.
        /// </summary>
        public bool TryConsumeSlot(int slotIndex, out ConsumableItemType usedType)
        {
            EnsureInitialized();

            usedType = GetEquippedItem(slotIndex);
            if (usedType == ConsumableItemType.None)
            {
                return false;
            }

            if (!IsTrainingFreeUseMode)
            {
                int currentCount = GetOwnedCount(usedType);
                if (currentCount <= 0)
                {
                    return false;
                }

                int remaining = currentCount - 1;
                ownedCounts[usedType] = remaining;
                PlayerPrefs.SetInt(GetOwnedPrefKey(usedType), remaining);
                PlayerPrefs.Save();

                OnInventoryChanged?.Invoke();
            }

            OnSlotConsumed?.Invoke(slotIndex, usedType);
            return true;
        }

        /// <summary>
        /// Lấy metadata ScriptableObject của vật phẩm.
        /// </summary>
        public ConsumableItemSO GetItemData(ConsumableItemType type)
        {
            EnsureInitialized();
            if (type == ConsumableItemType.None) return null;

            if (itemDataMap.TryGetValue(type, out ConsumableItemSO data))
            {
                return data;
            }

            return null;
        }

        /// <summary>
        /// Trả về toàn bộ danh sách vật phẩm theo thứ tự chuẩn của Shop.
        /// </summary>
        public ConsumableItemSO[] GetAllItems()
        {
            EnsureInitialized();
            return orderedCatalog.ToArray();
        }

        /// <summary>
        /// Đặt lại toàn bộ kho về 0 và trang bị mặc định (Q = SenzuBean, E = SpinalFluid).
        /// </summary>
        public void ResetInventory()
        {
            EnsureInitialized();

            for (int i = 0; i < DefaultCatalogOrder.Length; i++)
            {
                ConsumableItemType type = DefaultCatalogOrder[i];
                ownedCounts[type] = 0;
                PlayerPrefs.SetInt(GetOwnedPrefKey(type), 0);
            }

            equippedSlots[SLOT_Q] = ConsumableItemType.SenzuBean;
            equippedSlots[SLOT_E] = ConsumableItemType.SpinalFluid;
            PlayerPrefs.SetInt(GetSlotPrefKey(SLOT_Q), (int)equippedSlots[SLOT_Q]);
            PlayerPrefs.SetInt(GetSlotPrefKey(SLOT_E), (int)equippedSlots[SLOT_E]);
            PlayerPrefs.Save();

            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Nạp lại kho vật phẩm và 2 ô trang bị Q/E từ PlayerPrefs và phát OnInventoryChanged.
        /// </summary>
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
