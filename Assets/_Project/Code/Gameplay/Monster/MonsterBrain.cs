using System;
using UnityEngine;

using Game.Gameplay.Player;
namespace Game.Gameplay.Monster
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
        [SerializeField] private StalkPointRuntimeSet stalkPoints;
        [SerializeField] private Game.Gameplay.Player.PlayerVisionModel visionModel;

        public MonsterConfig Config => config;
        public MonsterPerception Perception => perception;
        public MonsterAngerController Anger => anger;
        public MonsterNavigator Navigator => navigator;
        public Transform Player => player;
        public StalkPointRuntimeSet StalkPoints => stalkPoints;
        public Game.Gameplay.Player.PlayerVisionModel VisionModel => visionModel;
        public Vector3 PatrolAnchor { get; private set; }
        public string CurrentStateName => fsm?.Current?.GetType().Name ?? "-";

        /// <summary>
        /// Set by the composition root when every fuse is complete. Final hunt keeps exact player
        /// awareness and the faster/closer final-phase stalk-point tuning, but deliberately does not
        /// turn the monster into a permanent direct-chase enemy.
        /// </summary>
        public bool IsFinalHunt { get; private set; }

        public void BeginFinalHunt()
        {
            EnsureInitialized();
            IsFinalHunt = true;
            SetDecisionReason("Final hunt: track player continuously through stalking routes");
            ChangeState(RepositionState);
        }

        /// <summary>
        /// True while the player is actively working a fuse box (TGD section 36 — the monster must be
        /// aware of fuse interactions and react, e.g. by drifting toward an approach position instead
        /// of patrolling obliviously). Set by the composition root via SetFuseAwareness, forwarded from
        /// ObjectiveManager's aggregated FuseBox events.
        /// </summary>
        public bool IsAwareOfFuse { get; private set; }
        public Vector3 FusePosition { get; private set; }

        private float fuseAngerAccumulator;

        public void SetFuseAwareness(bool active, Vector3 position)
        {
            IsAwareOfFuse = active;
            FusePosition = position;
            fuseAngerAccumulator = 0f;
        }

        /// <summary>True while the player is currently aiming at this monster's eyes (TGD section 22-24).</summary>
        public bool IsGazedAtNow => gazeDetector != null && ReferenceEquals(gazeDetector.CurrentTarget, gazeReceiver);

        /// <summary>
        /// A held-gaze presentation/anger threshold. Escape behavior deliberately uses the raw
        /// IsGazedAtNow edge so the monster begins fleeing on the first detected frame; confirmation
        /// remains responsible for the one-shot anger mutation and Yell event.
        /// </summary>
        public bool IsGazeConfirmed => gazeHoldSeconds >= config.GazeConfirmDurationSeconds;

        /// <summary>TGD section 21: CurrentSpeed = BaseSpeed x AngerMultiplier, interpolated by AggroChance up to AngerSpeedMultiplierMax.</summary>
        public float SpeedMultiplier => 1f + anger.AggroChance * (config.AngerSpeedMultiplierMax - 1f);

        /// <summary>Behaviour tuning for the current anger bracket (TGD section 20) — the single place states read stage-dependent numbers from.</summary>
        public AngerStageTuning Tuning => config.TuningFor(anger.Stage);

        private float gazeHoldSeconds;
        private bool wasGazeConfirmed;
        private bool wasRawGazedLastFrame;

        public event Action<string> OnStateChanged;
        /// <summary>
        /// Raised once on the false-to-true edge of a confirmed gaze. Presentation systems may
        /// observe this, but the Brain remains the sole owner of the accompanying anger mutation.
        /// </summary>
        public event Action OnConfirmedGazeStarted;
        public event Action OnKill;

        /// <summary>
        /// Short human-readable explanation for the most recent notable AI decision (MASTER_PROMPT-
        /// style "Decision Reason" — dev/debug only, never shown to the player). States call
        /// SetDecisionReason right before a ChangeState that isn't self-explanatory from the state name
        /// alone (e.g. why Reposition chose CutOff instead of Flank/Ambush this time). Surfaced by
        /// MonsterDebugHUD.
        /// </summary>
        public string LastDecisionReason { get; private set; } = "-";
        public void SetDecisionReason(string reason) => LastDecisionReason = reason;

        public IMonsterState PatrolState { get; private set; }
        public IMonsterState InterestedState { get; private set; }
        public IMonsterState ShyState { get; private set; }
        public IMonsterState AggressiveState { get; private set; }
        public IMonsterState KillState { get; private set; }

        // TGD section 12 expansion (see chat: TGD comparison) — PatrolState already fulfills the
        // "Hidden" role (idle, no player awareness), so no separate class was added for it; a
        // duplicate would just be two names for the same behavior.
        public IMonsterState RepositionState { get; private set; }
        public IMonsterState FlankState { get; private set; }
        public IMonsterState AmbushState { get; private set; }
        public IMonsterState CutOffState { get; private set; }
        public IMonsterState ObservedState { get; private set; }
        public IMonsterState FakeRetreatState { get; private set; }
        public IMonsterState CommitState { get; private set; }
        public IMonsterState SafeZoneCampState { get; private set; }

        /// <summary>True while the player is standing inside a SafeZone (TGD sections 29-30) — they cannot be harmed, but the monster still reacts by camping the exits.</summary>
        public bool IsPlayerInSafeZone { get; private set; }

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

        /// <summary>Optional: wires the StalkPoint/vision-model systems (TGD section 15-18) consumed by the expanded state set. Safe to leave unbound — states fall back to plain NavMesh sampling.</summary>
        public void BindStalkSystems(StalkPointRuntimeSet stalkPointSet, Game.Gameplay.Player.PlayerVisionModel playerVisionModel)
        {
            stalkPoints = stalkPointSet;
            visionModel = playerVisionModel;
        }

        private bool initialized;

        private void Awake() => EnsureInitialized();

        /// <summary>
        /// Builds fsm + all state instances exactly once. Guarded and called from both Awake() and
        /// OnEnable() (idempotent — same defensive pattern already used by MonsterNavigator's lazy
        /// Agent accessor and the SafeZone/StalkPoint/FuseBox OnEnable+Start fallback, see their own
        /// comments): scenes built by an Editor script (MonsterAITestSceneBuilder) or a domain reload
        /// that lands mid-transition don't reliably guarantee Awake runs before OnEnable the way normal
        /// interactive Play Mode does. Without this guard, OnEnable's ChangeState(PatrolState) call hit
        /// a null `fsm` and threw a NullReferenceException every single Update() thereafter — silently,
        /// since Unity logs but doesn't halt on it — meaning the monster ran zero AI logic for the rest
        /// of the session. Reproduced live via Unity MCP (see chat: Unity MCP validation pass) after a
        /// forced script recompile + Play Mode re-entry; not something visible from source alone.
        /// </summary>
        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            PatrolAnchor = transform.position;
            fsm = new MonsterStateMachine();
            PatrolState = new PatrolState(this);
            InterestedState = new InterestedState(this);
            ShyState = new ShyState(this);
            AggressiveState = new AggressiveState(this);
            KillState = new KillState(this);

            RepositionState = new RepositionState(this);
            FlankState = new FlankState(this);
            AmbushState = new AmbushState(this);
            CutOffState = new CutOffState(this);
            ObservedState = new ObservedState(this);
            FakeRetreatState = new FakeRetreatState(this);
            CommitState = new CommitState(this);
            SafeZoneCampState = new SafeZoneCampState(this);
        }

        private void OnEnable()
        {
            EnsureInitialized();
            gazeHoldSeconds = 0f;
            wasGazeConfirmed = false;
            wasRawGazedLastFrame = false;
            ChangeState(PatrolState);
        }

        private void Update()
        {
            // TGD section 29: a player standing inside a SafeZone cannot be attacked, even if the
            // monster is somehow within contact range of the zone's edge. Checked against the
            // player's own position, not the monster's — the monster is never allowed to enter a
            // SafeZone itself (see MonsterNavigator's destination filtering), so this only matters
            // for a player lingering right at the boundary.
            IsPlayerInSafeZone = navigator != null && player != null && navigator.IsPointInSafeZone(player.position);
            bool playerIsSafe = IsPlayerInSafeZone;

            if (!playerIsSafe && fsm.Current != KillState && perception != null
                && perception.ContactDistanceToPlayer <= config.ContactKillRange)
            {
                ChangeState(KillState);
            }

            bool rawGazedNow = IsGazedAtNow;
            bool rawGazeStartedThisFrame = rawGazedNow && !wasRawGazedLastFrame;
            gazeHoldSeconds = rawGazedNow ? gazeHoldSeconds + Time.deltaTime : 0f;

            bool gazeConfirmed = IsGazeConfirmed;
            if (gazeConfirmed && !wasGazeConfirmed && fsm.Current != KillState)
            {
                anger.AddAnger(config.AngerPerSpotted);
                OnConfirmedGazeStarted?.Invoke();
            }

            wasGazeConfirmed = gazeConfirmed;

            // TGD section 21: the anger multiplier is continuous, so it's pushed every frame rather
            // than baked into each state's Enter() — anger changes mid-state (decay while chasing, a
            // fresh gaze while stalking) must take effect immediately, not at the next transition.
            if (navigator != null) navigator.SetSpeedMultiplier(SpeedMultiplier);

            // TGD section 25: inside Critical Distance, gaze/freeze mechanics stop being relevant —
            // the monster is already close enough that hesitating would be nonsensical, so it commits
            // to a final rush instead. Doesn't override the SafeZone check above.
            if (!playerIsSafe && fsm.Current != KillState && fsm.Current != CommitState
                && perception != null && perception.DistanceToPlayer <= config.CriticalDistance)
            {
                ChangeState(CommitState);
            }

            // TGD section 30: a player who ducks into a SafeZone must not simply switch the AI off.
            // Any state that was actively hunting hands over to SafeZoneCamp, which lurks at the exits
            // instead of wandering away. Patrol is left alone — it wasn't hunting in the first place,
            // and Kill/Observed are mid-beat reactions that should be allowed to finish.
            if (playerIsSafe && perception != null
                && perception.IsPlayerWithinRadius(config.StalkAbandonRadius)
                && fsm.Current != SafeZoneCampState && fsm.Current != PatrolState
                && fsm.Current != KillState && fsm.Current != ObservedState)
            {
                ChangeState(SafeZoneCampState);
            }

            // TGD section 3 loop: "monster becomes more aggressive" while a fuse is being worked, not
            // only once every fuse is done (that part is covered by FinalChase/IsFinalHunt).
            // A slow passive drip while unaware would be pointless during Aggressive/Kill, so it only
            // accrues in the calmer states where the player is otherwise safe from an anger climb.
            if (IsAwareOfFuse && fsm.Current != KillState && fsm.Current != AggressiveState)
            {
                fuseAngerAccumulator += config.FuseAwarenessAngerPerSecond * Time.deltaTime;
                while (fuseAngerAccumulator >= 1f)
                {
                    anger.AddAnger(1);
                    fuseAngerAccumulator -= 1f;
                }
            }

            // Gaze is a global interruption for every calm/stalking state. It runs after the lethal
            // contact/critical-distance and SafeZone priorities above, so looking at the monster can
            // never cancel a kill, committed rush, or an already independent aggressive chase. The
            // raw detector edge is intentional: escape starts on the first acquired frame, while the
            // confirmed-gaze edge above remains available for Yell/anger presentation.
            if (!playerIsSafe && ShouldFleeFromPlayerGaze(rawGazeStartedThisFrame))
            {
                SetDecisionReason($"Shy: raw player gaze interrupted {CurrentStateName}");
                ChangeState(ShyState);
            }

            fsm.Tick(Time.deltaTime);
            // Consume this raw edge after every state transition in the frame. Holding the same gaze
            // through Shy expiry/FakeRetreat must not start a second Shy lifetime.
            wasRawGazedLastFrame = rawGazedNow;
        }

        private bool ShouldFleeFromPlayerGaze(bool rawGazeStartedThisFrame)
        {
            if (!rawGazeStartedThisFrame) return false;

            IMonsterState current = fsm.Current;
            return current != ShyState
                && current != AggressiveState
                && current != CommitState
                && current != KillState;
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
