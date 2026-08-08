using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Adds bounded spring-damped parallax on top of a widget's authored position.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class HudSway : MonoBehaviour
    {
        [SerializeField] private HudCameraMotion motion;

        [SerializeField, Tooltip("Signed pixels of lag per degree/second of camera rotation.")]
        private Vector2 pixelsPerDegreePerSecond = new Vector2(-0.06f, 0.04f);

        [SerializeField] private float maxOffsetPixels = 26f;
        [SerializeField] private float stiffness = 90f;
        [SerializeField] private float damping = 14f;

        private RectTransform rect;
        private Vector2 restPosition;
        private Vector2 offset;
        private Vector2 velocity;

        public void Bind(HudCameraMotion cameraMotion) => motion = cameraMotion;

        private void Awake()
        {
            rect = (RectTransform)transform;
            restPosition = rect.anchoredPosition;
        }

        private void LateUpdate()
        {
            if (motion == null) return;

            Vector2 target = Vector2.ClampMagnitude(
                Vector2.Scale(motion.AngularVelocity, pixelsPerDegreePerSecond), maxOffsetPixels);

            HudSpring.Step(ref offset, ref velocity, target, stiffness, damping, Time.deltaTime);
            rect.anchoredPosition = restPosition + offset;
        }
    }
}
