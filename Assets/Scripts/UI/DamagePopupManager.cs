using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Object-pooled floating combat text manager (pool size = 36, dedicated ScreenSpaceOverlay Canvas sortingOrder = 92).
    /// Uses Time.unscaledDeltaTime so hit-stop freeze frames never stall damage numbers.
    /// </summary>
    public class DamagePopupManager : MonoBehaviour
    {
        private const int PoolSize = 36;
        private const int PopupSortingOrder = 92;
        private const float PopupLifetime = 0.65f;

        private static readonly Color NormalHitColor = new Color(1f, 0.9529412f, 0.7215686f, 1f); // #FFF3B8
        private static readonly Color HeavyCritColor = new Color(1f, 0.36f, 0.14f, 1f);
        private static readonly Color ShieldBlockedColor = new Color(0.18f, 0.96f, 1f, 1f);
        private static readonly Color PlayerDamagedColor = new Color(1f, 0.24f, 0.34f, 1f);

        private struct PopupSlot
        {
            public GameObject gameObject;
            public RectTransform rectTransform;
            public TextMeshProUGUI tmp;
            public bool active;
            public float elapsed;
            public Vector2 startAnchoredPos;
            public float riseDistance;
            public float startScale;
            public Color baseColor;
        }

        private static DamagePopupManager instance;
        private static bool isQuitting;
        private static TMP_FontAsset cachedFontAsset;
        private static Font cachedOsFont;

        private Canvas popupCanvas;
        private RectTransform canvasRect;
        private PopupSlot[] pool;
        private int nextPoolIndex;
        private int alternatingJitterSign = 1;
        private bool poolInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            isQuitting = false;
            cachedFontAsset = null;
            cachedOsFont = null;
        }

        public static DamagePopupManager EnsureInstance()
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

            instance = FindAnyObjectByType<DamagePopupManager>();
            if (instance != null)
            {
                instance.EnsureInitialized();
                return instance;
            }

            GameObject go = new GameObject("DamagePopupManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DamagePopupManager>();
            instance.EnsureInitialized();
            return instance;
        }

        public static void SpawnDamage(
            Vector3 worldPos,
            float amount,
            AttackType attackType = AttackType.NormalAttack_J,
            bool isCriticalOrHeavy = false)
        {
            if (!Application.isPlaying || isQuitting || amount <= 0f) return;
            if (GameSettingsManager.HasInstance && !GameSettingsManager.Instance.ShowDamagePopups) return;

            DamagePopupManager mgr = EnsureInstance();
            if (mgr == null) return;

            int displayAmount = Mathf.Max(1, Mathf.RoundToInt(amount));
            bool isHeavyTier = isCriticalOrHeavy ||
                               amount >= 24f ||
                               attackType == AttackType.Skill_U ||
                               attackType == AttackType.Skill_I ||
                               attackType == AttackType.Combo_DashJ ||
                               attackType == AttackType.Combo_DashI ||
                               attackType == AttackType.TransformBurst;

            if (isHeavyTier)
            {
                bool showCritTag = amount >= 35f ||
                                   attackType == AttackType.Combo_DashI ||
                                   attackType == AttackType.Combo_DashJ;
                string label = showCritTag ? $"CRIT {displayAmount}!" : $"{displayAmount}!";
                mgr.SpawnInternal(worldPos, label, 35f, HeavyCritColor, 1.42f, 86f);
            }
            else
            {
                mgr.SpawnInternal(worldPos, displayAmount.ToString(), 27f, NormalHitColor, 1.22f, 70f);
            }
        }

        public static void SpawnBlocked(Vector3 worldPos, float chipDamage = 0f)
        {
            if (!Application.isPlaying || isQuitting) return;
            if (GameSettingsManager.HasInstance && !GameSettingsManager.Instance.ShowDamagePopups) return;

            DamagePopupManager mgr = EnsureInstance();
            if (mgr == null) return;

            string label = chipDamage > 0.01f && chipDamage < 1f
                ? "BLOCKED"
                : (chipDamage >= 1f ? $"ĐỠ -{Mathf.RoundToInt(chipDamage)}" : "BLOCKED");

            mgr.SpawnInternal(worldPos, label, 25f, ShieldBlockedColor, 1.20f, 62f);
        }

        public static void SpawnPlayerHit(Vector3 worldPos, float amount)
        {
            if (!Application.isPlaying || isQuitting || amount <= 0f) return;
            if (GameSettingsManager.HasInstance && !GameSettingsManager.Instance.ShowDamagePopups) return;

            DamagePopupManager mgr = EnsureInstance();
            if (mgr == null) return;

            int displayAmount = Mathf.Max(1, Mathf.RoundToInt(amount));
            mgr.SpawnInternal(worldPos, $"-{displayAmount}", 26f, PlayerDamagedColor, 1.24f, 68f);
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
        }

        private void EnsureInitialized()
        {
            if (poolInitialized) return;
            poolInitialized = true;

            GameObject canvasObj = new GameObject("DamagePopupCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObj.transform.SetParent(transform, false);

            popupCanvas = canvasObj.GetComponent<Canvas>();
            popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            popupCanvas.sortingOrder = PopupSortingOrder;
            popupCanvas.pixelPerfect = false;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasRect = canvasObj.GetComponent<RectTransform>();
            TMP_FontAsset fontAsset = ResolvePopupFont();

            pool = new PopupSlot[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                GameObject slotObj = new GameObject(
                    $"DamagePopup_{i:00}",
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI),
                    typeof(Outline),
                    typeof(Shadow));
                slotObj.transform.SetParent(canvasRect, false);

                RectTransform rt = slotObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(420f, 96f);
                rt.localScale = Vector3.one;

                TextMeshProUGUI tmp = slotObj.GetComponent<TextMeshProUGUI>();
                if (fontAsset != null)
                {
                    tmp.font = fontAsset;
                }
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.raycastTarget = false;

                Outline outline = slotObj.GetComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
                outline.effectDistance = new Vector2(1.4f, -1.4f);
                outline.useGraphicAlpha = true;

                Shadow[] shadows = slotObj.GetComponents<Shadow>();
                for (int s = 0; s < shadows.Length; s++)
                {
                    if (shadows[s] != null && shadows[s].GetType() == typeof(Shadow))
                    {
                        shadows[s].effectColor = new Color(0f, 0f, 0f, 0.85f);
                        shadows[s].effectDistance = new Vector2(2f, -2.2f);
                        shadows[s].useGraphicAlpha = true;
                        break;
                    }
                }

                slotObj.SetActive(false);

                pool[i] = new PopupSlot
                {
                    gameObject = slotObj,
                    rectTransform = rt,
                    tmp = tmp,
                    active = false,
                    elapsed = 0f,
                    startAnchoredPos = Vector2.zero,
                    riseDistance = 70f,
                    startScale = 1.22f,
                    baseColor = NormalHitColor
                };
            }
        }

        private void SpawnInternal(
            Vector3 worldPos,
            string text,
            float fontSize,
            Color color,
            float startScale,
            float riseDistance)
        {
            EnsureInitialized();
            if (pool == null || pool.Length == 0 || canvasRect == null) return;

            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = FindAnyObjectByType<Camera>();
            }
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0f) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    new Vector2(screenPos.x, screenPos.y),
                    null,
                    out Vector2 localPoint))
            {
                return;
            }

            alternatingJitterSign = -alternatingJitterSign;
            float jitterX = alternatingJitterSign * UnityEngine.Random.Range(6f, 22f);
            float jitterY = UnityEngine.Random.Range(10f, 24f);
            localPoint += new Vector2(jitterX, jitterY);

            Rect rect = canvasRect.rect;
            if (rect.width > 120f && rect.height > 120f)
            {
                float padX = 95f;
                float padY = 64f;
                localPoint.x = Mathf.Clamp(localPoint.x, rect.xMin + padX, rect.xMax - padX);
                localPoint.y = Mathf.Clamp(localPoint.y, rect.yMin + padY, rect.yMax - padY - riseDistance);
            }

            int idx = AcquireSlotIndex();
            PopupSlot slot = pool[idx];

            slot.active = true;
            slot.elapsed = 0f;
            slot.startAnchoredPos = localPoint;
            slot.riseDistance = riseDistance;
            slot.startScale = startScale;
            slot.baseColor = color;

            slot.rectTransform.anchoredPosition = localPoint;
            slot.rectTransform.localScale = Vector3.one * startScale;
            slot.rectTransform.SetAsLastSibling();

            slot.tmp.text = text;
            slot.tmp.fontSize = fontSize;
            slot.tmp.color = color;

            slot.gameObject.SetActive(true);
            pool[idx] = slot;
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

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].active) continue;

                PopupSlot slot = pool[i];
                slot.elapsed += dt;

                if (slot.elapsed >= PopupLifetime)
                {
                    slot.active = false;
                    if (slot.gameObject != null)
                    {
                        slot.gameObject.SetActive(false);
                    }
                    pool[i] = slot;
                    continue;
                }

                float t = Mathf.Clamp01(slot.elapsed / PopupLifetime);

                // Pop-in scale settles from startScale -> 1.0f within first 24% of lifetime
                float scalePhase = Mathf.Clamp01(t / 0.24f);
                float easedScalePhase = 1f - Mathf.Pow(1f - scalePhase, 2.5f);
                float scale = Mathf.Lerp(slot.startScale, 1f, easedScalePhase);
                slot.rectTransform.localScale = new Vector3(scale, scale, 1f);

                // Smooth upward float with gentle deceleration
                float riseProgress = 1f - (1f - t) * (1f - t);
                slot.rectTransform.anchoredPosition = slot.startAnchoredPos + new Vector2(0f, slot.riseDistance * riseProgress);

                // Hold solid readability then fade out cleanly in final 38%
                Color c = slot.baseColor;
                if (t > 0.62f)
                {
                    float fadeT = Mathf.Clamp01((t - 0.62f) / 0.38f);
                    c.a = slot.baseColor.a * (1f - fadeT * fadeT);
                }
                slot.tmp.color = c;

                pool[i] = slot;
            }
        }

        private static TMP_FontAsset ResolvePopupFont()
        {
            if (cachedFontAsset != null)
            {
                return cachedFontAsset;
            }

            Font sourceFont = null;
#if UNITY_EDITOR
            sourceFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SegoeUIBoldVN.ttf");
#endif
            if (sourceFont == null)
            {
                sourceFont = Resources.Load<Font>("Fonts/SegoeUIBoldVN") ?? Resources.Load<Font>("SegoeUIBoldVN");
            }

            if (sourceFont == null)
            {
                if (cachedOsFont == null)
                {
                    cachedOsFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 36);
                    if (cachedOsFont != null)
                    {
                        cachedOsFont.hideFlags = HideFlags.DontSave;
                    }
                }
                sourceFont = cachedOsFont;
            }

            if (sourceFont != null)
            {
                TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(sourceFont);
                if (created != null)
                {
                    created.name = "DamagePopupRuntimeTMPFont";
                    created.hideFlags = HideFlags.DontSave;
                    if (created.atlasTexture != null)
                    {
                        created.atlasTexture.hideFlags = HideFlags.DontSave;
                    }
                    if (created.material != null)
                    {
                        created.material.hideFlags = HideFlags.DontSave;
                    }
                    cachedFontAsset = created;
                    return cachedFontAsset;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }
    }
}
