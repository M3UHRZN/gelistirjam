using Game.Gameplay.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Draws and fades PlayerInteraction.CurrentPrompt.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionPromptWidget : MonoBehaviour
    {
        [SerializeField] private PlayerInteraction interaction;
        [SerializeField] private Text label;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float fadeSeconds = 0.12f;

        public void Bind(PlayerInteraction playerInteraction) => interaction = playerInteraction;

        private void Awake()
        {
            if (interaction == null || label == null || group == null)
            {
                Debug.LogWarning(
                    $"[HUD] {name}: interaction/label/group not fully assigned — interaction prompt disabled.",
                    this);
                enabled = false;
                return;
            }

            group.alpha = 0f;
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            string prompt = interaction.CurrentPrompt;
            bool show = !string.IsNullOrEmpty(prompt);

            if (show && label.text != prompt)
            {
                label.text = prompt;
            }

            float target = show ? 1f : 0f;
            group.alpha = fadeSeconds <= 0f
                ? target
                : Mathf.MoveTowards(group.alpha, target, Time.deltaTime / fadeSeconds);
        }
    }
}
