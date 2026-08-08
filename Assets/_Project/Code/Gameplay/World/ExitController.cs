using System;
using Game.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Opens once all fuses are complete (TGD section 37) and switches the game into FinalChase.
    /// Belongs to the hand-built monster AI test scene, where the exit sits at a hardcoded position.
    /// A generated map uses MapExitController instead, which builds its exit from Anchor_Exit.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ExitController : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("gameManager")] private GameFlowController gameFlow;

        public event Action OnExitOpened;
        public bool IsOpen { get; private set; }

        public void Bind(GameFlowController flow) => gameFlow = flow;

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            OnExitOpened?.Invoke();
            gameFlow?.ChangeState(gameFlow.FinalChaseState);
        }

        // Identifies the player by CharacterController rather than a tag — no tag scheme exists yet
        // in this test rig, and TestPlayerController is the only CharacterController in the scene.
        private void OnTriggerEnter(Collider other)
        {
            if (!IsOpen) return;
            if (other.GetComponentInParent<CharacterController>() == null) return;
            gameFlow?.ChangeState(gameFlow.VictoryState);
        }
    }
}
