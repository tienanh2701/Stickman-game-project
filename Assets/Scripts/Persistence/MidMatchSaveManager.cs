using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using StickmanBeatEmUp.Auth;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Rules.Persistence;
using StickmanBeatEmUp.Economy;
using StickmanBeatEmUp.Enemies;
using StickmanBeatEmUp.GameModes;
using StickmanBeatEmUp.Items;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Persistence
{
    /// <summary>
    /// Quản lý lưu và khôi phục 100% trạng thái giữa trận (GameStateDTO) xuyên phiên thoát app -> mở lại.
    /// KHÔNG sửa BaseCharacter.cs — sử dụng 100% public hooks của TV1 (Heal, TakeDamage+CleanseDebuffs, RageManager.RuleEngine.SetRageExact, SetStatSource).
    /// </summary>
    public class MidMatchSaveManager : MonoBehaviour
    {
        private const string BATTLE_SCENE_NAME = "Stickman_DemoScene";

        private static MidMatchSaveManager instance;
        private static bool isApplicationQuitting;

        private MidMatchSaveService saveService;
        private GameStateDTO pendingRestoreState;
        private float matchStartUnscaledTime;

        public static bool HasInstance => !isApplicationQuitting && instance != null;

        public static MidMatchSaveManager Instance
        {
            get
            {
                if (isApplicationQuitting) return null;
                if (instance == null)
                {
                    instance = FindAnyObjectByType<MidMatchSaveManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject(nameof(MidMatchSaveManager));
                        instance = go.AddComponent<MidMatchSaveManager>();
                    }
                }
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            isApplicationQuitting = false;
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
            EnsureService();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            matchStartUnscaledTime = Time.unscaledTime;
        }

        private void OnApplicationQuit()
        {
            isApplicationQuitting = true;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (instance == this)
            {
                instance = null;
            }
        }

        private void EnsureService()
        {
            if (saveService == null)
            {
                saveService = new MidMatchSaveService(new FileJsonSaveStorage(), new SystemGameClock());
            }
        }

        public string GetActiveSlotKey()
        {
            string user = AccountManager.HasInstance && AccountManager.Instance.HasActiveSession
                ? AccountManager.Instance.ActiveAccount.username
                : "guest";
            return "midmatch_" + user;
        }

        public bool HasSavedMatch()
        {
            EnsureService();
            return saveService.HasSave(GetActiveSlotKey());
        }

        public bool SaveCurrentMatch(out string feedbackMessage)
        {
            EnsureService();
            LevelManager levelMgr = LevelManager.Instance;
            BaseCharacter player = levelMgr != null && levelMgr.ActivePlayer != null
                ? levelMgr.ActivePlayer
                : FindAnyObjectByType<BaseCharacter>();

            if (player == null || player.IsDead)
            {
                feedbackMessage = "Không thể lưu khi nhân vật chưa sẵn sàng hoặc đã ngã xuống!";
                return false;
            }

            try
            {
                GameStateDTO dto = CaptureCurrentGameState(levelMgr, player);
                saveService.SaveState(dto, GetActiveSlotKey());
                feedbackMessage = $"Đã lưu trận đấu! (HP {dto.playerHp:0}/{dto.playerMaxHp:0}, Xu {dto.coins})";
                return true;
            }
            catch (StickmanGameException ex)
            {
                Debug.Log($"[MidMatchSaveManager][{ex.ErrorCode}] Save failed: {ex.Message}");
                feedbackMessage = $"Lỗi lưu trận [{ex.ErrorCode}]: {ex.Message}";
                return false;
            }
        }

        public bool TryLoadSavedMatch(out string feedbackMessage)
        {
            EnsureService();
            string key = GetActiveSlotKey();
            if (!saveService.HasSave(key))
            {
                feedbackMessage = "Chưa có bản lưu trận đấu nào!";
                return false;
            }

            try
            {
                GameStateDTO loaded = saveService.LoadState(key);
                ApplyPreSceneSelectionPrefs(loaded);
                pendingRestoreState = loaded;
                Time.timeScale = 1f;
                SceneManager.LoadScene(BATTLE_SCENE_NAME);
                feedbackMessage = "Đang khôi phục trận đấu đã lưu...";
                return true;
            }
            catch (SaveCorruptedException ex)
            {
                Debug.Log($"[MidMatchSaveManager][{ex.ErrorCode}] Key='{ex.StorageKey}': {ex.Message}");
                feedbackMessage = "Tệp lưu trận bị hỏng hoặc sai định dạng!";
                return false;
            }
        }

        public GameStateDTO LoadStateOrThrow()
        {
            EnsureService();
            return saveService.LoadState(GetActiveSlotKey());
        }

        public void DeleteSavedMatch()
        {
            EnsureService();
            saveService.DeleteSave(GetActiveSlotKey());
        }

        private GameStateDTO CaptureCurrentGameState(LevelManager levelMgr, BaseCharacter player)
        {
            StageDataSO stageData = levelMgr != null ? levelMgr.CurrentStageData : null;
            EndlessModeManager endlessMgr = FindAnyObjectByType<EndlessModeManager>();
            PlayerItemController itemCtrl = player.GetComponent<PlayerItemController>();
            InventoryManager inv = InventoryManager.Instance;
            CurrencyManager cm = CurrencyManager.Instance;

            int worldIndex = stageData != null ? ((int)stageData.worldType + 1) : (PlayerPrefs.GetInt("SelectedWorld", 0) + 1);
            int stageIndex = stageData != null ? Mathf.Max(1, stageData.stageNumber) : 1;
            int waveIndex = endlessMgr != null && endlessMgr.IsRunning ? Mathf.Max(1, endlessMgr.CurrentWave) : 1;

            List<ItemCountEntryDTO> extraItems = new List<ItemCountEntryDTO>();
            if (inv != null)
            {
                extraItems.Add(new ItemCountEntryDTO
                {
                    itemTypeId = (int)ConsumableItemType.RageElixir,
                    count = inv.GetOwnedCount(ConsumableItemType.RageElixir)
                });
            }

            List<EnemySnapshotDTO> enemySnapshots = new List<EnemySnapshotDTO>();
            EnemyBase[] enemies = FindObjectsByType<EnemyBase>();
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyBase e = enemies[i];
                if (e == null || e.IsDead) continue;
                enemySnapshots.Add(new EnemySnapshotDTO
                {
                    enemyId = e.Data != null ? e.Data.enemyName : e.gameObject.name.Replace("(Clone)", string.Empty).Trim(),
                    hp = e.CurrentHealth,
                    maxHp = e.MaxHealth,
                    posX = e.transform.position.x,
                    posY = e.transform.position.y,
                    phase = 1,
                    isEnraged = e.IsEndlessEnraged
                });
            }

            BossBase[] bosses = FindObjectsByType<BossBase>();
            for (int i = 0; i < bosses.Length; i++)
            {
                BossBase b = bosses[i];
                if (b == null || b.IsDead) continue;
                enemySnapshots.Add(new EnemySnapshotDTO
                {
                    enemyId = b.BossName,
                    hp = b.CurrentHealth,
                    maxHp = b.MaxHealth,
                    posX = b.transform.position.x,
                    posY = b.transform.position.y,
                    phase = b.CurrentHealth <= b.MaxHealth * 0.5f ? 2 : 1,
                    isEnraged = b.IsEndlessEnraged
                });
            }

            int coins = cm != null ? cm.CurrentCoins : 0;
            int score = (worldIndex * 1000) + (stageIndex * 250) + (waveIndex * 150) + (coins * 5);

            return new GameStateDTO
            {
                schemaVersion = 1,
                accountUsername = AccountManager.HasInstance && AccountManager.Instance.HasActiveSession
                    ? AccountManager.Instance.ActiveAccount.username
                    : "Guest",
                gameMode = (int)(levelMgr != null ? levelMgr.CurrentGameMode : StageManager.CurrentGameMode),
                worldIndex = Mathf.Max(1, worldIndex),
                stageIndex = Mathf.Max(1, stageIndex),
                currentWaveIndex = Mathf.Max(1, waveIndex),
                playerCharacterIndex = Mathf.Clamp(PlayerPrefs.GetInt("SelectedCharacter", 0), 0, 2),
                playerHp = Mathf.Clamp(player.CurrentHealth, 0f, player.MaxHealth),
                playerMaxHp = Mathf.Max(1f, player.MaxHealth),
                playerPosX = player.transform.position.x,
                playerPosY = player.transform.position.y,
                playerRage = player.RageManager != null ? player.RageManager.CurrentRageFloat : 0f,
                isTransformed = player.IsTransformed,
                virtualShieldHp = Mathf.Max(0f, player.VirtualShieldHP),
                activeEnemies = enemySnapshots,
                coins = coins,
                senzuCount = inv != null ? inv.GetOwnedCount(ConsumableItemType.SenzuBean) : 0,
                spinalFluidCount = inv != null ? inv.GetOwnedCount(ConsumableItemType.SpinalFluid) : 0,
                ultraWaterCount = inv != null ? inv.GetOwnedCount(ConsumableItemType.UltraDivineWater) : 0,
                extraItemCounts = extraItems,
                equippedSlotQ = inv != null ? (int)inv.GetEquippedItem(InventoryManager.SLOT_Q) : (int)ConsumableItemType.SenzuBean,
                equippedSlotE = inv != null ? (int)inv.GetEquippedItem(InventoryManager.SLOT_E) : (int)ConsumableItemType.SpinalFluid,
                activeBuffs = itemCtrl != null ? itemCtrl.ExportActiveBuffSnapshots() : new List<BuffSnapshotDTO>(),
                score = Mathf.Max(0, score),
                elapsedSeconds = Mathf.Max(0f, Time.unscaledTime - matchStartUnscaledTime),
                savedAtUtc = DateTime.UtcNow.ToString("o")
            };
        }

        private static void ApplyPreSceneSelectionPrefs(GameStateDTO dto)
        {
            GameMode mode = Enum.IsDefined(typeof(GameMode), dto.gameMode) ? (GameMode)dto.gameMode : GameMode.Campaign;
            StageManager.SetSelectedGameMode(mode);

            int worldZeroBased = Mathf.Clamp(dto.worldIndex - 1, 0, 2);
            int stageZeroBased = Mathf.Clamp(dto.stageIndex - 1, 0, 3);
            int globalStageIndex = (worldZeroBased * 4) + stageZeroBased;

            PlayerPrefs.SetInt("SelectedCharacter", Mathf.Clamp(dto.playerCharacterIndex, 0, 2));
            PlayerPrefs.SetInt("SelectedWorld", worldZeroBased);
            PlayerPrefs.SetInt("SelectedStageIndex", globalStageIndex);
            PlayerPrefs.Save();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode _)
        {
            matchStartUnscaledTime = Time.unscaledTime;
            if (pendingRestoreState == null) return;
            if (!string.Equals(scene.name, BATTLE_SCENE_NAME, StringComparison.OrdinalIgnoreCase)) return;

            GameStateDTO toRestore = pendingRestoreState;
            pendingRestoreState = null;
            StartCoroutine(ApplyRestoredStateNextFrame(toRestore));
        }

        private IEnumerator ApplyRestoredStateNextFrame(GameStateDTO dto)
        {
            yield return null;
            yield return null;

            CurrencyManager cm = CurrencyManager.Instance;
            if (cm != null)
            {
                int diff = dto.coins - cm.CurrentCoins;
                if (diff > 0) cm.AddCoins(diff);
                else if (diff < 0) cm.TrySpendCoins(-diff);
            }

            InventoryManager inv = InventoryManager.Instance;
            if (inv != null)
            {
                inv.ApplySnapshot(dto);
            }

            LevelManager levelMgr = LevelManager.Instance;
            BaseCharacter player = levelMgr != null && levelMgr.ActivePlayer != null
                ? levelMgr.ActivePlayer
                : FindAnyObjectByType<BaseCharacter>();

            if (player == null) yield break;

            player.transform.position = new Vector3(dto.playerPosX, dto.playerPosY, 0f);

            // Khôi phục chính xác HP qua public API BaseCharacter (không sửa BaseCharacter.cs)
            float targetHp = Mathf.Clamp(dto.playerHp, 1f, player.MaxHealth);
            if (targetHp > player.CurrentHealth)
            {
                player.Heal(targetHp - player.CurrentHealth);
            }
            else if (targetHp < player.CurrentHealth)
            {
                float delta = player.CurrentHealth - targetHp;
                player.TakeDamage(new DamageData(delta, Vector2.zero, AttackType.NormalAttack_J, false, null));
                player.CleanseDebuffs();
            }

            if (player.RageManager != null)
            {
                player.RageManager.RuleEngine.SetRageExact(dto.playerRage, notify: true);
            }

            PlayerItemController itemCtrl = PlayerItemController.EnsureAttached(player);
            if (itemCtrl != null)
            {
                itemCtrl.RestoreActiveBuffSnapshots(dto.activeBuffs, dto.virtualShieldHp);
            }

            Debug.Log($"[MidMatchSaveManager] Restored match state (W{dto.worldIndex}-S{dto.stageIndex} Wave {dto.currentWaveIndex}, HP {player.CurrentHealth:0}, Rage {dto.playerRage:0.0}, Coins {dto.coins}).");
        }
    }
}
