using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Moves and rolls the shared HUD plate so opposite corners fan out naturally.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class HudRootSway : MonoBehaviour
    {
        [SerializeField] private HudCameraMotion motion;

        [SerializeField, Tooltip("Signed pixels of lag per degree/second of camera rotation.")]
        private Vector2 pixelsPerDegreePerSecond = new Vector2(-0.05f, 0.035f);

        [SerializeField] private float maxOffsetPixels = 18f;

        [SerializeField, Tooltip("Signed degrees of Z roll per degree/second of yaw.")]
        private float degreesPerDegreePerSecond = -0.012f;

        [SerializeField] private float maxRotationDegrees = 2.5f;
        [SerializeField] private float stiffness = 70f;
        [SerializeField] private float damping = 12f;

        private RectTransform rect;
        private Vector2 restPosition;
        private float restRotation;
        private Vector2 offset;
        private Vector2 velocity;
        private float rotationOffset;
        private float rotationVelocity;

        public void Bind(HudCameraMotion cameraMotion) => motion = cameraMotion;

        private void Awake()
        {
            rect = (RectTransform)transform;
            restPosition = rect.anchoredPosition;
            restRotation = rect.localEulerAngles.z;
        }

        private void LateUpdate()
        {
            if (motion == null) return;

            float dt = Time.deltaTime;
            Vector2 positionTarget = Vector2.ClampMagnitude(
                Vector2.Scale(motion.AngularVelocity, pixelsPerDegreePerSecond), maxOffsetPixels);

            HudSpring.Step(ref offset, ref velocity, positionTarget, stiffness, damping, dt);
            rect.anchoredPosition = restPosition + offset;

            float rotationTarget = Mathf.Clamp(
                motion.AngularVelocity.x * degreesPerDegreePerSecond,
                -maxRotationDegrees, maxRotationDegrees);

            HudSpring.Step(ref rotationOffset, ref rotationVelocity, rotationTarget,
                stiffness, damping, dt);
            rect.localRotation = Quaternion.Euler(0f, 0f, restRotation + rotationOffset);
        }
    }
}
