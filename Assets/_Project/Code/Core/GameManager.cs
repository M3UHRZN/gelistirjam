using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Owns the game's own flow state machine (TGD section 4): Initialization -> Exploration ->
    /// FuseInteraction / FinalChase -> Death / Victory. Core never references concrete Gameplay
    /// types directly (MASTER_PROMPT.md section 9 — dependencies point inward only); it only knows
    /// the IPlayerControlGate interface. The composition root (scene builder) wires the concrete
    /// player and calls ChangeState(...) directly for events it observes (e.g. MonsterBrain.OnKill).
    ///
    /// FuseInteractionState and FinalChaseState are fully implemented, not stubs — but nothing
    /// triggers them yet at this phase (no ObjectiveManager/ExitController exist). That lands in the
    /// fuse/exit system phases.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private IPlayerControlGate playerControlGate;

        public event Action<string> OnStateChanged;
        public string CurrentStateName => fsm?.Current?.GetType().Name ?? "-";

        public IGameState InitializationState { get; private set; }
        public IGameState ExplorationState { get; private set; }
        public IGameState FuseInteractionState { get; private set; }
        public IGameState FinalChaseState { get; private set; }
        public IGameState DeathState { get; private set; }
        public IGameState VictoryState { get; private set; }

        private GameStateMachine fsm;

        public void Bind(IPlayerControlGate controlGate) => playerControlGate = controlGate;

        public void SetPlayerControlEnabled(bool enabled) => playerControlGate?.SetControlEnabled(enabled);

        private void Awake()
        {
            fsm = new GameStateMachine();
            InitializationState = new InitializationState(this);
            ExplorationState = new ExplorationState(this);
            FuseInteractionState = new FuseInteractionState(this);
            FinalChaseState = new FinalChaseState(this);
            DeathState = new DeathState(this);
            VictoryState = new VictoryState(this);
        }

        private void OnEnable() => ChangeState(InitializationState);

        private void Update() => fsm.Tick(Time.deltaTime);

        public void ChangeState(IGameState next)
        {
            if (fsm.Current == next) return;
            fsm.SetState(next);
            OnStateChanged?.Invoke(CurrentStateName);
        }
    }
}
