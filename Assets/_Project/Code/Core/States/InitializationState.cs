namespace Game.Core
{
    /// <summary>Momentary entry state (TGD section 4) — hands off to Exploration on the first tick.</summary>
    public class InitializationState : IGameState
    {
        private readonly GameFlowController flow;

        public InitializationState(GameFlowController flow) => this.flow = flow;

        public void Enter() { }

        public void Tick(float deltaTime) => flow.ChangeState(flow.ExplorationState);

        public void Exit() { }
    }
}
