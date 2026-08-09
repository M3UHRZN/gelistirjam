using System;
using System.Collections;
using Game.Core;
using Game.Gameplay.Interaction;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Gameplay.Objectives
{
    public enum FuseBoxState { Incomplete, Active, Complete }

    /// <summary>
    /// A single fuse objective (TGD section 33-36). Interacting hands off to a
    /// <see cref="FuseMiniGameController"/> 4-button memory minigame — replaces the previous
    /// hold-to-complete placeholder (see chat: Fuse Memory Minigame implementation). This class still
    /// owns the fuse's own state (Incomplete/Active/Complete) and round progress; the minigame itself
    /// is a generic, domain-agnostic runner that knows nothing about fuses, objectives, or the monster
    /// (prompt section 4/45 — no FuseBox -> MonsterBrain hard reference, and none here either).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FuseBox : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("How many memory-sequence rounds this fuse requires (TGD section 33 default split: Fuse 1 = 1, Fuse 2 = 2, Fuse 3 = 3).")]
        private int roundsRequired = 1;
        [SerializeField, FormerlySerializedAs("gameManager")] private GameFlowController gameFlow;
        [SerializeField] private ObjectiveManager objectiveManager;
        [SerializeField] private FuseMiniGameController miniGame;

        // Not serialized: IFuseViewPresenter is an interface, and every FuseBox in the game is spawned
        // by MapFuseGate, which always binds one.
        private IFuseViewPresenter viewPresenter;

        public FuseBoxState State { get; private set; } = FuseBoxState.Incomplete;
        public event Action OnCompleted;

        /// <summary>
        /// Raised when the player begins/ends working a fuse (TGD section 36 — the monster must be
        /// aware of fuse interactions and react, not just "player control is locked"). Args carry this
        /// box's world position so a listener doesn't need a separate lookup.
        /// </summary>
        public event Action<Vector3> OnInteractionStarted;
        public event Action OnInteractionStopped;

        public bool CanInteract => State == FuseBoxState.Incomplete;
        public string InteractionPrompt => "Hold E to activate fuse";

        /// <summary>Rounds finished so far on this fuse. Preserved across an ESC cancel (prompt section 15 — only the *current* round resets), reset to full on Complete.</summary>
        public int RoundsCompleted { get; private set; }
        public int RoundsRequired => roundsRequired;

        /// <summary>Sets the round requirement from code (composition root) instead of only via the Inspector — mirrors ObjectiveManager.SetRequiredCount's pattern.</summary>
        public void SetRoundsRequired(int rounds) => roundsRequired = Mathf.Max(1, rounds);

        // Registers immediately in addition to OnEnable, same defensive pattern as SafeZone/StalkPoint:
        // the editor scene builder creates this GameObject already active, so OnEnable can fire before
        // Bind() runs. Register() must therefore be idempotent, which ObjectiveManager guarantees.
        public void Bind(GameFlowController flow, ObjectiveManager objectives,
            FuseMiniGameController miniGameController, IFuseViewPresenter presenter)
        {
            gameFlow = flow;
            objectiveManager = objectives;
            miniGame = miniGameController;
            viewPresenter = presenter;
            if (isActiveAndEnabled) objectiveManager.Register(this);
        }

        private void OnEnable()
        {
            if (objectiveManager != null) objectiveManager.Register(this);
        }

        // Fallback for the same reason as SafeZone.Start() — see chat: OnEnable-time registration
        // isn't reliable when a scene is loaded/reopened via an Editor script. Register() is
        // idempotent, so this is a safe no-op if OnEnable already succeeded.
        private void Start()
        {
            if (objectiveManager != null) objectiveManager.Register(this);
        }

        private void OnDisable()
        {
            if (objectiveManager != null) objectiveManager.Unregister(this);
        }

        public void BeginInteract()
        {
            if (!CanInteract) return;
            if (miniGame == null || miniGame.IsActive) return; // prompt section 17: only one fuse interaction at a time

            State = FuseBoxState.Active;
            gameFlow?.ChangeState(gameFlow.FuseInteractionState);
            OnInteractionStarted?.Invoke(transform.position);
            viewPresenter?.Activate();

            miniGame.Begin(roundsRequired, RoundsCompleted, name, viewPresenter?.ScreenAnchor,
                onRoundComplete: HandleRoundComplete,
                onFullyComplete: HandleFullyComplete,
                onCancelled: HandleCancelled);
        }

        /// <summary>
        /// Intentionally a no-op now: PlayerInteraction fires this on E-release (or losing the raycast
        /// target), which happens almost immediately once the minigame UI takes over — the player lets
        /// go of E to reach for the mouse. Cancelling is now exclusively ESC's job (prompt section 15),
        /// handled inside FuseMiniGameController. Kept as a real method (not removed) only to satisfy
        /// IInteractable — see chat: Fuse Memory Minigame implementation.
        /// </summary>
        public void EndInteract() { }

        private void HandleRoundComplete() => RoundsCompleted++;

        private void HandleFullyComplete()
        {
            RoundsCompleted = roundsRequired;
            State = FuseBoxState.Complete;
            OnCompleted?.Invoke();
            StartCoroutine(CompleteRoutine());
        }

        /// <summary>
        /// The success flourish runs between "the fuse is done" and "the player gets the camera back":
        /// OnCompleted has already fired, so objectives/exit/lights react while the lever animates.
        /// </summary>
        private IEnumerator CompleteRoutine()
        {
            if (viewPresenter != null) yield return StartCoroutine(viewPresenter.PlayCompletion());
            viewPresenter?.Deactivate();
            FinishInteraction();
        }

        private void HandleCancelled()
        {
            // RoundsCompleted is untouched here — only HandleRoundComplete increments it, so rounds
            // finished before this cancel are preserved (prompt section 15), only the in-progress round
            // was discarded (inside the minigame itself, before this callback fires).
            State = FuseBoxState.Incomplete;
            viewPresenter?.Deactivate();
            FinishInteraction();
        }

        /// <summary>
        /// Shared "the player is no longer actively working this fuse" side effects. Only a flow still
        /// sitting in FuseInteractionState is pushed back to Exploration. Anything else already moved
        /// on and must not be overwritten: death (found live via Unity MCP testing — the monster
        /// killing the player mid-minigame used to be silently un-killed from the game flow's
        /// perspective) and, on the last fuse, Final Chase (OnCompleted above reaches
        /// ObjectiveManager.OnAllComplete -> MapExitController.Open(), which enters FinalChaseState
        /// before this method runs).
        /// </summary>
        private void FinishInteraction()
        {
            if (gameFlow != null && gameFlow.CurrentStateName == nameof(FuseInteractionState))
            {
                gameFlow.ChangeState(gameFlow.ExplorationState);
            }
            OnInteractionStopped?.Invoke();
        }
    }
}
