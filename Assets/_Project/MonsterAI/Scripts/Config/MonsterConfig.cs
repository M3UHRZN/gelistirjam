using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>
    /// Tunable values for the monster AI. Read-only at runtime — never mutate a config asset in a build.
    /// Values sourced from TECHNICAL GAME DESIGN DOCUMENT.md sections 6-25 and 52.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterConfig", menuName = "Gloomveil/Monster Config")]
    public class MonsterConfig : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField] private float baseSpeed = 2.2f;
        [SerializeField, Tooltip("Speed cap as a multiple of BaseSpeed (TGD: 1.7-1.8x).")]
        private float maxSpeedMultiplier = 1.75f;
        [SerializeField, Tooltip("Speed multiplier gained per Anger point (TGD: ~5% per successful gaze).")]
        private float speedPerAngerPoint = 0.05f;
        [SerializeField] private float commitSpeedBonus = 1.15f;

        [Header("Gaze / Critical Distance")]
        [SerializeField, Tooltip("Below this distance, gaze can no longer stop the monster (TGD: 2-3m).")]
        private float criticalDistance = 2.5f;
        [SerializeField, Tooltip("Continuous seconds of gaze required to freeze it (Bracken-inspired: 0.5s).")]
        private float gazeConfirmDuration = 0.5f;
        [SerializeField, Tooltip("Continuous seconds of staring past the freeze before it gets an extra Anger point and bolts (Bracken-inspired: 4s).")]
        private float gazeStareEscalationDuration = 4f;
        [SerializeField, Range(3, 9)] private int gazeRayCount = 5;
        [SerializeField, Tooltip("Of the rays cast, how many must hit the eyes for gaze to count.")]
        private int gazeRequiredHits = 3;
        [SerializeField, Tooltip("Half-angle in degrees of the ray cluster spread around screen center.")]
        private float gazeConeHalfAngleDegrees = 3f;
        [SerializeField] private float gazeMaxDistance = 25f;
        [SerializeField] private float attackRange = 1.3f;

        [Header("Anger")]
        [SerializeField] private int maxAnger = 12;

        [Header("Stalking / Positioning")]
        [SerializeField, Tooltip("How often candidate stalk points are re-evaluated (TGD: 0.4-0.8s).")]
        private float candidateReevaluateInterval = 0.6f;
        [SerializeField, Tooltip("Distance at which Retreat considers itself complete.")]
        private float retreatDistance = 6f;
        [SerializeField] private float retreatSpeedMultiplier = 1.3f;

        public float BaseSpeed => baseSpeed;
        public float MaxSpeedMultiplier => maxSpeedMultiplier;
        public float CriticalDistance => criticalDistance;
        public float GazeConfirmDuration => gazeConfirmDuration;
        public float GazeStareEscalationDuration => gazeStareEscalationDuration;
        public int GazeRayCount => gazeRayCount;
        public int GazeRequiredHits => gazeRequiredHits;
        public float GazeConeHalfAngleDegrees => gazeConeHalfAngleDegrees;
        public float GazeMaxDistance => gazeMaxDistance;
        public float AttackRange => attackRange;
        public int MaxAnger => maxAnger;
        public float CandidateReevaluateInterval => candidateReevaluateInterval;
        public float RetreatDistance => retreatDistance;
        public float RetreatSpeedMultiplier => retreatSpeedMultiplier;

        /// <summary>CurrentSpeed = BaseSpeed x AngerMultiplier, capped at BaseSpeed x MaxSpeedMultiplier (TGD section 21).</summary>
        public float GetSpeedForAnger(int anger)
        {
            float multiplier = Mathf.Min(1f + speedPerAngerPoint * anger, maxSpeedMultiplier);
            return baseSpeed * multiplier;
        }

        public float GetCommitSpeed(int anger) => GetSpeedForAnger(anger) * commitSpeedBonus;

        private void OnValidate()
        {
            baseSpeed = Mathf.Max(0.1f, baseSpeed);
            maxSpeedMultiplier = Mathf.Max(1f, maxSpeedMultiplier);
            criticalDistance = Mathf.Max(0.1f, criticalDistance);
            gazeConfirmDuration = Mathf.Max(0.01f, gazeConfirmDuration);
            gazeStareEscalationDuration = Mathf.Max(gazeConfirmDuration, gazeStareEscalationDuration);
            gazeRequiredHits = Mathf.Clamp(gazeRequiredHits, 1, gazeRayCount);
            maxAnger = Mathf.Max(1, maxAnger);
            candidateReevaluateInterval = Mathf.Max(0.05f, candidateReevaluateInterval);
        }
    }
}
