using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace StickmanBeatEmUp.Core
{
    /// <summary>
    /// Xử lý Input tập trung cho Player:
    /// - A/D: Di chuyển trái/phải
    /// - Space: Nhảy
    /// - Double-tap A / D: Lướt (Dash)
    /// - J: Đánh thường (Normal Attack)
    /// - U: Kỹ năng 1 (Skill 1)
    /// - I: Kỹ năng 2 (Skill 2)
    /// - O: Phím Biến Hình (Transform)
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        [Header("Double Tap Settings (Dash)")]
        [Tooltip("Khoảng thời gian tối đa giữa 2 lần nhấn để tính là Double-tap (giây)")]
        [SerializeField] private float doubleTapTimeThreshold = 0.25f;

        [Header("Key Bindings Config")]
        [SerializeField] private KeyCode leftKey = KeyCode.A;
        [SerializeField] private KeyCode rightKey = KeyCode.D;
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode altJumpKey = KeyCode.W;
        [SerializeField] private KeyCode attackKey = KeyCode.J;
        [SerializeField] private KeyCode skill1Key = KeyCode.U;
        [SerializeField] private KeyCode skill2Key = KeyCode.I;
        [SerializeField] private KeyCode transformKey = KeyCode.O;
        [SerializeField] private KeyCode blockKey = KeyCode.S;
        [SerializeField] private KeyCode itemQKey = KeyCode.Q;
        [SerializeField] private KeyCode itemEKey = KeyCode.E;
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

        // Bộ đếm thời gian Double-tap
        private float lastLeftTapTime = -10f;
        private float lastRightTapTime = -10f;

        // Các Action Event thông báo ra bên ngoài (Observer Pattern)
        public event Action<float> OnMoveEvent;         // horizontal (-1..1)
        public event Action OnJumpEvent;
        public event Action<float> OnDashEvent;         // direction (-1 hoặc 1)
        public event Action OnAttackEvent;             // Phím J
        public event Action OnSkill1Event;             // Phím U
        public event Action OnSkill2Event;             // Phím I
        public event Action OnTransformEvent;          // Phím O/T
        public event Action OnBlockEvent;              // Phím S (Đỡ đòn 1.0s)
        public event Action OnItemQEvent;              // Phím Q (Vật phẩm ô 1)
        public event Action OnItemEEvent;              // Phím E (Vật phẩm ô 2)
        public event Action OnPauseEvent;              // Phím Escape

        public float HorizontalInput { get; private set; }
        public bool IsInputBlocked { get; set; } = false;

        private void Update()
        {
            if (WasKeyPressedThisFrame(pauseKey))
            {
                OnPauseEvent?.Invoke();
            }

            if (Time.timeScale == 0f || IsInputBlocked)
            {
                HorizontalInput = 0f;
                return;
            }

            HandleMovementAndDashInput();
            HandleJumpInput();
            HandleCombatInput();
        }

        private void HandleMovementAndDashInput()
        {
            float horizontal = 0f;

            if (IsKeyPressed(rightKey)) horizontal += 1f;
            if (IsKeyPressed(leftKey)) horizontal -= 1f;

            HorizontalInput = horizontal;
            OnMoveEvent?.Invoke(horizontal);

            // Bắt Double-tap phím D (Lướt sang phải)
            if (WasKeyPressedThisFrame(rightKey))
            {
                if (Time.time - lastRightTapTime <= doubleTapTimeThreshold)
                {
                    OnDashEvent?.Invoke(1f);
                    lastRightTapTime = -10f; // Reset để tránh tap lần 3 bị tính liên tiếp
                }
                else
                {
                    lastRightTapTime = Time.time;
                }
            }

            // Bắt Double-tap phím A (Lướt sang trái)
            if (WasKeyPressedThisFrame(leftKey))
            {
                if (Time.time - lastLeftTapTime <= doubleTapTimeThreshold)
                {
                    OnDashEvent?.Invoke(-1f);
                    lastLeftTapTime = -10f;
                }
                else
                {
                    lastLeftTapTime = Time.time;
                }
            }
        }

        private void HandleJumpInput()
        {
            if (WasKeyPressedThisFrame(jumpKey) || WasKeyPressedThisFrame(altJumpKey))
            {
                OnJumpEvent?.Invoke();
            }
        }

        private void HandleCombatInput()
        {
            if (WasKeyPressedThisFrame(attackKey))
            {
                OnAttackEvent?.Invoke();
            }

            if (WasKeyPressedThisFrame(skill1Key))
            {
                OnSkill1Event?.Invoke();
            }

            if (WasKeyPressedThisFrame(skill2Key))
            {
                OnSkill2Event?.Invoke();
            }

            if (WasKeyPressedThisFrame(transformKey))
            {
                OnTransformEvent?.Invoke();
            }

            if (WasKeyPressedThisFrame(blockKey))
            {
                OnBlockEvent?.Invoke();
            }

            if (WasKeyPressedThisFrame(itemQKey))
            {
                OnItemQEvent?.Invoke();
            }

            if (WasKeyPressedThisFrame(itemEKey))
            {
                OnItemEEvent?.Invoke();
            }
        }

        private bool IsKeyPressed(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            KeyControl control = GetKeyControl(keyCode);
            return control != null && control.isPressed;
#else
            return Input.GetKey(keyCode);
#endif
        }

        private bool WasKeyPressedThisFrame(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            KeyControl control = GetKeyControl(keyCode);
            return control != null && control.wasPressedThisFrame;
#else
            return Input.GetKeyDown(keyCode);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static KeyControl GetKeyControl(KeyCode keyCode)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return null;

            return keyCode switch
            {
                KeyCode.A => keyboard.aKey,
                KeyCode.B => keyboard.bKey,
                KeyCode.C => keyboard.cKey,
                KeyCode.D => keyboard.dKey,
                KeyCode.E => keyboard.eKey,
                KeyCode.F => keyboard.fKey,
                KeyCode.G => keyboard.gKey,
                KeyCode.H => keyboard.hKey,
                KeyCode.I => keyboard.iKey,
                KeyCode.J => keyboard.jKey,
                KeyCode.K => keyboard.kKey,
                KeyCode.L => keyboard.lKey,
                KeyCode.M => keyboard.mKey,
                KeyCode.N => keyboard.nKey,
                KeyCode.O => keyboard.oKey,
                KeyCode.P => keyboard.pKey,
                KeyCode.Q => keyboard.qKey,
                KeyCode.R => keyboard.rKey,
                KeyCode.S => keyboard.sKey,
                KeyCode.T => keyboard.tKey,
                KeyCode.U => keyboard.uKey,
                KeyCode.V => keyboard.vKey,
                KeyCode.W => keyboard.wKey,
                KeyCode.X => keyboard.xKey,
                KeyCode.Y => keyboard.yKey,
                KeyCode.Z => keyboard.zKey,
                KeyCode.Space => keyboard.spaceKey,
                KeyCode.LeftArrow => keyboard.leftArrowKey,
                KeyCode.RightArrow => keyboard.rightArrowKey,
                KeyCode.UpArrow => keyboard.upArrowKey,
                KeyCode.DownArrow => keyboard.downArrowKey,
                KeyCode.LeftShift => keyboard.leftShiftKey,
                KeyCode.RightShift => keyboard.rightShiftKey,
                KeyCode.Return => keyboard.enterKey,
                KeyCode.Escape => keyboard.escapeKey,
                _ => null
            };
        }
#endif
    }
}
