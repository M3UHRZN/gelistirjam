using System;
using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>
    /// Composition root and P0 state owner for the monster. Wires perception, positioning, anger and
    /// navigation together and runs the state machine (TGD sections 11-12, priority P0 per section 55).
    /// The flashlight has no effect here on purpose — TGD section 8 is explicit that it does not
    /// influence the monster AI; it only helps the player see (FlashlightController.cs).
    /// </summary>
    public class MonsterBrain : MonoBehaviour
    {
        [SerializeField] private MonsterConfig config;
        [SerializeField] private MonsterPerception perception;
        [SerializeField] private MonsterPositionEvaluator positionEvaluator;
        [SerializeField] private MonsterAngerController anger;
        [SerializeField] private MonsterNavigator navigator;
        [SerializeField] private PlayerGazeDetector gazeDetector;
        [SerializeField] private MonsterGazeReceiver gazeReceiver;
        [SerializeField] private Transform player;

        public MonsterConfig Config => config;
        public MonsterPerception Perception => perception;
        public MonsterPositionEvaluator PositionEvaluator => positionEvaluator;
        public MonsterAngerController Anger => anger;
        public MonsterNavigator Navigator => navigator;
        public Transform Player => player;
        public string CurrentStateName => fsm?.Current?.GetType().Name ?? "-";

        /// <summary>True while the player is currently aiming at this monster's eyes (TGD section 22-24).</summary>
        public bool IsGazedAtNow => gazeDetector != null && ReferenceEquals(gazeDetector.CurrentTarget, gazeReceiver);

        /// <summary>True once inside Critical Distance — Commit overrides everything, gaze included (TGD section 25).</summary>
        public bool IsPastCriticalDistance => perception != null && config != null && perception.DistanceToPlayer <= config.CriticalDistance;

        public event Action<string> OnStateChanged;
        public event Action OnKill;

        public IMonsterState HiddenState { get; private set; }
        public IMonsterState StalkState { get; private set; }
        public IMonsterState ObservedState { get; private set; }
        public IMonsterState RetreatState { get; private set; }
        public IMonsterState CommitState { get; private set; }
        public IMonsterState KillState { get; private set; }

        private MonsterStateMachine fsm;

        public void Bind(MonsterConfig monsterConfig, MonsterPerception monsterPerception,
            MonsterPositionEvaluator evaluator, MonsterAngerController angerController,
            MonsterNavigator monsterNavigator, PlayerGazeDetector detector,
            MonsterGazeReceiver receiver, Transform playerTransform)
        {
            config = monsterConfig;
            perception = monsterPerception;
            positionEvaluator = evaluator;
            anger = angerController;
            navigator = monsterNavigator;
            gazeDetector = detector;
            gazeReceiver = receiver;
            player = playerTransform;
        }

        private void Awake()
        {
            fsm = new MonsterStateMachine();
            HiddenState = new HiddenState(this);
            StalkState = new StalkState(this);
            ObservedState = new ObservedState(this);
            RetreatState = new RetreatState(this);
            CommitState = new CommitState(this);
            KillState = new KillState(this);
        }

        private void OnEnable()
        {
            if (gazeDetector != null) gazeDetector.OnGazeConfirmed += HandleGazeConfirmed;
            ChangeState(StalkState);
        }

        private void OnDisable()
        {
            if (gazeDetector != null) gazeDetector.OnGazeConfirmed -= HandleGazeConfirmed;
        }

        private void Update()
        {
            bool alreadyCommitting = fsm.Current == CommitState || fsm.Current == KillState;

            if (IsPastCriticalDistance && !alreadyCommitting)
            {
                ChangeState(CommitState);
            }

            fsm.Tick(Time.deltaTime);
        }

        public void ChangeState(IMonsterState next)
        {
            if (fsm.Current == next) return;
            fsm.SetState(next);
            OnStateChanged?.Invoke(CurrentStateName);
        }

        public void RaiseKill() => OnKill?.Invoke();

        private void HandleGazeConfirmed(MonsterGazeReceiver receiver)
        {
            if (receiver != gazeReceiver) return;
            if (fsm.Current == CommitState || fsm.Current == KillState) return; // gaze no longer works past critical distance

            anger.AddAnger(1); // every successful eye contact makes it permanently more dangerous (TGD section 2.2, 19)
            ChangeState(ObservedState);
        }
    }
}
