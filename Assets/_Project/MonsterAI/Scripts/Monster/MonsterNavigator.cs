using UnityEngine;
using UnityEngine.AI;

namespace Game.MonsterAI
{
    /// <summary>
    /// Thin wrapper around NavMeshAgent — keeps state classes from touching the agent API directly.
    /// Uses a lazy accessor instead of relying on Awake() ordering: when a component is added to a
    /// GameObject that starts inactive and is only SetActive(true) once fully wired (the editor scene
    /// builder does this), Awake()/OnEnable() firing order across sibling components on that GameObject
    /// is not something to depend on — MonsterBrain.OnEnable() calling into this class before its own
    /// Awake() had run was a real, reproducible NullReferenceException (see chat / Editor.log).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MonsterNavigator : MonoBehaviour
    {
        private NavMeshAgent agent;
        private NavMeshAgent Agent => agent != null ? agent : (agent = GetComponent<NavMeshAgent>());

        public bool HasArrived => Agent.isOnNavMesh && !Agent.pathPending
            && Agent.remainingDistance <= Agent.stoppingDistance;

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
    }
}
