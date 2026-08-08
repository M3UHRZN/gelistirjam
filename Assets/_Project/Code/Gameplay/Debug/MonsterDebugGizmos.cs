using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

using Game.Gameplay.Monster;
namespace Game.Gameplay.DebugTools
{
    /// <summary>
    /// Scene-view visualization for AI tuning (MASTER_PROMPT.md section 32, TGD section 53 — "AI debug
    /// olmadan behavior tuning ciddi zaman kaybettirebilir"). Editor-only visuals; every draw is
    /// guarded so a partially-wired monster never throws from OnDrawGizmos.
    /// </summary>
    public class MonsterDebugGizmos : MonoBehaviour
    {
        [SerializeField] private MonsterBrain brain;

        [Header("Toggles")]
        [SerializeField] private bool drawRanges = true;
        [SerializeField] private bool drawTargetPoint = true;
        [SerializeField] private bool drawPlayerFov = true;
        [SerializeField] private bool drawStalkPoints = true;
        [SerializeField] private bool drawStalkPointScores = true;
        [SerializeField] private bool drawPrediction = true;

        public void Bind(MonsterBrain monsterBrain) => brain = monsterBrain;

        private void OnDrawGizmos()
        {
            if (brain == null || brain.Config == null) return;

            if (drawRanges) DrawRanges();
            if (drawTargetPoint) DrawTargetPoint();
            if (drawPrediction) DrawPrediction();
            if (drawPlayerFov) DrawPlayerFov();
            if (drawStalkPoints) DrawStalkPoints();

            DrawStatusLabel();
        }

        private void DrawRanges()
        {
            Vector3 position = brain.transform.position;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(position, brain.Config.ContactKillRange);

            // TGD section 53 "Critical distance" — the radius inside which gaze stops working at all.
            Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.7f);
            Gizmos.DrawWireSphere(position, brain.Config.CriticalDistance);

            Gizmos.color = new Color(1f, 0.6f, 0f, 0.4f);
            Gizmos.DrawWireSphere(brain.PatrolAnchor, brain.Config.PatrolRadius);

            Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
            Gizmos.DrawWireSphere(position, brain.Config.DetectionRadius);
        }

        private void DrawTargetPoint()
        {
            if (brain.Navigator == null) return;

            Vector3 target = brain.Navigator.CurrentDestination;
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(brain.transform.position, target);
            Gizmos.DrawSphere(target, 0.35f);
        }

        private void DrawPrediction()
        {
            if (brain.Perception == null || brain.Player == null) return;

            Vector3 predicted = brain.Perception.PredictedPlayerPosition(brain.Config.PredictionSeconds);

            // Green when prediction is live, grey when it degenerates to the player's current position
            // because no velocity source is wired — that distinction was previously invisible and hid a
            // real bug for a long time (see chat).
            bool live = brain.Perception.HasVelocitySource;
            Gizmos.color = live ? new Color(0.3f, 1f, 0.3f) : new Color(0.5f, 0.5f, 0.5f, 0.6f);
            Gizmos.DrawLine(brain.Player.position, predicted);
            Gizmos.DrawWireSphere(predicted, 0.4f);
        }

        private void DrawPlayerFov()
        {
#if UNITY_EDITOR
            if (brain.VisionModel == null || brain.Player == null) return;

            // Sampled rather than read from the model's private fields: draws the actual boundary the
            // scorer sees, whatever the FOV/range values happen to be.
            const int rayCount = 24;
            const float probeDistance = 20f;
            Vector3 origin = brain.Player.position;

            for (int i = 0; i < rayCount; i++)
            {
                float angle = i * (360f / rayCount);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 point = origin + direction * probeDistance;

                bool inCone = brain.VisionModel.IsPotentiallyVisible(point);
                if (!inCone) continue;

                Handles.color = new Color(1f, 1f, 0.2f, 0.25f);
                Handles.DrawLine(origin, point);
            }
#endif
        }

        private void DrawStalkPoints()
        {
            if (brain.StalkPoints == null || brain.Player == null || brain.Perception == null) return;

            Vector3 predicted = brain.Perception.PredictedPlayerPosition(brain.Config.PredictionSeconds);
            Vector3 facing = brain.Perception.PlayerFacingDirection;
            Vector3 monsterPosition = brain.transform.position;
            float riskTolerance = brain.Anger != null ? brain.Config.TuningFor(brain.Anger.Stage).VisibilityRiskTolerance : 0f;

            var items = brain.StalkPoints.Items;
            for (int i = 0; i < items.Count; i++)
            {
                StalkPoint point = items[i];
                if (point == null) continue;

                float score = StalkPointScorer.DebugScore(point, brain.Player.position, predicted,
                    facing, monsterPosition, brain.VisionModel, riskTolerance);

                bool visible = brain.VisionModel != null && brain.VisionModel.IsActuallyVisible(point.Position);
                Gizmos.color = visible ? new Color(1f, 0.3f, 0.3f, 0.9f) : new Color(0.4f, 0.8f, 1f, 0.9f);
                Gizmos.DrawWireSphere(point.Position, 0.4f);

#if UNITY_EDITOR
                if (drawStalkPointScores)
                {
                    Handles.color = Color.white;
                    Handles.Label(point.Position + Vector3.up * 0.8f, score.ToString("0.0"));
                }
#endif
            }
        }

        private void DrawStatusLabel()
        {
#if UNITY_EDITOR
            int angerValue = brain.Anger != null ? brain.Anger.Anger : 0;
            string stage = brain.Anger != null ? brain.Anger.Stage.ToString() : "-";
            string prediction = brain.Perception != null && !brain.Perception.HasVelocitySource ? "\n<no velocity source>" : string.Empty;

            Handles.Label(brain.transform.position + Vector3.up * 2.2f,
                $"State: {brain.CurrentStateName}\nAnger: {angerValue}/{brain.Config.MaxAnger} ({stage}){prediction}");
#endif
        }
    }
}
