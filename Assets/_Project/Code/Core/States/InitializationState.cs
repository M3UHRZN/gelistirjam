namespace Game.Core
{
    /// <summary>Momentary entry state (TGD section 4) — hands off to Exploration on the first tick.</summary>
    public class InitializationState : IGameState
    {
        private readonly GameManager manager;

        public InitializationState(GameManager manager) => this.manager = manager;

        public void Enter() { }

        public void Tick(float deltaTime) => manager.ChangeState(manager.ExplorationState);

        public void Exit() { }
    }
}
