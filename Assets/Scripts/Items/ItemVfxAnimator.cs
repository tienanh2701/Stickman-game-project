using UnityEngine;

namespace StickmanBeatEmUp.Items
{
    /// <summary>
    /// Bộ phát animation frame 2D gọn nhẹ (zero-allocation Update) cho các hiệu ứng vật phẩm trên nhân vật.
    /// Hỗ trợ chạy 1 lần (oneshot) hoặc lặp theo thời lượng buff.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ItemVfxAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float fps = 8f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool deactivateOnComplete = true;

        private bool isPlaying;
        private int currentFrameIndex;
        private float frameTimer;
        private float remainingDuration;
        private bool hasTimedDuration;
        private Color baseTint = Color.white;

        public bool IsPlaying => isPlaying;
        public SpriteRenderer TargetRenderer => EnsureRenderer();

        private void Awake()
        {
            EnsureRenderer();
            if (!isPlaying && targetRenderer != null)
            {
                targetRenderer.enabled = false;
            }
        }

        private SpriteRenderer EnsureRenderer()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }
            return targetRenderer;
        }

        /// <summary>
        /// Cấu hình tập frame, tốc độ phát, sorting order tương đối so với nhân vật và màu sắc.
        /// </summary>
        public void Configure(
            Sprite[] newFrames,
            float newFps,
            int sortingOrderOffset = 0,
            SpriteRenderer referenceRenderer = null,
            Color? tintColor = null)
        {
            EnsureRenderer();

            if (newFrames != null && newFrames.Length > 0)
            {
                frames = newFrames;
            }
            if (newFps > 0f)
            {
                fps = newFps;
            }

            if (tintColor.HasValue)
            {
                baseTint = tintColor.Value;
            }

            if (targetRenderer != null)
            {
                targetRenderer.color = baseTint;
                SyncSortingOrder(referenceRenderer, sortingOrderOffset);
            }
        }

        /// <summary>
        /// Đồng bộ sorting layer và sorting order theo SpriteRenderer tham chiếu.
        /// </summary>
        public void SyncSortingOrder(SpriteRenderer referenceRenderer, int sortingOrderOffset)
        {
            EnsureRenderer();
            if (targetRenderer == null || referenceRenderer == null) return;

            targetRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
            targetRenderer.sortingOrder = referenceRenderer.sortingOrder + sortingOrderOffset;
        }

        /// <summary>
        /// Chạy hiệu ứng một lần (hoặc lặp theo thời lượng duration nếu duration > 0) rồi tự ẩn.
        /// </summary>
        public void PlayOneShot(float duration = -1f)
        {
            StartPlayback(duration > 0f, duration);
        }

        /// <summary>
        /// Nạp frame mới và chạy hiệu ứng oneshot.
        /// </summary>
        public void PlayOneShot(Sprite[] newFrames, float newFps, float duration = -1f, Color? tintColor = null)
        {
            Configure(newFrames, newFps, 0, null, tintColor);
            StartPlayback(duration > 0f, duration);
        }

        /// <summary>
        /// Chạy vòng lặp hiệu ứng trong thời gian duration (giây), hoặc vô hạn nếu duration <= 0.
        /// </summary>
        public void PlayLoop(float duration = -1f)
        {
            StartPlayback(true, duration);
        }

        /// <summary>
        /// Nạp frame mới và chạy vòng lặp hiệu ứng.
        /// </summary>
        public void PlayLoop(Sprite[] newFrames, float newFps, float duration = -1f, Color? tintColor = null)
        {
            Configure(newFrames, newFps, 0, null, tintColor);
            StartPlayback(true, duration);
        }

        /// <summary>
        /// Dừng hiệu ứng ngay lập tức và ẩn GameObject.
        /// </summary>
        public void StopAndHide()
        {
            isPlaying = false;
            hasTimedDuration = false;
            remainingDuration = 0f;

            if (targetRenderer != null)
            {
                targetRenderer.enabled = false;
            }

            if (deactivateOnComplete && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void StartPlayback(bool shouldLoop, float duration)
        {
            EnsureRenderer();
            loop = shouldLoop;
            hasTimedDuration = duration > 0f;
            remainingDuration = hasTimedDuration ? duration : 0f;
            currentFrameIndex = 0;
            frameTimer = 0f;
            isPlaying = true;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (targetRenderer != null)
            {
                targetRenderer.color = baseTint;
                targetRenderer.enabled = true;
            }

            ApplyCurrentFrame();
        }

        private void Update()
        {
            if (!isPlaying) return;

            float dt = Time.deltaTime;
            if (hasTimedDuration)
            {
                remainingDuration -= dt;
                if (remainingDuration <= 0f)
                {
                    StopAndHide();
                    return;
                }
            }

            if (frames == null || frames.Length == 0 || fps <= 0f)
            {
                if (!hasTimedDuration && !loop)
                {
                    StopAndHide();
                }
                return;
            }

            if (frames.Length == 1)
            {
                return;
            }

            float frameInterval = 1f / fps;
            frameTimer += dt;
            while (frameTimer >= frameInterval)
            {
                frameTimer -= frameInterval;
                currentFrameIndex++;

                if (currentFrameIndex >= frames.Length)
                {
                    if (loop || hasTimedDuration)
                    {
                        currentFrameIndex = 0;
                    }
                    else
                    {
                        StopAndHide();
                        return;
                    }
                }

                ApplyCurrentFrame();
            }
        }

        private void ApplyCurrentFrame()
        {
            if (targetRenderer == null || frames == null || frames.Length == 0) return;

            int idx = Mathf.Clamp(currentFrameIndex, 0, frames.Length - 1);
            if (frames[idx] != null)
            {
                targetRenderer.sprite = frames[idx];
            }
        }
    }
}
