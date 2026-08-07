using System;
using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>
    /// Composition root and state owner for the Bracken-style monster AI (see chat: full rewrite
    /// based on Lethal Company's Bracken). States: Patrol -> Interested -> Shy / Aggressive -> Kill.
    /// Contact-range kill is checked globally every frame in any state except Kill itself — Bracken
    /// snaps your neck on physical contact regardless of what phase it's in.
    /// The flashlight has no effect here on purpose (TGD section 8 / prior chat decision).
    /// </summary>
    public class MonsterBrain : MonoBehaviour
    {
        [SerializeField] private MonsterConfig config;
        [SerializeField] private MonsterPerception perception;
        [SerializeField] private MonsterAngerController anger;
        [SerializeField] private MonsterNavigator navigator;
        [SerializeField] private PlayerGazeDetector gazeDetector;
        [SerializeField] private MonsterGazeReceiver gazeReceiver;
        [SerializeField] private Transform player;

        public MonsterConfig Config => config;
        public MonsterPerception Perception => perception;
        public MonsterAngerController Anger => anger;
        public MonsterNavigator Navigator => navigator;
        public Transform Player => player;
        public Vector3 PatrolAnchor { get; private set; }
        public string CurrentStateName => fsm?.Current?.GetType().Name ?? "-";

        /// <summary>True while the player is currently aiming at this monster's eyes (TGD section 22-24).</summary>
        public bool IsGazedAtNow => gazeDetector != null && ReferenceEquals(gazeDetector.CurrentTarget, gazeReceiver);

        public event Action<string> OnStateChanged;
        public event Action OnKill;

        public IMonsterState PatrolState { get; private set; }
        public IMonsterState InterestedState { get; private set; }
        public IMonsterState ShyState { get; private set; }
        public IMonsterState AggressiveState { get; private set; }
        public IMonsterState KillState { get; private set; }

        private MonsterStateMachine fsm;

        public void Bind(MonsterConfig monsterConfig, MonsterPerception monsterPerception,
            MonsterAngerController angerController, MonsterNavigator monsterNavigator,
            PlayerGazeDetector detector, MonsterGazeReceiver receiver, Transform playerTransform)
        {
            config = monsterConfig;
            perception = monsterPerception;
            anger = angerController;
            navigator = monsterNavigator;
            gazeDetector = detector;
            gazeReceiver = receiver;
            player = playerTransform;
        }

        private void Awake()
        {
            PatrolAnchor = transform.position;
            fsm = new MonsterStateMachine();
            PatrolState = new PatrolState(this);
            InterestedState = new InterestedState(this);
            ShyState = new ShyState(this);
            AggressiveState = new AggressiveState(this);
            KillState = new KillState(this);
        }

        private void OnEnable()
        {
            ChangeState(PatrolState);
        }

        private void Update()
        {
            if (fsm.Current != KillState && perception != null && perception.DistanceToPlayer <= config.ContactKillRange)
            {
                ChangeState(KillState);
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
    }
}
