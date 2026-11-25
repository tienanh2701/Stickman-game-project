using System.Collections.Generic;
using UnityEngine;

namespace StickmanGame.VFX
{
    /// <summary>
    /// Object-pooled lightweight 2D dash wind-streak and dust-puff VFX manager (pool size = 32).
    /// Renders soft procedural sprites via SpriteRenderer (URP 2D default material, behind characters)
    /// replacing the legacy TrailRenderer ribbon.
    /// </summary>
    public class DashVfxManager : MonoBehaviour
    {
        private const int PoolSize = 32;
        private const int VfxSortingOrder = 4;

        private static readonly Color DustBaseColor = new Color(0.88f, 0.86f, 0.82f, 0.72f);
        private static readonly Color AirPuffColor = new Color(0.92f, 0.95f, 1.0f, 0.50f);
        private static readonly Color WindBaseColor = new Color(0.95f, 0.97f, 1.0f, 0.78f);

        private enum VfxKind
        {
            DustPuff = 0,
            WindStreak = 1
        }

        private struct VfxSlot
        {
            public GameObject gameObject;
            public Transform transform;
            public SpriteRenderer renderer;
            public bool active;
            public float elapsed;
            public float duration;
            public Vector3 velocity;
            public Vector3 startScale;
            public Vector3 endScale;
            public Color baseColor;
            public float drag;
        }

        private static DashVfxManager instance;
        private static bool isQuitting;
        private static Sprite dustPuffSprite;
        private static Sprite windStreakSprite;
        private static readonly List<Texture2D> runtimeTextures = new List<Texture2D>();
        private static readonly List<Sprite> runtimeSprites = new List<Sprite>();

        private VfxSlot[] pool;
        private int nextPoolIndex;
        private bool poolInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            isQuitting = false;
            dustPuffSprite = null;
            windStreakSprite = null;
            runtimeTextures.Clear();
            runtimeSprites.Clear();
        }

        /// <summary>
        /// Ensures an active runtime DashVfxManager singleton exists in Play Mode without leaking EditMode objects.
        /// </summary>
        public static DashVfxManager EnsureInstance()
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

            instance = FindAnyObjectByType<DashVfxManager>();
            if (instance != null)
            {
                instance.EnsureInitialized();
                return instance;
            }

            GameObject go = new GameObject("DashVfxManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DashVfxManager>();
            instance.EnsureInitialized();
            return instance;
        }

        /// <summary>
        /// Spawns the initial dash burst: compact heel dust puffs (grounded) or light air puffs plus horizontal wind streaks.
        /// </summary>
        public static void SpawnDashBurst(Vector3 feetPosition, int dashDirection, bool isGrounded)
        {
            DashVfxManager mgr = EnsureInstance();
            if (mgr == null) return;
            mgr.SpawnDashBurstInternal(feetPosition, dashDirection, isGrounded);
        }

        /// <summary>
        /// Spawns a periodic mid-dash wind streak (and trailing ground dust puff when grounded).
        /// </summary>
        public static void SpawnDashStep(Vector3 feetPosition, int dashDirection, bool isGrounded)
        {
            DashVfxManager mgr = EnsureInstance();
            if (mgr == null) return;
            mgr.SpawnDashStepInternal(feetPosition, dashDirection, isGrounded);
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
                for (int i = 0; i < runtimeSprites.Count; i++)
                {
                    if (runtimeSprites[i] != null)
                    {
                        Destroy(runtimeSprites[i]);
                    }
                }
                runtimeSprites.Clear();

                for (int i = 0; i < runtimeTextures.Count; i++)
                {
                    if (runtimeTextures[i] != null)
                    {
                        Destroy(runtimeTextures[i]);
                    }
                }
                runtimeTextures.Clear();

                dustPuffSprite = null;
                windStreakSprite = null;
            }
        }

        private void EnsureInitialized()
        {
            if (poolInitialized) return;
            poolInitialized = true;

            EnsureProceduralSprites();
            pool = new VfxSlot[PoolSize];

            for (int i = 0; i < PoolSize; i++)
            {
                GameObject slotObj = new GameObject($"DashVfx_{i:00}");
                slotObj.transform.SetParent(transform, false);

                SpriteRenderer sr = slotObj.AddComponent<SpriteRenderer>();
                sr.sortingOrder = VfxSortingOrder;
                sr.sprite = dustPuffSprite;
                slotObj.SetActive(false);

                pool[i] = new VfxSlot
                {
                    gameObject = slotObj,
                    transform = slotObj.transform,
                    renderer = sr,
                    active = false
                };
            }
        }

        private void SpawnDashBurstInternal(Vector3 feetPosition, int dashDirection, bool isGrounded)
        {
            EnsureInitialized();
            float dir = dashDirection >= 0 ? 1f : -1f;

            if (isGrounded)
            {
                for (int i = 0; i < 4; i++)
                {
                    float backOffset = Mathf.Lerp(0.18f, 0.50f, i / 3f) + Random.Range(-0.04f, 0.04f);
                    float yOffset = Random.Range(0.08f, 0.22f);
                    Vector3 pos = new Vector3(feetPosition.x - dir * backOffset, feetPosition.y + yOffset, 0f);
                    float baseScale = Random.Range(0.55f, 0.85f);
                    float endMul = Random.Range(1.40f, 1.75f);
                    Vector3 vel = new Vector3(-dir * Random.Range(1.4f, 2.5f), Random.Range(0.28f, 0.68f), 0f);
                    float dur = Random.Range(0.26f, 0.34f);

                    Color col = DustBaseColor;
                    col.a = Random.Range(0.58f, 0.78f);

                    SpawnSlot(
                        VfxKind.DustPuff,
                        pos,
                        Random.Range(-18f, 18f),
                        new Vector3(baseScale, baseScale * 0.88f, 1f),
                        new Vector3(baseScale * endMul, baseScale * endMul * 0.92f, 1f),
                        vel,
                        drag: 4.5f,
                        dur,
                        col);
                }
            }
            else
            {
                for (int i = 0; i < 2; i++)
                {
                    float backOffset = 0.24f + i * 0.18f;
                    float yOffset = 0.28f + i * 0.22f;
                    Vector3 pos = new Vector3(feetPosition.x - dir * backOffset, feetPosition.y + yOffset, 0f);
                    float baseScale = Random.Range(0.45f, 0.65f);
                    Vector3 vel = new Vector3(-dir * Random.Range(1.5f, 2.2f), Random.Range(-0.15f, 0.20f), 0f);

                    SpawnSlot(
                        VfxKind.DustPuff,
                        pos,
                        Random.Range(-15f, 15f),
                        new Vector3(baseScale, baseScale * 0.85f, 1f),
                        new Vector3(baseScale * 1.45f, baseScale * 1.25f, 1f),
                        vel,
                        drag: 4.0f,
                        Random.Range(0.20f, 0.26f),
                        AirPuffColor);
                }
            }

            float[] streakHeights = { 0.55f, 1.00f };
            for (int i = 0; i < streakHeights.Length; i++)
            {
                float backOffset = 0.22f + i * 0.16f;
                Vector3 pos = new Vector3(feetPosition.x - dir * backOffset, feetPosition.y + streakHeights[i], 0f);
                float lenScale = Random.Range(0.85f, 1.20f);
                float thickScale = Random.Range(0.65f, 0.90f);
                Vector3 vel = new Vector3(-dir * Random.Range(3.6f, 5.2f), 0f, 0f);
                Color col = WindBaseColor;
                col.a = Random.Range(0.62f, 0.82f);

                SpawnSlot(
                    VfxKind.WindStreak,
                    pos,
                    0f,
                    new Vector3(dir * lenScale * 0.85f, thickScale, 1f),
                    new Vector3(dir * lenScale * 1.35f, thickScale * 0.35f, 1f),
                    vel,
                    drag: 3.0f,
                    Random.Range(0.16f, 0.21f),
                    col);
            }
        }

        private void SpawnDashStepInternal(Vector3 feetPosition, int dashDirection, bool isGrounded)
        {
            EnsureInitialized();
            float dir = dashDirection >= 0 ? 1f : -1f;

            float streakY = Random.Range(0.35f, 1.10f);
            Vector3 streakPos = new Vector3(feetPosition.x - dir * Random.Range(0.20f, 0.45f), feetPosition.y + streakY, 0f);
            float lenScale = Random.Range(0.70f, 1.05f);
            float thickScale = Random.Range(0.55f, 0.80f);
            Color windCol = WindBaseColor;
            windCol.a = Random.Range(0.48f, 0.72f);

            SpawnSlot(
                VfxKind.WindStreak,
                streakPos,
                0f,
                new Vector3(dir * lenScale * 0.80f, thickScale, 1f),
                new Vector3(dir * lenScale * 1.25f, thickScale * 0.30f, 1f),
                new Vector3(-dir * Random.Range(3.2f, 4.6f), 0f, 0f),
                drag: 3.0f,
                Random.Range(0.14f, 0.19f),
                windCol);

            if (isGrounded)
            {
                Vector3 dustPos = new Vector3(feetPosition.x - dir * Random.Range(0.18f, 0.35f), feetPosition.y + Random.Range(0.08f, 0.18f), 0f);
                float dScale = Random.Range(0.40f, 0.62f);
                Color dustCol = DustBaseColor;
                dustCol.a = Random.Range(0.45f, 0.65f);

                SpawnSlot(
                    VfxKind.DustPuff,
                    dustPos,
                    Random.Range(-15f, 15f),
                    new Vector3(dScale, dScale * 0.85f, 1f),
                    new Vector3(dScale * 1.55f, dScale * 1.35f, 1f),
                    new Vector3(-dir * Random.Range(1.0f, 1.8f), Random.Range(0.22f, 0.48f), 0f),
                    drag: 4.5f,
                    Random.Range(0.22f, 0.29f),
                    dustCol);
            }
        }

        private void SpawnSlot(
            VfxKind kind,
            Vector3 worldPos,
            float zRotationDeg,
            Vector3 startScale,
            Vector3 endScale,
            Vector3 velocity,
            float drag,
            float duration,
            Color baseColor)
        {
            if (pool == null || pool.Length == 0) return;

            int slotIndex = AcquireSlotIndex();
            VfxSlot slot = pool[slotIndex];

            slot.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
            slot.transform.localRotation = Quaternion.Euler(0f, 0f, zRotationDeg);
            slot.transform.localScale = startScale;

            slot.active = true;
            slot.elapsed = 0f;
            slot.duration = Mathf.Max(0.05f, duration);
            slot.velocity = velocity;
            slot.drag = drag;
            slot.startScale = startScale;
            slot.endScale = endScale;
            slot.baseColor = baseColor;

            if (slot.renderer != null)
            {
                slot.renderer.sortingOrder = VfxSortingOrder;
                slot.renderer.sprite = kind == VfxKind.DustPuff ? dustPuffSprite : windStreakSprite;
                slot.renderer.color = baseColor;
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
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].active) continue;

                VfxSlot slot = pool[i];
                slot.elapsed += dt;
                float t = Mathf.Clamp01(slot.elapsed / slot.duration);

                if (t >= 1f)
                {
                    slot.active = false;
                    if (slot.gameObject != null)
                    {
                        slot.gameObject.SetActive(false);
                    }
                    pool[i] = slot;
                    continue;
                }

                if (slot.transform != null)
                {
                    slot.velocity *= Mathf.Clamp01(1f - slot.drag * dt);
                    slot.transform.position += slot.velocity * dt;
                    float easeOut = 1f - (1f - t) * (1f - t);
                    slot.transform.localScale = Vector3.LerpUnclamped(slot.startScale, slot.endScale, easeOut);
                }

                if (slot.renderer != null)
                {
                    Color c = slot.baseColor;
                    float fadeIn = Mathf.Clamp01(t / 0.12f);
                    float fadeOut = Mathf.Pow(1f - t, 1.35f);
                    c.a = slot.baseColor.a * fadeIn * fadeOut;
                    slot.renderer.color = c;
                }

                pool[i] = slot;
            }
        }

        private static void EnsureProceduralSprites()
        {
            if (dustPuffSprite == null)
            {
                dustPuffSprite = CreateDustPuffSprite();
            }

            if (windStreakSprite == null)
            {
                windStreakSprite = CreateWindStreakSprite();
            }
        }

        private static Sprite CreateDustPuffSprite()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            Vector2[] lobes =
            {
                new Vector2(0.00f,  0.04f),
                new Vector2(-0.26f, -0.06f),
                new Vector2(0.26f, -0.05f),
                new Vector2(0.04f, -0.22f)
            };
            float[] lobeRadii = { 0.46f, 0.36f, 0.35f, 0.34f };

            float invHalf = 2f / (size - 1f);
            for (int y = 0; y < size; y++)
            {
                float ny = y * invHalf - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = x * invHalf - 1f;
                    float density = 0f;

                    for (int l = 0; l < lobes.Length; l++)
                    {
                        float dx = (nx - lobes[l].x) / lobeRadii[l];
                        float dy = (ny - lobes[l].y) / lobeRadii[l];
                        float r2 = dx * dx + dy * dy;
                        if (r2 < 1f)
                        {
                            float falloff = 1f - r2;
                            density = Mathf.Max(density, falloff * falloff);
                        }
                    }

                    // Keep soft border alpha strictly 0 at outer pixels so sprites never show square boxes
                    float edgeDist = Mathf.Sqrt(nx * nx + ny * ny);
                    float edgeFade = edgeDist < 0.96f ? Mathf.Clamp01((0.96f - edgeDist) / 0.16f) : 0f;
                    float a = Mathf.Clamp01(density * edgeFade);
                    pixels[y * size + x] = new Color(0.92f, 0.90f, 0.86f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            runtimeTextures.Add(tex);

            Sprite sp = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sp.name = "DashDustPuff_Procedural";
            runtimeSprites.Add(sp);
            return sp;
        }

        private static Sprite CreateWindStreakSprite()
        {
            const int width = 96;
            const int height = 24;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = (y / (float)(height - 1)) * 2f - 1f;
                for (int x = 0; x < width; x++)
                {
                    float nx = (x / (float)(width - 1)) * 2f - 1f;
                    // Sleek needle profile: sharpest near center, tapered cleanly to zero at both tips
                    float xProfile = Mathf.Pow(Mathf.Clamp01(1f - nx * nx), 1.45f);
                    float localHalfThickness = Mathf.Lerp(0.22f, 0.92f, xProfile);
                    float yNorm = Mathf.Abs(ny) / Mathf.Max(0.05f, localHalfThickness);
                    float yProfile = yNorm < 1f ? Mathf.Pow(1f - yNorm * yNorm, 1.75f) : 0f;

                    float alpha = Mathf.Clamp01(xProfile * yProfile);
                    pixels[y * width + x] = new Color(0.96f, 0.98f, 1.0f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            runtimeTextures.Add(tex);

            Sprite sp = Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            sp.name = "DashWindStreak_Procedural";
            runtimeSprites.Add(sp);
            return sp;
        }
    }
}
