using UnityEngine;
using UnityEngine.UI;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Điều khiển hình nền Main Menu: hỗ trợ kéo thả Sprite Thumbnail tùy chỉnh qua Inspector
    /// kèm lớp phủ Dark Overlay Alpha = 0.6f để làm nổi bật nút bấm và chữ trắng.
    /// </summary>
    [ExecuteAlways]
    public class MenuBackgroundLoader : MonoBehaviour
    {
        [Header("Custom Thumbnail (Drag & Drop in Inspector)")]
        [SerializeField] private Sprite customThumbnail;
        [SerializeField] [Range(0f, 1f)] private float darkOverlayAlpha = 0.6f;

        [Header("UI Targets")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image darkOverlayImage;

        private void Awake()
        {
            ApplyVisuals();
        }

        private void OnValidate()
        {
            ApplyVisuals();
        }

        public void ConfigureTargets(Image bgImg, Image overlayImg, Sprite defaultThumb)
        {
            backgroundImage = bgImg;
            darkOverlayImage = overlayImg;
            if (customThumbnail == null && defaultThumb != null)
            {
                customThumbnail = defaultThumb;
            }
            ApplyVisuals();
        }

        public void ApplyVisuals()
        {
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            if (backgroundImage != null)
            {
                if (customThumbnail != null)
                {
                    backgroundImage.sprite = customThumbnail;
                }
                backgroundImage.color = Color.white;
                backgroundImage.preserveAspect = false;
                backgroundImage.raycastTarget = false;
            }

            if (darkOverlayImage != null)
            {
                darkOverlayImage.color = new Color(0.02f, 0.03f, 0.07f, darkOverlayAlpha);
                darkOverlayImage.raycastTarget = false;
            }
        }
    }
}
