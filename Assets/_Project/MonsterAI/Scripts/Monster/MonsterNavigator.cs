using UnityEngine;
using UnityEngine.AI;

namespace Game.MonsterAI
{
    /// <summary>
    /// Thin wrapper around NavMeshAgent — the equivalent of the tutorial's BrackenPathController,
    /// translated from A* Pathfinding Project to Unity's built-in NavMesh (see chat: no new package
    /// dependency for a debug test rig). Uses lazy Agent access instead of relying on Awake()
    /// ordering — AddComponent() from an Editor-script menu item does not reliably fire Awake()
    /// synchronously the way Play Mode does (empirically confirmed — see chat / Editor.log).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MonsterNavigator : MonoBehaviour
    {
        [SerializeField] private SafeZoneRuntimeSet safeZones;

        private NavMeshAgent agent;
        private NavMeshAgent Agent => agent != null ? agent : (agent = GetComponent<NavMeshAgent>());

        public bool HasArrived => Agent.isOnNavMesh && !Agent.pathPending
            && Agent.remainingDistance <= Agent.stoppingDistance;

        public void Bind(SafeZoneRuntimeSet zones) => safeZones = zones;

        public void SetDestination(Vector3 point)
        {
            if (Agent.isOnNavMesh) Agent.SetDestination(point);
        }

        public void SetSpeed(float speed) => Agent.speed = speed;

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
            float[] rearAngles = { 0f, -42f, 42f, -72f, 72f };
            foreach (float angle in rearAngles)
            {
                Vector3 rearDirection = Quaternion.Euler(0f, angle, 0f) * -forward;
                Vector3 candidate = target.position + rearDirection * behindDistance;
                if (!TrySetReachableDestination(candidate, 1f)) continue;
                return true;
            }

            return false;
        }

        /// <summary>Runs as far as possible in the opposite direction from a threat (tutorial's GetFurthestPointInOppositeDirection / SetFurthestDestination).</summary>
        public bool SetFleeDestinationAwayFrom(Vector3 threatPosition, float radius)
        {
            Vector3 away = (transform.position - threatPosition);
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitSphere;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                // Bias each retry a little off the exact opposite direction so a wall behind the
                // monster doesn't repeatedly fail NavMesh.SamplePosition at the same dead point.
                Vector3 jitter = Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * away;
                Vector3 candidate = transform.position + jitter * radius;

                if (TrySetReachableDestination(candidate, radius)) return true;
            }
            return false;
        }

        // NavMesh.SamplePosition only checks "is there walkable navmesh near this point" — it does
        // NOT check that the point is actually reachable from here. Our test floor is one big slab
        // wider than the wall-maze, so an unreachable candidate can snap onto a disconnected pocket
        // outside the maze; the agent then paths as far as it can and sits stuck at that boundary,
        // which looks exactly like walking into an invisible wall (see chat).
        private bool IsPathReachable(Vector3 destination)
        {
            NavMeshPath path = new NavMeshPath();
            if (!Agent.isOnNavMesh) return false;
            return Agent.CalculatePath(destination, path) && path.status == NavMeshPathStatus.PathComplete;
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
