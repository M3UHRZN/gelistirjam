using System;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>Opens once all fuses are complete (TGD section 37) and switches the game into FinalChase.</summary>
    [RequireComponent(typeof(Collider))]
    public class ExitController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        public event Action OnExitOpened;
        public bool IsOpen { get; private set; }

        public void Bind(GameManager manager) => gameManager = manager;

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            OnExitOpened?.Invoke();
            gameManager?.ChangeState(gameManager.FinalChaseState);
        }

        // Identifies the player by CharacterController rather than a tag — no tag scheme exists yet
        // in this test rig, and TestPlayerController is the only CharacterController in the scene.
        private void OnTriggerEnter(Collider other)
        {
            if (!IsOpen) return;
            if (other.GetComponentInParent<CharacterController>() == null) return;
            gameManager?.ChangeState(gameManager.VictoryState);
        }
    }
}
