using UnityEngine;

namespace Game.Core
{
    /// <summary>Terminal state: the monster caught the player (TGD section 4, 12). Placeholder for a real death sequence/UI.</summary>
    public class DeathState : IGameState
    {
        private readonly GameFlowController flow;

        public DeathState(GameFlowController flow) => this.flow = flow;

        public void Enter()
        {
            flow.SetPlayerControlEnabled(false);
            Debug.Log("[GameFlow] Death — placeholder for a real death sequence/UI.");
        }

        public void Tick(float deltaTime) { }

        public void Exit() { }
    }
}
