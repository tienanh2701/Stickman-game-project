using System.IO;
using UnityEngine;
using StickmanBeatEmUp.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StickmanBeatEmUp.Level
{
    /// <summary>
    /// Multi-layer parallax background controller for the Stickman Beat 'em Up.
    /// Automatically creates/updates child SpriteRenderer layers and a particle system.
    /// Zero-GC LateUpdate via pre-allocated struct array.
    /// Works in Editor Play Mode AND standalone Player builds.
    /// Must be called by StageManager.ApplyLevelVisuals AFTER EnsureGroundArenaCoverage.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapParallaxController : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Constants
        // ------------------------------------------------------------------ //
        private const float GROUND_Y      = -2.5f;
        private const float ARENA_CENTER_X = 10f;
        private const float ARENA_WIDTH    = 85f;   // matches EnsureGroundArenaCoverage
        private const float TILED_WIDTH    = 120f;  // extra margin beyond arena edges

        private const float FACTOR_FAR   = 0.12f;
        private const float FACTOR_MID   = 0.35f;
        private const float FACTOR_NEAR  = 0.65f;
        private const float FACTOR_MIST  = 1.15f;

        // Sorting orders (behind gameplay). groundRenderer.sortingOrder set to -44.
        private const int ORDER_FAR        = -48;
        private const int ORDER_MID        = -47;
        private const int ORDER_NEAR       = -46;
        private const int ORDER_MIST       = -45;
        private const int ORDER_GROUND_SR  = -44;   // groundRenderer itself
        private const int ORDER_GND_OVR    = -43;   // ground pattern overlay (above groundRenderer)
        private const int ORDER_PARTICLES  = -12;

        // Native sprite heights (pixels) used for world-unit height calculation at 100 ppu
        private static readonly float[] FAR_HEIGHTS  = { 0f, 512f/100f, 512f/100f, 512f/100f };
        private static readonly float[] MID_HEIGHTS  = { 0f, 512f/100f, 512f/100f, 512f/100f };
        private static readonly float[] NEAR_HEIGHTS = { 0f, 384f/100f, 384f/100f, 384f/100f };
        private static readonly float[] MIST_HEIGHTS = { 0f, 256f/100f, 256f/100f, 256f/100f };

        // ------------------------------------------------------------------ //
        //  Parallax layer struct — zero-GC, pre-allocated
        // ------------------------------------------------------------------ //
        private struct ParallaxLayer
        {
            public Transform tr;
            public float     factor;
            public float     baseOffsetY;
            public float     driftSpeed;
            public float     driftPhase;
        }

        // ------------------------------------------------------------------ //
        //  State
        // ------------------------------------------------------------------ //
        private ParallaxLayer[]  _layers     = new ParallaxLayer[4]; // Far, Mid, Near, Mist
        private int              _layerCount = 0;

        private SpriteRenderer   _farSR, _midSR, _nearSR, _mistSR;
        private SpriteRenderer   _groundOverlaySR;
        private ParticleSystem   _ps;

        private Transform        _camTransform;
        private float            _prevCamX;
        private float            _time;

        private int              _currentWorld;
        private bool             _initialized;

        // Static caches — survive domain reloads via [RuntimeInitializeOnLoadMethod]
        private static Texture2D[]  _procFallbackTex = new Texture2D[4];   // indexed by world 1-3
        private static Material[]   _particleMat     = new Material[4];
        private static Texture2D    _softCircleTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _procFallbackTex = new Texture2D[4];
            _particleMat     = new Material[4];
            _softCircleTex   = null;
        }

        // ------------------------------------------------------------------ //
        //  Public API — called by StageManager AFTER EnsureGroundArenaCoverage
        // ------------------------------------------------------------------ //
        public void Setup(LevelDataSO level, SpriteRenderer backgroundRenderer, SpriteRenderer groundRenderer)
        {
            int world = DetectWorld(level);

            // Always re-run so ground overlay picks up new bounds even on same world
            _currentWorld = world;
            _initialized  = false;

            EnsureCamera();
            EnsureChildRenderers(world);
            EnsureGroundOverlay(world, groundRenderer);
            EnsureParticleSystem(world);
            RebuildLayerArray(world);

            _initialized = true;
        }

        // ------------------------------------------------------------------ //
        //  World detection
        // ------------------------------------------------------------------ //
        private static int DetectWorld(LevelDataSO level)
        {
            if (level == null) return 1;
            switch (level.worldType)
            {
                case WorldType.World2_Wasteland: return 2;
                case WorldType.World3_NeoCity:   return 3;
                default:                         return 1;
            }
        }

        // ------------------------------------------------------------------ //
        //  LateUpdate — ZERO GC
        // ------------------------------------------------------------------ //
        private void LateUpdate()
        {
            if (!_initialized) return;

            // Late-init camera: handle case where camera wasn't ready at Setup time
            if (_camTransform == null)
            {
                EnsureCamera();
                if (_camTransform == null) return;
                if (_layerCount == 0) RebuildLayerArray(_currentWorld);
            }

            if (_layerCount == 0) return;

            float camX      = _camTransform.position.x;
            float camRelX   = camX - ARENA_CENTER_X;
            _prevCamX       = camX;
            _time          += Time.deltaTime;

            for (int i = 0; i < _layerCount; i++)
            {
                ref ParallaxLayer lay = ref _layers[i];
                if (lay.tr == null) continue;

                float drift = Mathf.Sin(_time * lay.driftSpeed + lay.driftPhase) * 0.3f;

                Vector3 p = lay.tr.position;
                p.x = ARENA_CENTER_X + camRelX * lay.factor + drift;
                p.y = lay.baseOffsetY;
                lay.tr.position = p;
            }
        }

        // ------------------------------------------------------------------ //
        //  Camera
        // ------------------------------------------------------------------ //
        private void EnsureCamera()
        {
            if (CameraFollowBeatEmUp.Instance != null)
                _camTransform = CameraFollowBeatEmUp.Instance.transform;
            else if (Camera.main != null)
                _camTransform = Camera.main.transform;

            if (_camTransform != null)
                _prevCamX = _camTransform.position.x;
        }

        // ------------------------------------------------------------------ //
        //  Parallax layer child renderers (tiled, full-arena width)
        // ------------------------------------------------------------------ //
        private void EnsureChildRenderers(int world)
        {
            _farSR  = GetOrAddTiledSR("Parallax_Far",  ORDER_FAR);
            _midSR  = GetOrAddTiledSR("Parallax_Mid",  ORDER_MID);
            _nearSR = GetOrAddTiledSR("Parallax_Near", ORDER_NEAR);
            _mistSR = GetOrAddTiledSR("Parallax_Mist", ORDER_MIST);

            LoadAndSizeTiledSprite(_farSR,  world, "Far",  FAR_HEIGHTS[world]);
            LoadAndSizeTiledSprite(_midSR,  world, "Mid",  MID_HEIGHTS[world]);
            LoadAndSizeTiledSprite(_nearSR, world, "Near", NEAR_HEIGHTS[world]);
            LoadAndSizeTiledSprite(_mistSR, world, "Mist", MIST_HEIGHTS[world]);
        }

        private SpriteRenderer GetOrAddTiledSR(string childName, int sortOrder)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(transform, false);
            }
            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
            if (sr == null) sr = child.gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortOrder;
            sr.drawMode     = SpriteDrawMode.Tiled;
            sr.tileMode     = SpriteTileMode.Continuous;
            return sr;
        }

        private void LoadAndSizeTiledSprite(SpriteRenderer sr, int world, string layerName, float worldHeight)
        {
            Sprite spr = LoadSprite(world, layerName, isTiled: true);
            if (spr != null) sr.sprite = spr;

            // Size to cover full arena width + margin; height from sprite native size
            float h = (worldHeight > 0.01f) ? worldHeight : 5f;
            sr.size = new Vector2(TILED_WIDTH, h);

            // Anchor: bottom of sprite sits on GROUND_Y
            Vector3 pos = sr.transform.position;
            pos.x = ARENA_CENTER_X;
            pos.y = GROUND_Y + h * 0.5f;
            sr.transform.position = pos;
        }

        // ------------------------------------------------------------------ //
        //  Sprite loading
        // ------------------------------------------------------------------ //
        private static Sprite LoadSprite(int world, string layerName, bool isTiled)
        {
            string relPath  = string.Format("Assets/Sprites/Maps/Layers/W{0}_{1}.png", world, layerName);
            string fileName = string.Format("W{0}_{1}.png", world, layerName);
            Sprite spr = null;

#if UNITY_EDITOR
            spr = AssetDatabase.LoadAssetAtPath<Sprite>(relPath);
#endif

            if (spr == null)
            {
                string fullPath = Path.Combine(Application.dataPath, "Sprites", "Maps", "Layers", fileName);
                if (File.Exists(fullPath))
                {
                    byte[] bytes = File.ReadAllBytes(fullPath);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.wrapMode = isTiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    if (tex.LoadImage(bytes))
                    {
                        spr = Sprite.Create(tex,
                            new Rect(0, 0, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f), 100f,
                            0, SpriteMeshType.FullRect);
                    }
                }
            }

            if (spr == null)
                spr = GetProceduralSprite(world, isTiled);

            return spr;
        }

        private static Sprite GetProceduralSprite(int world, bool isTiled)
        {
            int idx = Mathf.Clamp(world, 1, 3);
            if (_procFallbackTex[idx] == null)
            {
                Color[] worldColors = {
                    Color.clear,
                    new Color(0.22f, 0.21f, 0.19f, 0.4f),
                    new Color(0.42f, 0.25f, 0.12f, 0.4f),
                    new Color(0.08f, 0.06f, 0.18f, 0.4f),
                };
                Texture2D tex = new Texture2D(64, 32, TextureFormat.RGBA32, false);
                tex.wrapMode = isTiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                Color[] px = new Color[64 * 32];
                Color c = worldColors[idx];
                for (int i = 0; i < px.Length; i++) px[i] = c;
                px[0] = Color.clear; // (0,0) alpha == 0
                tex.SetPixels(px);
                tex.Apply();
                _procFallbackTex[idx] = tex;
            }
            Texture2D t = _procFallbackTex[idx];
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        // ------------------------------------------------------------------ //
        //  Ground pattern overlay
        //  Parented to THIS controller (localScale = 1), positioned at
        //  groundRenderer.bounds.center. groundRenderer.sortingOrder = -44.
        // ------------------------------------------------------------------ //
        private void EnsureGroundOverlay(int world, SpriteRenderer groundRenderer)
        {
            if (groundRenderer == null) return;

            // Set groundRenderer sorting order so overlay (at -43) draws above it
            groundRenderer.sortingOrder = ORDER_GROUND_SR;

            Transform overlayTr = transform.Find("GroundPatternOverlay");
            if (overlayTr == null)
            {
                overlayTr = new GameObject("GroundPatternOverlay").transform;
                overlayTr.SetParent(transform, false);   // parent = this controller, scale = 1
            }
            overlayTr.localScale    = Vector3.one;

            _groundOverlaySR = overlayTr.GetComponent<SpriteRenderer>();
            if (_groundOverlaySR == null)
                _groundOverlaySR = overlayTr.gameObject.AddComponent<SpriteRenderer>();

            _groundOverlaySR.sortingOrder = ORDER_GND_OVR;
            _groundOverlaySR.drawMode     = SpriteDrawMode.Tiled;
            _groundOverlaySR.tileMode     = SpriteTileMode.Continuous;

            // Bounds are valid because StageManager called EnsureGroundArenaCoverage first
            Bounds gb = groundRenderer.bounds;
            overlayTr.position = gb.center;

            Sprite groundSpr = LoadGroundSprite(world);
            if (groundSpr != null)
            {
                _groundOverlaySR.sprite = groundSpr;
                _groundOverlaySR.size   = new Vector2(gb.size.x, gb.size.y);
            }
        }

        private static Sprite LoadGroundSprite(int world)
        {
            string relPath  = string.Format("Assets/Sprites/Maps/Layers/W{0}_Ground.png", world);
            string fileName = string.Format("W{0}_Ground.png", world);
            Sprite spr = null;

#if UNITY_EDITOR
            spr = AssetDatabase.LoadAssetAtPath<Sprite>(relPath);
#endif

            if (spr == null)
            {
                string fullPath = Path.Combine(Application.dataPath, "Sprites", "Maps", "Layers", fileName);
                if (File.Exists(fullPath))
                {
                    byte[] bytes = File.ReadAllBytes(fullPath);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.wrapMode = TextureWrapMode.Repeat;
                    if (tex.LoadImage(bytes))
                    {
                        spr = Sprite.Create(tex,
                            new Rect(0, 0, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f), 100f,
                            0, SpriteMeshType.FullRect, Vector4.zero, true);
                    }
                }
            }
            return spr;
        }

        // ------------------------------------------------------------------ //
        //  Build layer struct array
        // ------------------------------------------------------------------ //
        private void RebuildLayerArray(int world)
        {
            _layerCount = 0;
            if (_camTransform == null) return;

            void AddLayer(SpriteRenderer sr, float factor, float driftSpeed, float driftPhase)
            {
                if (sr == null || sr.transform == null) return;
                ref ParallaxLayer lay = ref _layers[_layerCount];
                lay.tr         = sr.transform;
                lay.factor     = factor;
                lay.baseOffsetY = sr.transform.position.y;
                lay.driftSpeed = driftSpeed;
                lay.driftPhase = driftPhase;
                _layerCount++;
            }

            AddLayer(_farSR,  FACTOR_FAR,  0.12f, 0.0f);
            AddLayer(_midSR,  FACTOR_MID,  0.08f, 1.2f);
            AddLayer(_nearSR, FACTOR_NEAR, 0.05f, 2.4f);
            AddLayer(_mistSR, FACTOR_MIST, 0.18f, 0.6f);
        }

        // ------------------------------------------------------------------ //
        //  Particle system (cached material + soft-circle texture)
        // ------------------------------------------------------------------ //
        private void EnsureParticleSystem(int world)
        {
            Transform psTr = transform.Find("AtmosphericParticles");
            if (psTr == null)
            {
                psTr = new GameObject("AtmosphericParticles").transform;
                psTr.SetParent(transform, false);
                psTr.localPosition = Vector3.zero;
            }

            _ps = psTr.GetComponent<ParticleSystem>();
            if (_ps == null) _ps = psTr.gameObject.AddComponent<ParticleSystem>();

            ParticleSystemRenderer psr = psTr.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sortingOrder = ORDER_PARTICLES;
                psr.material     = GetParticleMaterial(world);
                psr.renderMode   = ParticleSystemRenderMode.Billboard;
            }

            ConfigureParticleSystem(world);
        }

        private static Material GetParticleMaterial(int world)
        {
            int idx = Mathf.Clamp(world, 1, 3);
            if (_particleMat[idx] != null) return _particleMat[idx];

            // Build a cached soft-circle Texture2D (32x32 radial gradient)
            if (_softCircleTex == null)
            {
                _softCircleTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                _softCircleTex.wrapMode = TextureWrapMode.Clamp;
                Color[] px = new Color[32 * 32];
                for (int y = 0; y < 32; y++)
                {
                    for (int x = 0; x < 32; x++)
                    {
                        float dx = (x - 15.5f) / 15.5f;
                        float dy = (y - 15.5f) / 15.5f;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - dist);
                        a = a * a; // soft quadratic falloff
                        px[y * 32 + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                _softCircleTex.SetPixels(px);
                _softCircleTex.Apply();
            }

            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Hidden/InternalErrorShader");
            Material mat = new Material(sh != null ? sh : Shader.Find("Standard"));
            mat.mainTexture = _softCircleTex;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.EnableKeyword("_ALPHABLEND_ON");
            _particleMat[idx] = mat;
            return mat;
        }

        private void ConfigureParticleSystem(int world)
        {
            var main  = _ps.main;
            var emit  = _ps.emission;
            var shape = _ps.shape;

            main.loop            = true;
            main.playOnAwake     = true;
            main.maxParticles    = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(0.05f, 0.4f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.02f, 0.01f);

            emit.rateOverTime    = new ParticleSystem.MinMaxCurve(4f);

            switch (world)
            {
                case 2: // DBZ — golden Ki motes & desert dust
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(1.0f, 0.80f, 0.20f, 0.7f),
                        new Color(0.30f, 1.0f, 0.40f, 0.5f));
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
                    break;
                case 3: // Diend — cyan/magenta neon data motes
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(0.0f, 0.86f, 1.0f, 0.8f),
                        new Color(1.0f, 0.12f, 0.86f, 0.6f));
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
                    break;
                default: // World 1 — warm gray/amber ash & titan steam
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(0.70f, 0.68f, 0.62f, 0.6f),
                        new Color(0.80f, 0.65f, 0.40f, 0.4f));
                    break;
            }

            shape.enabled  = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale    = new Vector3(ARENA_WIDTH, 8f, 1f);
            shape.position = new Vector3(ARENA_CENTER_X, GROUND_Y + 5f, 0f);

            if (!_ps.isPlaying) _ps.Play();
        }
    }
}
