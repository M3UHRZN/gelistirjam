using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// Real Rigidbody-based first-person controller (replaces TestPlayerController for actual
    /// gameplay; TestPlayerController stays untouched for the AI test scene). Movement speed is
    /// never smoothed except the walk&lt;-&gt;sprint ramp — releasing input stops the player dead on
    /// the next fixed step, and pressing a direction snaps straight to walk speed. Everything else
    /// about the feel (ground snapping, slope handling) exists purely to make that instant-response
    /// rule survive the map's stair geometry without sliding, bouncing, or launching.
    ///
    /// useGravity is off and gravity is integrated by hand so a grounded frame has exactly zero
    /// accumulated downward velocity — that single fact is what keeps the player static on a
    /// stationary 27 degree ramp with zero reliance on friction. The Rigidbody itself never
    /// rotates (rotation frozen for its lifetime); yaw/pitch live entirely on cameraPivot and are
    /// rebuilt into a yaw-only movement basis each FixedUpdate. Callers must use Yaw/Forward, not
    /// transform.forward, which is meaningless here.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class PlayerController : MonoBehaviour, IPlayerControlGate
    {
        [Header("Movement (TGD section 6)")]
        [SerializeField] private float walkSpeed = 3.75f;
        [SerializeField] private float sprintSpeed = 6.5f;
        [SerializeField, Tooltip("m/s^2-equivalent ramp rate from walk toward sprint. This is the ONLY smoothing in the controller.")]
        private float sprintRampUpPerSecond = 8f;
        [SerializeField, Tooltip("Ramp rate from sprint back toward walk.")]
        private float sprintRampDownPerSecond = 14f;

        [Header("Gravity / Air")]
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float maxFallSpeed = 20f;

        [Header("Ground Probe")]
        [SerializeField, Tooltip("Beyond this angle from vertical, a surface is not walkable. Stair ramp/landing SEAM edges present ~39deg normals even though the ramp itself is 27deg -- a tighter cutoff (e.g. 35deg) jams the player at every staircase. Keep at 50.")]
        private float maxGroundAngleDegrees = 50f;
        [SerializeField, Tooltip("How far below the capsule the probe still counts as grounded/snappable.")]
        private float groundSnapDistance = 0.35f;
        [SerializeField] private float maxSnapSpeed = 10f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Look")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField, Tooltip("Degrees rotated per pixel of raw pointer delta.")]
        private float mouseSensitivity = 0.15f;
        [SerializeField] private float maxLookAngleDegrees = 85f;

        [Header("Stamina (TGD section 7)")]
        [SerializeField] private float maxStamina = 5.5f;
        [SerializeField] private float staminaDrainPerSecond = 1f;
        [SerializeField] private float staminaRegenDelaySeconds = 1f;
        [SerializeField] private float staminaRegenPerSecond = 0.7f;

        [SerializeField] private FlashlightController flashlight;
        [SerializeField, Tooltip("Locks/hides the cursor while control is enabled. Disable if a pause menu should own cursor state instead.")]
        private bool manageCursor = true;

        private const int MaxGroundHits = 8;
        private readonly RaycastHit[] groundHits = new RaycastHit[MaxGroundHits];

        private GameInputActions actions;
        private Rigidbody rb;
        private CapsuleCollider capsule;

        private float yaw;
        private float pitch;
        private float currentSpeed;
        private float verticalVelocity;
        private float staminaRegenTimer;
        private bool isExhausted;
        private float minGroundDot;

        public float CurrentStamina { get; private set; }
        public float MaxStamina => maxStamina;
        public float CurrentSpeed { get; private set; }
        public Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsExhausted => isExhausted;
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public float GroundGap { get; private set; }
        public float Yaw => yaw;
        public Quaternion YawRotation => Quaternion.Euler(0f, yaw, 0f);
        public Vector3 Forward => YawRotation * Vector3.forward;

        public void Bind(Transform lookPivot, FlashlightController flashlightController)
        {
            cameraPivot = lookPivot;
            flashlight = flashlightController;
        }

        /// <summary>
        /// IPlayerControlGate. Disabling stops FixedUpdate/Update, but a Rigidbody keeps its last
        /// velocity when the driving component stops -- with useGravity off it would coast forever.
        /// OnDisable does the real teardown so this stays a one-liner.
        /// </summary>
        public void SetControlEnabled(bool enabled) => this.enabled = enabled;

        /// <summary>Places the player without going through transform -- writes rb.position directly
        /// since m_AutoSyncTransforms is off project-wide and a plain transform write would not
        /// reach PhysX until the next step.</summary>
        public void Teleport(Vector3 feetPosition, float yawDegrees)
        {
            HaltMotion();
            rb.position = feetPosition;
            rb.rotation = Quaternion.identity;
            yaw = yawDegrees;
            pitch = 0f;
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            ConfigureRigidbody();

            actions = new GameInputActions();
            CurrentStamina = maxStamina;
            currentSpeed = walkSpeed;
            minGroundDot = Mathf.Cos(maxGroundAngleDegrees * Mathf.Deg2Rad);
        }

        private void ConfigureRigidbody()
        {
            rb.useGravity = false;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.maxDepenetrationVelocity = 3f;
        }

        private void OnEnable()
        {
            actions.Player.Enable();
            actions.Player.Flashlight.performed += OnFlashlightPerformed;
            if (manageCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            actions.Player.Flashlight.performed -= OnFlashlightPerformed;
            actions.Player.Disable();
            HaltMotion();
            if (manageCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void OnDestroy() => actions?.Dispose();

        private void OnFlashlightPerformed(InputAction.CallbackContext context) => flashlight?.Toggle();

        private void Update() => HandleLook();

        private void FixedUpdate()
        {
            ProbeGround();
            ReadMoveAndApply();
        }

        private void HandleLook()
        {
            // Pointer/delta is already "pixels moved this frame", not a per-second rate -- do not
            // multiply by Time.deltaTime (see TestPlayerController's HandleLook for the history of
            // that specific bug). The gamepad stick binding was deliberately removed from the Look
            // action so this rule has no device-dependent exception.
            Vector2 delta = actions.Player.Look.ReadValue<Vector2>() * mouseSensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -maxLookAngleDegrees, maxLookAngleDegrees);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void ProbeGround()
        {
            float probeRadius = capsule.radius - 0.01f;
            float originLift = 0.05f;
            Vector3 origin = rb.position + Vector3.up * (capsule.radius + originLift);
            float maxDistance = originLift + groundSnapDistance;

            int count = Physics.SphereCastNonAlloc(origin, probeRadius, Vector3.down, groundHits,
                maxDistance, groundMask, QueryTriggerInteraction.Ignore);

            bool found = false;
            float bestDistance = float.MaxValue;
            RaycastHit best = default;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundHits[i];
                // No custom physics layers exist in this project, so groundMask is effectively
                // "everything", including the player's own capsule. A sweep (unlike a simple
                // raycast) reports the origin's own collider as a distance-0 hit -- filter it out,
                // or the player believes it's standing on itself with a garbage normal every frame.
                if (hit.collider == null) continue;
                if (hit.rigidbody == rb) continue;
                if (hit.distance <= 0f) continue;
                if (hit.distance >= bestDistance) continue;

                bestDistance = hit.distance;
                best = hit;
                found = true;
            }

            if (!found)
            {
                IsGrounded = false;
                GroundNormal = Vector3.up;
                GroundGap = float.PositiveInfinity;
                return;
            }

            float slopeDot = best.normal.y;
            bool walkable = slopeDot >= minGroundDot;
            float gap = Mathf.Max(0f, best.distance - originLift);

            IsGrounded = walkable && gap <= groundSnapDistance;
            GroundNormal = best.normal;
            GroundGap = gap;
        }

        private void ReadMoveAndApply()
        {
            Vector2 rawInput = actions.Player.Move.ReadValue<Vector2>();
            Vector2 input = Vector2.ClampMagnitude(rawInput, 1f);
            bool hasInput = input.sqrMagnitude > 0.0001f;

            bool wantsSprint = actions.Player.Sprint.IsPressed() && input.y > 0f && !isExhausted;
            bool isSprinting = wantsSprint && CurrentStamina > 0f;
            IsSprinting = isSprinting;
            UpdateStamina(isSprinting);

            // The only smoothing in this controller: the walk<->sprint ramp. Zero input hard-resets
            // currentSpeed to walkSpeed immediately, so pressing a key always yields exactly
            // walkSpeed on the very next fixed step -- never a stale mid-ramp value left over from a
            // sprint that ended moments ago.
            float target = isSprinting ? sprintSpeed : walkSpeed;
            if (!hasInput)
            {
                currentSpeed = walkSpeed;
            }
            else
            {
                float rate = target > currentSpeed ? sprintRampUpPerSecond : sprintRampDownPerSecond;
                currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * Time.fixedDeltaTime);
            }

            CurrentSpeed = hasInput ? currentSpeed : 0f;

            Vector3 wish = YawRotation * new Vector3(input.x, 0f, input.y);

            if (IsGrounded)
            {
                Vector3 dir = Vector3.ProjectOnPlane(wish, GroundNormal);
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.zero;

                Vector3 v = dir * CurrentSpeed;

                float snapSpeed = GroundGap > 0f ? Mathf.Min(GroundGap / Time.fixedDeltaTime, maxSnapSpeed) : 0f;
                v -= GroundNormal * snapSpeed;

                verticalVelocity = 0f;
                rb.linearVelocity = v;
            }
            else
            {
                verticalVelocity = Mathf.Max(verticalVelocity + gravity * Time.fixedDeltaTime, -maxFallSpeed);
                Vector3 h = wish * CurrentSpeed;
                rb.linearVelocity = new Vector3(h.x, verticalVelocity, h.z);
            }
        }

        private void UpdateStamina(bool isSprinting)
        {
            if (isSprinting)
            {
                CurrentStamina -= staminaDrainPerSecond * Time.fixedDeltaTime;
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
                staminaRegenTimer -= Time.fixedDeltaTime;
            }
            else
            {
                CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + staminaRegenPerSecond * Time.fixedDeltaTime);
            }

            if (isExhausted && CurrentStamina >= maxStamina * 0.3f) isExhausted = false;
        }

        private void HaltMotion()
        {
            currentSpeed = walkSpeed;
            verticalVelocity = 0f;
            if (rb == null) return;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
