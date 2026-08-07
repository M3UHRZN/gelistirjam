namespace Game.Core
{
    /// <summary>Normal gameplay (TGD section 4). Player has full control; monster runs its own FSM independently.</summary>
    public class ExplorationState : IGameState
    {
        private readonly GameManager manager;

        public ExplorationState(GameManager manager) => this.manager = manager;

        public void Enter()
        {
            manager.SetPlayerControlEnabled(true);
        }

        public void Tick(float deltaTime) { } // Faz 2 (ObjectiveManager) will drive FuseInteraction from here

        public void Exit() { }
    }
}
