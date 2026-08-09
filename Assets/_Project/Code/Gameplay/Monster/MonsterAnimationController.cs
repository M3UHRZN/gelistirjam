using UnityEngine;
using UnityEngine.AI;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Presentation-only bridge between the monster AI/navigation and Mecanim. Gameplay remains
    /// authoritative in MonsterBrain; animation never moves the NavMeshAgent or decides a kill.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterAnimationController : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int MovingId = Animator.StringToHash("Moving");
        private static readonly int AggressiveId = Animator.StringToHash("Aggressive");
        private static readonly int RetreatingId = Animator.StringToHash("Retreating");
        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int YellId = Animator.StringToHash("Yell");

        [SerializeField] private Animator animator;
        [SerializeField] private MonsterBrain brain;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField, Min(0f)] private float movingThreshold = 0.05f;
        [SerializeField, Min(0f)] private float speedDampSeconds = 0.12f;

        private bool subscribed;
        private string pendingStateName;
        private bool attackPending;
        private bool yellPending;

        public bool IsBound => animator != null && brain != null && agent != null;

        public void Bind(Animator targetAnimator, MonsterBrain monsterBrain, NavMeshAgent navMeshAgent)
        {
            Unsubscribe();

            animator = targetAnimator;
            brain = monsterBrain;
            agent = navMeshAgent;

            if (animator != null) animator.applyRootMotion = false;
            Subscribe();
            SynchronizeState();
        }

        private void OnEnable()
        {
            ResolveComponents();
            if (animator != null) animator.applyRootMotion = false;
            Subscribe();
            SynchronizeState();
        }

        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            ApplyPendingSignals();
            if (!AnimatorReady || agent == null) return;

            float speed = agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            animator.SetFloat(SpeedId, speed, speedDampSeconds, Time.deltaTime);
            animator.SetBool(MovingId, speed > movingThreshold);
        }

        private void ResolveComponents()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (brain == null) brain = GetComponent<MonsterBrain>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || brain == null || subscribed) return;

            brain.OnStateChanged += HandleStateChanged;
            brain.OnConfirmedGazeStarted += HandleConfirmedGazeStarted;
            brain.OnKill += HandleKill;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || brain == null) return;

            brain.OnStateChanged -= HandleStateChanged;
            brain.OnConfirmedGazeStarted -= HandleConfirmedGazeStarted;
            brain.OnKill -= HandleKill;
            subscribed = false;
        }

        private void SynchronizeState()
        {
            if (brain != null) HandleStateChanged(brain.CurrentStateName);
        }

        private void HandleStateChanged(string stateName)
        {
            pendingStateName = stateName;
            ApplyPendingSignals();
        }

        private void HandleKill()
        {
            yellPending = false;
            attackPending = true;
            ApplyPendingSignals();
        }

        private void HandleConfirmedGazeStarted()
        {
            if (attackPending) return;

            yellPending = true;
            ApplyPendingSignals();
        }

        private bool AnimatorReady => animator != null
            && animator.isActiveAndEnabled
            && animator.runtimeAnimatorController != null
            && animator.isInitialized;

        private void ApplyPendingSignals()
        {
            if (!AnimatorReady) return;

            if (pendingStateName != null)
            {
                bool aggressive = pendingStateName == nameof(AggressiveState)
                    || pendingStateName == nameof(CommitState)
                    || pendingStateName == nameof(KillState);
                bool retreating = pendingStateName == nameof(ShyState)
                    || pendingStateName == nameof(FakeRetreatState);
                animator.SetBool(AggressiveId, aggressive);
                animator.SetBool(RetreatingId, retreating);
                pendingStateName = null;
            }

            if (attackPending)
            {
                animator.ResetTrigger(YellId);
                yellPending = false;
                animator.ResetTrigger(AttackId);
                animator.SetTrigger(AttackId);
                attackPending = false;
                return;
            }

            if (yellPending)
            {
                animator.ResetTrigger(YellId);
                animator.SetTrigger(YellId);
                yellPending = false;
            }
        }
    }
}
