using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StickmanBeatEmUp.Economy;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Hiển thị tổng số Xu hiện có ("{displayedCoins} Xu") kèm icon Icon_CoinXu ở góc trên bên phải
    /// trên cả MainMenu và Battle HUD. Hỗ trợ animation đếm số mượt và hiệu ứng đồng Xu bay từ vị trí quái chết.
    /// </summary>
    public class CoinCounterUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private Text legacyCoinLabel;
        [SerializeField] private Image coinIcon;
        [SerializeField] private RectTransform coinTargetRect;
        [SerializeField] private RectTransform flyOverlayLayer;
        [SerializeField] private Sprite coinOrbSprite;

        [Header("Animation Settings")]
        [SerializeField] private float countDuration = 0.45f;
        [SerializeField] private float flyDuration = 0.45f;
        [SerializeField] private float arcHeight = 90f;
        [SerializeField] private float iconPunchScale = 1.22f;
        [SerializeField] private float iconPunchDuration = 0.18f;

        private readonly List<GameObject> activeOrbs = new List<GameObject>(8);
        private int displayedCoins;
        private int targetCoins;
        private Coroutine countCoroutine;
        private Coroutine punchCoroutine;
        private Vector3 baseIconScale = Vector3.one;
        private bool hasCapturedBaseScale;
        private bool isSubscribed;

        private static Sprite fallbackCircleSprite;

        public void Configure(
            TMP_Text label,
            Image icon,
            RectTransform targetRect,
            RectTransform flyOverlayLayer,
            Sprite orbSprite)
        {
            coinLabel = label;
            coinIcon = icon;
            coinTargetRect = targetRect;
            this.flyOverlayLayer = flyOverlayLayer;
            coinOrbSprite = orbSprite;

            CaptureBaseIconScale();
            if (coinIcon != null && coinIcon.sprite == null && orbSprite != null)
            {
                coinIcon.sprite = orbSprite;
                coinIcon.preserveAspect = true;
            }

            SyncImmediate();
        }

        public void ConfigureLegacy(
            Text legacyLabel,
            Image icon,
            RectTransform targetRect,
            RectTransform flyOverlayLayer,
            Sprite orbSprite)
        {
            legacyCoinLabel = legacyLabel;
            coinIcon = icon;
            coinTargetRect = targetRect;
            this.flyOverlayLayer = flyOverlayLayer;
            coinOrbSprite = orbSprite;

            CaptureBaseIconScale();
            if (coinIcon != null && coinIcon.sprite == null && orbSprite != null)
            {
                coinIcon.sprite = orbSprite;
                coinIcon.preserveAspect = true;
            }

            SyncImmediate();
        }

        public void RefreshImmediate()
        {
            SyncImmediate();
        }

        private void Awake()
        {
            CaptureBaseIconScale();
        }

        private void OnEnable()
        {
            SubscribeToCurrency();
            SyncImmediate();
        }

        private void Start()
        {
            SubscribeToCurrency();
            SyncImmediate();
        }

        private void OnDisable()
        {
            UnsubscribeFromCurrency();
            StopAllAnimationsAndClearOrbs();
        }

        private void OnDestroy()
        {
            UnsubscribeFromCurrency();
            StopAllAnimationsAndClearOrbs();
        }

        private void CaptureBaseIconScale()
        {
            if (coinIcon != null && !hasCapturedBaseScale)
            {
                baseIconScale = coinIcon.transform.localScale;
                if (baseIconScale == Vector3.zero)
                {
                    baseIconScale = Vector3.one;
                }
                hasCapturedBaseScale = true;
            }
        }

        private void SubscribeToCurrency()
        {
            if (isSubscribed) return;

            CurrencyManager manager = CurrencyManager.Instance;
            if (manager == null) return;

            manager.OnCoinsChanged -= HandleCoinsChanged;
            manager.OnCoinsEarnedWorld -= HandleCoinsEarnedWorld;
            manager.OnCoinsChanged += HandleCoinsChanged;
            manager.OnCoinsEarnedWorld += HandleCoinsEarnedWorld;
            isSubscribed = true;
        }

        private void UnsubscribeFromCurrency()
        {
            if (!isSubscribed) return;

            if (CurrencyManager.HasInstance)
            {
                CurrencyManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
                CurrencyManager.Instance.OnCoinsEarnedWorld -= HandleCoinsEarnedWorld;
            }
            isSubscribed = false;
        }

        private void SyncImmediate()
        {
            if (countCoroutine != null)
            {
                StopCoroutine(countCoroutine);
                countCoroutine = null;
            }

            int current = 0;
            if (CurrencyManager.HasInstance)
            {
                current = CurrencyManager.Instance.CurrentCoins;
            }
            else if (Application.isPlaying && CurrencyManager.Instance != null)
            {
                current = CurrencyManager.Instance.CurrentCoins;
            }

            displayedCoins = Mathf.Max(0, current);
            targetCoins = displayedCoins;
            UpdateLabel(displayedCoins);
        }

        private void HandleCoinsChanged(int newTotal)
        {
            targetCoins = Mathf.Max(0, newTotal);

            if (!gameObject.activeInHierarchy)
            {
                displayedCoins = targetCoins;
                UpdateLabel(displayedCoins);
                return;
            }

            if (displayedCoins == targetCoins)
            {
                UpdateLabel(displayedCoins);
                return;
            }

            if (countCoroutine != null)
            {
                StopCoroutine(countCoroutine);
            }
            countCoroutine = StartCoroutine(CountCoinsRoutine(displayedCoins, targetCoins));
        }

        private void HandleCoinsEarnedWorld(int amount, Vector3 worldPos)
        {
            if (amount <= 0 || !gameObject.activeInHierarchy) return;
            StartCoroutine(AnimateFlyingCoinOrbRoutine(worldPos));
        }

        private IEnumerator CountCoinsRoutine(int fromValue, int toValue)
        {
            float duration = Mathf.Max(0.05f, countDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - ((1f - t) * (1f - t));
                displayedCoins = Mathf.RoundToInt(Mathf.Lerp(fromValue, toValue, eased));
                UpdateLabel(displayedCoins);
                yield return null;
            }

            displayedCoins = toValue;
            UpdateLabel(displayedCoins);
            countCoroutine = null;
        }

        private IEnumerator AnimateFlyingCoinOrbRoutine(Vector3 worldPos)
        {
            RectTransform layer = ResolveOverlayLayer();
            if (layer == null)
            {
                TriggerIconPunch();
                yield break;
            }

            Canvas canvas = layer.GetComponentInParent<Canvas>();
            Camera uiCamera = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera
                : null;

            Camera mainCam = Camera.main;
            Vector3 startScreenPos;
            if (mainCam != null)
            {
                startScreenPos = mainCam.WorldToScreenPoint(worldPos);
                if (startScreenPos.z < 0f)
                {
                    startScreenPos = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
                }
            }
            else
            {
                startScreenPos = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, startScreenPos, uiCamera, out Vector2 startLocal);

            RectTransform destRect = coinTargetRect != null
                ? coinTargetRect
                : (coinIcon != null ? coinIcon.rectTransform : transform as RectTransform);

            Vector2 endScreenPos = destRect != null
                ? RectTransformUtility.WorldToScreenPoint(uiCamera, destRect.position)
                : new Vector2(Screen.width * 0.9f, Screen.height * 0.9f);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, endScreenPos, uiCamera, out Vector2 endLocal);

            GameObject orbObj = new GameObject("FlyingCoinOrb", typeof(RectTransform), typeof(Image));
            orbObj.transform.SetParent(layer, false);
            orbObj.transform.SetAsLastSibling();
            activeOrbs.Add(orbObj);

            RectTransform orbRt = orbObj.GetComponent<RectTransform>();
            orbRt.sizeDelta = new Vector2(38f, 38f);
            orbRt.anchoredPosition = startLocal;

            Image orbImg = orbObj.GetComponent<Image>();
            orbImg.raycastTarget = false;
            orbImg.preserveAspect = true;

            Sprite chosenSprite = coinOrbSprite != null
                ? coinOrbSprite
                : (coinIcon != null && coinIcon.sprite != null ? coinIcon.sprite : GetFallbackCircleSprite());

            bool hasIconSprite = coinOrbSprite != null || (coinIcon != null && coinIcon.sprite != null);
            orbImg.sprite = chosenSprite;
            orbImg.color = hasIconSprite ? Color.white : new Color(1f, 0.84f, 0.16f, 1f);

            Vector2 controlPoint = ((startLocal + endLocal) * 0.5f) + (Vector2.up * arcHeight);
            float duration = Mathf.Max(0.1f, flyDuration);
            float elapsed = 0f;

            while (elapsed < duration && orbObj != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                float inv = 1f - eased;

                Vector2 bezierPos = (inv * inv * startLocal)
                    + (2f * inv * eased * controlPoint)
                    + (eased * eased * endLocal);

                orbRt.anchoredPosition = bezierPos;
                float scale = Mathf.Lerp(1.15f, 0.82f, eased);
                orbRt.localScale = new Vector3(scale, scale, 1f);

                yield return null;
            }

            activeOrbs.Remove(orbObj);
            if (orbObj != null)
            {
                Destroy(orbObj);
            }

            TriggerIconPunch();
        }

        private RectTransform ResolveOverlayLayer()
        {
            if (flyOverlayLayer != null) return flyOverlayLayer;

            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                return parentCanvas.transform as RectTransform;
            }

            return transform as RectTransform;
        }

        private void TriggerIconPunch()
        {
            if (coinIcon == null || !gameObject.activeInHierarchy) return;

            CaptureBaseIconScale();
            if (punchCoroutine != null)
            {
                StopCoroutine(punchCoroutine);
            }
            punchCoroutine = StartCoroutine(PunchIconRoutine());
        }

        private IEnumerator PunchIconRoutine()
        {
            Transform iconTr = coinIcon.transform;
            Vector3 peakScale = baseIconScale * Mathf.Max(1f, iconPunchScale);
            float halfDuration = Mathf.Max(0.03f, iconPunchDuration * 0.5f);

            float elapsed = 0f;
            while (elapsed < halfDuration && iconTr != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                iconTr.localScale = Vector3.Lerp(baseIconScale, peakScale, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration && iconTr != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                iconTr.localScale = Vector3.Lerp(peakScale, baseIconScale, t);
                yield return null;
            }

            if (iconTr != null)
            {
                iconTr.localScale = baseIconScale;
            }
            punchCoroutine = null;
        }

        private void StopAllAnimationsAndClearOrbs()
        {
            if (countCoroutine != null)
            {
                StopCoroutine(countCoroutine);
                countCoroutine = null;
            }

            if (punchCoroutine != null)
            {
                StopCoroutine(punchCoroutine);
                punchCoroutine = null;
            }

            if (coinIcon != null && hasCapturedBaseScale)
            {
                coinIcon.transform.localScale = baseIconScale;
            }

            for (int i = activeOrbs.Count - 1; i >= 0; i--)
            {
                if (activeOrbs[i] != null)
                {
                    Destroy(activeOrbs[i]);
                }
            }
            activeOrbs.Clear();
        }

        private void UpdateLabel(int value)
        {
            string formatted = $"{value} Xu";
            if (coinLabel != null)
            {
                coinLabel.text = formatted;
            }
            if (legacyCoinLabel != null)
            {
                legacyCoinLabel.text = formatted;
            }
        }

        private static Sprite GetFallbackCircleSprite()
        {
            if (fallbackCircleSprite != null) return fallbackCircleSprite;

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = dist <= radius ? 1f : Mathf.Clamp01(1f - (dist - radius) * 0.6f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            fallbackCircleSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 32f);
            return fallbackCircleSprite;
        }
    }
}
