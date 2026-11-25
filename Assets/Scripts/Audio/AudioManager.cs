using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.CorePure.Interfaces;
using StickmanBeatEmUp.Economy;
using StickmanBeatEmUp.Items;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Audio
{
    /// <summary>
    /// Quản lý âm thanh toàn cục triển khai IAudioService (DIP) và Observer Pattern.
    /// Tự động đăng ký sự kiện từ LevelManager, BaseCharacter, InputHandler, PlayerItemController, ShopManager
    /// và luôn hủy đăng ký (-=) đối xứng trong OnDisable/OnDestroy để chống rò rỉ bộ nhớ.
    /// </summary>
    public class AudioManager : MonoBehaviour, IAudioService
    {
        public const string MASTER_VOL_KEY = "MDS_Audio_MasterVol";
        public const string BGM_VOL_KEY = "MDS_Audio_BgmVol";
        public const string SFX_VOL_KEY = "MDS_Audio_SfxVol";
        public const string MUTED_KEY = "MDS_Audio_Muted";

        public const string BGM_MENU = "bgm_menu";
        public const string BGM_WORLD1 = "bgm_world1";
        public const string BGM_WORLD2 = "bgm_world2";
        public const string BGM_WORLD3 = "bgm_world3";

        public const string SFX_ATTACK = "sfx_attack";
        public const string SFX_HIT = "sfx_hit";
        public const string SFX_JUMP = "sfx_jump";
        public const string SFX_ITEM_USE = "sfx_item_use";
        public const string SFX_SHOP_BUY = "sfx_shop_buy";
        public const string SFX_UI_CLICK = "sfx_ui_click";

        private static AudioManager instance;
        private static bool isApplicationQuitting;

        [SerializeField] private AudioLibrarySO audioLibrary;

        private AudioSource bgmSource;
        private AudioSource sfxSource;
        private readonly Dictionary<string, AudioClip> bgmCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AudioClip> sfxCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

        private float masterVolume = 1f;
        private float bgmVolume = 0.65f;
        private float sfxVolume = 0.85f;
        private bool isMuted;
        private string currentBgmId = string.Empty;

        private bool isSceneSubscribed;
        private LevelManager subscribedLevelManager;
        private BaseCharacter subscribedPlayer;
        private InputHandler subscribedInput;
        private PlayerItemController subscribedItemController;
        private ShopManager subscribedShopManager;

        public static bool HasInstance => !isApplicationQuitting && instance != null;

        public static AudioManager Instance
        {
            get
            {
                if (isApplicationQuitting) return null;
                if (instance == null)
                {
                    instance = FindAnyObjectByType<AudioManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject(nameof(AudioManager));
                        instance = go.AddComponent<AudioManager>();
                    }
                }
                return instance;
            }
        }

        public float MasterVolume => masterVolume;
        public float BgmVolume => bgmVolume;
        public float SfxVolume => sfxVolume;
        public bool IsMuted => isMuted;
        public string CurrentBgmId => currentBgmId;

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

            EnsureAudioSources();
            LoadLibrary();
            LoadSavedAudioSettings();
            ApplyVolumesToSources();
        }

        private void OnEnable()
        {
            SubscribeGlobalEvents();
            BindActiveScenePublishers();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        private void OnApplicationQuit()
        {
            isApplicationQuitting = true;
        }

        private void OnDestroy()
        {
            UnsubscribeAll();
            if (instance == this)
            {
                instance = null;
            }
        }

        public void PlayBgm(string trackId)
        {
            if (string.IsNullOrWhiteSpace(trackId)) return;
            EnsureAudioSources();

            if (string.Equals(currentBgmId, trackId, StringComparison.OrdinalIgnoreCase) && bgmSource.isPlaying)
            {
                return;
            }

            AudioClip clip = ResolveBgmClip(trackId);
            currentBgmId = trackId;
            if (clip == null) return;

            bgmSource.clip = clip;
            bgmSource.loop = true;
            ApplyVolumesToSources();
            bgmSource.Play();
        }

        public void StopBgm()
        {
            currentBgmId = string.Empty;
            if (bgmSource != null && bgmSource.isPlaying)
            {
                bgmSource.Stop();
            }
        }

        public void PlaySfx(string sfxId)
        {
            if (isMuted || string.IsNullOrWhiteSpace(sfxId)) return;
            EnsureAudioSources();

            AudioClip clip = ResolveSfxClip(sfxId);
            if (clip == null) return;

            float effectiveVolume = Mathf.Clamp01(masterVolume * sfxVolume);
            if (effectiveVolume <= 0.001f) return;

            sfxSource.PlayOneShot(clip, effectiveVolume);
        }

        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MASTER_VOL_KEY, masterVolume);
            PlayerPrefs.Save();
            ApplyVolumesToSources();
        }

        public void SetBgmVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(BGM_VOL_KEY, bgmVolume);
            PlayerPrefs.Save();
            ApplyVolumesToSources();
        }

        public void SetSfxVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(SFX_VOL_KEY, sfxVolume);
            PlayerPrefs.Save();
            ApplyVolumesToSources();
        }

        public void SetMuted(bool muted)
        {
            isMuted = muted;
            PlayerPrefs.SetInt(MUTED_KEY, isMuted ? 1 : 0);
            PlayerPrefs.Save();
            ApplyVolumesToSources();
        }

        private void EnsureAudioSources()
        {
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
                bgmSource.spatialBlend = 0f;
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
                sfxSource.spatialBlend = 0f;
            }
        }

        private void LoadLibrary()
        {
            if (audioLibrary == null)
            {
                audioLibrary = Resources.Load<AudioLibrarySO>("Audio/AudioLibrarySO");
            }
        }

        private void LoadSavedAudioSettings()
        {
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MASTER_VOL_KEY, 1f));
            bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BGM_VOL_KEY, 0.65f));
            sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SFX_VOL_KEY, 0.85f));
            isMuted = PlayerPrefs.GetInt(MUTED_KEY, 0) != 0;
        }

        private void ApplyVolumesToSources()
        {
            if (bgmSource != null)
            {
                bgmSource.mute = isMuted;
                bgmSource.volume = Mathf.Clamp01(masterVolume * bgmVolume);
            }
            if (sfxSource != null)
            {
                sfxSource.mute = isMuted;
                sfxSource.volume = Mathf.Clamp01(masterVolume * sfxVolume);
            }
        }

        private AudioClip ResolveBgmClip(string trackId)
        {
            if (bgmCache.TryGetValue(trackId, out AudioClip cached) && cached != null) return cached;

            AudioClip clip = audioLibrary != null ? audioLibrary.FindBgm(trackId) : null;
#if UNITY_EDITOR
            if (clip == null)
            {
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/BGM/{trackId}.wav")
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/BGM/{trackId}.ogg");
            }
#endif
            if (clip != null)
            {
                bgmCache[trackId] = clip;
            }
            return clip;
        }

        private AudioClip ResolveSfxClip(string sfxId)
        {
            if (sfxCache.TryGetValue(sfxId, out AudioClip cached) && cached != null) return cached;

            AudioClip clip = audioLibrary != null ? audioLibrary.FindSfx(sfxId) : null;
#if UNITY_EDITOR
            if (clip == null)
            {
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/SFX/{sfxId}.wav")
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/SFX/{sfxId}.ogg");
            }
#endif
            if (clip != null)
            {
                sfxCache[sfxId] = clip;
            }
            return clip;
        }

        private void SubscribeGlobalEvents()
        {
            if (!isSceneSubscribed)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                SceneManager.sceneLoaded += HandleSceneLoaded;
                isSceneSubscribed = true;
            }
        }

        private void UnsubscribeAll()
        {
            if (isSceneSubscribed)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                isSceneSubscribed = false;
            }

            UnbindLevelManager();
            UnbindPlayer();
            UnbindShopManager();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode _)
        {
            BindActiveScenePublishers();
            PlaySceneBgm(scene.name);
        }

        private void BindActiveScenePublishers()
        {
            BindLevelManager(LevelManager.Instance != null ? LevelManager.Instance : FindAnyObjectByType<LevelManager>());
            BindShopManager(ShopManager.Instance != null ? ShopManager.Instance : FindAnyObjectByType<ShopManager>());

            BaseCharacter activePlayer = subscribedLevelManager != null && subscribedLevelManager.ActivePlayer != null
                ? subscribedLevelManager.ActivePlayer
                : FindAnyObjectByType<BaseCharacter>();
            BindPlayer(activePlayer);
        }

        private void PlaySceneBgm(string sceneName)
        {
            if (string.Equals(sceneName, "Stickman_DemoScene", StringComparison.OrdinalIgnoreCase))
            {
                int world = Mathf.Clamp(PlayerPrefs.GetInt("SelectedWorld", 0), 0, 2);
                string track = world == 1 ? BGM_WORLD2 : (world == 2 ? BGM_WORLD3 : BGM_WORLD1);
                PlayBgm(track);
            }
            else
            {
                PlayBgm(BGM_MENU);
            }
        }

        private void BindLevelManager(LevelManager lm)
        {
            if (subscribedLevelManager == lm) return;
            UnbindLevelManager();
            subscribedLevelManager = lm;
            if (subscribedLevelManager != null)
            {
                subscribedLevelManager.OnPlayerSpawned += HandlePlayerSpawned;
                subscribedLevelManager.OnStageCompleted += HandleStageCompleted;
                subscribedLevelManager.OnStageFailed += HandleStageFailed;
            }
        }

        private void UnbindLevelManager()
        {
            if (subscribedLevelManager != null)
            {
                subscribedLevelManager.OnPlayerSpawned -= HandlePlayerSpawned;
                subscribedLevelManager.OnStageCompleted -= HandleStageCompleted;
                subscribedLevelManager.OnStageFailed -= HandleStageFailed;
                subscribedLevelManager = null;
            }
        }

        private void BindPlayer(BaseCharacter player)
        {
            if (subscribedPlayer == player) return;
            UnbindPlayer();
            subscribedPlayer = player;
            if (subscribedPlayer == null) return;

            subscribedPlayer.OnEnemyHit += HandleEnemyHit;
            subscribedPlayer.OnTransformedChanged += HandleTransformedChanged;

            subscribedInput = subscribedPlayer.GetComponent<InputHandler>();
            if (subscribedInput != null)
            {
                subscribedInput.OnAttackEvent += HandleAttackInput;
                subscribedInput.OnJumpEvent += HandleJumpInput;
            }

            subscribedItemController = subscribedPlayer.GetComponent<PlayerItemController>();
            if (subscribedItemController != null)
            {
                subscribedItemController.OnItemActivated += HandleItemActivated;
            }
        }

        private void UnbindPlayer()
        {
            if (subscribedPlayer != null)
            {
                subscribedPlayer.OnEnemyHit -= HandleEnemyHit;
                subscribedPlayer.OnTransformedChanged -= HandleTransformedChanged;
                subscribedPlayer = null;
            }
            if (subscribedInput != null)
            {
                subscribedInput.OnAttackEvent -= HandleAttackInput;
                subscribedInput.OnJumpEvent -= HandleJumpInput;
                subscribedInput = null;
            }
            if (subscribedItemController != null)
            {
                subscribedItemController.OnItemActivated -= HandleItemActivated;
                subscribedItemController = null;
            }
        }

        private void BindShopManager(ShopManager shop)
        {
            if (subscribedShopManager == shop) return;
            UnbindShopManager();
            subscribedShopManager = shop;
            if (subscribedShopManager != null)
            {
                subscribedShopManager.OnPurchaseResult += HandlePurchaseResult;
            }
        }

        private void UnbindShopManager()
        {
            if (subscribedShopManager != null)
            {
                subscribedShopManager.OnPurchaseResult -= HandlePurchaseResult;
                subscribedShopManager = null;
            }
        }

        private void HandlePlayerSpawned(BaseCharacter player) => BindPlayer(player);
        private void HandleAttackInput() => PlaySfx(SFX_ATTACK);
        private void HandleJumpInput() => PlaySfx(SFX_JUMP);
        private void HandleEnemyHit(AttackType _, IDamageable __) => PlaySfx(SFX_HIT);
        private void HandleTransformedChanged(bool transformed) { if (transformed) PlaySfx(SFX_ITEM_USE); }
        private void HandleItemActivated(int _, ConsumableItemType __) => PlaySfx(SFX_ITEM_USE);
        private void HandleStageCompleted() => PlaySfx(SFX_SHOP_BUY);
        private void HandleStageFailed() => PlaySfx(SFX_HIT);

        private void HandlePurchaseResult(ConsumableItemType _, bool succeeded, string __)
        {
            PlaySfx(succeeded ? SFX_SHOP_BUY : SFX_UI_CLICK);
        }
    }
}
