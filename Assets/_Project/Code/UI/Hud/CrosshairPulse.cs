using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Expresses camera inertia through crosshair spread and opacity without moving its aim point.
    /// </summary>
    [DisallowMultipleComponent]
    public class CrosshairPulse : MonoBehaviour
    {
        [SerializeField] private HudCameraMotion motion;
        [SerializeField] private RectTransform[] ticks = new RectTransform[4];
        [SerializeField] private CanvasGroup group;

        [SerializeField] private float maxExtraSpread = 7f;
        [SerializeField] private float fullSpreadRate = 260f;
        [SerializeField, Range(0f, 1f)] private float restAlpha = 0.85f;
        [SerializeField, Range(0f, 1f)] private float movingAlpha = 0.35f;
        [SerializeField] private float stiffness = 120f;
        [SerializeField] private float damping = 18f;

        private Vector2[] directions;
        private float[] restDistances;
        private float spread;
        private float spreadVelocity;

        public void Bind(HudCameraMotion cameraMotion) => motion = cameraMotion;

        private void Awake()
        {
            directions = new Vector2[ticks.Length];
            restDistances = new float[ticks.Length];

            for (int i = 0; i < ticks.Length; i++)
            {
                if (ticks[i] == null) continue;

                Vector2 authored = ticks[i].anchoredPosition;
                restDistances[i] = authored.magnitude;
                directions[i] = authored.sqrMagnitude > 0.0001f
                    ? authored.normalized
                    : Vector2.up;
            }
        }

        private void LateUpdate()
        {
            if (motion == null) return;

            float rate = motion.AngularVelocity.magnitude;
            float normalized = fullSpreadRate > 0f
                ? Mathf.Clamp01(rate / fullSpreadRate)
                : 0f;

            HudSpring.Step(ref spread, ref spreadVelocity, normalized * maxExtraSpread,
                stiffness, damping, Time.deltaTime);

            for (int i = 0; i < ticks.Length; i++)
            {
                if (ticks[i] == null) continue;
                ticks[i].anchoredPosition = directions[i] * (restDistances[i] + spread);
            }

            if (group != null)
            {
                group.alpha = Mathf.Lerp(restAlpha, movingAlpha, normalized);
            }
        }
    }
}
