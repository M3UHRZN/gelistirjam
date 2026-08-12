using UnityEngine;
using UnityEngine.AI;

using Game.Gameplay.World;
namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Thin wrapper around NavMeshAgent — the equivalent of the tutorial's BrackenPathController,
    /// translated from A* Pathfinding Project to Unity's built-in NavMesh. The agent and reusable
    /// path buffer are cached once in Awake so destination validation never allocates at runtime.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public class MonsterNavigator : MonoBehaviour
    {
        private static readonly float[] RearAngles = { 0f, -42f, 42f, -72f, 72f };

        [SerializeField] private SafeZoneRuntimeSet safeZones;

        private NavMeshAgent agent;
        private NavMeshPath path;
        private float baseSpeed = 1f;
        private float speedMultiplier = 1f;

        // Lazy accessors, not just Awake-assignment: this component is used from PatrolState.Enter(),
        // which MonsterBrain.OnEnable() can call before this object's own Awake has run when the
        // scene is built/reloaded via an Editor script instead of normal interactive Play Mode (same
        // ordering issue documented across StalkPoint/SafeZone/FuseBox — see chat). Awake() below still
        // runs and is harmless/idempotent; these just guarantee a non-null agent no matter which fires first.
        private NavMeshAgent Agent => agent != null ? agent : (agent = GetComponent<NavMeshAgent>());
        private NavMeshPath Path => path ??= new NavMeshPath();

        private void Awake()
        {
            agent = Agent;
            path = Path;
        }

        public bool HasArrived => Agent.isOnNavMesh && !Agent.pathPending
            && Agent.remainingDistance <= Agent.stoppingDistance;

        public void Bind(SafeZoneRuntimeSet zones) => safeZones = zones;

        /// <summary>Current NavMeshAgent destination, for debug visualization (TGD section 53).</summary>
        public Vector3 CurrentDestination => Agent.destination;

        /// <summary>Live agent capsule dimensions used to test a hypothetical Shy destination's body visibility.</summary>
        public float AgentHeight => Agent.height;
        public float AgentRadius => Agent.radius;

        /// <summary>True if worldPoint falls inside any registered SafeZone (TGD section 29).</summary>
        public bool IsPointInSafeZone(Vector3 worldPoint)
        {
            if (safeZones == null) return false;
            var zones = safeZones.Items;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].Contains(worldPoint)) return true;
            }
            return false;
        }

        /// <summary>
        /// Raw destination assignment with no validation. Only safe for points already known to be on
        /// the NavMesh (e.g. the player's own position). For any computed//sampled point use
        /// TrySetValidatedDestination instead — see its doc comment.
        /// </summary>
        public void SetDestination(Vector3 point)
        {
            if (Agent.isOnNavMesh) Agent.SetDestination(point);
        }

        /// <summary>
        /// Snaps `point` onto the NavMesh, rejects SafeZones and incomplete paths, then paths there.
        /// Returns false when no valid destination exists, which callers must treat as "this plan
        /// failed" rather than ignoring — an unvalidated SetDestination on an unreachable point makes
        /// the agent walk as far as it can and then grind against a wall, which is exactly the
        /// wall-hugging behaviour this project already fixed once for Patrol/Interested/Shy (see chat).
        /// </summary>
        public bool TrySetValidatedDestination(Vector3 point, float sampleRadius = 2f)
            => TrySetReachableDestination(point, sampleRadius);

        /// <summary>
        /// Reachability test without committing to the destination — used by StalkPointScorer to drop
        /// candidates outright (TGD section 16: "NavMesh path yok: -Invalid").
        /// </summary>
        public bool IsReachable(Vector3 point, float sampleRadius = 2f)
        {
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas)) return false;
            return !IsInsideAnySafeZone(hit.position) && IsPathReachable(hit.position);
        }

        /// <summary>
        /// Sets the state's *base* speed. The live anger multiplier is applied on top and refreshed
        /// every frame by MonsterBrain — previously each state baked `base * multiplier` once in
        /// Enter(), so a long chase kept whatever multiplier happened to apply at entry even as anger
        /// changed underneath it, contradicting TGD section 21's continuous CurrentSpeed formula.
        /// </summary>
        public void SetSpeed(float speed)
        {
            baseSpeed = speed;
            ApplySpeed();
        }

        /// <summary>Live anger multiplier (TGD section 21). Cheap enough to push every frame; only touches the agent when it actually changes.</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            if (Mathf.Approximately(speedMultiplier, multiplier)) return;
            speedMultiplier = multiplier;
            ApplySpeed();
        }

        private void ApplySpeed() => Agent.speed = baseSpeed * speedMultiplier;

        public void Stop()
        {
            if (Agent.isOnNavMesh) Agent.isStopped = true;
        }

        public void Resume()
        {
            if (Agent.isOnNavMesh) Agent.isStopped = false;
        }

        /// <summary>Disables the agent's automatic look-where-you're-walking rotation so code can drive facing manually (tutorial's "EnableRotation").</summary>
        public void SetAutoRotate(bool enabled) => Agent.updateRotation = enabled;

        /// <summary>Manually turns to face a target — used while auto-rotate is off and the monster is staring at the player.</summary>
        public void FaceTowards(Vector3 worldPoint, float degreesPerSecond, float deltaTime)
        {
            Vector3 direction = worldPoint - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion target = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, degreesPerSecond * deltaTime);
        }

        /// <summary>Picks a random reachable point within radius of the anchor and paths to it (tutorial's PickRandomPoint + SetRandomDestination). Rejects points inside a SafeZone.</summary>
        public bool SetRandomDestinationAround(Vector3 anchor, float radius)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 candidate = anchor + new Vector3(offset.x, 0f, offset.y);

                // Tolerance must scale with radius, not a small fixed value: in a narrow corridor
                // maze most of a wide random circle lands off the walkable strip entirely, so a
                // tight snap tolerance either fails outright or snaps onto a point hugging the wall
                // edge — which looked like the monster "bumping into walls" (see chat).
                if (TrySetReachableDestination(candidate, radius)) return true;
            }
            return false;
        }

        /// <summary>
        /// The Bracken knows both the player's position and facing direction.  Rather than walking
        /// at the player during its hunting phase, it asks for a reachable point behind them, then
        /// tries the two rear diagonals if that exact route is blocked.  Every accepted point is a
        /// complete NavMesh path, which prevents the wall-hugging/partial-path behaviour.
        /// </summary>
        public bool SetStalkDestinationBehind(Transform target, float behindDistance)
        {
            if (target == null) return false;

            Vector3 forward = target.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            // Exact rear first, then rear-left and rear-right.  The side offsets mean the monster
            // can wait on a stair/corner instead of repeatedly attempting an unavailable tile.
            for (int i = 0; i < RearAngles.Length; i++)
            {
                float angle = RearAngles[i];
                Vector3 rearDirection = Quaternion.Euler(0f, angle, 0f) * -forward;
                Vector3 candidate = target.position + rearDirection * behindDistance;
                if (!TrySetReachableDestination(candidate, 1f)) continue;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Runs as far as possible in the opposite direction from a threat (tutorial's
        /// GetFurthestPointInOppositeDirection / SetFurthestDestination).
        /// </summary>
        /// <remarks>
        /// Retries at progressively smaller radii before giving up entirely. A single fixed radius
        /// (the original behavior) fails outright in a small room — e.g. a 5x4m fuse room against the
        /// default 14m ShyFleeRadius — because every full-radius candidate lands past a wall with an
        /// incomplete path. When every radius fails, the caller's *previous* destination (often the
        /// "stalk behind the player" point from InterestedState — i.e. right next to the player) was
        /// silently left active, so a monster that supposedly just started fleeing kept walking toward
        /// its old near-player target instead: visually "Shy running at the player", ending in a
        /// contact kill (see chat — this was a real reported bug, not a design choice). Stopping the
        /// agent on total failure guarantees Shy never continues toward a stale, possibly-near-player
        /// destination.
        /// </remarks>
        public bool SetFleeDestinationAwayFrom(Vector3 threatPosition, float radius)
        {
            Vector3 away = (transform.position - threatPosition);
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f)
            {
                away.Normalize();
            }
            else
            {
                Vector2 randomDirection = Random.insideUnitCircle.normalized;
                away = new Vector3(randomDirection.x, 0f, randomDirection.y);
            }

            float[] radiusTiers = { radius, radius * 0.5f, radius * 0.25f };
            for (int tier = 0; tier < radiusTiers.Length; tier++)
            {
                float tierRadius = radiusTiers[tier];
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    // Bias each retry a little off the exact opposite direction so a wall directly
                    // behind the monster doesn't repeatedly fail NavMesh.SamplePosition at the same
                    // dead point.
                    Vector3 jitter = Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * away;
                    Vector3 candidate = transform.position + jitter * tierRadius;

                    if (TrySetReachableDestination(candidate, tierRadius)) return true;
                }
            }

            // Nowhere to flee to at any radius (fully enclosed room). Stop rather than silently
            // continuing toward whatever destination was active before this call.
            Stop();
            return false;
        }

        // NavMesh.SamplePosition only checks "is there walkable navmesh near this point" — it does
        // NOT check that the point is actually reachable from here. Our test floor is one big slab
        // wider than the wall-maze, so an unreachable candidate can snap onto a disconnected pocket
        // outside the maze; the agent then paths as far as it can and sits stuck at that boundary,
        // which looks exactly like walking into an invisible wall (see chat).
        private bool IsPathReachable(Vector3 destination)
        {
            if (!Agent.isOnNavMesh) return false;
            return Agent.CalculatePath(destination, Path) && Path.status == NavMeshPathStatus.PathComplete;
        }

        private bool TrySetReachableDestination(Vector3 candidate, float sampleRadius)
        {
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas)) return false;
            if (IsInsideAnySafeZone(hit.position) || !IsPathReachable(hit.position)) return false;
            SetDestination(hit.position);
            return true;
        }

        private bool IsInsideAnySafeZone(Vector3 point)
        {
            if (safeZones == null) return false;
            var zones = safeZones.Items;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].Contains(point)) return true;
            }
            return false;
        }
    }
}
