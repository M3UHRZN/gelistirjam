using Game.Gameplay.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Displays explicit flashlight state. No battery mechanic is implied.
    /// </summary>
    [DisallowMultipleComponent]
    public class FlashlightWidget : MonoBehaviour
    {
        [SerializeField] private FlashlightController flashlight;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private Graphic[] visualParts;
        [SerializeField] private CanvasGroup group;

        [SerializeField] private string onText = "AÇIK";
        [SerializeField] private string offText = "KAPALI";
        [SerializeField] private Color onColor = new Color32(121, 255, 155, 242);
        [SerializeField] private Color offColor = new Color32(121, 255, 155, 105);

        private int lastState = -1;

        public void Bind(FlashlightController controller) => flashlight = controller;

        private void Awake()
        {
            if (flashlight == null || stateLabel == null || group == null ||
                visualParts == null || visualParts.Length == 0)
            {
                Debug.LogWarning(
                    $"[HUD] {name}: flashlight terminal references incomplete — widget disabled.",
                    this);
                if (group != null) group.alpha = 0f;
                enabled = false;
                return;
            }

            group.alpha = 1f;
            Refresh();
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            int state = flashlight.IsOn ? 1 : 0;
            if (state == lastState) return;

            lastState = state;
            stateLabel.text = state == 1 ? onText : offText;
            Color color = state == 1 ? onColor : offColor;

            for (int i = 0; i < visualParts.Length; i++)
            {
                if (visualParts[i] != null) visualParts[i].color = color;
            }
        }
    }
}
