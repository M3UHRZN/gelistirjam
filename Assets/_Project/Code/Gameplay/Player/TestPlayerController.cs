using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// Throwaway WASD + mouse-look rig for testing the monster AI in isolation.
    /// Values from TECHNICAL GAME DESIGN DOCUMENT.md sections 6-7.
    /// Reads devices directly instead of an Input Actions asset — the real game should route through
    /// the project's .inputactions asset with rebinding support (MASTER_PROMPT.md section 27). Flagged
    /// here as an accepted shortcut because this controller only exists to drive the AI test scene.
    /// Implements IPlayerControlGate so Core's GameFlowController can freeze/unfreeze control without
    /// referencing this concrete type (MASTER_PROMPT.md section 9 — inward-only dependencies).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class TestPlayerController : MonoBehaviour, IPlayerControlGate, IPlayerVelocitySource
    {
        [Header("Movement (TGD section 6)")]
        [SerializeField] private float walkSpeed = 3.75f;
        [SerializeField] private float sprintSpeed = 6.5f;
        [SerializeField] private float gravity = -9.81f;

        [Header("Look")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField, Tooltip("Degrees rotated per pixel of raw mouse delta. Tune from the Inspector to taste.")]
        private float mouseSensitivity = 0.15f;
        [SerializeField] private float maxLookAngleDegrees = 85f;

        [Header("Stamina (TGD section 7)")]
        [SerializeField] private float maxStamina = 5.5f;
        [SerializeField] private float staminaDrainPerSecond = 1f;
        [SerializeField] private float staminaRegenDelaySeconds = 1f;
        [SerializeField] private float staminaRegenPerSecond = 0.7f;

        [SerializeField] private FlashlightController flashlight;

        private CharacterController controller;
        private float verticalVelocity;
        private float pitch;
        private float staminaRegenTimer;
        private bool isExhausted;

        public float CurrentStamina { get; private set; }
        public float MaxStamina => maxStamina;
        public float CurrentSpeed { get; private set; }
        public Vector3 Velocity => Controller.velocity;

        private CharacterController Controller => controller != null ? controller : (controller = GetComponent<CharacterController>());

        public void Bind(Transform lookPivot, FlashlightController flashlightController)
        {
            cameraPivot = lookPivot;
            flashlight = flashlightController;
        }

        /// <summary>IPlayerControlGate — disabling the component stops Update(), which is the only thing driving input here.</summary>
        public void SetControlEnabled(bool enabled) => this.enabled = enabled;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            CurrentStamina = maxStamina;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            HandleLook();
            HandleMovement();
            HandleFlashlight();
        }

        private void HandleLook()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // Mouse.delta is already "pixels moved this frame", not a per-second rate — multiplying
            // by Time.deltaTime here double-scaled it and made look rotation absurdly sluggish
            // (that's why a sensitivity of ~77 felt necessary before; it no longer applies — see chat).
            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
            transform.Rotate(Vector3.up, delta.x);
            pitch = Mathf.Clamp(pitch - delta.y, -maxLookAngleDegrees, maxLookAngleDegrees);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            Vector2 input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            bool wantsSprint = keyboard.leftShiftKey.isPressed && input.y > 0f && !isExhausted;
            bool isSprinting = wantsSprint && CurrentStamina > 0f;
            UpdateStamina(isSprinting);

            float speed = isSprinting ? sprintSpeed : walkSpeed;
            CurrentSpeed = input.sqrMagnitude > 0f ? speed : 0f;

            Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;
            move.y = verticalVelocity;

            controller.Move(move * Time.deltaTime);
        }

        private void UpdateStamina(bool isSprinting)
        {
            if (isSprinting)
            {
                CurrentStamina -= staminaDrainPerSecond * Time.deltaTime;
                staminaRegenTimer = staminaRegenDelaySeconds;
                if (CurrentStamina <= 0f)
                {
                    CurrentStamina = 0f;
                    isExhausted = true;
                }
                return;
            }

            if (staminaRegenTimer > 0f)
            {
                staminaRegenTimer -= Time.deltaTime;
            }
            else
            {
                CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + staminaRegenPerSecond * Time.deltaTime);
            }

            if (isExhausted && CurrentStamina >= maxStamina * 0.3f) isExhausted = false;
        }

        private void HandleFlashlight()
        {
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                flashlight?.Toggle();
            }
        }
    }
}
