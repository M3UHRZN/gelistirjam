namespace Game.MonsterAI
{
    /// <summary>Class-per-state FSM contract (MASTER_PROMPT.md section 17).</summary>
    public interface IMonsterState
    {
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }
}
