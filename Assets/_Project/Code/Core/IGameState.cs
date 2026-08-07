namespace Game.Core
{
    /// <summary>Class-per-state FSM contract for the game's own flow (MASTER_PROMPT.md section 17).</summary>
    public interface IGameState
    {
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }
}
