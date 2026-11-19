using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Hiệu ứng nổi nhẹ (+10px trục Y) và phóng to (1.05x) mượt mà theo Time.unscaledDeltaTime
    /// cho các thẻ chế độ chơi 3D trên màn hình Mode Select và nút điều hướng.
    /// Tự động mở rộng raycastPadding cạnh dưới/bên trong lúc nổi hoặc nhấn để tránh rớt hover/click.
    /// </summary>
    public class ModeCardHoverEffect : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] private float hoverLiftY = 10f;
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float pressScale = 0.97f;
        [SerializeField] private float transitionSpeed = 14f;

        private RectTransform rectTransform;
        private Selectable selectable;
        private Graphic hitGraphic;
        private Vector2 baseAnchoredPosition;
        private Vector3 baseScale = Vector3.one;
        private Vector4 baseRaycastPadding;
        private bool hasCapturedBase;
        private bool hasCapturedPadding;
        private bool hasExpandedHitbox;
        private bool isHovered;
        private bool isPressed;
        private bool isAnimating;
        private float currentLiftY;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            selectable = GetComponent<Selectable>();
            CaptureBaseTransform();
        }

        private void OnEnable()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }
            if (selectable == null)
            {
                selectable = GetComponent<Selectable>();
            }
            isHovered = false;
            isPressed = false;
            isAnimating = false;
            currentLiftY = 0f;
            RefreshBaseFromCurrent();
            RestoreHitboxPadding();
        }

        private void OnDisable()
        {
            isHovered = false;
            isPressed = false;
            isAnimating = false;
            currentLiftY = 0f;
            SnapToBase();
            RestoreHitboxPadding();
        }

        public void Configure(float liftY, float targetHoverScale, float speed = 14f)
        {
            hoverLiftY = liftY;
            hoverScale = targetHoverScale;
            transitionSpeed = Mathf.Max(1f, speed);
        }

        public void SetBaseAnchoredPosition(Vector2 anchoredPos)
        {
            baseAnchoredPosition = anchoredPos;
            hasCapturedBase = true;
            if (!isHovered && !isPressed && !isAnimating && rectTransform != null)
            {
                rectTransform.anchoredPosition = anchoredPos;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable()) return;
            CaptureHitboxIfNeeded();
            isHovered = true;
            isAnimating = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable()) return;
            CaptureHitboxIfNeeded();
            isPressed = true;
            isAnimating = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }

        private void Update()
        {
            if (rectTransform == null) return;
            if (!hasCapturedBase)
            {
                CaptureBaseTransform();
            }

            bool active = IsInteractable();
            if (!active)
            {
                isHovered = false;
                isPressed = false;
            }

            bool shouldAnimate = active && (isHovered || isPressed);
            if (!isAnimating && !shouldAnimate)
            {
                baseAnchoredPosition = rectTransform.anchoredPosition;
                if (rectTransform.localScale.sqrMagnitude > 0.0001f)
                {
                    baseScale = rectTransform.localScale;
                }
                currentLiftY = 0f;
                RestoreHitboxPadding();
                return;
            }

            isAnimating = true;
            float targetOffsetY = (active && isHovered)
                ? (isPressed ? hoverLiftY * 0.4f : hoverLiftY)
                : 0f;
            float scaleFactor = active
                ? (isPressed ? pressScale : (isHovered ? hoverScale : 1f))
                : 1f;

            Vector2 targetPos = baseAnchoredPosition + new Vector2(0f, targetOffsetY);
            Vector3 targetScale = baseScale * scaleFactor;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = Mathf.Clamp01(dt * transitionSpeed);

            rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, targetPos, t);
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, t);
            currentLiftY = rectTransform.anchoredPosition.y - baseAnchoredPosition.y;

            if (!shouldAnimate &&
                Vector2.Distance(rectTransform.anchoredPosition, baseAnchoredPosition) < 0.05f &&
                Vector3.Distance(rectTransform.localScale, baseScale) < 0.001f)
            {
                rectTransform.anchoredPosition = baseAnchoredPosition;
                rectTransform.localScale = baseScale;
                currentLiftY = 0f;
                isAnimating = false;
                RestoreHitboxPadding();
                return;
            }

            if (active && (isHovered || isPressed || currentLiftY > 0.05f))
            {
                ApplyExpandedHitboxPadding(currentLiftY);
            }
            else
            {
                RestoreHitboxPadding();
            }
        }

        private bool IsInteractable()
        {
            return selectable == null || selectable.IsInteractable();
        }

        private void CaptureBaseTransform()
        {
            if (hasCapturedBase || rectTransform == null) return;
            baseAnchoredPosition = rectTransform.anchoredPosition;
            baseScale = rectTransform.localScale == Vector3.zero ? Vector3.one : rectTransform.localScale;
            hasCapturedBase = true;
            CaptureHitboxIfNeeded();
        }

        private void RefreshBaseFromCurrent()
        {
            if (rectTransform == null) return;
            if (!hasCapturedBase)
            {
                CaptureBaseTransform();
                return;
            }
            baseAnchoredPosition = rectTransform.anchoredPosition;
            if (rectTransform.localScale.sqrMagnitude > 0.0001f)
            {
                baseScale = rectTransform.localScale;
            }
            CaptureHitboxIfNeeded();
        }

        private void CaptureHitboxIfNeeded()
        {
            if (hitGraphic == null)
            {
                hitGraphic = GetComponent<Graphic>();
                if (hitGraphic == null && selectable != null)
                {
                    hitGraphic = selectable.targetGraphic;
                }
            }
            if (!hasCapturedPadding && hitGraphic != null)
            {
                baseRaycastPadding = hitGraphic.raycastPadding;
                hasCapturedPadding = true;
            }
        }

        private void ApplyExpandedHitboxPadding(float liftY)
        {
            CaptureHitboxIfNeeded();
            if (hitGraphic == null || !hasCapturedPadding) return;
            hitGraphic.raycastPadding = baseRaycastPadding + new Vector4(-8f, -(8f + Mathf.Max(0f, liftY)), -8f, -8f);
            hasExpandedHitbox = true;
        }

        private void RestoreHitboxPadding()
        {
            if (hasExpandedHitbox && hitGraphic != null && hasCapturedPadding)
            {
                hitGraphic.raycastPadding = baseRaycastPadding;
            }
            hasExpandedHitbox = false;
        }

        private void SnapToBase()
        {
            if (!hasCapturedBase || rectTransform == null) return;
            rectTransform.anchoredPosition = baseAnchoredPosition;
            rectTransform.localScale = baseScale;
        }
    }
}
