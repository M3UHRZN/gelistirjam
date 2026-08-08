using UnityEngine;

namespace Game.Core
{
    /// <summary>Terminal state: the monster caught the player (TGD section 4, 12). Placeholder for a real death sequence/UI.</summary>
    public class DeathState : IGameState
    {
        private readonly GameManager manager;

        public DeathState(GameManager manager) => this.manager = manager;

        public void Enter()
        {
            manager.SetPlayerControlEnabled(false);
            Debug.Log("[GameManager] Death — placeholder for a real death sequence/UI.");
        }

        public void Tick(float deltaTime) { }

        public void Exit() { }
    }
}
