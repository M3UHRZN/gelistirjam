using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>Toggleable flashlight. Provides visibility only — no safety, no AI effect (TGD section 8).</summary>
    public class FlashlightController : MonoBehaviour
    {
        [SerializeField] private Light flashlight;
        [SerializeField, Tooltip("Optional. Played on every Toggle() if both this and toggleSound are set.")]
        private AudioSource audioSource;
        [SerializeField] private AudioClip toggleSound;

        private Light Flashlight => flashlight != null ? flashlight : (flashlight = GetComponentInChildren<Light>());

        public bool IsOn => Flashlight != null && Flashlight.enabled;

        public void Bind(Light light)
        {
            flashlight = light;
            if (flashlight != null) flashlight.enabled = false;
        }

        public void Toggle()
        {
            if (Flashlight != null) Flashlight.enabled = !Flashlight.enabled;
            if (audioSource != null && toggleSound != null) audioSource.PlayOneShot(toggleSound);
        }
    }
}
