using Game.Gameplay.Monster;
using Game.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Game.Gameplay.Entrance
{
    /// <summary>
    /// Coordinates the entrance's one-shot lightweight monster actor without activating the full
    /// monster runtime rig or AI state machine.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntranceMonsterTrialController : MonoBehaviour, IEntranceMonsterTrialStarter
    {
        [Header("Scene References")]
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerGazeDetector gazeDetector;
        [SerializeField] private MonsterGazeReceiver gazeReceiver;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private GameObject monsterRoot;
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform retreatPoint;
        [SerializeField] private EntranceDoorActuator corridorDoor;
        [SerializeField] private EntranceSceneResetter sceneResetter;

        [Header("Trial Tuning")]
        [SerializeField, Min(0f)] private float approachSpeed = 3.5f;
        [SerializeField, Min(0f)] private float retreatSpeed = 5f;
        [SerializeField, Min(0f)] private float pathRefreshInterval = 0.25f;
        [SerializeField, Min(0f)] private float contactDistance = 1.2f;
        [SerializeField, Min(0.01f)] private float navMeshSampleRadius = 2f;
        [SerializeField, Min(0f)] private float retreatArrivalEpsilon = 0.05f;

        [Header("Optional Presentation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string approachTrigger = "Approach";
        [SerializeField] private string retreatTrigger = "Retreat";
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private UnityEvent onApproach;
        [SerializeField] private UnityEvent onRetreat;
        [SerializeField] private UnityEvent onCompleted;
        [SerializeField] private UnityEvent onCaught;

        private readonly EntranceMonsterTrialStateMachine stateMachine = new EntranceMonsterTrialStateMachine();
        private NavMeshPath cachedPath;
        private float nextPathRefreshTime;

        public EntranceMonsterTrialState State => stateMachine.State;

        private void Awake()
        {
            cachedPath = new NavMeshPath();
            if (CanDeactivateMonsterRoot()) monsterRoot.SetActive(false);
            corridorDoor?.SetOpen(false, true);
        }

        private void LateUpdate()
        {
            switch (State)
            {
                case EntranceMonsterTrialState.Approaching:
                    UpdateApproach();
                    break;
                case EntranceMonsterTrialState.Retreating:
                    UpdateRetreat();
                    break;
            }
        }

        public bool TryStart()
        {
            if (!stateMachine.TryBegin()) return false;
            if (!ValidateRequiredReferences(out string validationError))
                return AbortSetup(validationError);

            float sampleRadius = Mathf.Max(0.01f, navMeshSampleRadius);
            if (!NavMesh.SamplePosition(startPoint.position, out NavMeshHit startHit, sampleRadius, NavMesh.AllAreas))
                return AbortSetup($"No NavMesh position was found near start point '{startPoint.name}'.");

            monsterRoot.SetActive(true);
            if (!agent.isActiveAndEnabled)
                return AbortSetup("The lightweight actor's NavMeshAgent is not active and enabled after actor activation.");
            if (!agent.Warp(startHit.position) || !agent.isOnNavMesh)
                return AbortSetup($"The lightweight actor could not warp onto NavMesh near '{startPoint.name}'.");
            if (!TryCalculateCompletePath(player.transform.position, out string approachPathError))
                return AbortSetup(approachPathError);

            corridorDoor.SetOpen(true);
            agent.speed = Mathf.Max(0f, approachSpeed);
            agent.isStopped = false;
            if (!agent.SetPath(cachedPath))
                return AbortSetup("The lightweight actor rejected the complete approach path.");

            nextPathRefreshTime = Time.time + Mathf.Max(0f, pathRefreshInterval);
            SetAnimatorTrigger(approachTrigger);
            onApproach?.Invoke();
            return true;
        }

        private void UpdateApproach()
        {
            Vector3 toPlayer = player.transform.position - agent.transform.position;
            toPlayer.y = 0f;
            float planarCatchDistance = Mathf.Max(0f, contactDistance);
            EntranceMonsterTrialState approachDecision = ArbitrateApproach(
                gazeDetector.CurrentTarget == gazeReceiver,
                toPlayer.sqrMagnitude <= planarCatchDistance * planarCatchDistance);

            if (approachDecision == EntranceMonsterTrialState.Retreating)
            {
                BeginRetreat();
                return;
            }

            if (Time.time >= nextPathRefreshTime)
            {
                nextPathRefreshTime = Time.time + Mathf.Max(0f, pathRefreshInterval);
                if (TryCalculateCompletePath(player.transform.position, out _)) agent.SetPath(cachedPath);
            }

            if (approachDecision == EntranceMonsterTrialState.PlayerCaught) CatchPlayer();
        }

        private static EntranceMonsterTrialState ArbitrateApproach(bool isGazedAt, bool isWithinContactDistance)
        {
            if (isGazedAt) return EntranceMonsterTrialState.Retreating;
            return isWithinContactDistance
                ? EntranceMonsterTrialState.PlayerCaught
                : EntranceMonsterTrialState.Approaching;
        }

        private void BeginRetreat()
        {
            if (!stateMachine.TryRepel()) return;

            SetAnimatorTrigger(retreatTrigger);
            onRetreat?.Invoke();

            agent.speed = Mathf.Max(0f, retreatSpeed);
            agent.isStopped = false;
            if (!TryCalculateCompletePath(retreatPoint.position, out string retreatPathError) || !agent.SetPath(cachedPath))
            {
                Debug.LogWarning(
                    $"Entrance monster trial could not establish a complete retreat path and will complete safely. {retreatPathError}",
                    this);
                CompleteRetreat();
            }
        }

        private void UpdateRetreat()
        {
            if (!agent.isOnNavMesh || agent.pathPending) return;
            if (agent.remainingDistance > agent.stoppingDistance + Mathf.Max(0f, retreatArrivalEpsilon)) return;

            CompleteRetreat();
        }

        private void CompleteRetreat()
        {
            if (!stateMachine.TryCompleteRetreat()) return;

            StopAgentSafely();
            monsterRoot.SetActive(false);
            corridorDoor.SetOpen(false);
            onCompleted?.Invoke();
        }

        private void CatchPlayer()
        {
            if (!stateMachine.TryCatchPlayer()) return;

            StopAgentSafely();
            SetAnimatorTrigger(attackTrigger);
            onCaught?.Invoke();
            if (sceneResetter.BeginReset() || sceneResetter.IsResetting) return;

            if (CanDeactivateMonsterRoot()) monsterRoot.SetActive(false);
            corridorDoor?.SetOpen(false, true);
            player.SetControlEnabled(true);
            Debug.LogError(
                "Entrance monster trial caught reset could not start. The actor was hidden, the corridor was closed, and player control remains enabled. Ensure the scene resetter is active and enabled.",
                this);
        }

        private bool TryCalculateCompletePath(Vector3 target, out string error)
        {
            float sampleRadius = Mathf.Max(0.01f, navMeshSampleRadius);
            if (!NavMesh.SamplePosition(target, out NavMeshHit targetHit, sampleRadius, NavMesh.AllAreas))
            {
                error = "No sampled NavMesh destination was found for the trial target.";
                return false;
            }

            if (!agent.CalculatePath(targetHit.position, cachedPath) || cachedPath.status != NavMeshPathStatus.PathComplete)
            {
                error = "NavMeshAgent did not calculate a complete path to the trial target.";
                return false;
            }

            error = null;
            return true;
        }

        private bool ValidateRequiredReferences(out string error)
        {
            if (!isActiveAndEnabled)
            {
                error = "The coordinator must be active and enabled before the trial can start.";
                return false;
            }
            if (player == null) return MissingReference(nameof(player), out error);
            if (gazeDetector == null) return MissingReference(nameof(gazeDetector), out error);
            if (gazeReceiver == null) return MissingReference(nameof(gazeReceiver), out error);
            if (agent == null) return MissingReference(nameof(agent), out error);
            if (monsterRoot == null) return MissingReference(nameof(monsterRoot), out error);
            if (monsterRoot == gameObject || transform.IsChildOf(monsterRoot.transform))
            {
                error = "The controller must live on a separate coordinator object, not on the lightweight actor root.";
                return false;
            }
            if (agent.transform != monsterRoot.transform && !agent.transform.IsChildOf(monsterRoot.transform))
            {
                error = "The NavMeshAgent must belong to the lightweight actor root hierarchy.";
                return false;
            }
            if (startPoint == null) return MissingReference(nameof(startPoint), out error);
            if (retreatPoint == null) return MissingReference(nameof(retreatPoint), out error);
            if (corridorDoor == null) return MissingReference(nameof(corridorDoor), out error);
            if (sceneResetter == null) return MissingReference(nameof(sceneResetter), out error);
            if (!sceneResetter.isActiveAndEnabled)
            {
                error = "The scene resetter must be active and enabled before the trial can start.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool MissingReference(string fieldName, out string error)
        {
            error = $"Required reference '{fieldName}' is not assigned.";
            return false;
        }

        private bool AbortSetup(string reason)
        {
            StopAgentSafely();
            if (CanDeactivateMonsterRoot()) monsterRoot.SetActive(false);
            corridorDoor?.SetOpen(false, true);
            stateMachine.TryAbortSetup();
            Debug.LogError($"Entrance monster trial setup failed and returned to Ready. {reason}", this);
            return false;
        }

        private bool CanDeactivateMonsterRoot()
        {
            return monsterRoot != null && monsterRoot != gameObject && !transform.IsChildOf(monsterRoot.transform);
        }

        private void StopAgentSafely()
        {
            if (agent == null || !agent.enabled || !agent.gameObject.activeInHierarchy || !agent.isOnNavMesh) return;

            agent.isStopped = true;
            agent.ResetPath();
        }

        private void SetAnimatorTrigger(string triggerName)
        {
            if (animator != null && !string.IsNullOrWhiteSpace(triggerName)) animator.SetTrigger(triggerName);
        }
    }
}
