using System;
using UnityEngine;

namespace StickmanBeatEmUp.Economy
{
    /// <summary>
    /// Quản lý tiền tệ Xu (Coins) toàn cục giữa Main Menu, Shop và các màn chơi.
    /// Lưu trữ bền vững qua PlayerPrefs với khóa "MDS_PlayerCoins".
    /// </summary>
    public class CurrencyManager : MonoBehaviour
    {
        public const string COINS_PREF_KEY = "MDS_PlayerCoins";

        public const int REWARD_NORMAL_CREEP = 5;
        public const int REWARD_SAIBAMAN = 8;
        public const int REWARD_ELITE = 25;
        public const int REWARD_BOSS = 100;

        private static CurrencyManager instance;
        private static bool isApplicationQuitting;

        [SerializeField] private int currentCoins;

        public static bool HasInstance => instance != null;

        public static CurrencyManager Instance
        {
            get
            {
                if (isApplicationQuitting) return null;

                if (instance == null)
                {
                    instance = FindAnyObjectByType<CurrencyManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject(nameof(CurrencyManager));
                        instance = go.AddComponent<CurrencyManager>();
                    }
                }
                return instance;
            }
        }

        public int CurrentCoins => currentCoins;
        public bool SuppressEnemyRewards { get; set; }

        /// <summary>
        /// Thông báo tổng số Xu hiện tại mỗi khi thay đổi.
        /// </summary>
        public event Action<int> OnCoinsChanged;

        /// <summary>
        /// Thông báo khi nhận Xu từ một vị trí trong thế giới (ví dụ xác Enemy) để HUD phát VFX bay về quầy Xu.
        /// </summary>
        public event Action<int, Vector3> OnCoinsEarnedWorld;

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
            LoadCoins();
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

        /// <summary>
        /// Trả về mức thưởng Xu mặc định theo phân loại Enemy.
        /// </summary>
        public static int GetDefaultRewardForEnemy(bool isKamikazeOrSaibaman, bool isElite, bool isBoss)
        {
            if (isBoss) return REWARD_BOSS;
            if (isElite) return REWARD_ELITE;
            if (isKamikazeOrSaibaman) return REWARD_SAIBAMAN;
            return REWARD_NORMAL_CREEP;
        }

        /// <summary>
        /// Thưởng Xu ngay lập tức khi tiêu diệt Enemy tại vị trí deathWorldPos.
        /// </summary>
        public static void RewardEnemyKill(int rewardAmount, Vector3 deathWorldPos)
        {
            if (rewardAmount <= 0) return;

            CurrencyManager manager = Instance;
            if (manager == null || manager.SuppressEnemyRewards) return;

            manager.AddCoins(rewardAmount, deathWorldPos);
        }

        /// <summary>
        /// Cộng Xu, lưu PlayerPrefs ngay lập tức và phát sự kiện cập nhật HUD/Shop.
        /// </summary>
        public void AddCoins(int amount, Vector3? worldSourcePos = null)
        {
            if (amount <= 0) return;

            currentCoins += amount;
            SaveCoins();
            OnCoinsChanged?.Invoke(currentCoins);

            if (worldSourcePos.HasValue)
            {
                OnCoinsEarnedWorld?.Invoke(amount, worldSourcePos.Value);
            }
        }

        /// <summary>
        /// Trừ Xu nếu đủ số dư. Lưu ngay vào PlayerPrefs khi giao dịch thành công.
        /// </summary>
        public bool TrySpendCoins(int amount)
        {
            if (amount < 0) return false;
            if (amount == 0) return true;
            if (currentCoins < amount) return false;

            currentCoins -= amount;
            SaveCoins();
            OnCoinsChanged?.Invoke(currentCoins);
            return true;
        }

        /// <summary>
        /// Kiểm tra nhanh có đủ Xu để mua hay không.
        /// </summary>
        public bool CanAfford(int amount)
        {
            return amount >= 0 && currentCoins >= amount;
        }

        /// <summary>
        /// Cộng Xu phục vụ kiểm thử / QA.
        /// </summary>
        public void AddDebugCoins(int amount)
        {
            if (amount <= 0) return;
            AddCoins(amount, null);
        }

        /// <summary>
        /// Đặt lại số Xu về 0 và lưu PlayerPrefs.
        /// </summary>
        public void ResetCoins()
        {
            currentCoins = 0;
            SaveCoins();
            OnCoinsChanged?.Invoke(currentCoins);
        }

        private void LoadCoins()
        {
            currentCoins = Mathf.Max(0, PlayerPrefs.GetInt(COINS_PREF_KEY, 0));
        }

        private void SaveCoins()
        {
            PlayerPrefs.SetInt(COINS_PREF_KEY, currentCoins);
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        [ContextMenu("Add 100 Xu (Debug)")]
        private void DebugAdd100Coins() => AddDebugCoins(100);

        [ContextMenu("Reset Xu (Debug)")]
        private void DebugResetCoins() => ResetCoins();
#endif
    }
}
