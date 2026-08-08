using Game.Gameplay.Objectives;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Displays fuse progress without owning any objective or exit gameplay logic.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjectiveWidget : MonoBehaviour
    {
        [SerializeField] private ObjectiveManager objectives;
        [SerializeField] private Text label;

        [SerializeField, Tooltip("{0} = completed, {1} = required.")]
        private string format = "SİGORTA {0}/{1}";

        [SerializeField] private string allCompleteText = "ÇIKIŞ AÇILDI";
        [SerializeField] private Color normalColor = new Color(0.88f, 0.88f, 0.85f, 0.92f);
        [SerializeField] private Color flashColor = new Color(1f, 0.9f, 0.5f, 1f);
        [SerializeField] private float flashSeconds = 0.6f;

        private bool allComplete;
        private float flashTimer;

        public void Bind(ObjectiveManager objectiveManager) => objectives = objectiveManager;

        private void Awake()
        {
            if (objectives == null || label == null)
            {
                Debug.LogWarning(
                    $"[HUD] {name}: ObjectiveManager/label not assigned — fuse counter disabled.",
                    this);
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

        private void HandleAllComplete() => allComplete = true;

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
                label.enabled = true;
                label.text = allCompleteText;
                return;
            }

            if (required <= 0)
            {
                label.enabled = false;
                return;
            }

            label.enabled = true;
            label.text = string.Format(format, objectives.CompletedCount, required);
        }
    }
}
