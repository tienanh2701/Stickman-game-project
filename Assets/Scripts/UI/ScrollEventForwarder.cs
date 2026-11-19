using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Forwards drag and mouse-wheel scroll events from child UI buttons to a parent ScrollRect
    /// while suppressing accidental button clicks during a horizontal drag.
    /// </summary>
    public class ScrollEventForwarder : MonoBehaviour,
        IInitializePotentialDragHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IScrollHandler
    {
        private const float DragSuppressThreshold = 4f;

        [SerializeField] private ScrollRect targetScrollRect;
        [SerializeField] private Button childButton;

        private bool isDragging;
        private bool suppressedButtonForDrag;
        private Vector2 pointerDownPos;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            isDragging = false;
            RestoreChildButton();
        }

        public void Initialize(ScrollRect scrollRect)
        {
            targetScrollRect = scrollRect;
            if (childButton == null)
            {
                childButton = GetComponent<Button>();
            }
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData == null) return;

            pointerDownPos = eventData.position;
            ScrollRect sr = ResolveScrollRect();
            if (sr != null)
            {
                sr.OnInitializePotentialDrag(eventData);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null) return;

            isDragging = true;
            SuppressChildButton(eventData);

            ScrollRect sr = ResolveScrollRect();
            if (sr != null)
            {
                sr.OnBeginDrag(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null) return;

            if (!isDragging)
            {
                isDragging = true;
            }

            if (!suppressedButtonForDrag && Vector2.Distance(eventData.position, pointerDownPos) > DragSuppressThreshold)
            {
                SuppressChildButton(eventData);
            }
            else if (suppressedButtonForDrag)
            {
                eventData.eligibleForClick = false;
            }

            ScrollRect sr = ResolveScrollRect();
            if (sr != null)
            {
                sr.OnDrag(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData == null) return;

            if (suppressedButtonForDrag)
            {
                eventData.eligibleForClick = false;
            }

            ScrollRect sr = ResolveScrollRect();
            if (sr != null && isDragging)
            {
                sr.OnEndDrag(eventData);
            }

            isDragging = false;
            RestoreChildButton();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (eventData == null) return;

            ScrollRect sr = ResolveScrollRect();
            if (sr != null)
            {
                sr.OnScroll(eventData);
            }
        }

        private void ResolveReferences()
        {
            if (childButton == null)
            {
                childButton = GetComponent<Button>();
            }
            if (targetScrollRect == null)
            {
                targetScrollRect = GetComponentInParent<ScrollRect>();
            }
        }

        private ScrollRect ResolveScrollRect()
        {
            if (targetScrollRect == null)
            {
                targetScrollRect = GetComponentInParent<ScrollRect>();
            }
            return targetScrollRect;
        }

        private void SuppressChildButton(PointerEventData eventData)
        {
            eventData.eligibleForClick = false;
            if (childButton == null)
            {
                childButton = GetComponent<Button>();
            }
            if (childButton != null && childButton.enabled)
            {
                childButton.enabled = false;
                suppressedButtonForDrag = true;
            }
        }

        private void RestoreChildButton()
        {
            if (suppressedButtonForDrag && childButton != null)
            {
                childButton.enabled = true;
            }
            suppressedButtonForDrag = false;
        }
    }
}
