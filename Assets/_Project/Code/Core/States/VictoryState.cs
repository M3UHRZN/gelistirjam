using UnityEngine;

namespace Game.Core
{
    /// <summary>Terminal state: the player escaped (TGD section 4). Placeholder for a real victory sequence/UI.</summary>
    public class VictoryState : IGameState
    {
        private readonly GameManager manager;

        public VictoryState(GameManager manager) => this.manager = manager;

        public void Enter()
        {
            manager.SetPlayerControlEnabled(false);
            Debug.Log("[GameManager] Victory — placeholder for a real victory sequence/UI.");
        }

        public void Tick(float deltaTime) { }

        public void Exit() { }
    }
}
