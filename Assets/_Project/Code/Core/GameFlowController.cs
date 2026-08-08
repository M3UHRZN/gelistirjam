using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Drives the game's flow state machine (TGD section 4): Initialization -> Exploration ->
    /// FuseInteraction / FinalChase -> Death / Victory. That is its whole job — it owns no
    /// objectives, no inventory, no services, which is why it is not called a "manager". Core never
    /// references concrete Gameplay types directly (MASTER_PROMPT.md section 9 — dependencies point
    /// inward only); it only knows the IPlayerControlGate interface. The composition root (scene
    /// builder) wires the concrete player and calls ChangeState(...) directly for events it observes
    /// (e.g. MonsterBrain.OnKill).
    ///
    /// FuseInteractionState and FinalChaseState are fully implemented and fully wired: FuseBox.Begin/
    /// EndInteract drives FuseInteractionState <-> ExplorationState, and an exit controller drives
    /// FinalChaseState once every fuse reports complete. (Was previously documented as "nothing
    /// triggers them yet" — accurate when written, stale once those systems landed; corrected
    /// here — see chat: Unity MCP validation pass.)
    /// </summary>
    public class GameFlowController : MonoBehaviour
    {
        // Serialized as MonoBehaviour, not as IPlayerControlGate: Unity's serializer ignores
        // interface-typed fields entirely (same failure class already fixed once in
        // MonsterPerception.IPlayerVelocitySource — see that file's comment for the full
        // explanation). The previous `private IPlayerControlGate playerControlGate` field was
        // correctly assigned by Bind() at Editor build time, but silently reset to null the moment
        // Play Mode actually started (or the scene was reopened) since nothing about that assignment
        // was ever persisted. SetPlayerControlEnabled(false) was therefore a no-op the entire time —
        // found live via Unity MCP testing after a user report that camera look still worked while
        // the Fuse minigame panel was open (see chat).
        [SerializeField] private MonoBehaviour playerControlGateBehaviour;
        private IPlayerControlGate cachedPlayerControlGate;
        private IPlayerControlGate PlayerControlGate =>
            cachedPlayerControlGate ??= playerControlGateBehaviour as IPlayerControlGate;

        public event Action<string> OnStateChanged;
        public string CurrentStateName => fsm?.Current?.GetType().Name ?? "-";

        public IGameState InitializationState { get; private set; }
        public IGameState ExplorationState { get; private set; }
        public IGameState FuseInteractionState { get; private set; }
        public IGameState FinalChaseState { get; private set; }
        public IGameState DeathState { get; private set; }
        public IGameState VictoryState { get; private set; }

        private GameStateMachine fsm;

        public void Bind(IPlayerControlGate controlGate)
        {
            cachedPlayerControlGate = controlGate;
            playerControlGateBehaviour = controlGate as MonoBehaviour;
        }

        public void SetPlayerControlEnabled(bool enabled) => PlayerControlGate?.SetControlEnabled(enabled);

        private bool initialized;

        private void Awake() => EnsureInitialized();

        /// <summary>
        /// Builds fsm + all state instances exactly once. Guarded and called from both Awake() and
        /// OnEnable() — same idempotent-init fix applied to MonsterBrain after Unity MCP validation
        /// found this exact class of bug live (OnEnable's ChangeState hitting a null `fsm` when Awake
        /// hadn't run yet, e.g. after a script recompile lands mid Play Mode transition or the scene is
        /// built by an Editor script). See MonsterBrain.EnsureInitialized's comment for the full
        /// reproduction — this file threw the identical NullReferenceException at the identical call
        /// site (OnEnable -> ChangeState) in the same session.
        /// </summary>
        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            fsm = new GameStateMachine();
            InitializationState = new InitializationState(this);
            ExplorationState = new ExplorationState(this);
            FuseInteractionState = new FuseInteractionState(this);
            FinalChaseState = new FinalChaseState(this);
            DeathState = new DeathState(this);
            VictoryState = new VictoryState(this);
        }

        private void OnEnable()
        {
            EnsureInitialized();
            ChangeState(InitializationState);
        }

        private void Update() => fsm.Tick(Time.deltaTime);

        public void ChangeState(IGameState next)
        {
            if (fsm.Current == next) return;
            fsm.SetState(next);
            OnStateChanged?.Invoke(CurrentStateName);
        }
    }
}
