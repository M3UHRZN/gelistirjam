using Game.Gameplay.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Colors the crosshair when the centered interaction ray has a usable target.
    /// </summary>
    [DisallowMultipleComponent]
    public class CrosshairWidget : MonoBehaviour
    {
        [SerializeField] private PlayerInteraction interaction;
        [SerializeField] private Graphic[] parts;
        [SerializeField] private Color idleColor = new Color32(121, 255, 155, 150);
        [SerializeField] private Color targetedColor = new Color32(121, 255, 155, 255);

        private bool lastTargeted;

        public void Bind(PlayerInteraction playerInteraction) => interaction = playerInteraction;

        private void Awake()
        {
            if (interaction == null)
            {
                Debug.LogWarning(
                    $"[HUD] {name}: PlayerInteraction not assigned — crosshair target highlight disabled.",
                    this);
                enabled = false;
                return;
            }

            lastTargeted = false;
            Apply(false);
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            bool targeted = !string.IsNullOrEmpty(interaction.CurrentPrompt);
            if (targeted == lastTargeted) return;

            lastTargeted = targeted;
            Apply(targeted);
        }

        private void Apply(bool targeted)
        {
            Color color = targeted ? targetedColor : idleColor;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null) parts[i].color = color;
            }
        }
    }
}
