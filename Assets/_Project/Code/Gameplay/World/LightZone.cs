using System.Collections;
using UnityEngine;

namespace Game.Gameplay.World
{
    public enum LightZoneState { Active, Flickering, Disabled }

    /// <summary>Group of lights toggled together as the game shuts areas down over time (TGD section 31-32).</summary>
    public class LightZone : MonoBehaviour
    {
        [SerializeField] private Light[] lights;
        [SerializeField] private bool startActive = true;
        [SerializeField, Tooltip("How long the zone flickers before finally going dark (TGD section 31 — a warning state, not an instant cut).")]
        private float flickerDurationSeconds = 2.5f;
        [SerializeField] private float flickerIntervalSeconds = 0.12f;

        public LightZoneState State { get; private set; }

        /// <summary>Back-compat: true only while fully lit. Callers that also care about "still lit at all" should check State != Disabled.</summary>
        public bool IsActive => State == LightZoneState.Active;

        private Coroutine flickerRoutine;

        public void Bind(Light[] zoneLights) => lights = zoneLights;

        private void Awake() => SetActive(startActive);

        public void SetActive(bool active)
        {
            StopFlicker();
            State = active ? LightZoneState.Active : LightZoneState.Disabled;
            ApplyLights(active);
        }

        public void Toggle() => SetActive(!IsActive);

        /// <summary>Warns before going dark instead of an instant cut (TGD section 31). Ends in Disabled.</summary>
        public void Flicker()
        {
            if (State == LightZoneState.Disabled) return;
            StopFlicker();
            flickerRoutine = StartCoroutine(FlickerRoutine());
        }

        private IEnumerator FlickerRoutine()
        {
            State = LightZoneState.Flickering;
            float elapsed = 0f;
            bool on = true;
            while (elapsed < flickerDurationSeconds)
            {
                on = !on;
                ApplyLights(on);
                yield return new WaitForSeconds(flickerIntervalSeconds);
                elapsed += flickerIntervalSeconds;
            }

            State = LightZoneState.Disabled;
            ApplyLights(false);
            flickerRoutine = null;
        }

        private void StopFlicker()
        {
            if (flickerRoutine == null) return;
            StopCoroutine(flickerRoutine);
            flickerRoutine = null;
        }

        private void ApplyLights(bool on)
        {
            if (lights == null) return;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null) lights[i].enabled = on;
            }
        }
    }
}
