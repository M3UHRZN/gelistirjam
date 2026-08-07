using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>Group of lights toggled together as the game shuts areas down over time (TGD section 31-32).</summary>
    public class LightZone : MonoBehaviour
    {
        [SerializeField] private Light[] lights;
        [SerializeField] private bool startActive = true;

        public bool IsActive { get; private set; }

        public void Bind(Light[] zoneLights) => lights = zoneLights;

        private void Awake() => SetActive(startActive);

        public void SetActive(bool active)
        {
            IsActive = active;
            if (lights == null) return;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null) lights[i].enabled = active;
            }
        }

        public void Toggle() => SetActive(!IsActive);
    }
}
