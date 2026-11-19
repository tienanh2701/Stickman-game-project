using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Economy;
using StickmanBeatEmUp.Items;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Auth
{
    /// <summary>
    /// Quản lý đăng ký, đăng nhập, đăng xuất và đồng bộ tiến trình riêng cho nhiều tài khoản cục bộ.
    /// Lưu trữ tại Application.persistentDataPath/stickman_accounts.json bằng SHA-256 kèm salt riêng mỗi tài khoản.
    /// </summary>
    public class AccountManager : MonoBehaviour
    {
        public const string ACTIVE_ACCOUNT_PREF_KEY = "MDS_ActiveAccountUsername";
        public const string ACCOUNTS_FILE_NAME = "stickman_accounts.json";

        private const string SELECTED_CHAR_KEY = "SelectedCharacter";
        private const string SELECTED_WORLD_KEY = "SelectedWorld";
        private const string SELECTED_STAGE_INDEX_KEY = "SelectedStageIndex";
        private const string SELECTED_GAME_MODE_KEY = "SelectedGameMode";
        private const string UNLOCKED_STAGE_KEY = "Stickman_UnlockedStage";

        private const int MIN_USERNAME_LENGTH = 3;
        private const int MAX_USERNAME_LENGTH = 20;
        private const int MIN_PASSWORD_LENGTH = 4;
        private const int SALT_BYTE_LENGTH = 16;

        private static AccountManager instance;
        private static bool isApplicationQuitting;

        private AccountSaveDatabase database = new AccountSaveDatabase();
        private UserAccountRecord activeAccount;
        private bool isApplyingProfile;
        private bool isSceneLoadedSubscribed;
        private CurrencyManager subscribedCurrencyManager;
        private InventoryManager subscribedInventoryManager;

        public static bool HasInstance => !isApplicationQuitting && instance != null;

        public static AccountManager Instance
        {
            get
            {
                if (isApplicationQuitting) return null;

                if (instance == null)
                {
                    instance = FindAnyObjectByType<AccountManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject(nameof(AccountManager));
                        instance = go.AddComponent<AccountManager>();
                    }
                }
                return instance;
            }
        }

        public bool HasActiveSession => activeAccount != null;
        public UserAccountRecord ActiveAccount => activeAccount;
        public IReadOnlyList<UserAccountRecord> Accounts =>
            database != null && database.accounts != null
                ? database.accounts
                : Array.Empty<UserAccountRecord>();

        public event Action<UserAccountRecord> OnActiveAccountChanged;

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
            LoadDatabaseFromDisk();
            RestoreSessionFromPrefs();
            SubscribeSyncEvents();
        }

        private void OnEnable()
        {
            SubscribeSyncEvents();
        }

        private void OnDisable()
        {
            UnsubscribeSyncEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeSyncEvents();
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && HasActiveSession)
            {
                SyncActiveProfileFromPrefs(saveToDisk: true);
            }
        }

        private void OnApplicationQuit()
        {
            if (HasActiveSession)
            {
                SyncActiveProfileFromPrefs(saveToDisk: true);
            }
            isApplicationQuitting = true;
        }

        public bool TryRegister(string username, string password, string confirmPassword, out string errorMessage)
        {
            string trimmed = username != null ? username.Trim() : string.Empty;
            if (!ValidateUsernameFormat(trimmed, out errorMessage))
            {
                return false;
            }

            if (FindAccountByUsername(trimmed) != null)
            {
                errorMessage = "Tên đăng nhập đã tồn tại.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < MIN_PASSWORD_LENGTH)
            {
                errorMessage = $"Mật khẩu phải có ít nhất {MIN_PASSWORD_LENGTH} ký tự.";
                return false;
            }

            if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
            {
                errorMessage = "Mật khẩu nhập lại không khớp.";
                return false;
            }

            if (HasActiveSession)
            {
                SyncActiveProfileFromPrefs(saveToDisk: false);
            }

            byte[] saltBytes = new byte[SALT_BYTE_LENGTH];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }

            string saltBase64 = Convert.ToBase64String(saltBytes);
            string hashHex = ComputePasswordHashHex(saltBase64, password);
            string utcNow = DateTime.UtcNow.ToString("o");

            UserAccountRecord newRecord = UserAccountRecord.CreateDefault(trimmed, saltBase64, hashHex, utcNow);
            EnsureDatabaseInitialized();
            database.accounts.Add(newRecord);
            activeAccount = newRecord;

            ApplyActiveProfileToPrefs();
            SaveDatabaseToDisk();
            OnActiveAccountChanged?.Invoke(activeAccount);
            errorMessage = string.Empty;
            return true;
        }

        public bool TryLogin(string username, string password, out string errorMessage)
        {
            string trimmed = username != null ? username.Trim() : string.Empty;
            if (string.IsNullOrEmpty(trimmed))
            {
                errorMessage = "Vui lòng nhập tên đăng nhập.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                errorMessage = "Mật khẩu không được để trống.";
                return false;
            }

            UserAccountRecord found = FindAccountByUsername(trimmed);
            if (found == null || !VerifyPassword(password, found.saltBase64, found.passwordHashHex))
            {
                errorMessage = "Tên đăng nhập hoặc mật khẩu không đúng.";
                return false;
            }

            if (HasActiveSession)
            {
                SyncActiveProfileFromPrefs(saveToDisk: false);
            }

            found.lastLoginUtc = DateTime.UtcNow.ToString("o");
            activeAccount = found;

            ApplyActiveProfileToPrefs();
            SaveDatabaseToDisk();
            OnActiveAccountChanged?.Invoke(activeAccount);
            errorMessage = string.Empty;
            return true;
        }

        public void Logout()
        {
            if (activeAccount != null)
            {
                SyncActiveProfileFromPrefs(saveToDisk: true);
            }

            activeAccount = null;
            ApplyActiveProfileToPrefs();
            OnActiveAccountChanged?.Invoke(null);
        }

        public void SyncActiveProfileFromPrefs()
        {
            SyncActiveProfileFromPrefs(saveToDisk: true);
        }

        public void SyncActiveProfileFromPrefs(bool saveToDisk)
        {
            if (activeAccount == null || isApplyingProfile) return;

            if (!isApplicationQuitting && (subscribedCurrencyManager == null || subscribedInventoryManager == null))
            {
                SubscribeSyncEvents();
            }

            int coins = CurrencyManager.HasInstance
                ? Mathf.Max(0, CurrencyManager.Instance.CurrentCoins)
                : Mathf.Max(0, PlayerPrefs.GetInt(CurrencyManager.COINS_PREF_KEY, 0));

            int senzu = InventoryManager.HasInstance
                ? Mathf.Max(0, InventoryManager.Instance.GetOwnedCount(ConsumableItemType.SenzuBean))
                : Mathf.Max(0, PlayerPrefs.GetInt(InventoryManager.OWNED_PREF_PREFIX + ConsumableItemType.SenzuBean, 0));

            int spinal = InventoryManager.HasInstance
                ? Mathf.Max(0, InventoryManager.Instance.GetOwnedCount(ConsumableItemType.SpinalFluid))
                : Mathf.Max(0, PlayerPrefs.GetInt(InventoryManager.OWNED_PREF_PREFIX + ConsumableItemType.SpinalFluid, 0));

            int ultra = InventoryManager.HasInstance
                ? Mathf.Max(0, InventoryManager.Instance.GetOwnedCount(ConsumableItemType.UltraDivineWater))
                : Mathf.Max(0, PlayerPrefs.GetInt(InventoryManager.OWNED_PREF_PREFIX + ConsumableItemType.UltraDivineWater, 0));

            int slotQ = InventoryManager.HasInstance
                ? (int)InventoryManager.Instance.GetEquippedItem(InventoryManager.SLOT_Q)
                : PlayerPrefs.GetInt(InventoryManager.EQUIPPED_SLOT_PREFIX + InventoryManager.SLOT_Q, (int)ConsumableItemType.SenzuBean);

            int slotE = InventoryManager.HasInstance
                ? (int)InventoryManager.Instance.GetEquippedItem(InventoryManager.SLOT_E)
                : PlayerPrefs.GetInt(InventoryManager.EQUIPPED_SLOT_PREFIX + InventoryManager.SLOT_E, (int)ConsumableItemType.SpinalFluid);

            activeAccount.coins = coins;
            activeAccount.senzuCount = senzu;
            activeAccount.spinalFluidCount = spinal;
            activeAccount.ultraWaterCount = ultra;
            activeAccount.equippedSlotQ = ValidateConsumableRaw(slotQ, (int)ConsumableItemType.SenzuBean);
            activeAccount.equippedSlotE = ValidateConsumableRaw(slotE, (int)ConsumableItemType.SpinalFluid);
            activeAccount.unlockedStage = Mathf.Max(1, PlayerPrefs.GetInt(UNLOCKED_STAGE_KEY, 1));
            activeAccount.selectedCharacter = Mathf.Clamp(PlayerPrefs.GetInt(SELECTED_CHAR_KEY, 0), 0, 2);
            activeAccount.selectedWorld = Mathf.Clamp(PlayerPrefs.GetInt(SELECTED_WORLD_KEY, 0), 0, 2);
            activeAccount.selectedStageIndex = Mathf.Max(0, PlayerPrefs.GetInt(SELECTED_STAGE_INDEX_KEY, 0));
            activeAccount.selectedGameMode = Mathf.Clamp(PlayerPrefs.GetInt(SELECTED_GAME_MODE_KEY, 0), 0, 2);

            if (saveToDisk)
            {
                SaveDatabaseToDisk();
            }
        }

        public void ApplyActiveProfileToPrefs()
        {
            isApplyingProfile = true;
            try
            {
                if (activeAccount != null)
                {
                    PlayerPrefs.SetString(ACTIVE_ACCOUNT_PREF_KEY, activeAccount.username);
                    PlayerPrefs.SetInt(CurrencyManager.COINS_PREF_KEY, Mathf.Max(0, activeAccount.coins));
                    PlayerPrefs.SetInt(InventoryManager.OWNED_PREF_PREFIX + ConsumableItemType.SenzuBean, Mathf.Max(0, activeAccount.senzuCount));
                    PlayerPrefs.SetInt(InventoryManager.OWNED_PREF_PREFIX + ConsumableItemType.SpinalFluid, Mathf.Max(0, activeAccount.spinalFluidCount));
                    PlayerPrefs.SetInt(InventoryManager.OWNED_PREF_PREFIX + ConsumableItemType.UltraDivineWater, Mathf.Max(0, activeAccount.ultraWaterCount));
                    PlayerPrefs.SetInt(
                        InventoryManager.EQUIPPED_SLOT_PREFIX + InventoryManager.SLOT_Q,
                        ValidateConsumableRaw(activeAccount.equippedSlotQ, (int)ConsumableItemType.SenzuBean));
                    PlayerPrefs.SetInt(
                        InventoryManager.EQUIPPED_SLOT_PREFIX + InventoryManager.SLOT_E,
                        ValidateConsumableRaw(activeAccount.equippedSlotE, (int)ConsumableItemType.SpinalFluid));
                    PlayerPrefs.SetInt(UNLOCKED_STAGE_KEY, Mathf.Max(1, activeAccount.unlockedStage));
                    PlayerPrefs.SetInt(SELECTED_CHAR_KEY, Mathf.Clamp(activeAccount.selectedCharacter, 0, 2));
                    PlayerPrefs.SetInt(SELECTED_WORLD_KEY, Mathf.Clamp(activeAccount.selectedWorld, 0, 2));
                    PlayerPrefs.SetInt(SELECTED_STAGE_INDEX_KEY, Mathf.Max(0, activeAccount.selectedStageIndex));
                    PlayerPrefs.SetInt(SELECTED_GAME_MODE_KEY, Mathf.Clamp(activeAccount.selectedGameMode, 0, 2));
                }
                else
                {
                    PlayerPrefs.DeleteKey(ACTIVE_ACCOUNT_PREF_KEY);
                    PlayerPrefs.SetInt(CurrencyManager.COINS_PREF_KEY, 0);
                    PlayerPrefs.SetInt(InventoryManager.OWNED_PREF_PREFIX + ConsumableItemType.SenzuBean, 0);
                    PlayerPrefs.SetInt(InventoryManager.OWNED_PREF_PREFIX + ConsumableItemType.SpinalFluid, 0);
                    PlayerPrefs.SetInt(InventoryManager.OWNED_PREF_PREFIX + ConsumableItemType.UltraDivineWater, 0);
                    PlayerPrefs.SetInt(InventoryManager.EQUIPPED_SLOT_PREFIX + InventoryManager.SLOT_Q, (int)ConsumableItemType.SenzuBean);
                    PlayerPrefs.SetInt(InventoryManager.EQUIPPED_SLOT_PREFIX + InventoryManager.SLOT_E, (int)ConsumableItemType.SpinalFluid);
                    PlayerPrefs.SetInt(UNLOCKED_STAGE_KEY, 1);
                    PlayerPrefs.SetInt(SELECTED_CHAR_KEY, 0);
                    PlayerPrefs.SetInt(SELECTED_WORLD_KEY, 0);
                    PlayerPrefs.SetInt(SELECTED_STAGE_INDEX_KEY, 0);
                    PlayerPrefs.SetInt(SELECTED_GAME_MODE_KEY, (int)GameMode.Campaign);
                }

                PlayerPrefs.Save();

                if (!isApplicationQuitting)
                {
                    if (CurrencyManager.Instance != null)
                    {
                        CurrencyManager.Instance.ReloadProfileState();
                    }
                    if (InventoryManager.Instance != null)
                    {
                        InventoryManager.Instance.ReloadProfileState();
                    }
                    SubscribeSyncEvents();
                    StageManager.LoadSelectedGameMode();
                }
            }
            finally
            {
                isApplyingProfile = false;
            }
        }

        private void RestoreSessionFromPrefs()
        {
            if (!PlayerPrefs.HasKey(ACTIVE_ACCOUNT_PREF_KEY))
            {
                activeAccount = null;
                return;
            }

            string savedUsername = PlayerPrefs.GetString(ACTIVE_ACCOUNT_PREF_KEY, string.Empty);
            if (string.IsNullOrWhiteSpace(savedUsername))
            {
                activeAccount = null;
                PlayerPrefs.DeleteKey(ACTIVE_ACCOUNT_PREF_KEY);
                ApplyActiveProfileToPrefs();
                return;
            }

            UserAccountRecord found = FindAccountByUsername(savedUsername.Trim());
            if (found != null)
            {
                activeAccount = found;
                ApplyActiveProfileToPrefs();
            }
            else
            {
                activeAccount = null;
                PlayerPrefs.DeleteKey(ACTIVE_ACCOUNT_PREF_KEY);
                ApplyActiveProfileToPrefs();
            }
        }

        private void SubscribeSyncEvents()
        {
            if (!isSceneLoadedSubscribed)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                SceneManager.sceneLoaded += HandleSceneLoaded;
                isSceneLoadedSubscribed = true;
            }

            if (!isApplicationQuitting)
            {
                CurrencyManager cm = CurrencyManager.HasInstance ? CurrencyManager.Instance : null;
                if (cm != null && subscribedCurrencyManager != cm)
                {
                    if (subscribedCurrencyManager != null)
                    {
                        subscribedCurrencyManager.OnCoinsChanged -= HandleCoinsChanged;
                    }
                    subscribedCurrencyManager = cm;
                    subscribedCurrencyManager.OnCoinsChanged -= HandleCoinsChanged;
                    subscribedCurrencyManager.OnCoinsChanged += HandleCoinsChanged;
                }

                InventoryManager inv = InventoryManager.HasInstance ? InventoryManager.Instance : null;
                if (inv != null && subscribedInventoryManager != inv)
                {
                    if (subscribedInventoryManager != null)
                    {
                        subscribedInventoryManager.OnInventoryChanged -= HandleInventoryChanged;
                    }
                    subscribedInventoryManager = inv;
                    subscribedInventoryManager.OnInventoryChanged -= HandleInventoryChanged;
                    subscribedInventoryManager.OnInventoryChanged += HandleInventoryChanged;
                }
            }
        }

        private void UnsubscribeSyncEvents()
        {
            if (isSceneLoadedSubscribed)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                isSceneLoadedSubscribed = false;
            }

            if (subscribedCurrencyManager != null)
            {
                subscribedCurrencyManager.OnCoinsChanged -= HandleCoinsChanged;
                subscribedCurrencyManager = null;
            }

            if (subscribedInventoryManager != null)
            {
                subscribedInventoryManager.OnInventoryChanged -= HandleInventoryChanged;
                subscribedInventoryManager = null;
            }
        }

        private void HandleCoinsChanged(int _)
        {
            if (!isApplyingProfile && HasActiveSession)
            {
                SyncActiveProfileFromPrefs(saveToDisk: true);
            }
        }

        private void HandleInventoryChanged()
        {
            if (!isApplyingProfile && HasActiveSession)
            {
                SyncActiveProfileFromPrefs(saveToDisk: true);
            }
        }

        private void HandleSceneLoaded(Scene _, LoadSceneMode __)
        {
            SubscribeSyncEvents();
            if (!isApplyingProfile && HasActiveSession)
            {
                SyncActiveProfileFromPrefs(saveToDisk: true);
            }
        }

        private void EnsureDatabaseInitialized()
        {
            if (database == null)
            {
                database = new AccountSaveDatabase();
            }
            if (database.accounts == null)
            {
                database.accounts = new List<UserAccountRecord>();
            }
        }

        private UserAccountRecord FindAccountByUsername(string username)
        {
            EnsureDatabaseInitialized();
            for (int i = 0; i < database.accounts.Count; i++)
            {
                UserAccountRecord rec = database.accounts[i];
                if (rec != null && string.Equals(rec.username, username, StringComparison.OrdinalIgnoreCase))
                {
                    return rec;
                }
            }
            return null;
        }

        private void LoadDatabaseFromDisk()
        {
            string filePath = GetAccountsFilePath();
            if (!File.Exists(filePath))
            {
                database = new AccountSaveDatabase();
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    database = new AccountSaveDatabase();
                    return;
                }

                AccountSaveDatabase loaded = JsonUtility.FromJson<AccountSaveDatabase>(json);
                database = loaded ?? new AccountSaveDatabase();
                EnsureDatabaseInitialized();

                HashSet<string> seenUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                List<UserAccountRecord> validAccounts = new List<UserAccountRecord>(database.accounts.Count);
                for (int i = 0; i < database.accounts.Count; i++)
                {
                    UserAccountRecord rec = database.accounts[i];
                    if (rec == null || string.IsNullOrWhiteSpace(rec.username))
                    {
                        continue;
                    }

                    string trimmedUsername = rec.username.Trim();
                    if (!ValidateUsernameFormat(trimmedUsername, out _) ||
                        string.IsNullOrWhiteSpace(rec.passwordHashHex) ||
                        string.IsNullOrWhiteSpace(rec.saltBase64))
                    {
                        continue;
                    }

                    if (!seenUsernames.Add(trimmedUsername))
                    {
                        continue;
                    }

                    rec.username = trimmedUsername;
                    rec.coins = Mathf.Max(0, rec.coins);
                    rec.senzuCount = Mathf.Max(0, rec.senzuCount);
                    rec.spinalFluidCount = Mathf.Max(0, rec.spinalFluidCount);
                    rec.ultraWaterCount = Mathf.Max(0, rec.ultraWaterCount);
                    rec.equippedSlotQ = ValidateConsumableRaw(rec.equippedSlotQ, (int)ConsumableItemType.SenzuBean);
                    rec.equippedSlotE = ValidateConsumableRaw(rec.equippedSlotE, (int)ConsumableItemType.SpinalFluid);
                    rec.unlockedStage = Mathf.Max(1, rec.unlockedStage);
                    rec.selectedCharacter = Mathf.Clamp(rec.selectedCharacter, 0, 2);
                    rec.selectedWorld = Mathf.Clamp(rec.selectedWorld, 0, 2);
                    rec.selectedStageIndex = Mathf.Max(0, rec.selectedStageIndex);
                    rec.selectedGameMode = Mathf.Clamp(rec.selectedGameMode, 0, 2);
                    validAccounts.Add(rec);
                }
                database.accounts = validAccounts;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AccountManager] Failed to read accounts file: {ex.Message}");
                database = new AccountSaveDatabase();
            }
        }

        private void SaveDatabaseToDisk()
        {
            EnsureDatabaseInitialized();
            string targetPath = GetAccountsFilePath();
            string directory = Path.GetDirectoryName(targetPath);
            string tmpPath = targetPath + ".tmp";
            string bakPath = targetPath + ".bak";

            try
            {
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(database, true);
                File.WriteAllText(tmpPath, json, new UTF8Encoding(false));

                if (File.Exists(targetPath))
                {
                    try
                    {
                        File.Replace(tmpPath, targetPath, bakPath, ignoreMetadataErrors: true);
                    }
                    catch (Exception)
                    {
                        File.Copy(tmpPath, targetPath, overwrite: true);
                    }
                }
                else
                {
                    try
                    {
                        File.Move(tmpPath, targetPath);
                    }
                    catch (Exception)
                    {
                        File.Copy(tmpPath, targetPath, overwrite: true);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AccountManager] Failed to save accounts file: {ex.Message}");
            }
            finally
            {
                TryDeleteFile(tmpPath);
                TryDeleteFile(bakPath);
            }
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // Ignore cleanup error for transient temp/backup files.
            }
        }

        private static string GetAccountsFilePath()
        {
            return Path.Combine(Application.persistentDataPath, ACCOUNTS_FILE_NAME);
        }

        private static bool ValidateUsernameFormat(string trimmedUsername, out string errorMessage)
        {
            if (string.IsNullOrEmpty(trimmedUsername) ||
                trimmedUsername.Length < MIN_USERNAME_LENGTH ||
                trimmedUsername.Length > MAX_USERNAME_LENGTH)
            {
                errorMessage = $"Tên đăng nhập phải từ {MIN_USERNAME_LENGTH} đến {MAX_USERNAME_LENGTH} ký tự.";
                return false;
            }

            for (int i = 0; i < trimmedUsername.Length; i++)
            {
                char c = trimmedUsername[i];
                bool valid =
                    (c >= 'A' && c <= 'Z') ||
                    (c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_' ||
                    c == '.';
                if (!valid)
                {
                    errorMessage = "Tên đăng nhập chỉ được chứa chữ cái, số, '_' hoặc '.'.";
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private static string ComputePasswordHashHex(string saltBase64, string password)
        {
            string payload = (saltBase64 ?? string.Empty) + ":" + (password ?? string.Empty);
            byte[] inputBytes = Encoding.UTF8.GetBytes(payload);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hashBytes = sha.ComputeHash(inputBytes);
                StringBuilder sb = new StringBuilder(hashBytes.Length * 2);
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private static bool VerifyPassword(string password, string saltBase64, string expectedHashHex)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(expectedHashHex))
            {
                return false;
            }

            string computed = ComputePasswordHashHex(saltBase64, password);
            return FixedTimeEqualsHex(computed, expectedHashHex);
        }

        private static bool FixedTimeEqualsHex(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= char.ToLowerInvariant(a[i]) ^ char.ToLowerInvariant(b[i]);
            }
            return diff == 0;
        }

        private static int ValidateConsumableRaw(int raw, int fallback)
        {
            return Enum.IsDefined(typeof(ConsumableItemType), raw) ? raw : fallback;
        }
    }
}
