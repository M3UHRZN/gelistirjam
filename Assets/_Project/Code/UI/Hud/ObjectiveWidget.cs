using Game.Gameplay.Objectives;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Displays fuse progress without owning any objective or exit gameplay logic.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjectiveWidget : MonoBehaviour
    {
        [SerializeField] private ObjectiveManager objectives;
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup group;

        [SerializeField, Tooltip("{0} = completed, {1} = required.")]
        private string format = "SİGORTA // {0:00}/{1:00}";

        [SerializeField] private string allCompleteText = "ÇIKIŞ // AÇIK";
        [SerializeField] private Color normalColor = new Color32(121, 255, 155, 235);
        [SerializeField] private Color flashColor = new Color32(121, 255, 155, 255);
        [SerializeField] private float flashSeconds = 0.6f;

        private bool allComplete;
        private float flashTimer;

        public void Bind(ObjectiveManager objectiveManager) => objectives = objectiveManager;

        private void Awake()
        {
            if (objectives == null || label == null || group == null)
            {
                Debug.LogWarning(
                    $"[HUD] {name}: ObjectiveManager/label/group not assigned — fuse counter disabled.",
                    this);
                if (group != null) group.alpha = 0f;
                if (label != null) label.enabled = false;
                enabled = false;
                return;
            }

            label.color = normalColor;
        }

        private void OnEnable()
        {
            if (objectives == null) return;

            objectives.OnAnyFuseCompleted += HandleFuseCompleted;
            objectives.OnAllComplete += HandleAllComplete;
            Refresh();
        }

        private void OnDisable()
        {
            if (objectives == null) return;

            objectives.OnAnyFuseCompleted -= HandleFuseCompleted;
            objectives.OnAllComplete -= HandleAllComplete;
        }

        private void HandleFuseCompleted() => flashTimer = flashSeconds;

        private void HandleAllComplete()
        {
            allComplete = true;
            Refresh();
        }

        private void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                float t = flashSeconds > 0f
                    ? Mathf.Clamp01(flashTimer / flashSeconds)
                    : 0f;
                label.color = Color.Lerp(normalColor, flashColor, t);
            }
            else if (label.color != normalColor)
            {
                label.color = normalColor;
            }

            Refresh();
        }

        private void Refresh()
        {
            int required = objectives.RequiredCount > 0
                ? objectives.RequiredCount
                : objectives.RegisteredCount;

            if (allComplete ||
                (required > 0 && objectives.CompletedCount >= required))
            {
                group.alpha = 1f;
                label.enabled = true;
                label.text = allCompleteText;
                return;
            }

            if (required <= 0)
            {
                group.alpha = 0f;
                label.enabled = false;
                return;
            }

            group.alpha = 1f;
            label.enabled = true;
            label.text = string.Format(format, objectives.CompletedCount, required);
        }
    }
}
