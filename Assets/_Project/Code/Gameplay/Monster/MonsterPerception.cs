using Game.Gameplay.Player;
using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Distance-only player awareness — Bracken is explicitly omniscient about player positions
    /// ("fully aware of the locations of every single employee"), no FOV/line-of-sight gating here.
    /// Gaze detection (whether the *player* is looking back at the monster) is a separate concern
    /// handled by PlayerGazeDetector/MonsterGazeReceiver.
    /// </summary>
    public class MonsterPerception : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField, Tooltip("Colliders that block line-of-sight checks (walls etc.) — used only by IsPlayerVisibleFrom, not by DistanceToPlayer/detection which stay omniscient by design.")]
        private LayerMask occluderMask = ~0;
        [SerializeField] private float eyeHeight = 1.6f;

        // Serialized as MonoBehaviour, not as IPlayerVelocitySource: Unity's serializer ignores
        // interface-typed fields entirely, so the previous `private IPlayerVelocitySource` field was
        // silently null on every scene reload even though the composition root had assigned it. That
        // made PredictedPlayerPosition() fall back to zero velocity — i.e. "predicted" position was
        // always the player's *current* position, which quietly reduced CutOff to a plain chase and
        // neutered the scorer's predicted-route term (see chat; same failure class as SceneWiring).
        [SerializeField, Tooltip("Component implementing IPlayerVelocitySource (the player rig).")]
        private MonoBehaviour velocitySourceBehaviour;

        [Header("Prediction Confidence")]
        [SerializeField, Tooltip("How quickly PredictionConfidence reacts to a direction change (higher = faster). Prompt section 18: 'stronger prediction when movement is stable, weaker when the player stops/turns/near a junction'.")]
        private float directionStabilitySmoothing = 6f;
        [SerializeField, Range(0f, 1f), Tooltip("PredictedPlayerPosition's time horizon is scaled by Lerp(this, 1, confidence) — never drops to a flat 'predict nothing' even right after a sharp turn, just leans less far ahead.")]
        private float minPredictionHorizonMultiplier = 0.25f;

        private Vector3 lastVelocityDirection;
        private float directionStability = 1f;

        private IPlayerVelocitySource cachedVelocitySource;
        private IPlayerVelocitySource VelocitySource =>
            cachedVelocitySource ??= velocitySourceBehaviour as IPlayerVelocitySource;

        /// <summary>
        /// 0..1, 1 = the player has been moving in a consistent direction, 0 = they just changed
        /// direction sharply. Tracked here (not read on-demand) because it needs a running history of
        /// the velocity vector across frames, not a snapshot. Feeds PredictedPlayerPosition's horizon
        /// scaling; also useful for debug display.
        /// </summary>
        public float PredictionConfidence => directionStability;

        private void Update()
        {
            IPlayerVelocitySource source = VelocitySource;
            if (source == null) return;

            Vector3 velocity = source.Velocity;
            velocity.y = 0f;

            // Below walking-noise speed the direction is meaningless (a nearly-stopped player's
            // heading jitters) — hold the last known stability instead of feeding noise into it.
            if (velocity.sqrMagnitude < 0.04f) return;

            Vector3 direction = velocity.normalized;
            if (lastVelocityDirection.sqrMagnitude > 0.0001f)
            {
                // 0 (no change) .. 1 (90 degrees or more — a real direction change, not drift).
                float instability = Mathf.Clamp01(Vector3.Angle(lastVelocityDirection, direction) / 90f);
                float target = 1f - instability;
                directionStability = Mathf.Lerp(directionStability, target,
                    1f - Mathf.Exp(-directionStabilitySmoothing * Time.deltaTime));
            }

            lastVelocityDirection = direction;
        }

        // The player CharacterController's pivot is at chest height while the NavMeshAgent pivot
        // is at floor height. AI range and contact decisions are horizontal gameplay distances.
        public float DistanceToPlayer
        {
            get
            {
                if (player == null) return Mathf.Infinity;

                Vector3 delta = player.position - transform.position;
                delta.y = 0f;
                return delta.magnitude;
            }
        }
        public bool IsPlayerWithinRadius(float radius) => DistanceToPlayer <= radius;

        /// <summary>
        /// Direction the player is currently facing, flattened to the horizontal plane (TGD section 68
        /// — "the AI reacts to which way the player is looking", needed by the vision-model rework).
        /// Distinct from gaze-at-monster detection, which is about the player's screen-center ray, not
        /// general facing.
        /// </summary>
        public Vector3 PlayerFacingDirection
        {
            get
            {
                if (player == null) return Vector3.forward;
                Vector3 forward = player.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            }
        }

        /// <summary>
        /// True if nothing on occluderMask blocks a straight line from a candidate monster position to
        /// the player's eye height (TGD section 13 — "Potentially Visible" vs "Actually Visible": this
        /// answers the latter; StalkPoint scoring answers the former using static room metadata).
        /// </summary>
        public bool IsPlayerVisibleFrom(Vector3 fromPosition)
        {
            if (player == null) return false;

            Vector3 origin = fromPosition + Vector3.up * eyeHeight;
            Vector3 target = player.position + Vector3.up * eyeHeight;
            return !Physics.Linecast(origin, target, occluderMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Linear extrapolation of where the player will be in `seconds` at their current velocity
        /// (TGD section 18). Still deliberately naive — no curve-fitting or path prediction, matching
        /// the TGD's own scope ("simple linear prediction is acceptable") — but the time horizon itself
        /// is scaled by PredictionConfidence: a player who just changed direction gets a shorter
        /// look-ahead so a straight-line guess doesn't overshoot into a wall they're about to turn away
        /// from (see chat — this is the "stronger prediction when stable, weaker when the player turns"
        /// behavior called for on top of the TGD's plain formula, without adding actual turn modeling).
        /// A stopped player already predicts to ~their current position via the velocity term alone, so
        /// no separate handling is needed for that case.
        /// </summary>
        public Vector3 PredictedPlayerPosition(float seconds)
        {
            if (player == null) return Vector3.zero;

            IPlayerVelocitySource source = VelocitySource;
            Vector3 velocity = source != null ? source.Velocity : Vector3.zero;
            velocity.y = 0f; // prediction is a floor-plane concern; falling shouldn't skew the target

            float horizonMultiplier = Mathf.Lerp(minPredictionHorizonMultiplier, 1f, directionStability);
            return player.position + velocity * (seconds * horizonMultiplier);
        }

        /// <summary>True when a velocity source is actually wired — prediction silently degrades to "current position" otherwise.</summary>
        public bool HasVelocitySource => VelocitySource != null;

        public void Bind(Transform playerTransform) => player = playerTransform;
        public void Bind(Transform playerTransform, IPlayerVelocitySource playerVelocitySource)
        {
            player = playerTransform;
            cachedVelocitySource = playerVelocitySource;
            velocitySourceBehaviour = playerVelocitySource as MonoBehaviour;
        }
    }
}
