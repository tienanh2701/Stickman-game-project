using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using StickmanBeatEmUp.Core;

namespace StickmanGame.VFX
{
    /// <summary>
    /// Object-pooled 2D hit-spark VFX manager (pool size = 20, 24 FPS over 0.16s).
    /// Safe across Editor, Standalone Player builds, and Domain Reload disabled Play Mode.
    /// </summary>
    public class HitSparkManager : MonoBehaviour
    {
        public static readonly Color DefaultSparkColor = Color.white;
        public static readonly Color ShieldBlockCyanColor = new Color(0.15f, 0.96f, 1f, 1f);

        private const int PoolSize = 20;
        private const float SparkFps = 24f;
        private const float SparkDuration = 0.16f;
        private const int SparkSortingOrder = 65;

        [SerializeField] private Sprite[] sparkFrames;
        [SerializeField] private int sortingOrder = SparkSortingOrder;

        private struct SparkSlot
        {
            public GameObject gameObject;
            public Transform transform;
            public SpriteRenderer renderer;
            public bool active;
            public float elapsed;
            public Color baseColor;
            public Vector3 baseScale;
            public int lastFrameIndex;
        }

        private static HitSparkManager instance;
        private static bool isQuitting;
        private static Sprite[] cachedFrames;
        private static readonly List<Texture2D> runtimeTextures = new List<Texture2D>();

        private SparkSlot[] pool;
        private int nextPoolIndex;
        private bool poolInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            isQuitting = false;
            cachedFrames = null;
            runtimeTextures.Clear();
        }

        /// <summary>
        /// Ensures an active runtime HitSparkManager singleton exists in Play Mode without leaking Edit-Mode objects.
        /// </summary>
        public static HitSparkManager EnsureInstance()
        {
            if (!Application.isPlaying || isQuitting)
            {
                return null;
            }

            if (instance != null)
            {
                instance.EnsureInitialized();
                return instance;
            }

            instance = FindAnyObjectByType<HitSparkManager>();
            if (instance != null)
            {
                instance.EnsureInitialized();
                return instance;
            }

            GameObject managerObj = new GameObject("HitSparkManager");
            DontDestroyOnLoad(managerObj);
            instance = managerObj.AddComponent<HitSparkManager>();
            instance.EnsureInitialized();
            return instance;
        }

        /// <summary>
        /// Spawns a default hit spark at the given world position.
        /// </summary>
        public static void Spawn(Vector3 hitPoint)
        {
            Spawn(hitPoint, DefaultSparkColor, 1f);
        }

        /// <summary>
        /// Spawns a tinted and scaled hit spark at the given world position.
        /// </summary>
        public static void Spawn(Vector3 hitPoint, Color tint, float scale = 1f)
        {
            if (GameSettingsManager.HasInstance && !GameSettingsManager.Instance.ShowHitSparks) return;
            HitSparkManager mgr = EnsureInstance();
            if (mgr == null) return;
            mgr.SpawnInternal(hitPoint, tint, scale);
        }

        /// <summary>
        /// Computes a world-space impact point using target collider/sprite bounds, nudged slightly toward the attacker.
        /// </summary>
        public static Vector3 ResolveHitPoint(
            Transform targetTransform,
            Collider2D targetCollider = null,
            SpriteRenderer targetRenderer = null,
            GameObject attacker = null)
        {
            if (targetTransform == null)
            {
                return Vector3.zero;
            }

            Bounds? bounds = null;
            if (targetCollider != null && targetCollider.enabled && targetCollider.bounds.size.sqrMagnitude > 0.0001f)
            {
                bounds = targetCollider.bounds;
            }
            else if (targetRenderer != null && targetRenderer.bounds.size.sqrMagnitude > 0.0001f)
            {
                bounds = targetRenderer.bounds;
            }

            Vector3 hitPos = bounds.HasValue
                ? bounds.Value.center
                : targetTransform.position + Vector3.up * 0.85f;

            if (attacker != null && bounds.HasValue)
            {
                float dx = attacker.transform.position.x - hitPos.x;
                if (Mathf.Abs(dx) > 0.05f)
                {
                    hitPos.x += Mathf.Sign(dx) * Mathf.Min(bounds.Value.extents.x * 0.4f, 0.38f);
                }
            }

            hitPos.z = 0f;
            return hitPos;
        }

        private void Awake()
        {
            if (!Application.isPlaying) return;

            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
            EnsureInitialized();
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            if (isQuitting)
            {
                for (int i = 0; i < runtimeTextures.Count; i++)
                {
                    if (runtimeTextures[i] != null)
                    {
                        Destroy(runtimeTextures[i]);
                    }
                }
                runtimeTextures.Clear();
                cachedFrames = null;
            }
        }

        private void EnsureInitialized()
        {
            if (poolInitialized) return;
            poolInitialized = true;

            sparkFrames = ResolveSparkFrames();
            pool = new SparkSlot[PoolSize];

            Sprite initialSprite = (sparkFrames != null && sparkFrames.Length > 0) ? sparkFrames[0] : null;
            for (int i = 0; i < PoolSize; i++)
            {
                GameObject sparkObj = new GameObject($"HitSpark_{i:00}");
                sparkObj.transform.SetParent(transform, false);

                SpriteRenderer sr = sparkObj.AddComponent<SpriteRenderer>();
                sr.sortingOrder = sortingOrder;
                sr.sprite = initialSprite;
                sparkObj.SetActive(false);

                pool[i] = new SparkSlot
                {
                    gameObject = sparkObj,
                    transform = sparkObj.transform,
                    renderer = sr,
                    active = false,
                    elapsed = 0f,
                    baseColor = DefaultSparkColor,
                    baseScale = Vector3.one,
                    lastFrameIndex = -1
                };
            }
        }

        private void SpawnInternal(Vector3 hitPoint, Color tint, float scale)
        {
            EnsureInitialized();
            if (pool == null || pool.Length == 0) return;

            if (sparkFrames == null || sparkFrames.Length == 0)
            {
                sparkFrames = ResolveSparkFrames();
            }

            int slotIndex = AcquireSlotIndex();
            SparkSlot slot = pool[slotIndex];

            Vector3 spawnPos = new Vector3(hitPoint.x, hitPoint.y, 0f);
            float safeScale = Mathf.Max(0.1f, scale);
            Vector3 targetScale = new Vector3(safeScale, safeScale, 1f);

            slot.transform.position = spawnPos;
            slot.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            slot.transform.localScale = targetScale * 1.08f;

            slot.active = true;
            slot.elapsed = 0f;
            slot.baseColor = tint;
            slot.baseScale = targetScale;
            slot.lastFrameIndex = 0;

            if (slot.renderer != null)
            {
                slot.renderer.sortingOrder = sortingOrder;
                slot.renderer.sprite = (sparkFrames != null && sparkFrames.Length > 0) ? sparkFrames[0] : null;
                slot.renderer.color = tint;
            }

            slot.gameObject.SetActive(true);
            pool[slotIndex] = slot;
        }

        private int AcquireSlotIndex()
        {
            for (int offset = 0; offset < pool.Length; offset++)
            {
                int idx = (nextPoolIndex + offset) % pool.Length;
                if (!pool[idx].active)
                {
                    nextPoolIndex = (idx + 1) % pool.Length;
                    return idx;
                }
            }

            int recycled = nextPoolIndex % pool.Length;
            nextPoolIndex = (recycled + 1) % pool.Length;
            return recycled;
        }

        private void Update()
        {
            if (!poolInitialized || pool == null) return;

            float dt = Time.deltaTime;
            int frameCount = sparkFrames != null ? sparkFrames.Length : 0;

            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].active) continue;

                SparkSlot slot = pool[i];
                slot.elapsed += dt;

                if (slot.elapsed >= SparkDuration)
                {
                    slot.active = false;
                    if (slot.gameObject != null)
                    {
                        slot.gameObject.SetActive(false);
                    }
                    pool[i] = slot;
                    continue;
                }

                if (frameCount > 0 && slot.renderer != null)
                {
                    int frameIdx = frameCount > 1
                        ? Mathf.Min(frameCount - 1, Mathf.FloorToInt(slot.elapsed * SparkFps))
                        : 0;
                    if (frameIdx != slot.lastFrameIndex)
                    {
                        slot.lastFrameIndex = frameIdx;
                        slot.renderer.sprite = sparkFrames[frameIdx];
                    }
                }

                float t = Mathf.Clamp01(slot.elapsed / SparkDuration);
                float pop = Mathf.Lerp(1.08f, 0.92f, t);
                if (slot.transform != null)
                {
                    slot.transform.localScale = slot.baseScale * pop;
                }

                if (slot.renderer != null)
                {
                    Color c = slot.baseColor;
                    if (t > 0.7f)
                    {
                        c.a *= Mathf.Clamp01(1f - (t - 0.7f) / 0.3f);
                    }
                    slot.renderer.color = c;
                }

                pool[i] = slot;
            }
        }

        private Sprite[] ResolveSparkFrames()
        {
            if (sparkFrames != null && sparkFrames.Length > 0)
            {
                return sparkFrames;
            }

            if (cachedFrames != null && cachedFrames.Length > 0)
            {
                return cachedFrames;
            }

#if UNITY_EDITOR
            Sprite[] editorSprites = LoadFromEditorAssetDatabase();
            if (editorSprites != null && editorSprites.Length > 0)
            {
                cachedFrames = editorSprites;
                return cachedFrames;
            }
#endif

            Sprite[] resourceSprites = Resources.LoadAll<Sprite>("VFX/HitSpark_Sheet");
            if (resourceSprites != null && resourceSprites.Length > 0)
            {
                Array.Sort(resourceSprites, (a, b) => string.CompareOrdinal(a.name, b.name));
                cachedFrames = resourceSprites;
                return cachedFrames;
            }

            Sprite[] diskSprites = LoadFromDiskFallback();
            if (diskSprites != null && diskSprites.Length > 0)
            {
                cachedFrames = diskSprites;
                return cachedFrames;
            }

            cachedFrames = CreateProceduralSparkFrames();
            return cachedFrames;
        }

#if UNITY_EDITOR
        private static Sprite[] LoadFromEditorAssetDatabase()
        {
            UnityEngine.Object[] sheetAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/VFX/HitSpark_Sheet.png");
            if (sheetAssets != null && sheetAssets.Length > 0)
            {
                List<Sprite> sprites = new List<Sprite>(4);
                for (int i = 0; i < sheetAssets.Length; i++)
                {
                    if (sheetAssets[i] is Sprite s)
                    {
                        sprites.Add(s);
                    }
                }

                if (sprites.Count > 0)
                {
                    sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                    return sprites.ToArray();
                }
            }

            List<Sprite> numbered = new List<Sprite>(4);
            for (int i = 0; i < 4; i++)
            {
                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/VFX/HitSpark_{i}.png");
                if (s != null)
                {
                    numbered.Add(s);
                }
            }

            return numbered.Count > 0 ? numbered.ToArray() : null;
        }
#endif

        private static Sprite[] LoadFromDiskFallback()
        {
            string[] sheetCandidates =
            {
                Path.Combine(Application.dataPath, "Sprites/VFX/HitSpark_Sheet.png"),
                Path.Combine(Application.streamingAssetsPath, "Sprites/VFX/HitSpark_Sheet.png")
            };

            for (int i = 0; i < sheetCandidates.Length; i++)
            {
                Texture2D sheetTex = TryLoadTextureFromDisk(sheetCandidates[i]);
                if (sheetTex != null)
                {
                    return SliceSparkSheet(sheetTex);
                }
            }

            string[] dirCandidates =
            {
                Path.Combine(Application.dataPath, "Sprites/VFX"),
                Path.Combine(Application.streamingAssetsPath, "Sprites/VFX")
            };

            for (int d = 0; d < dirCandidates.Length; d++)
            {
                List<Sprite> frames = new List<Sprite>(4);
                for (int i = 0; i < 4; i++)
                {
                    string filePath = Path.Combine(dirCandidates[d], $"HitSpark_{i}.png");
                    Texture2D tex = TryLoadTextureFromDisk(filePath);
                    if (tex != null)
                    {
                        Sprite sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        sp.name = $"HitSpark_{i}";
                        frames.Add(sp);
                    }
                }

                if (frames.Count > 0)
                {
                    return frames.ToArray();
                }
            }

            return null;
        }

        private static Texture2D TryLoadTextureFromDisk(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes, false))
            {
                Destroy(tex);
                return null;
            }

            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            runtimeTextures.Add(tex);
            return tex;
        }

        private static Sprite[] SliceSparkSheet(Texture2D sheetTex)
        {
            int w = sheetTex.width;
            int h = sheetTex.height;
            List<Sprite> sprites = new List<Sprite>(4);

            if (w >= h * 3)
            {
                int frameW = w / 4;
                for (int i = 0; i < 4; i++)
                {
                    Sprite sp = Sprite.Create(sheetTex, new Rect(i * frameW, 0f, frameW, h), new Vector2(0.5f, 0.5f), 100f);
                    sp.name = $"HitSpark_Sheet_{i}";
                    sprites.Add(sp);
                }
            }
            else if (w == 256 && h == 256)
            {
                Rect[] rects =
                {
                    new Rect(0f, 128f, 128f, 128f),
                    new Rect(128f, 128f, 128f, 128f),
                    new Rect(0f, 0f, 128f, 128f),
                    new Rect(128f, 0f, 128f, 128f)
                };
                for (int i = 0; i < rects.Length; i++)
                {
                    Sprite sp = Sprite.Create(sheetTex, rects[i], new Vector2(0.5f, 0.5f), 100f);
                    sp.name = $"HitSpark_Sheet_{i}";
                    sprites.Add(sp);
                }
            }
            else if (h >= w * 3)
            {
                int frameH = h / 4;
                for (int i = 0; i < 4; i++)
                {
                    int y = h - (i + 1) * frameH;
                    Sprite sp = Sprite.Create(sheetTex, new Rect(0f, y, w, frameH), new Vector2(0.5f, 0.5f), 100f);
                    sp.name = $"HitSpark_Sheet_{i}";
                    sprites.Add(sp);
                }
            }
            else
            {
                Sprite sp = Sprite.Create(sheetTex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
                sp.name = "HitSpark_Sheet_0";
                sprites.Add(sp);
            }

            return sprites.ToArray();
        }

        private static Sprite[] CreateProceduralSparkFrames()
        {
            const int size = 128;
            const int frameCount = 4;
            Sprite[] frames = new Sprite[frameCount];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxRadius = size * 0.46f;

            for (int f = 0; f < frameCount; f++)
            {
                float progress = f / (float)(frameCount - 1);
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;

                Color[] pixels = new Color[size * size];
                float coreRadius = Mathf.Lerp(0.32f, 0.08f, progress);
                float spikeReach = Mathf.Lerp(0.55f, 0.98f, progress);
                float ringRadius = Mathf.Lerp(0.22f, 0.85f, progress);
                float ringAlpha = Mathf.Lerp(0.85f, 0.15f, progress);
                float fade = Mathf.Lerp(1.0f, 0.45f, progress);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - center.x) / maxRadius;
                        float dy = (y - center.y) / maxRadius;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist >= 1f)
                        {
                            pixels[y * size + x] = Color.clear;
                            continue;
                        }

                        float angle = Mathf.Atan2(dy, dx);
                        float primaryStar = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f)), 6f);
                        float diagonalStar = Mathf.Pow(Mathf.Abs(Mathf.Cos((angle + Mathf.PI * 0.25f) * 2f)), 10f) * 0.72f;
                        float rayProfile = Mathf.Max(primaryStar, diagonalStar);

                        float rayDist = dist / Mathf.Max(0.15f, spikeReach);
                        float starAlpha = rayDist < 1f ? rayProfile * Mathf.Pow(1f - rayDist, 1.35f) : 0f;
                        float coreAlpha = dist < coreRadius ? Mathf.Pow(1f - dist / coreRadius, 1.5f) : 0f;
                        float ringBand = Mathf.Clamp01(1f - Mathf.Abs(dist - ringRadius) * 7.5f) * ringAlpha;

                        float alpha = Mathf.Clamp01(Mathf.Max(coreAlpha, Mathf.Max(starAlpha, ringBand)) * fade);
                        float warmth = Mathf.Clamp01(dist * 0.85f);
                        Color col = Color.Lerp(Color.white, new Color(1f, 0.91f, 0.56f, 1f), warmth * (1f - coreAlpha));
                        col.a = alpha;
                        pixels[y * size + x] = col;
                    }
                }

                tex.SetPixels(pixels);
                tex.Apply();
                runtimeTextures.Add(tex);

                Sprite sp = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                sp.name = $"HitSpark_Fallback_{f}";
                frames[f] = sp;
            }

            return frames;
        }
    }
}
