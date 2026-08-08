namespace Game.Gameplay.Monster
{
    /// <summary>Owns the current state and drives Enter/Exit transitions. Transition rules live in the states/brain, not here.</summary>
    public class MonsterStateMachine
    {
        public IMonsterState Current { get; private set; }

        public void SetState(IMonsterState next)
        {
            if (next == Current) return;
            Current?.Exit();
            Current = next;
            Current.Enter();
        }

        public void Tick(float deltaTime) => Current?.Tick(deltaTime);
    }
}
