using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Measures camera yaw/pitch angular velocity for all HUD inertia components.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class HudCameraMotion : MonoBehaviour
    {
        [SerializeField, Tooltip("Camera transform whose rotation drives HUD inertia.")]
        private Transform cameraTransform;

        [SerializeField, Tooltip("Per-second exponential smoothing rate.")]
        private float smoothing = 18f;

        [SerializeField, Tooltip("Degrees/second the measurement is clamped to.")]
        private float maxTrackedRate = 400f;

        [SerializeField, Tooltip("A frame faster than this is treated as a teleport or respawn.")]
        private float teleportThresholdDegrees = 1500f;

        /// <summary>x = yaw degrees/second, y = pitch degrees/second.</summary>
        public Vector2 AngularVelocity { get; private set; }

        private float previousYaw;
        private float previousPitch;
        private bool hasPrevious;

        public void Bind(Transform camera)
        {
            cameraTransform = camera;
            ResetMotion();
        }

        public void ResetMotion()
        {
            AngularVelocity = Vector2.zero;
            hasPrevious = false;
        }

        private void OnEnable() => ResetMotion();

        private void LateUpdate()
        {
            if (cameraTransform == null)
            {
                AngularVelocity = Vector2.zero;
                return;
            }

            Vector3 euler = cameraTransform.eulerAngles;
            float yaw = euler.y;
            float pitch = euler.x;

            float dt = Time.deltaTime;
            if (!hasPrevious || dt <= 0f)
            {
                previousYaw = yaw;
                previousPitch = pitch;
                hasPrevious = true;
                return;
            }

            float yawRate = Mathf.DeltaAngle(previousYaw, yaw) / dt;
            float pitchRate = Mathf.DeltaAngle(previousPitch, pitch) / dt;

            previousYaw = yaw;
            previousPitch = pitch;

            if (Mathf.Abs(yawRate) > teleportThresholdDegrees ||
                Mathf.Abs(pitchRate) > teleportThresholdDegrees)
            {
                ResetMotion();
                return;
            }

            Vector2 raw = new Vector2(
                Mathf.Clamp(yawRate, -maxTrackedRate, maxTrackedRate),
                Mathf.Clamp(pitchRate, -maxTrackedRate, maxTrackedRate));

            float t = 1f - Mathf.Exp(-smoothing * dt);
            AngularVelocity = Vector2.Lerp(AngularVelocity, raw, t);
        }
    }
}
