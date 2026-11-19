using UnityEngine;
using UnityEngine.UI;
using StickmanBeatEmUp.Enemies;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Reusable delayed damage-trail accumulator for HP bars (Player, Boss, and Enemy World Bars).
    /// Holds the previous trail level briefly on damage before smoothly decaying to current HP,
    /// and snaps immediately upward on healing or resets.
    /// </summary>
    internal struct DelayedHealthTrail
    {
        public float CurrentFill;
        public float TrailFill;
        private float holdTimer;
        private bool initialized;

        public void Reset(float initialNormalized)
        {
            float clamped = Mathf.Clamp01(initialNormalized);
            CurrentFill = clamped;
            TrailFill = clamped;
            holdTimer = 0f;
            initialized = true;
        }

        public void Update(float targetNormalized, float deltaTime, float holdDuration, float decayDuration)
        {
            float target = Mathf.Clamp01(targetNormalized);
            if (!initialized)
            {
                Reset(target);
                return;
            }

            if (target > CurrentFill + 0.0005f)
            {
                CurrentFill = target;
                TrailFill = target;
                holdTimer = 0f;
                return;
            }

            if (target < CurrentFill - 0.0005f)
            {
                if (TrailFill < CurrentFill)
                {
                    TrailFill = CurrentFill;
                }
                holdTimer = Mathf.Max(0f, holdDuration);
                CurrentFill = target;
            }
            else
            {
                CurrentFill = target;
            }

            if (TrailFill > CurrentFill)
            {
                if (holdTimer > 0f)
                {
                    holdTimer = Mathf.Max(0f, holdTimer - deltaTime);
                }
                else
                {
                    float diff = TrailFill - CurrentFill;
                    float safeDecay = Mathf.Max(0.12f, decayDuration);
                    float speed = Mathf.Max(0.40f, (diff * 2.2f) / safeDecay);
                    TrailFill = Mathf.MoveTowards(TrailFill, CurrentFill, speed * deltaTime);
                }
            }
            else
            {
                TrailFill = CurrentFill;
            }
        }
    }

    /// <summary>
    /// Lightweight World-Space overhead HP bar for active EnemyBase instances (Creeps & Elites).
    /// Constructs a child "WorldHealthBar" Canvas above the enemy's bounds, protects against parent
    /// sprite X-flipping so the bar always drains left-to-right, and renders a delayed damage trail.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyWorldHealthBar : MonoBehaviour
    {
        private const string BarChildName = "WorldHealthBar";
        private const float BarWorldScale = 0.01f;
        private const float CreepWorldWidth = 135f;
        private const float CreepWorldHeight = 18f;
        private const float EliteWorldWidth = 175f;
        private const float EliteWorldHeight = 22f;
        private const float HeadClearanceWorldY = 0.35f;
        private const float TrailHoldSeconds = 0.25f;
        private const float TrailDecaySeconds = 0.38f;

        [Header("Target & Visual References")]
        [SerializeField] private EnemyBase targetEnemy;
        [SerializeField] private Canvas barCanvas;
        [SerializeField] private CanvasGroup barCanvasGroup;
        [SerializeField] private RectTransform barRectTransform;
        [SerializeField] private Image frameImage;
        [SerializeField] private Image trailImage;
        [SerializeField] private Image fillImage;

        private bool isEliteEnemy;
        private bool isBound;
        private bool hasResolvedHeadOffset;
        private float cachedHeadWorldOffset = 1.85f;
        private float latestNormalizedHp = 1f;
        private DelayedHealthTrail trailState;

        private static Sprite fallbackWhiteSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (fallbackWhiteSprite != null)
            {
                if (fallbackWhiteSprite.texture != null)
                {
                    DestroyImmediate(fallbackWhiteSprite.texture);
                }
                DestroyImmediate(fallbackWhiteSprite);
                fallbackWhiteSprite = null;
            }
        }

        /// <summary>
        /// Attaches or resolves an EnemyWorldHealthBar for a live Creep/Elite EnemyBase.
        /// </summary>
        public static EnemyWorldHealthBar AttachTo(EnemyBase enemy)
        {
            if (enemy == null || enemy.IsDead) return null;
            if (enemy.Data != null && enemy.Data.isBoss) return null;

            EnemyWorldHealthBar existing = enemy.GetComponentInChildren<EnemyWorldHealthBar>(true);
            if (existing != null)
            {
                existing.Bind(enemy);
                return existing;
            }

            EnemyWorldHealthBar bar = enemy.gameObject.AddComponent<EnemyWorldHealthBar>();
            bar.Bind(enemy);
            return bar;
        }

        private void Awake()
        {
            ResolveEnemyReference();
            EnsureBarHierarchy();
        }

        private void Start()
        {
            ResolveEnemyReference();
            if (targetEnemy != null)
            {
                Bind(targetEnemy);
            }
            else
            {
                SetBarVisible(false);
            }
        }

        private void OnDestroy()
        {
            UnbindEnemyEvents();
        }

        /// <summary>
        /// Binds this world bar to the specified EnemyBase and initializes layout/events.
        /// </summary>
        public void Bind(EnemyBase enemy)
        {
            if (enemy == null) return;

            if (targetEnemy != enemy)
            {
                UnbindEnemyEvents();
                targetEnemy = enemy;
            }

            isEliteEnemy = (targetEnemy is EliteEnemy) || (targetEnemy.Data != null && targetEnemy.Data.isElite);
            EnsureBarHierarchy();
            ApplyThemeSprites();
            UpdateBarDimensions();
            ResolveHeadWorldOffset(forceRefresh: true);

            if (!isBound && targetEnemy != null)
            {
                targetEnemy.OnHealthChanged -= HandleEnemyHealthChanged;
                targetEnemy.OnDied -= HandleEnemyDied;
                targetEnemy.OnHealthChanged += HandleEnemyHealthChanged;
                targetEnemy.OnDied += HandleEnemyDied;
                isBound = true;
            }

            RefreshFromEnemy(snapTrail: true);
            UpdateTransformAndFlipProtection();
        }

        /// <summary>
        /// Immediately synchronizes fill, trail (optionally snapping), and visibility with the enemy's HP.
        /// </summary>
        public void RefreshFromEnemy(bool snapTrail = false)
        {
            if (targetEnemy == null || targetEnemy.IsDead)
            {
                SetBarVisible(false);
                return;
            }

            float maxHp = Mathf.Max(0.01f, targetEnemy.MaxHealth);
            float curHp = Mathf.Clamp(targetEnemy.CurrentHealth, 0f, maxHp);
            latestNormalizedHp = Mathf.Clamp01(curHp / maxHp);

            if (snapTrail)
            {
                trailState.Reset(latestNormalizedHp);
            }
            else
            {
                trailState.Update(latestNormalizedHp, 0f, TrailHoldSeconds, TrailDecaySeconds);
            }

            ApplyFillValues();
            UpdateVisibility(curHp, maxHp);
        }

        private void LateUpdate()
        {
            if (targetEnemy == null || targetEnemy.IsDead)
            {
                SetBarVisible(false);
                return;
            }

            if (!hasResolvedHeadOffset)
            {
                ResolveHeadWorldOffset(forceRefresh: false);
            }

            float maxHp = Mathf.Max(0.01f, targetEnemy.MaxHealth);
            float curHp = Mathf.Clamp(targetEnemy.CurrentHealth, 0f, maxHp);
            latestNormalizedHp = Mathf.Clamp01(curHp / maxHp);

            trailState.Update(latestNormalizedHp, Time.unscaledDeltaTime, TrailHoldSeconds, TrailDecaySeconds);
            ApplyFillValues();
            UpdateVisibility(curHp, maxHp);
            UpdateTransformAndFlipProtection();
        }

        private void ResolveEnemyReference()
        {
            if (targetEnemy != null) return;
            targetEnemy = GetComponent<EnemyBase>();
            if (targetEnemy == null)
            {
                targetEnemy = GetComponentInParent<EnemyBase>();
            }
        }

        private void UnbindEnemyEvents()
        {
            if (isBound && targetEnemy != null)
            {
                targetEnemy.OnHealthChanged -= HandleEnemyHealthChanged;
                targetEnemy.OnDied -= HandleEnemyDied;
            }
            isBound = false;
        }

        private void HandleEnemyHealthChanged(float current, float max)
        {
            float safeMax = Mathf.Max(0.01f, max);
            float safeCur = Mathf.Clamp(current, 0f, safeMax);
            latestNormalizedHp = Mathf.Clamp01(safeCur / safeMax);
            trailState.Update(latestNormalizedHp, 0f, TrailHoldSeconds, TrailDecaySeconds);
            ApplyFillValues();
            UpdateVisibility(safeCur, safeMax);
        }

        private void HandleEnemyDied(EnemyBase deadEnemy)
        {
            SetBarVisible(false);
            UnbindEnemyEvents();
        }

        private void EnsureBarHierarchy()
        {
            Transform enemyRoot = targetEnemy != null ? targetEnemy.transform : transform;
            bool componentIsOnChildBar = GetComponent<EnemyBase>() == null && GetComponentInParent<EnemyBase>() != null;

            if (barRectTransform == null)
            {
                if (componentIsOnChildBar)
                {
                    barRectTransform = GetComponent<RectTransform>();
                    if (barRectTransform == null) barRectTransform = gameObject.AddComponent<RectTransform>();
                }
                else
                {
                    Transform existingBar = enemyRoot.Find(BarChildName);
                    GameObject barObj = existingBar != null
                        ? existingBar.gameObject
                        : new GameObject(BarChildName, typeof(RectTransform));

                    if (existingBar == null)
                    {
                        barObj.transform.SetParent(enemyRoot, false);
                    }
                    barRectTransform = barObj.GetComponent<RectTransform>();
                }
            }

            GameObject rootGo = barRectTransform.gameObject;
            if (barCanvas == null)
            {
                barCanvas = rootGo.GetComponent<Canvas>();
                if (barCanvas == null) barCanvas = rootGo.AddComponent<Canvas>();
            }

            barCanvas.renderMode = RenderMode.WorldSpace;
            barCanvas.overrideSorting = true;
            barCanvas.sortingOrder = 25;
            if (barCanvas.worldCamera == null)
            {
                barCanvas.worldCamera = Camera.main;
            }

            if (barCanvasGroup == null)
            {
                barCanvasGroup = rootGo.GetComponent<CanvasGroup>();
                if (barCanvasGroup == null) barCanvasGroup = rootGo.AddComponent<CanvasGroup>();
            }
            barCanvasGroup.interactable = false;
            barCanvasGroup.blocksRaycasts = false;

            barRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            barRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            barRectTransform.pivot = new Vector2(0.5f, 0.5f);

            frameImage = EnsureChildImage(rootGo.transform, "Frame", Vector2.zero, Vector2.zero, 0);
            Vector2 innerPad = isEliteEnemy ? new Vector2(4f, 3f) : new Vector2(3.5f, 2.5f);
            trailImage = EnsureChildImage(rootGo.transform, "Trail", innerPad, -innerPad, 1);
            fillImage = EnsureChildImage(rootGo.transform, "Fill", innerPad, -innerPad, 2);
        }

        private Image EnsureChildImage(Transform parent, string childName, Vector2 offsetMin, Vector2 offsetMax, int siblingIndex)
        {
            Transform existing = parent.Find(childName);
            GameObject go = existing != null
                ? existing.gameObject
                : new GameObject(childName, typeof(RectTransform), typeof(Image));

            if (existing == null)
            {
                go.transform.SetParent(parent, false);
            }

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            go.transform.SetSiblingIndex(siblingIndex);

            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        private void ApplyThemeSprites()
        {
            if (frameImage != null)
            {
                GlassHudTheme.ApplySprite(frameImage, "WorldHPBar_Frame_Dark.png", sliced: true, preserveAspect: false);
                if (frameImage.sprite == null)
                {
                    frameImage.sprite = GetFallbackWhiteSprite();
                    frameImage.type = Image.Type.Simple;
                    frameImage.color = new Color(0.08f, 0.10f, 0.14f, 0.90f);
                }
            }

            if (trailImage != null)
            {
                GlassHudTheme.ApplyFilledBar(trailImage, "HPBar_Trail_WhiteAmber.png");
                if (trailImage.sprite == null)
                {
                    trailImage.sprite = GetFallbackWhiteSprite();
                    trailImage.type = Image.Type.Filled;
                    trailImage.fillMethod = Image.FillMethod.Horizontal;
                    trailImage.fillOrigin = 0;
                    trailImage.color = new Color(1f, 0.84f, 0.48f, 0.92f);
                }
            }

            if (fillImage != null)
            {
                GlassHudTheme.ApplyFilledBar(fillImage, "WorldHPBar_Fill_Red.png");
                if (fillImage.sprite == null)
                {
                    fillImage.sprite = GetFallbackWhiteSprite();
                    fillImage.type = Image.Type.Filled;
                    fillImage.fillMethod = Image.FillMethod.Horizontal;
                    fillImage.fillOrigin = 0;
                    fillImage.color = isEliteEnemy
                        ? new Color(0.95f, 0.28f, 0.16f, 1f)
                        : new Color(0.88f, 0.16f, 0.16f, 1f);
                }
            }
        }

        private void UpdateBarDimensions()
        {
            if (barRectTransform == null) return;
            barRectTransform.sizeDelta = isEliteEnemy
                ? new Vector2(EliteWorldWidth, EliteWorldHeight)
                : new Vector2(CreepWorldWidth, CreepWorldHeight);
        }

        private void ResolveHeadWorldOffset(bool forceRefresh)
        {
            if (targetEnemy == null) return;
            if (hasResolvedHeadOffset && !forceRefresh) return;

            float originY = targetEnemy.transform.position.y;
            float bestTopOffset = 0f;

            Collider2D col = targetEnemy.GetComponent<Collider2D>();
            if (col != null && col.enabled)
            {
                bestTopOffset = Mathf.Max(bestTopOffset, col.bounds.max.y - originY);
            }

            SpriteRenderer sr = targetEnemy.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                bestTopOffset = Mathf.Max(bestTopOffset, sr.bounds.max.y - originY);
            }

            if (bestTopOffset > 0.25f)
            {
                cachedHeadWorldOffset = Mathf.Clamp(bestTopOffset, 0.9f, 4.5f) + HeadClearanceWorldY;
                hasResolvedHeadOffset = true;
            }
            else
            {
                float fallbackScaleY = Mathf.Max(0.5f, Mathf.Abs(targetEnemy.transform.lossyScale.y));
                cachedHeadWorldOffset = (isEliteEnemy ? 1.95f : 1.55f) * fallbackScaleY + HeadClearanceWorldY;
            }
        }

        private void UpdateTransformAndFlipProtection()
        {
            if (barRectTransform == null) return;

            Transform parentTr = barRectTransform.parent != null ? barRectTransform.parent : transform;
            Vector3 parentLossy = parentTr.lossyScale;

            float safeAbsX = Mathf.Max(0.0001f, Mathf.Abs(parentLossy.x));
            float safeAbsY = Mathf.Max(0.0001f, Mathf.Abs(parentLossy.y));
            float flipSignX = parentLossy.x < 0f ? -1f : 1f;

            barRectTransform.localScale = new Vector3(
                flipSignX * (BarWorldScale / safeAbsX),
                BarWorldScale / safeAbsY,
                1f);
            barRectTransform.localPosition = new Vector3(0f, cachedHeadWorldOffset / safeAbsY, 0f);
            barRectTransform.rotation = Quaternion.identity;

            if (barCanvas != null && barCanvas.worldCamera == null)
            {
                barCanvas.worldCamera = Camera.main;
            }
        }

        private void ApplyFillValues()
        {
            if (fillImage != null)
            {
                fillImage.fillAmount = Mathf.Clamp01(trailState.CurrentFill);
            }

            if (trailImage != null)
            {
                trailImage.fillAmount = Mathf.Clamp01(Mathf.Max(trailState.TrailFill, trailState.CurrentFill));
            }
        }

        private void UpdateVisibility(float currentHp, float maxHp)
        {
            if (targetEnemy == null || targetEnemy.IsDead || maxHp <= 0.01f)
            {
                SetBarVisible(false);
                return;
            }

            bool damaged = currentHp < maxHp - 0.01f;
            bool shouldShow = isEliteEnemy || damaged;
            SetBarVisible(shouldShow);
        }

        private void SetBarVisible(bool visible)
        {
            if (barCanvas != null)
            {
                barCanvas.enabled = visible;
            }

            if (barCanvasGroup != null)
            {
                barCanvasGroup.alpha = visible ? 1f : 0f;
            }
        }

        private static Sprite GetFallbackWhiteSprite()
        {
            if (fallbackWhiteSprite != null) return fallbackWhiteSprite;

            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.DontSave;
            Color32[] pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            fallbackWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            fallbackWhiteSprite.hideFlags = HideFlags.DontSave;
            return fallbackWhiteSprite;
        }
    }
}
