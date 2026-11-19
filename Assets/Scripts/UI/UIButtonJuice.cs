using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Adds responsive hover scale (1.05x), subtle graphic brightness lift, and click squish (0.94x)
    /// using Time.unscaledDeltaTime so interactions remain smooth while Time.timeScale == 0.
    /// Multiplies relative to external scale owners (e.g. CharCard 1.08x selection) without fighting them.
    /// </summary>
    public class UIButtonJuice : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] private float hoverScale = 1.04f;
        [SerializeField] private float pressScale = 0.97f;
        [SerializeField] private float transitionDuration = 0.08f;
        [SerializeField] private float hoverBrightnessBoost = 0.16f;

        private static UIButtonJuiceBootstrapper bootstrapperInstance;
        private static bool isQuitting;

        private Selectable selectable;
        private Graphic targetGraphic;
        private Graphic hitGraphic;
        private Transform scaleTarget;

        private bool isHovered;
        private bool isPressed;

        private float currentScaleFactor = 1f;
        private float lastAppliedFactor = 1f;
        private Vector3 baseScale = Vector3.one;
        private Vector3 lastWrittenScale = Vector3.one;
        private bool hasWrittenScale;

        private Vector4 baseRaycastPadding;
        private bool hasCapturedPadding;
        private bool hasExpandedHitbox;

        private float currentTintState;
        private Color baseGraphicColor = Color.white;
        private Color lastWrittenColor = Color.white;
        private bool hasWrittenColor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            bootstrapperInstance = null;
            isQuitting = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeBootstrapperAfterSceneLoad()
        {
            EnsureBootstrapper();
        }

        internal static void MarkQuitting()
        {
            isQuitting = true;
        }

        internal static void ClearBootstrapperIfSame(UIButtonJuiceBootstrapper candidate)
        {
            if (bootstrapperInstance == candidate)
            {
                bootstrapperInstance = null;
            }
        }

        public static void EnsureBootstrapper()
        {
            if (!Application.isPlaying || isQuitting) return;

            if (bootstrapperInstance != null)
            {
                bootstrapperInstance.AttachToSceneButtons();
                return;
            }

            bootstrapperInstance = FindAnyObjectByType<UIButtonJuiceBootstrapper>();
            if (bootstrapperInstance != null)
            {
                bootstrapperInstance.AttachToSceneButtons();
                return;
            }

            GameObject go = new GameObject("UIButtonJuiceBootstrapper");
            DontDestroyOnLoad(go);
            bootstrapperInstance = go.AddComponent<UIButtonJuiceBootstrapper>();
            bootstrapperInstance.AttachToSceneButtons();
        }

        private void Awake()
        {
            if (GetComponent<ModeCardHoverEffect>() != null)
            {
                Destroy(this);
                return;
            }

            CacheComponents();
        }

        private void OnEnable()
        {
            if (GetComponent<ModeCardHoverEffect>() != null)
            {
                Destroy(this);
                return;
            }

            CacheComponents();
            isHovered = false;
            isPressed = false;
            currentScaleFactor = 1f;
            currentTintState = 0f;
            lastAppliedFactor = 1f;
            hasWrittenScale = false;
            hasWrittenColor = false;
        }

        private void OnDisable()
        {
            RestoreVisualState();
        }

        private void OnDestroy()
        {
            RestoreVisualState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable()) return;
            isHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable()) return;
            isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }

        private void Update()
        {
            if (GetComponent<ModeCardHoverEffect>() != null)
            {
                RestoreVisualState();
                Destroy(this);
                return;
            }

            bool interactable = IsInteractable();
            if (!interactable)
            {
                isHovered = false;
                isPressed = false;
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float rate = 4.2f / Mathf.Max(0.01f, transitionDuration);
            float blend = 1f - Mathf.Exp(-dt * rate);

            UpdateScaleFactor(interactable, blend);
            UpdateGraphicTint(interactable, blend);
            UpdateHitboxPadding(interactable && (isHovered || isPressed));
        }

        private void UpdateScaleFactor(bool interactable, float blend)
        {
            Transform activeTarget = scaleTarget != null ? scaleTarget : transform;
            if (activeTarget == null) return;

            float targetFactor = !interactable
                ? 1f
                : (isPressed ? pressScale : (isHovered ? hoverScale : 1f));

            currentScaleFactor = Mathf.Lerp(currentScaleFactor, targetFactor, blend);
            if (Mathf.Abs(currentScaleFactor - targetFactor) < 0.0005f)
            {
                currentScaleFactor = targetFactor;
            }

            if (hasWrittenScale)
            {
                if (Approximately(activeTarget.localScale, lastWrittenScale))
                {
                    float safeLast = Mathf.Abs(lastAppliedFactor) > 0.001f ? lastAppliedFactor : 1f;
                    baseScale = activeTarget.localScale / safeLast;
                }
                else if (activeTarget.localScale.sqrMagnitude >= 0.00001f)
                {
                    baseScale = activeTarget.localScale;
                }
            }
            else
            {
                if (Mathf.Approximately(currentScaleFactor, 1f))
                {
                    return;
                }

                if (activeTarget.localScale.sqrMagnitude < 0.00001f)
                {
                    return;
                }

                baseScale = activeTarget.localScale;
            }

            if (baseScale.sqrMagnitude < 0.00001f)
            {
                return;
            }

            if (Mathf.Approximately(currentScaleFactor, 1f) && !isHovered && !isPressed)
            {
                activeTarget.localScale = baseScale;
                lastAppliedFactor = 1f;
                hasWrittenScale = false;
                return;
            }

            Vector3 nextScale = baseScale * currentScaleFactor;
            activeTarget.localScale = nextScale;
            lastWrittenScale = nextScale;
            lastAppliedFactor = currentScaleFactor;
            hasWrittenScale = true;
        }

        private void UpdateGraphicTint(bool interactable, float blend)
        {
            if (targetGraphic == null && selectable != null)
            {
                targetGraphic = selectable.targetGraphic;
            }
            if (targetGraphic == null) return;

            float targetTint = !interactable
                ? 0f
                : (isPressed ? -0.8f : (isHovered ? 1f : 0f));

            currentTintState = Mathf.Lerp(currentTintState, targetTint, blend);
            if (Mathf.Abs(currentTintState - targetTint) < 0.002f)
            {
                currentTintState = targetTint;
            }

            if (hasWrittenColor)
            {
                if (!ColorsApproximately(targetGraphic.color, lastWrittenColor))
                {
                    baseGraphicColor = targetGraphic.color;
                    hasWrittenColor = false;
                }
            }
            else
            {
                if (Mathf.Approximately(currentTintState, 0f))
                {
                    return;
                }

                baseGraphicColor = targetGraphic.color;
            }

            if (baseGraphicColor.a <= 0.05f)
            {
                hasWrittenColor = false;
                return;
            }

            if (Mathf.Approximately(currentTintState, 0f) && !isHovered && !isPressed)
            {
                if (hasWrittenColor)
                {
                    targetGraphic.color = baseGraphicColor;
                    hasWrittenColor = false;
                }
                return;
            }

            Color desiredColor;
            if (currentTintState >= 0f)
            {
                Color liftTarget = new Color(1f, 1f, 1f, baseGraphicColor.a);
                desiredColor = Color.Lerp(baseGraphicColor, liftTarget, hoverBrightnessBoost * currentTintState);
            }
            else
            {
                Color pressTarget = new Color(0.84f, 0.88f, 0.95f, baseGraphicColor.a);
                desiredColor = Color.Lerp(baseGraphicColor, pressTarget, 0.16f * Mathf.Abs(currentTintState));
            }

            targetGraphic.color = desiredColor;
            lastWrittenColor = desiredColor;
            hasWrittenColor = true;
        }

        private void RestoreVisualState()
        {
            isHovered = false;
            isPressed = false;
            currentScaleFactor = 1f;
            currentTintState = 0f;

            UpdateHitboxPadding(false);

            Transform activeTarget = scaleTarget != null ? scaleTarget : transform;
            if (hasWrittenScale)
            {
                if (activeTarget != null && Approximately(activeTarget.localScale, lastWrittenScale))
                {
                    activeTarget.localScale = baseScale;
                }
                hasWrittenScale = false;
                lastAppliedFactor = 1f;
            }

            if (hasWrittenColor)
            {
                if (targetGraphic != null && ColorsApproximately(targetGraphic.color, lastWrittenColor))
                {
                    targetGraphic.color = baseGraphicColor;
                }
                hasWrittenColor = false;
            }
        }

        private void CacheComponents()
        {
            if (selectable == null) selectable = GetComponent<Selectable>();
            if (targetGraphic == null && selectable != null) targetGraphic = selectable.targetGraphic;

            if (gameObject.name == "CardInner" && transform.parent != null)
            {
                scaleTarget = transform.parent;
                if (TryGetComponent(out Image ownImg))
                {
                    ownImg.raycastTarget = true;
                    ownImg.raycastPadding = new Vector4(-8f, -8f, -8f, -8f);
                }
                if (transform.parent.TryGetComponent(out Image parentImg) &&
                    transform.parent.GetComponent<Selectable>() == null)
                {
                    parentImg.raycastTarget = false;
                }
            }
            else if (scaleTarget == null)
            {
                scaleTarget = transform;
            }

            if (hitGraphic == null)
            {
                hitGraphic = GetComponent<Graphic>();
                if (hitGraphic == null) hitGraphic = targetGraphic;
            }

            if (!hasCapturedPadding && hitGraphic != null)
            {
                baseRaycastPadding = hitGraphic.raycastPadding;
                hasCapturedPadding = true;
            }
        }

        private void UpdateHitboxPadding(bool expand)
        {
            if (hitGraphic == null || !hasCapturedPadding) return;

            if (expand)
            {
                hitGraphic.raycastPadding = baseRaycastPadding + new Vector4(-8f, -8f, -8f, -8f);
                hasExpandedHitbox = true;
            }
            else if (hasExpandedHitbox)
            {
                hitGraphic.raycastPadding = baseRaycastPadding;
                hasExpandedHitbox = false;
            }
        }

        private bool IsInteractable()
        {
            return selectable == null || selectable.IsInteractable();
        }

        private static bool Approximately(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.001f &&
                   Mathf.Abs(a.y - b.y) < 0.001f &&
                   Mathf.Abs(a.z - b.z) < 0.001f;
        }

        private static bool ColorsApproximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.005f &&
                   Mathf.Abs(a.g - b.g) < 0.005f &&
                   Mathf.Abs(a.b - b.b) < 0.005f &&
                   Mathf.Abs(a.a - b.a) < 0.005f;
        }
    }

    /// <summary>
    /// Persistent runtime scanner attaching UIButtonJuice to existing and dynamically spawned Buttons.
    /// </summary>
    internal sealed class UIButtonJuiceBootstrapper : MonoBehaviour
    {
        private const float ScanIntervalSeconds = 0.65f;
        private float nextScanUnscaledTime;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            nextScanUnscaledTime = Time.unscaledTime + ScanIntervalSeconds;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnApplicationQuit()
        {
            UIButtonJuice.MarkQuitting();
        }

        private void OnDestroy()
        {
            UIButtonJuice.ClearBootstrapperIfSame(this);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AttachToSceneButtons();
            nextScanUnscaledTime = Time.unscaledTime + ScanIntervalSeconds;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScanUnscaledTime) return;
            nextScanUnscaledTime = Time.unscaledTime + ScanIntervalSeconds;
            AttachToSceneButtons();
        }

        public void AttachToSceneButtons()
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button btn = buttons[i];
                if (btn == null) continue;
                if (btn.GetComponent<ModeCardHoverEffect>() != null) continue;
                if (btn.GetComponent<UIButtonJuice>() != null) continue;
                btn.gameObject.AddComponent<UIButtonJuice>();
            }
        }
    }
}
