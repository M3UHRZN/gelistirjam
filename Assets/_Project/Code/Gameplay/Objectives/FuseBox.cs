using System;
using Game.Core;
using Game.Gameplay.Interaction;
using UnityEngine;

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
        [SerializeField] private GameManager gameManager;
        [SerializeField] private ObjectiveManager objectiveManager;

        public FuseBoxState State { get; private set; } = FuseBoxState.Incomplete;
        public event Action OnCompleted;

        public bool CanInteract => State == FuseBoxState.Incomplete;
        public string InteractionPrompt => "Hold E to activate fuse";
        public float HoldProgress01 => Mathf.Clamp01(holdProgress / holdDurationSeconds);

        private float holdProgress;

        // Registers immediately in addition to OnEnable, same defensive pattern as SafeZone/StalkPoint:
        // the editor scene builder creates this GameObject already active, so OnEnable can fire before
        // Bind() runs. Register() must therefore be idempotent, which ObjectiveManager guarantees.
        public void Bind(GameManager manager, ObjectiveManager objectives)
        {
            gameManager = manager;
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
            gameManager?.ChangeState(gameManager.FuseInteractionState);
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

            gameManager?.ChangeState(gameManager.ExplorationState);
        }

        private void Update()
        {
            if (State != FuseBoxState.Active) return;

            holdProgress += Time.deltaTime;
            if (holdProgress >= holdDurationSeconds)
            {
                Complete();
                gameManager?.ChangeState(gameManager.ExplorationState);
            }
        }

        private void Complete()
        {
            State = FuseBoxState.Complete;
            holdProgress = holdDurationSeconds;
            OnCompleted?.Invoke();
        }
    }
}
