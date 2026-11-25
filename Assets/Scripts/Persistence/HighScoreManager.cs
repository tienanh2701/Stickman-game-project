using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using StickmanBeatEmUp.Auth;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Rules.Persistence;
using StickmanBeatEmUp.Economy;
using StickmanBeatEmUp.GameModes;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Persistence
{
    /// <summary>
    /// Quản lý bảng xếp hạng Top-10 HighScore bền vững (JSON) và tự ghi nhận điểm sau mỗi trận đấu.
    /// </summary>
    public class HighScoreManager : MonoBehaviour
    {
        private static HighScoreManager instance;
        private static bool isApplicationQuitting;

        private HighScoreService scoreService;
        private LevelManager subscribedLevelManager;
        private float matchStartTime;

        public static bool HasInstance => !isApplicationQuitting && instance != null;

        public static HighScoreManager Instance
        {
            get
            {
                if (isApplicationQuitting) return null;
                if (instance == null)
                {
                    instance = FindAnyObjectByType<HighScoreManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject(nameof(HighScoreManager));
                        instance = go.AddComponent<HighScoreManager>();
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
            TrySubscribeLevelManager();
        }

        private void OnApplicationQuit()
        {
            isApplicationQuitting = true;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeLevelManager();
            if (instance == this)
            {
                instance = null;
            }
        }

        private void EnsureService()
        {
            if (scoreService == null)
            {
                scoreService = new HighScoreService(new FileJsonSaveStorage(), new SystemGameClock(), HighScoreRanker.DEFAULT_TOP_K);
            }
        }

        public List<HighScoreEntryDTO> GetTopScores(int? gameModeFilter = null)
        {
            EnsureService();
            try
            {
                return scoreService.LoadTopScores(gameModeFilter);
            }
            catch (SaveCorruptedException ex)
            {
                Debug.Log($"[HighScoreManager][{ex.ErrorCode}] HighScore file corrupted: {ex.Message}");
                return new List<HighScoreEntryDTO>();
            }
        }

        public void SubmitScore(int gameMode, int worldOrWaveReached, int score, float clearTimeSeconds)
        {
            EnsureService();
            string user = AccountManager.HasInstance && AccountManager.Instance.HasActiveSession
                ? AccountManager.Instance.ActiveAccount.username
                : "Guest";

            HighScoreEntryDTO entry = new HighScoreEntryDTO
            {
                username = user,
                gameMode = gameMode,
                worldOrWaveReached = Mathf.Max(1, worldOrWaveReached),
                score = Mathf.Max(0, score),
                clearTimeSeconds = Mathf.Max(0f, clearTimeSeconds),
                achievedAtUtc = DateTime.UtcNow.ToString("o")
            };

            try
            {
                scoreService.RecordScore(entry);
            }
            catch (SaveCorruptedException ex)
            {
                Debug.Log($"[HighScoreManager][{ex.ErrorCode}] Resetting corrupted highscore board: {ex.Message}");
                new FileJsonSaveStorage().Delete(HighScoreService.HIGHSCORE_STORAGE_KEY);
                scoreService.RecordScore(entry);
            }
        }

        private void HandleSceneLoaded(Scene _, LoadSceneMode __)
        {
            matchStartTime = Time.unscaledTime;
            TrySubscribeLevelManager();
        }

        private void TrySubscribeLevelManager()
        {
            LevelManager current = LevelManager.Instance;
            if (current == subscribedLevelManager) return;

            UnsubscribeLevelManager();
            if (current != null)
            {
                subscribedLevelManager = current;
                subscribedLevelManager.OnStageCompleted += HandleStageCompleted;
                subscribedLevelManager.OnStageFailed += HandleStageFailed;
            }
        }

        private void UnsubscribeLevelManager()
        {
            if (subscribedLevelManager != null)
            {
                subscribedLevelManager.OnStageCompleted -= HandleStageCompleted;
                subscribedLevelManager.OnStageFailed -= HandleStageFailed;
                subscribedLevelManager = null;
            }
        }

        private void HandleStageCompleted()
        {
            RecordFromActiveMatch(victoryBonus: 500);
        }

        private void HandleStageFailed()
        {
            RecordFromActiveMatch(victoryBonus: 0);
        }

        private void RecordFromActiveMatch(int victoryBonus)
        {
            LevelManager lm = LevelManager.Instance;
            if (lm == null || lm.CurrentGameMode == GameMode.Training) return;

            float elapsed = Mathf.Max(1f, Time.unscaledTime - matchStartTime);
            int coins = CurrencyManager.HasInstance ? CurrencyManager.Instance.CurrentCoins : 0;
            int worldOrWave = 1;

            if (lm.CurrentGameMode == GameMode.Endless)
            {
                EndlessModeManager em = FindAnyObjectByType<EndlessModeManager>();
                worldOrWave = em != null ? Mathf.Max(1, em.CurrentWave) : 1;
            }
            else if (lm.CurrentStageData != null)
            {
                worldOrWave = ((int)lm.CurrentStageData.worldType * 4) + Mathf.Max(1, lm.CurrentStageData.stageNumber);
            }

            int score = victoryBonus + (worldOrWave * 250) + (coins * 4);
            SubmitScore((int)lm.CurrentGameMode, worldOrWave, score, elapsed);
        }
    }
}
