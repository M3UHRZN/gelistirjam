using UnityEngine;

namespace Game.Core
{
    /// <summary>Terminal state: the player escaped (TGD section 4). Placeholder for a real victory sequence/UI.</summary>
    public class VictoryState : IGameState
    {
        private readonly GameFlowController flow;

        public VictoryState(GameFlowController flow) => this.flow = flow;

        public void Enter()
        {
            flow.SetPlayerControlEnabled(false);
            Debug.Log("[GameFlow] Victory — placeholder for a real victory sequence/UI.");
        }

        public void Tick(float deltaTime) { }

        public void Exit() { }
    }
}
