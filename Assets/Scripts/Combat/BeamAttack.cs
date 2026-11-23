using System.Collections.Generic;
using System.IO;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Combat
{
    /// <summary>
    /// Tia năng lượng / Laser kéo dài liên tục hoặc bắn quét (Kamehameha, Tia Lazer Boss, Đạn xuyên Diend)
    /// Hỗ trợ cross-fade 60+ FPS mượt mà bằng 2 lớp SpriteRenderer cho kỹ năng biến hình của Goku & Diend.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class BeamAttack : MonoBehaviour, IPoolableCombatObject
    {
        public enum BeamVisualStyle
        {
            DefaultLine = 0,
            GokuKamehameha = 1,
            DiendPiercingCard = 2
        }

        private const int SkillFrameCount = 6;
        private const float SkillSpritePpu = 100f;
        private static readonly Vector2 SkillPivot = new Vector2(0.045f, 0.5f);

        private static Sprite[] gokuSkillSprites;
        private static Sprite[] diendSkillSprites;
        private static readonly List<Texture2D> runtimeLoadedTextures = new List<Texture2D>();

        [Header("Beam Config")]
        [SerializeField] private float beamLength = 15f;
        [SerializeField] private float beamWidth = 1.5f;
        [SerializeField] private float duration = 1.0f;
        [SerializeField] private float tickRate = 0.2f;
        [SerializeField] private LayerMask targetLayer;

        [Header("Visual")]
        [SerializeField] private LineRenderer lineRenderer;

        private BoxCollider2D boxCol;
        private DamageData damageData;
        private float timer = 0f;
        private float nextTickTime = 0f;
        private int direction = 1;

        // Skill VFX & smooth cross-fade state
        private BeamVisualStyle visualStyle = BeamVisualStyle.DefaultLine;
        private Transform followAnchor;
        private Vector3 anchorLocalOffset;
        private float warmupTime = 0f;
        private float fadeOutTime = 0f;
        private bool singleHitPerTarget = false;
        private bool activeStarted = false;
        private bool fadeOutStarted = false;
        private bool isCancelling = false;
        private float cancelElapsed = 0f;
        private const float CancelFadeDuration = 0.10f;
        private float nextActiveTickElapsed = 0f;

        private readonly HashSet<IDamageable> alreadyHitTargets = new HashSet<IDamageable>();
        private Sprite[] activeVfxSprites;
        private Transform vfxRoot;
        private SpriteRenderer vfxLayerA;
        private SpriteRenderer vfxLayerB;

        private float lastFrameFloat = 0f;
        private float lastMasterAlpha = 1f;
        private Vector3 lastVfxScale = Vector3.one;
        private Vector3 lastVfxLocalPos = Vector3.zero;
        private Color lastTint = Color.white;
        private System.Action<BeamAttack> returnToPoolCallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticSpriteCache()
        {
            gokuSkillSprites = null;
            diendSkillSprites = null;
            runtimeLoadedTextures.Clear();
        }

        private void Awake()
        {
            boxCol = GetComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            UpdateColliderSize();
        }

        public void SetReturnToPoolCallback(System.Action<BeamAttack> callback)
        {
            returnToPoolCallback = callback;
        }

        public void SetPoolReturnAction(System.Action<BeamAttack> callback)
        {
            returnToPoolCallback = callback;
        }

        public void OnSpawnedFromPool()
        {
            timer = 0f;
            nextTickTime = 0f;
            visualStyle = BeamVisualStyle.DefaultLine;
            followAnchor = null;
            anchorLocalOffset = Vector3.zero;
            warmupTime = 0f;
            fadeOutTime = 0f;
            singleHitPerTarget = false;
            activeStarted = false;
            fadeOutStarted = false;
            isCancelling = false;
            cancelElapsed = 0f;
            nextActiveTickElapsed = 0f;
            activeVfxSprites = null;
            alreadyHitTargets.Clear();

            if (boxCol == null) boxCol = GetComponent<BoxCollider2D>();
            if (boxCol != null) boxCol.enabled = true;
            if (vfxLayerA != null) vfxLayerA.enabled = false;
            if (vfxLayerB != null) vfxLayerB.enabled = false;
        }

        public void OnReturnedToPool()
        {
            followAnchor = null;
            activeVfxSprites = null;
            alreadyHitTargets.Clear();
            if (boxCol != null) boxCol.enabled = false;
            if (lineRenderer != null) lineRenderer.enabled = false;
            if (vfxLayerA != null) vfxLayerA.enabled = false;
            if (vfxLayerB != null) vfxLayerB.enabled = false;
        }

        private void ReleaseOrDestroy()
        {
            if (returnToPoolCallback != null)
            {
                returnToPoolCallback(this);
                return;
            }

            Destroy(gameObject);
        }

        public void SetupBeam(DamageData damage, int dir, LayerMask mask, float activeDuration, float length = 15f, float width = 1.5f)
        {
            damageData = damage;
            direction = dir != 0 ? (dir > 0 ? 1 : -1) : 1;
            targetLayer = mask;
            duration = Mathf.Max(0.01f, activeDuration);
            beamLength = length;
            beamWidth = width;

            UpdateColliderSize();
            RefreshBeamVisual();
        }

        /// <summary>
        /// Cấu hình VFX sprite cross-fade mượt 60+ FPS cho kỹ năng I dạng biến hình (Goku / Diend).
        /// </summary>
        public void ConfigureSkillVfx(BeamVisualStyle style, Transform followAnchor, Vector3 anchorLocalOffset, float warmupTime, float fadeOutTime, bool singleHitPerTarget = false)
        {
            visualStyle = style;
            this.followAnchor = followAnchor;
            this.anchorLocalOffset = anchorLocalOffset;
            this.warmupTime = Mathf.Max(0f, warmupTime);
            this.fadeOutTime = Mathf.Max(0f, fadeOutTime);
            this.singleHitPerTarget = singleHitPerTarget;

            timer = 0f;
            activeStarted = false;
            fadeOutStarted = false;
            isCancelling = false;
            cancelElapsed = 0f;
            nextActiveTickElapsed = 0f;
            alreadyHitTargets.Clear();

            activeVfxSprites = GetSkillSprites(style);
            if (activeVfxSprites != null && activeVfxSprites.Length == SkillFrameCount)
            {
                EnsureVfxRenderers();
                if (lineRenderer != null)
                {
                    lineRenderer.enabled = false;
                }
            }
            else if (lineRenderer != null)
            {
                lineRenderer.enabled = true;
            }

            if (boxCol != null)
            {
                boxCol.enabled = this.warmupTime <= 0f;
            }

            UpdateFollowAnchor();
            RefreshBeamVisual();
        }

        /// <summary>
        /// Hủy giai đoạn gồng năng lượng nếu nhân vật bị ngắt chiêu trước khi phóng tia.
        /// </summary>
        public void CancelCharge()
        {
            if (activeStarted || isCancelling)
            {
                return;
            }

            isCancelling = true;
            cancelElapsed = 0f;
            if (boxCol != null)
            {
                boxCol.enabled = false;
            }

            if (activeVfxSprites == null || vfxRoot == null)
            {
                ReleaseOrDestroy();
            }
        }

        private void UpdateColliderSize()
        {
            if (boxCol != null)
            {
                boxCol.size = new Vector2(beamLength, beamWidth);
                boxCol.offset = new Vector2(direction * (beamLength / 2f), 0f);
            }
        }

        private void RefreshBeamVisual()
        {
            if (activeVfxSprites != null && visualStyle != BeamVisualStyle.DefaultLine)
            {
                if (lineRenderer != null)
                {
                    lineRenderer.enabled = false;
                }
                UpdateSkillVfxVisual();
                return;
            }

            if (lineRenderer != null)
            {
                lineRenderer.enabled = true;
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, transform.position);
                lineRenderer.SetPosition(1, transform.position + new Vector3(direction * beamLength, 0f, 0f));
            }
        }

        private void Update()
        {
            if (visualStyle == BeamVisualStyle.DefaultLine || activeVfxSprites == null)
            {
                timer += Time.deltaTime;
                RefreshBeamVisual();

                if (Time.time >= nextTickTime)
                {
                    ApplyDamageTick();
                    nextTickTime = Time.time + tickRate;
                }

                if (timer >= duration)
                {
                    ReleaseOrDestroy();
                }
                return;
            }

            if (isCancelling)
            {
                cancelElapsed += Time.deltaTime;
                float ct = Mathf.Clamp01(cancelElapsed / CancelFadeDuration);
                float alpha = lastMasterAlpha * (1f - ct);
                if (vfxRoot != null)
                {
                    vfxRoot.localScale = Vector3.Lerp(lastVfxScale, new Vector3(lastVfxScale.x * 0.7f, lastVfxScale.y * 0.35f, 1f), ct);
                    vfxRoot.localPosition = lastVfxLocalPos;
                }
                ApplyCrossfadeBlend(activeVfxSprites, lastFrameFloat, alpha, lastTint);

                if (ct >= 1f)
                {
                    ReleaseOrDestroy();
                }
                return;
            }

            timer += Time.deltaTime;

            float activeEnd = warmupTime + duration;
            float totalLifetime = activeEnd + fadeOutTime;

            if (timer < activeEnd)
            {
                UpdateFollowAnchor();
            }

            if (timer >= warmupTime && timer < activeEnd)
            {
                if (!activeStarted)
                {
                    activeStarted = true;
                    nextActiveTickElapsed = 0f;
                    if (boxCol != null)
                    {
                        boxCol.enabled = true;
                    }
                }

                float activeElapsed = timer - warmupTime;
                if (singleHitPerTarget)
                {
                    ApplyDamageTick();
                }
                else
                {
                    float safeTick = Mathf.Max(0.05f, tickRate);
                    while (activeElapsed >= nextActiveTickElapsed && nextActiveTickElapsed < duration - 0.0001f)
                    {
                        ApplyDamageTick();
                        nextActiveTickElapsed += safeTick;
                    }
                }
            }
            else if (timer >= activeEnd && !fadeOutStarted)
            {
                fadeOutStarted = true;
                if (boxCol != null)
                {
                    boxCol.enabled = false;
                }
            }

            UpdateSkillVfxVisual();

            if (timer >= totalLifetime)
            {
                ReleaseOrDestroy();
            }
        }

        private void UpdateFollowAnchor()
        {
            if (followAnchor != null)
            {
                transform.position = followAnchor.position + anchorLocalOffset;
            }
        }

        private void UpdateSkillVfxVisual()
        {
            if (activeVfxSprites == null || vfxRoot == null)
            {
                return;
            }

            Sprite refSprite = activeVfxSprites[activeVfxSprites.Length - 1];
            float spriteWorldWidth = refSprite.rect.width / Mathf.Max(1f, refSprite.pixelsPerUnit);
            float pivotFracX = refSprite.rect.width > 0f ? (refSprite.pivot.x / refSprite.rect.width) : SkillPivot.x;
            float activeLocalLen = Mathf.Max(0.1f, spriteWorldWidth * (1f - pivotFracX));
            float fullBeamScaleX = beamLength / activeLocalLen;

            float fIndex = 0f;
            float masterAlpha = 1f;
            float scaleX = fullBeamScaleX;
            float scaleY = 1f;
            Vector3 localPos = Vector3.zero;
            Color tint = Color.white;

            if (visualStyle == BeamVisualStyle.GokuKamehameha)
            {
                float baseScaleY = Mathf.Max(0.85f, beamWidth / 2.25f);
                EvaluateGokuTimeline(fullBeamScaleX, baseScaleY, out fIndex, out masterAlpha, out scaleX, out scaleY, out localPos, out tint);
            }
            else if (visualStyle == BeamVisualStyle.DiendPiercingCard)
            {
                float baseScaleY = Mathf.Max(0.95f, beamWidth / 1.5f);
                EvaluateDiendTimeline(fullBeamScaleX, baseScaleY, out fIndex, out masterAlpha, out scaleX, out scaleY, out localPos, out tint);
            }

            lastFrameFloat = fIndex;
            lastMasterAlpha = masterAlpha;
            lastVfxScale = new Vector3(direction * scaleX, scaleY, 1f);
            lastVfxLocalPos = localPos;
            lastTint = tint;

            vfxRoot.localScale = lastVfxScale;
            vfxRoot.localPosition = lastVfxLocalPos;
            ApplyCrossfadeBlend(activeVfxSprites, fIndex, masterAlpha, tint);
        }

        private void EvaluateGokuTimeline(
            float fullScaleX,
            float baseScaleY,
            out float fIndex,
            out float masterAlpha,
            out float scaleX,
            out float scaleY,
            out Vector3 localPos,
            out Color tint)
        {
            float activeEnd = warmupTime + duration;
            localPos = Vector3.zero;
            tint = Color.white;

            if (timer < warmupTime && warmupTime > 0f)
            {
                float u = Mathf.Clamp01(timer / warmupTime);
                float ease = 1f - (1f - u) * (1f - u);
                fIndex = Mathf.Pow(u, 1.35f);
                scaleX = fullScaleX * Mathf.Lerp(0.28f, 0.50f, ease);
                scaleY = baseScaleY * Mathf.Lerp(0.55f, 1.05f, ease);
                masterAlpha = Mathf.Clamp01(u / 0.14f);
                localPos = new Vector3(Mathf.Sin(timer * 65f) * 0.035f, Mathf.Cos(timer * 80f) * 0.035f, 0f);
                return;
            }

            if (timer < activeEnd)
            {
                float activeT = Mathf.Max(0f, timer - warmupTime);
                const float blastSpan = 0.22f;
                if (activeT < blastSpan)
                {
                    float b = Mathf.Clamp01(activeT / blastSpan);
                    float cubicOut = 1f - Mathf.Pow(1f - b, 3f);
                    fIndex = 1f + 3f * b;
                    scaleX = fullScaleX * Mathf.Lerp(0.50f, 1.0f, cubicOut);
                    scaleY = baseScaleY * Mathf.Lerp(1.05f, 1.0f, cubicOut);
                    masterAlpha = 1f;
                }
                else
                {
                    float surgeT = activeT - blastSpan;
                    float osc = 0.5f - 0.5f * Mathf.Cos(surgeT * 11f * Mathf.PI * 2f);
                    fIndex = 4f + osc;
                    scaleX = fullScaleX * (1f + 0.015f * Mathf.Sin(surgeT * 18f * Mathf.PI * 2f));
                    scaleY = baseScaleY * (1f + 0.06f * Mathf.Sin(surgeT * 14f * Mathf.PI * 2f));
                    masterAlpha = 1f;
                    tint = Color.Lerp(Color.white, new Color(0.86f, 0.97f, 1f, 1f), 0.5f + 0.5f * Mathf.Sin(surgeT * 20f));
                    localPos = new Vector3(0f, Mathf.Sin(surgeT * 75f) * 0.025f, 0f);
                }
                return;
            }

            float d = fadeOutTime > 0f ? Mathf.Clamp01((timer - activeEnd) / fadeOutTime) : 1f;
            float smoothD = d * d * (3f - 2f * d);
            float surgeEndT = Mathf.Max(0f, timer - warmupTime - 0.22f);
            fIndex = 4f + (0.5f - 0.5f * Mathf.Cos(surgeEndT * 11f * Mathf.PI * 2f));
            scaleX = fullScaleX;
            scaleY = baseScaleY * Mathf.Lerp(1.0f, 0.10f, smoothD);
            masterAlpha = 1f - smoothD;
        }

        private void EvaluateDiendTimeline(
            float fullScaleX,
            float baseScaleY,
            out float fIndex,
            out float masterAlpha,
            out float scaleX,
            out float scaleY,
            out Vector3 localPos,
            out Color tint)
        {
            float activeEnd = warmupTime + duration;
            localPos = Vector3.zero;
            tint = Color.white;

            if (timer < warmupTime && warmupTime > 0f)
            {
                float u = Mathf.Clamp01(timer / warmupTime);
                float ease = 1f - (1f - u) * (1f - u);
                fIndex = 2f * u;
                scaleX = fullScaleX * Mathf.Lerp(0.35f, 0.95f, ease);
                scaleY = baseScaleY * Mathf.Lerp(0.75f, 1.0f, ease);
                masterAlpha = Mathf.Clamp01(u / 0.18f);
                return;
            }

            if (timer < activeEnd)
            {
                float a = duration > 0f ? Mathf.Clamp01((timer - warmupTime) / duration) : 1f;
                fIndex = 2f + 2f * a;
                scaleX = fullScaleX * Mathf.Lerp(0.95f, 1.0f, Mathf.Clamp01(a * 4f));
                scaleY = baseScaleY * (1f + 0.04f * Mathf.Sin(a * Mathf.PI));
                masterAlpha = 1f;
                return;
            }

            float d = fadeOutTime > 0f ? Mathf.Clamp01((timer - activeEnd) / fadeOutTime) : 1f;
            float easeOut = 1f - (1f - d) * (1f - d);
            float smoothD = d * d * (3f - 2f * d);
            fIndex = 4f + d;
            scaleX = fullScaleX;
            scaleY = baseScaleY * Mathf.Lerp(1.0f, 0.55f, smoothD);
            localPos = new Vector3(direction * (4.5f * easeOut), 0f, 0f);
            masterAlpha = 1f - smoothD;
        }

        private void ApplyCrossfadeBlend(Sprite[] frames, float floatFrame, float masterAlpha, Color tint)
        {
            if (vfxLayerA == null || vfxLayerB == null || frames == null || frames.Length == 0)
            {
                return;
            }

            float clamped = Mathf.Clamp(floatFrame, 0f, frames.Length - 1);
            int idxA = Mathf.FloorToInt(clamped);
            int idxB = Mathf.Min(frames.Length - 1, idxA + 1);
            float frac = clamped - idxA;
            float smoothFrac = frac * frac * (3f - 2f * frac);

            if (idxA == idxB)
            {
                smoothFrac = 0f;
            }

            float alphaA = Mathf.Clamp01((1f - smoothFrac) * masterAlpha);
            float alphaB = Mathf.Clamp01(smoothFrac * masterAlpha);

            vfxLayerA.sprite = frames[idxA];
            vfxLayerA.color = new Color(tint.r, tint.g, tint.b, alphaA);
            vfxLayerA.enabled = alphaA > 0.002f;

            vfxLayerB.sprite = frames[idxB];
            vfxLayerB.color = new Color(tint.r, tint.g, tint.b, alphaB);
            vfxLayerB.enabled = alphaB > 0.002f;
        }

        private void EnsureVfxRenderers()
        {
            if (vfxRoot != null && vfxLayerA != null && vfxLayerB != null)
            {
                return;
            }

            int sortingLayerId = lineRenderer != null ? lineRenderer.sortingLayerID : 0;

            GameObject rootGo = new GameObject("VfxRoot");
            vfxRoot = rootGo.transform;
            vfxRoot.SetParent(transform, false);
            vfxRoot.localPosition = Vector3.zero;
            vfxRoot.localRotation = Quaternion.identity;
            vfxRoot.localScale = Vector3.one;

            vfxLayerA = CreateLayerRenderer("VfxLayerA", sortingLayerId, 24);
            vfxLayerB = CreateLayerRenderer("VfxLayerB", sortingLayerId, 25);
        }

        private SpriteRenderer CreateLayerRenderer(string childName, int sortingLayerId, int sortingOrder)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(vfxRoot, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = sortingLayerId;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        private static Sprite[] GetSkillSprites(BeamVisualStyle style)
        {
            if (style == BeamVisualStyle.GokuKamehameha)
            {
                if (gokuSkillSprites == null)
                {
                    gokuSkillSprites = LoadSkillSprites("Goku_TransSkill2");
                }
                return gokuSkillSprites;
            }

            if (style == BeamVisualStyle.DiendPiercingCard)
            {
                if (diendSkillSprites == null)
                {
                    diendSkillSprites = LoadSkillSprites("Diend_TransSkill2");
                }
                return diendSkillSprites;
            }

            return null;
        }

        private static Sprite[] LoadSkillSprites(string filePrefix)
        {
            Sprite[] result = new Sprite[SkillFrameCount];

            for (int i = 0; i < SkillFrameCount; i++)
            {
                string fileName = $"{filePrefix}_{i}.png";
                Sprite loaded = null;

#if UNITY_EDITOR
                string assetPath = $"Assets/Sprites/Combat/Skills/{fileName}";
                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
#endif

                if (loaded == null)
                {
                    loaded = TryLoadSpriteFromDisk(fileName);
                }

                if (loaded == null)
                {
                    return null;
                }

                result[i] = loaded;
            }

            return result;
        }

        private static Sprite TryLoadSpriteFromDisk(string fileName)
        {
            string[] candidates =
            {
                Path.Combine(Application.dataPath, "Sprites", "Combat", "Skills", fileName),
                Path.Combine(Application.streamingAssetsPath, "Sprites", "Combat", "Skills", fileName)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string fullPath = candidates[i];
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                byte[] bytes = File.ReadAllBytes(fullPath);
                if (bytes == null || bytes.Length == 0)
                {
                    continue;
                }

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, bytes, false))
                {
                    Destroy(tex);
                    continue;
                }

                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                runtimeLoadedTextures.Add(tex);

                Sprite sp = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    SkillPivot,
                    SkillSpritePpu,
                    0,
                    SpriteMeshType.FullRect
                );
                sp.name = Path.GetFileNameWithoutExtension(fileName);
                return sp;
            }

            return null;
        }

        private void ApplyDamageTick()
        {
            Vector2 origin = (Vector2)transform.position + new Vector2(direction * (beamLength / 2f), 0f);
            Vector2 size = new Vector2(beamLength, beamWidth);

            Collider2D[] hits = Physics2D.OverlapBoxAll(origin, size, 0f, targetLayer);
            HashSet<IDamageable> damaged = new HashSet<IDamageable>();

            foreach (var col in hits)
            {
                IDamageable target = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead && !damaged.Contains(target))
                {
                    if (singleHitPerTarget && alreadyHitTargets.Contains(target)) continue;
                    if (damageData.attacker != null && target.Transform.gameObject == damageData.attacker) continue;

                    damaged.Add(target);
                    if (singleHitPerTarget)
                    {
                        alreadyHitTargets.Add(target);
                    }

                    DamageData tickDamage = damageData;
                    tickDamage.knockbackForce = new Vector2(direction * Mathf.Abs(damageData.knockbackForce.x), damageData.knockbackForce.y);

                    target.TakeDamage(tickDamage);
                    StickmanBeatEmUp.Characters.BaseCharacter.NotifyAttackerHit(damageData.attacker, damageData.attackType, target);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector2 center = (Vector2)transform.position + new Vector2(direction * (beamLength / 2f), 0f);
            Gizmos.DrawWireCube(center, new Vector2(beamLength, beamWidth));
        }
    }
}
