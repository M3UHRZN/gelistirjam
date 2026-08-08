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

        private IPlayerVelocitySource cachedVelocitySource;
        private IPlayerVelocitySource VelocitySource =>
            cachedVelocitySource ??= velocitySourceBehaviour as IPlayerVelocitySource;

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
        /// (TGD section 18). Deliberately naive — no pathing/turn prediction — matching the TGD's own
        /// scope ("simple linear prediction is acceptable").
        /// </summary>
        public Vector3 PredictedPlayerPosition(float seconds)
        {
            if (player == null) return Vector3.zero;

            IPlayerVelocitySource source = VelocitySource;
            Vector3 velocity = source != null ? source.Velocity : Vector3.zero;
            velocity.y = 0f; // prediction is a floor-plane concern; falling shouldn't skew the target
            return player.position + velocity * seconds;
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
