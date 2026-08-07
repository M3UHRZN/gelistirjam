using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>Toggleable flashlight. Provides visibility only — no safety, no AI effect (TGD section 8).</summary>
    public class FlashlightController : MonoBehaviour
    {
        [SerializeField] private Light flashlight;

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
        }
    }
}
