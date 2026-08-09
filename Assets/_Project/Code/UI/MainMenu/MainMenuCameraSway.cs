using UnityEngine;

namespace Game.UI.MainMenu
{
    /// <summary>Very small idle sway so the main menu shot doesn't read as a frozen screenshot.
    /// Deliberately subtle (spec: "without being distracting") — do not raise the defaults without
    /// checking it in the Editor first.</summary>
    public class MainMenuCameraSway : MonoBehaviour
    {
        [SerializeField, Range(0f, 5f)] private float maxYawDegrees = 1.5f;
        [SerializeField, Range(0f, 5f)] private float maxPitchDegrees = 1f;
        [SerializeField] private float speed = 0.15f;

        private Quaternion baseRotation;
        private float seed;

        private void Awake()
        {
            baseRotation = transform.localRotation;
            seed = Random.Range(0f, 1000f);
        }

        private void Update()
        {
            float t = (Time.unscaledTime + seed) * speed;
            float yaw = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * maxYawDegrees;
            float pitch = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * maxPitchDegrees;
            transform.localRotation = baseRotation * Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
