using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Audio
{
    public enum UiSfxType
    {
        Click = 0,
        Hover = 1,
        Confirm = 2,
        Back = 3,
        Error = 4
    }

    /// <summary>
    /// Persistent audio hub for BGM (Menu + 3 Worlds stage/boss) and combat/UI SFX,
    /// synchronized in real time with GameSettingsManager volumes & mutes.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private const float DefaultFadeSeconds = 0.45f;

        private static AudioManager instance;
        private static bool isQuitting;

        public static bool HasInstance => instance != null;

        public static AudioManager Instance
        {
            get
            {
                if (instance == null && Application.isPlaying && !isQuitting)
                {
                    instance = FindAnyObjectByType<AudioManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("AudioManager");
                        DontDestroyOnLoad(go);
                        instance = go.AddComponent<AudioManager>();
                    }
                }
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            isQuitting = false;
        }

        [Header("BGM Clips (Optional Inspector Assignment / Resources Fallback)")]
        [SerializeField] private AudioClip menuBgm;
        [SerializeField] private AudioClip world1StageBgm;
        [SerializeField] private AudioClip world1BossBgm;
        [SerializeField] private AudioClip world2StageBgm;
        [SerializeField] private AudioClip world2BossBgm;
        [SerializeField] private AudioClip world3StageBgm;
        [SerializeField] private AudioClip world3BossBgm;

        [Header("UI & Test SFX")]
        [SerializeField] private AudioClip uiClickSfx;
        [SerializeField] private AudioClip testSfxClip;

        private AudioSource bgmSource;
        private AudioSource sfxSource;
        private AudioSource uiSfxSource;

        private readonly Dictionary<string, AudioClip> cachedClips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> lastSfxPlayTimes = new Dictionary<string, float>();
        private AudioClip proceduralPingClip;
        private AudioClip proceduralUiClickClip;
        private Coroutine bgmFadeCoroutine;
        private float currentBgmMixFactor = 1f;
        private bool isSettingsSubscribed;

        public AudioClip CurrentBgmClip => bgmSource != null ? bgmSource.clip : null;
        public bool IsBgmPlaying => bgmSource != null && bgmSource.isPlaying;
        public float CurrentBgmVolume => bgmSource != null ? bgmSource.volume : 0f;
        public bool IsBgmMuted => bgmSource != null && bgmSource.mute;
        public bool IsSfxMuted => sfxSource != null && sfxSource.mute;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (transform.parent == null && Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsureAudioSources();
            SubscribeSettings();
            RefreshVolumes();
        }

        private void OnEnable()
        {
            SubscribeSettings();
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
        }

        private void OnDestroy()
        {
            UnsubscribeSettings();
            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }
            if (proceduralPingClip != null)
            {
                Destroy(proceduralPingClip);
                proceduralPingClip = null;
            }
            if (proceduralUiClickClip != null)
            {
                Destroy(proceduralUiClickClip);
                proceduralUiClickClip = null;
            }
            lastSfxPlayTimes.Clear();
            cachedClips.Clear();
            if (instance == this)
            {
                instance = null;
            }
        }

        private void EnsureAudioSources()
        {
            if (bgmSource == null)
            {
                bgmSource = CreateChannelSource("BGM_Channel", loop: true, ignorePause: true);
            }
            if (sfxSource == null)
            {
                sfxSource = CreateChannelSource("SFX_Channel", loop: false, ignorePause: false);
            }
            if (uiSfxSource == null)
            {
                uiSfxSource = CreateChannelSource("UI_SFX_Channel", loop: false, ignorePause: true);
            }
        }

        private AudioSource CreateChannelSource(string childName, bool loop, bool ignorePause)
        {
            Transform child = transform.Find(childName);
            GameObject obj = child != null ? child.gameObject : new GameObject(childName);
            if (child == null)
            {
                obj.transform.SetParent(transform, false);
            }

            AudioSource src = obj.GetComponent<AudioSource>();
            if (src == null)
            {
                src = obj.AddComponent<AudioSource>();
            }
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;
            src.ignoreListenerPause = ignorePause;
            return src;
        }

        private void SubscribeSettings()
        {
            if (isSettingsSubscribed || !Application.isPlaying) return;

            GameSettingsManager settings = GameSettingsManager.Instance;
            if (settings == null) return;

            settings.OnAudioSettingsChanged -= RefreshVolumes;
            settings.OnAudioSettingsChanged += RefreshVolumes;
            isSettingsSubscribed = true;
        }

        private void UnsubscribeSettings()
        {
            if (!isSettingsSubscribed) return;

            if (GameSettingsManager.HasInstance)
            {
                GameSettingsManager.Instance.OnAudioSettingsChanged -= RefreshVolumes;
            }
            isSettingsSubscribed = false;
        }

        public void RefreshVolumes()
        {
            EnsureAudioSources();
            float effectiveBgm = GetEffectiveBgmVolume();
            bgmSource.volume = effectiveBgm * currentBgmMixFactor;
            bgmSource.mute = effectiveBgm <= 0.0001f;

            float effectiveSfx = GetEffectiveSfxVolume();
            bool muteSfx = effectiveSfx <= 0.0001f;
            sfxSource.mute = muteSfx;
            uiSfxSource.mute = muteSfx;
        }

        public void PlayMenuBgm()
        {
            PlayMenuBgm(false);
        }

        public void PlayMenuBgm(bool immediate)
        {
            AudioClip clip = menuBgm != null ? menuBgm : LoadCachedClip("Audio/BGM/bgm_menu");
            if (clip == null)
            {
                clip = world1StageBgm != null ? world1StageBgm : LoadCachedClip("Audio/BGM/bgm_world1_aot_stage");
            }
            TransitionBgm(clip, immediate);
        }

        public void PlayWorldBgm(WorldType world, bool isBossStage)
        {
            PlayWorldBgm(world, isBossStage, false);
        }

        public void PlayWorldBgm(WorldType world, bool isBossStage, bool immediate)
        {
            AudioClip clip = ResolveWorldBgmClip(world, isBossStage);
            TransitionBgm(clip, immediate);
        }

        public void StopBgm()
        {
            TransitionBgm(null, true);
        }

        public AudioClip ResolveClip(string resourcePath)
        {
            return LoadCachedClip(resourcePath);
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            EnsureAudioSources();

            float vol = GetEffectiveSfxVolume() * Mathf.Clamp01(volumeScale);
            if (vol <= 0.0001f) return;

            sfxSource.PlayOneShot(clip, vol);
        }

        public void PlaySfxByResourceName(string resourceSubPath, float volumeScale = 1f)
        {
            PlaySfxByResourceName(resourceSubPath, volumeScale, 0f);
        }

        public void PlaySfxByResourceName(string resourceSubPath, float volumeScale, float minInterval)
        {
            if (string.IsNullOrEmpty(resourceSubPath)) return;
            string path = resourceSubPath.StartsWith("Audio/") ? resourceSubPath : "Audio/SFX/" + resourceSubPath;

            if (minInterval > 0f)
            {
                float now = Time.unscaledTime;
                if (lastSfxPlayTimes.TryGetValue(path, out float lastTime) && (now - lastTime) < minInterval)
                {
                    return;
                }
                lastSfxPlayTimes[path] = now;
            }

            AudioClip clip = LoadCachedClip(path);
            if (clip != null)
            {
                PlaySfx(clip, volumeScale);
            }
        }

        public void PlayUiSfx(UiSfxType type, float volumeScale = 1f)
        {
            if (!Application.isPlaying || isQuitting) return;

            string resourcePath;
            float minInterval;
            switch (type)
            {
                case UiSfxType.Hover:
                    resourcePath = "Audio/SFX/UI/ui_hover";
                    minInterval = 0.045f;
                    break;
                case UiSfxType.Confirm:
                    resourcePath = "Audio/SFX/UI/ui_confirm";
                    minInterval = 0.05f;
                    break;
                case UiSfxType.Back:
                    resourcePath = "Audio/SFX/UI/ui_back";
                    minInterval = 0.05f;
                    break;
                case UiSfxType.Error:
                    resourcePath = "Audio/SFX/UI/ui_error";
                    minInterval = 0.09f;
                    break;
                default:
                    resourcePath = "Audio/SFX/UI/ui_click";
                    minInterval = 0.04f;
                    break;
            }

            float now = Time.unscaledTime;
            if (lastSfxPlayTimes.TryGetValue(resourcePath, out float lastTime) && (now - lastTime) < minInterval)
            {
                return;
            }
            lastSfxPlayTimes[resourcePath] = now;

            AudioClip target = LoadCachedClip(resourcePath);
            if (target == null && type != UiSfxType.Error)
            {
                target = uiClickSfx != null ? uiClickSfx : GetOrCreateProceduralUiClickClip();
            }
            if (target == null) return;

            EnsureAudioSources();
            float vol = GetEffectiveSfxVolume() * Mathf.Clamp01(volumeScale);
            if (vol <= 0.0001f) return;

            uiSfxSource.PlayOneShot(target, vol);
        }

        public void PlayUiSfx(AudioClip clip = null, float volumeScale = 1f)
        {
            AudioClip target = clip != null ? clip : uiClickSfx;
            if (target == null)
            {
                target = LoadCachedClip("Audio/SFX/UI/ui_click") ?? GetOrCreateProceduralUiClickClip();
            }
            if (target == null) return;
            EnsureAudioSources();

            float vol = GetEffectiveSfxVolume() * Mathf.Clamp01(volumeScale);
            if (vol <= 0.0001f) return;

            uiSfxSource.PlayOneShot(target, vol);
        }

        public void PlayTestSfx()
        {
            EnsureAudioSources();
            float vol = GetEffectiveSfxVolume();
            if (vol <= 0.0001f) return;

            float now = Time.unscaledTime;
            if (lastSfxPlayTimes.TryGetValue("__UI_TEST_PING__", out float lastTime) && (now - lastTime) < 0.06f)
            {
                return;
            }
            lastSfxPlayTimes["__UI_TEST_PING__"] = now;

            AudioClip clip = testSfxClip != null ? testSfxClip : LoadCachedClip("Audio/SFX/UI/ui_confirm");
            if (clip == null)
            {
                clip = uiClickSfx != null ? uiClickSfx : GetOrCreateProceduralPingClip();
            }
            if (clip != null)
            {
                uiSfxSource.PlayOneShot(clip, vol);
            }
        }

        private AudioClip ResolveWorldBgmClip(WorldType world, bool isBossStage)
        {
            switch (world)
            {
                case WorldType.World2_Wasteland:
                    if (isBossStage)
                    {
                        return world2BossBgm != null
                            ? world2BossBgm
                            : LoadCachedClip("Audio/BGM/bgm_world2_db_boss") ?? LoadCachedClip("Audio/BGM/bgm_world2_db_stage");
                    }
                    return world2StageBgm != null
                        ? world2StageBgm
                        : LoadCachedClip("Audio/BGM/bgm_world2_db_stage");

                case WorldType.World3_NeoCity:
                    if (isBossStage)
                    {
                        return world3BossBgm != null
                            ? world3BossBgm
                            : LoadCachedClip("Audio/BGM/bgm_world3_diend_boss") ?? LoadCachedClip("Audio/BGM/bgm_world3_diend_stage");
                    }
                    return world3StageBgm != null
                        ? world3StageBgm
                        : LoadCachedClip("Audio/BGM/bgm_world3_diend_stage");

                default:
                    if (isBossStage)
                    {
                        return world1BossBgm != null
                            ? world1BossBgm
                            : LoadCachedClip("Audio/BGM/bgm_world1_aot_boss") ?? LoadCachedClip("Audio/BGM/bgm_world1_aot_stage");
                    }
                    return world1StageBgm != null
                        ? world1StageBgm
                        : LoadCachedClip("Audio/BGM/bgm_world1_aot_stage");
            }
        }

        private AudioClip LoadCachedClip(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            if (cachedClips.TryGetValue(resourcePath, out AudioClip cached))
            {
                return cached;
            }

            AudioClip loaded = Resources.Load<AudioClip>(resourcePath);
            cachedClips[resourcePath] = loaded;
            return loaded;
        }

        private void TransitionBgm(AudioClip nextClip, bool immediate = false)
        {
            EnsureAudioSources();

            if (nextClip == null)
            {
                if (bgmFadeCoroutine != null)
                {
                    StopCoroutine(bgmFadeCoroutine);
                    bgmFadeCoroutine = null;
                }
                bgmSource.Stop();
                bgmSource.clip = null;
                return;
            }

            if (bgmSource.clip == nextClip && bgmSource.isPlaying)
            {
                RefreshVolumes();
                return;
            }

            if (immediate || !gameObject.activeInHierarchy)
            {
                if (bgmFadeCoroutine != null)
                {
                    StopCoroutine(bgmFadeCoroutine);
                    bgmFadeCoroutine = null;
                }
                bgmSource.Stop();
                bgmSource.clip = nextClip;
                currentBgmMixFactor = 1f;
                RefreshVolumes();
                bgmSource.Play();
                return;
            }

            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
            }
            bgmFadeCoroutine = StartCoroutine(CrossfadeBgmRoutine(nextClip));
        }

        private IEnumerator CrossfadeBgmRoutine(AudioClip nextClip)
        {
            float halfDuration = DefaultFadeSeconds * 0.5f;

            if (bgmSource.isPlaying)
            {
                float startFactor = currentBgmMixFactor;
                float elapsed = 0f;
                while (elapsed < halfDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    currentBgmMixFactor = Mathf.Lerp(startFactor, 0f, Mathf.Clamp01(elapsed / halfDuration));
                    bgmSource.volume = GetEffectiveBgmVolume() * currentBgmMixFactor;
                    yield return null;
                }
                bgmSource.Stop();
            }

            bgmSource.clip = nextClip;
            currentBgmMixFactor = 0f;
            bgmSource.volume = 0f;
            bgmSource.mute = GetEffectiveBgmVolume() <= 0.0001f;
            bgmSource.Play();

            float fadeInElapsed = 0f;
            while (fadeInElapsed < halfDuration)
            {
                fadeInElapsed += Time.unscaledDeltaTime;
                currentBgmMixFactor = Mathf.Clamp01(fadeInElapsed / halfDuration);
                bgmSource.volume = GetEffectiveBgmVolume() * currentBgmMixFactor;
                yield return null;
            }

            currentBgmMixFactor = 1f;
            RefreshVolumes();
            bgmFadeCoroutine = null;
        }

        private AudioClip GetOrCreateProceduralUiClickClip()
        {
            if (proceduralUiClickClip != null) return proceduralUiClickClip;

            const int sampleRate = 48000;
            int sampleCount = Mathf.RoundToInt(sampleRate * 0.028f);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01(t / 0.003f) * Mathf.Exp(-t * 95f);
                float freq = Mathf.Lerp(1050f, 1480f, (float)i / sampleCount);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.20f;
            }

            proceduralUiClickClip = AudioClip.Create("UiSoftClick", sampleCount, 1, sampleRate, false);
            proceduralUiClickClip.SetData(samples, 0);
            return proceduralUiClickClip;
        }

        private AudioClip GetOrCreateProceduralPingClip()
        {
            if (proceduralPingClip != null) return proceduralPingClip;

            const int sampleRate = 48000;
            int sampleCount = Mathf.RoundToInt(sampleRate * 0.09f);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01(t / 0.008f) * Mathf.Exp(-t * 32f);
                float freq = Mathf.Lerp(660f, 920f, (float)i / sampleCount);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.35f;
            }

            proceduralPingClip = AudioClip.Create("SettingsTestPing", sampleCount, 1, sampleRate, false);
            proceduralPingClip.SetData(samples, 0);
            return proceduralPingClip;
        }

        private static float GetEffectiveBgmVolume()
        {
            if (GameSettingsManager.HasInstance)
            {
                return GameSettingsManager.Instance.EffectiveBgmVolume;
            }
            return 0.70f;
        }

        private static float GetEffectiveSfxVolume()
        {
            if (GameSettingsManager.HasInstance)
            {
                return GameSettingsManager.Instance.EffectiveSfxVolume;
            }
            return 1f;
        }
    }
}
