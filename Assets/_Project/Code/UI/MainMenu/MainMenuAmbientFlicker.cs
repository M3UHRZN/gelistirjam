using System.Collections;
using UnityEngine;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// Independent ambient-light flicker for the main menu scene, in the same visual language as
    /// Game.Gameplay.World.LightZone's flicker (TGD section 31-32) but a separate component — the
    /// menu scene has no LightZone-relevant gameplay state (fuses, shutdown timers) to gate a
    /// flicker on, so this one is a standalone, always-on ambience loop instead of state-driven.
    /// </summary>
    public class MainMenuAmbientFlicker : MonoBehaviour
    {
        [SerializeField] private Light[] lights;
        [SerializeField] private float minIntervalSeconds = 2f;
        [SerializeField] private float maxIntervalSeconds = 6f;
        [SerializeField, Range(0f, 1f)] private float dimAmount = 0.35f;
        [SerializeField] private float flickerDurationSeconds = 0.08f;

        private float[] baseIntensities;

        private void OnEnable()
        {
            if (lights == null || lights.Length == 0) return;

            baseIntensities = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null) baseIntensities[i] = lights[i].intensity;
            }

            StartCoroutine(FlickerLoop());
        }

        private IEnumerator FlickerLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(minIntervalSeconds, maxIntervalSeconds));

                SetIntensityScale(1f - dimAmount);
                yield return new WaitForSeconds(flickerDurationSeconds);
                SetIntensityScale(1f);
            }
        }

        private void SetIntensityScale(float scale)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null) lights[i].intensity = baseIntensities[i] * scale;
            }
        }
    }
}
