namespace Game.Core
{
    /// <summary>Owns the current game-flow state and drives Enter/Exit transitions.</summary>
    public class GameStateMachine
    {
        public IGameState Current { get; private set; }

        public void SetState(IGameState next)
        {
            if (next == Current) return;
            Current?.Exit();
            Current = next;
            Current.Enter();
        }

        public void Tick(float deltaTime) => Current?.Tick(deltaTime);
    }
}
