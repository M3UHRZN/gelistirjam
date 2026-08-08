using Game.Gameplay.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Displays the player's stamina and makes the exhausted lockout visible.
    /// </summary>
    [DisallowMultipleComponent]
    public class StaminaWidget : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Image fill;
        [SerializeField] private CanvasGroup group;

        [SerializeField] private Color normalColor = new Color32(121, 255, 155, 230);
        [SerializeField] private Color exhaustedColor = new Color32(255, 90, 54, 242);
        [SerializeField] private float exhaustedPulseHz = 1f;
        [SerializeField, Range(0f, 1f)] private float exhaustedPulseDepth = 0.45f;
        [SerializeField, Range(0f, 1f)] private float idleAlpha = 1f;

        public void Bind(PlayerController playerController) => player = playerController;

        private void Awake()
        {
            if (player == null || fill == null || group == null)
            {
                Debug.LogWarning(
                    $"[HUD] {name}: player/fill/group not assigned — stamina bar disabled.", this);
                if (group != null) group.alpha = 0f;
                enabled = false;
                return;
            }

            group.alpha = 1f;
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            float max = player.MaxStamina;
            fill.fillAmount = max > 0f
                ? Mathf.Clamp01(player.CurrentStamina / max)
                : 0f;

            if (player.IsExhausted)
            {
                float pulse = 0.5f +
                    0.5f * Mathf.Sin(Time.time * exhaustedPulseHz * Mathf.PI * 2f);
                Color color = exhaustedColor;
                color.a *= Mathf.Lerp(1f - exhaustedPulseDepth, 1f, pulse);
                fill.color = color;
            }
            else
            {
                fill.color = normalColor;
            }

            if (group == null) return;

            bool idle = !player.IsSprinting && !player.IsExhausted &&
                player.CurrentStamina >= max - 0.001f;
            group.alpha = idle ? idleAlpha : 1f;
        }
    }
}
