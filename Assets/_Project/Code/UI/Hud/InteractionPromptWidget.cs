using Game.Gameplay.Interaction;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Draws and fades PlayerInteraction.CurrentPrompt as a terminal command.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionPromptWidget : MonoBehaviour
    {
        [SerializeField] private PlayerInteraction interaction;
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private string promptFormat = "> {0}";
        [SerializeField] private float fadeSeconds = 0.12f;

        private string lastPrompt;

        public void Bind(PlayerInteraction playerInteraction) => interaction = playerInteraction;

        private void Awake()
        {
            if (interaction == null || label == null || group == null)
            {
                Debug.LogWarning(
                    $"[HUD] {name}: interaction/label/group not fully assigned — interaction prompt disabled.",
                    this);
                if (group != null) group.alpha = 0f;
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

            if (show && prompt != lastPrompt)
            {
                lastPrompt = prompt;
                label.text = string.Format(promptFormat, prompt);
            }
            else if (!show)
            {
                lastPrompt = null;
            }

            float target = show ? 1f : 0f;
            group.alpha = fadeSeconds <= 0f
                ? target
                : Mathf.MoveTowards(group.alpha, target, Time.deltaTime / fadeSeconds);
        }
    }
}
