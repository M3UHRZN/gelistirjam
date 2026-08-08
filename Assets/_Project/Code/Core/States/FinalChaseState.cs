namespace Game.Core
{
    /// <summary>
    /// All fuses complete, exit is open (TGD section 4, 38). Nothing triggers this yet — the exit
    /// system (Faz 3) will call GameManager.ChangeState(FinalChaseState) once ObjectiveManager
    /// reports completion, and it will also be the hook point for LightDirector's final shutdown
    /// and the monster's raised aggression for the remainder of the run.
    /// </summary>
    public class FinalChaseState : IGameState
    {
        private readonly GameManager manager;

        public FinalChaseState(GameManager manager) => this.manager = manager;

        public void Enter() { }

        public void Tick(float deltaTime) { }

        public void Exit() { }
    }
}
