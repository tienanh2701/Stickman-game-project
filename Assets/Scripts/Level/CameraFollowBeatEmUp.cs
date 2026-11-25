using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Level
{
    /// <summary>
    /// Camera bám theo Player chuẩn Beat 'em up:
    /// - Khi đi bình thường: Bám theo mượt mà
    /// - Khi bước vào khu vực giao chiến (Arena Lock): Khóa cứng camera trong khoảng [minX, maxX]
    ///   đồng thời dựng 2 bức tường vô hình ngăn Player lùi hoặc chạy trốn
    /// </summary>
    public class CameraFollowBeatEmUp : MonoBehaviour
    {
        public static CameraFollowBeatEmUp Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [Header("Follow Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private float smoothSpeed = 0.125f;
        [SerializeField] private Vector3 offset = new Vector3(0, 1.5f, -10f);

        [Header("Arena Camera Bounds Lock")]
        [SerializeField] private bool isLocked = false;
        [SerializeField] private float minX = -100f;
        [SerializeField] private float maxX = 100f;

        [Header("Invisible Boundary Walls (Beat 'em up standard)")]
        [SerializeField] private BoxCollider2D leftWallCollider;
        [SerializeField] private BoxCollider2D rightWallCollider;

        private Vector3 velocity = Vector3.zero;
        private float shakeTimer;
        private float shakeMagnitude;
        private bool useMapBoundsClamp = false;
        private float mapBoundsMinX = -22f;
        private float mapBoundsMaxX = 42f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Shake(float duration, float magnitude)
        {
            if (GameSettingsManager.HasInstance)
            {
                float scale = GameSettingsManager.Instance.EffectiveScreenShakeScale;
                if (scale <= 0.001f) return;
                magnitude *= scale;
            }

            shakeTimer = Mathf.Max(shakeTimer, duration);
            shakeMagnitude = Mathf.Max(shakeMagnitude, magnitude);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void LockCamera(float lockedMinX, float lockedMaxX)
        {
            isLocked = true;
            useMapBoundsClamp = false;
            minX = lockedMinX;
            maxX = lockedMaxX;
            UpdateWallPositions();
        }

        public void UnlockCamera()
        {
            isLocked = false;
            useMapBoundsClamp = false;
            DisableWalls();
        }

        public void FollowWithinMapBounds(float mapLeftX, float mapRightX)
        {
            isLocked = false;
            useMapBoundsClamp = true;
            mapBoundsMinX = mapLeftX;
            mapBoundsMaxX = mapRightX;
            DisableWalls();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.position + offset;

            if (isLocked)
            {
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
                UpdateWallPositions();
            }
            else if (useMapBoundsClamp)
            {
                Camera cam = Camera.main != null ? Camera.main : GetComponent<Camera>();
                if (cam != null)
                {
                    float camHalfWidth = cam.orthographicSize * cam.aspect;
                    if (mapBoundsMaxX - mapBoundsMinX > camHalfWidth * 2f)
                    {
                        desiredPosition.x = Mathf.Clamp(desiredPosition.x, mapBoundsMinX + camHalfWidth, mapBoundsMaxX - camHalfWidth);
                    }
                    else
                    {
                        desiredPosition.x = (mapBoundsMinX + mapBoundsMaxX) * 0.5f;
                    }
                }
            }

            Vector3 smoothedPosition = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothSpeed);
            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.deltaTime;
                Vector2 rand = Random.insideUnitCircle * shakeMagnitude;
                smoothedPosition += new Vector3(rand.x, rand.y, 0f);
                if (shakeTimer <= 0f) shakeMagnitude = 0f;
            }
            transform.position = smoothedPosition;
        }

        private void UpdateWallPositions()
        {
            Camera cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            float camHalfWidth = cam.orthographicSize * cam.aspect;

            if (leftWallCollider != null)
            {
                leftWallCollider.gameObject.SetActive(true);
                leftWallCollider.transform.position = new Vector3(minX - camHalfWidth - 0.5f, transform.position.y, 0);
            }

            if (rightWallCollider != null)
            {
                rightWallCollider.gameObject.SetActive(true);
                rightWallCollider.transform.position = new Vector3(maxX + camHalfWidth + 0.5f, transform.position.y, 0);
            }
        }

        private void DisableWalls()
        {
            if (leftWallCollider != null) leftWallCollider.gameObject.SetActive(false);
            if (rightWallCollider != null) rightWallCollider.gameObject.SetActive(false);
        }
    }
}
