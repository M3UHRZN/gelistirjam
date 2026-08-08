using Game.Gameplay.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Displays flashlight on/off state. No battery mechanic is implied.
    /// </summary>
    [DisallowMultipleComponent]
    public class FlashlightWidget : MonoBehaviour
    {
        [SerializeField] private FlashlightController flashlight;
        [SerializeField] private Graphic icon;

        [SerializeField] private Color onColor = new Color(1f, 0.95f, 0.75f, 0.95f);
        [SerializeField] private Color offColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);

        private int lastState = -1;

        public void Bind(FlashlightController controller) => flashlight = controller;

        private void Awake()
        {
            if (flashlight == null || icon == null)
            {
                Debug.LogWarning(
                    $"[HUD] {name}: flashlight/icon not assigned — flashlight indicator disabled.",
                    this);
                enabled = false;
            }
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            int state = flashlight.IsOn ? 1 : 0;
            if (state == lastState) return;

            lastState = state;
            icon.color = state == 1 ? onColor : offColor;
        }
    }
}
