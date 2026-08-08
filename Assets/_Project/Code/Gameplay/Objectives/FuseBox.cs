using System;
using Game.Core;
using Game.Gameplay.Interaction;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Gameplay.Objectives
{
    public enum FuseBoxState { Incomplete, Active, Complete }

    /// <summary>
    /// A single fuse objective (TGD section 33-36). Hold-to-complete is a placeholder for the real
    /// cable-matching minigame described in the TGD — flagged explicitly, not a silent simplification.
    /// Releasing early cancels and resets progress to 0 (TGD section 35: "Cancel → progress reset").
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FuseBox : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("Placeholder minigame duration (TGD section 34: real minigame targets 5-8s).")]
        private float holdDurationSeconds = 6f;
        [SerializeField, FormerlySerializedAs("gameManager")] private GameFlowController gameFlow;
        [SerializeField] private ObjectiveManager objectiveManager;

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
        public float HoldProgress01 => Mathf.Clamp01(holdProgress / holdDurationSeconds);

        private float holdProgress;

        // Registers immediately in addition to OnEnable, same defensive pattern as SafeZone/StalkPoint:
        // the editor scene builder creates this GameObject already active, so OnEnable can fire before
        // Bind() runs. Register() must therefore be idempotent, which ObjectiveManager guarantees.
        public void Bind(GameFlowController flow, ObjectiveManager objectives)
        {
            gameFlow = flow;
            objectiveManager = objectives;
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
            State = FuseBoxState.Active;
            holdProgress = 0f;
            gameFlow?.ChangeState(gameFlow.FuseInteractionState);
            OnInteractionStarted?.Invoke(transform.position);
        }

        public void EndInteract()
        {
            if (State != FuseBoxState.Active) return;

            if (holdProgress >= holdDurationSeconds)
            {
                Complete();
            }
            else
            {
                State = FuseBoxState.Incomplete; // cancelled — progress reset (TGD section 35)
                holdProgress = 0f;
            }

            FinishInteraction();
        }

        private void Update()
        {
            if (State != FuseBoxState.Active) return;

            holdProgress += Time.deltaTime;
            if (holdProgress >= holdDurationSeconds)
            {
                // Auto-complete: the player is still physically holding E past the exact frame the
                // minigame finishes, so this fires before EndInteract() ever will. Previously this
                // duplicated only *half* of EndInteract()'s cleanup (Complete() + return-to-Exploration)
                // and skipped FinishInteraction()'s OnInteractionStopped — by the time the player later
                // released E, State was already Complete, so EndInteract()'s own guard short-circuited
                // and OnInteractionStopped never fired at all. That event is the only thing that clears
                // MonsterBrain.IsAwareOfFuse (via ObjectiveManager), so completing a single fuse the
                // normal way (holding to completion, not releasing early) left the monster "aware of a
                // fuse" — and passively gaining anger from it (TGD section 36) — for the rest of the
                // run. Found via careful code audit, not runtime reproduction (see chat).
                Complete();
                FinishInteraction();
            }
        }

        private void Complete()
        {
            State = FuseBoxState.Complete;
            holdProgress = holdDurationSeconds;
            OnCompleted?.Invoke();
        }

        /// <summary>Shared "the player is no longer actively working this fuse" side effects — same call in both the normal-release and auto-complete paths, so they can't drift out of sync again.</summary>
        private void FinishInteraction()
        {
            gameFlow?.ChangeState(gameFlow.ExplorationState);
            OnInteractionStopped?.Invoke();
        }
    }
}
