namespace Game.Core
{
    /// <summary>Normal gameplay (TGD section 4). Player has full control; monster runs its own FSM independently.</summary>
    public class ExplorationState : IGameState
    {
        private readonly GameFlowController flow;

        public ExplorationState(GameFlowController flow) => this.flow = flow;

        public void Enter()
        {
            flow.SetPlayerControlEnabled(true);
        }

        public void Tick(float deltaTime) { } // Faz 2 (ObjectiveManager) will drive FuseInteraction from here

        public void Exit() { }
    }
}
