namespace Game.MonsterAI
{
    /// <summary>Idle/out-of-play state used after Retreat completes or before the first Stalk (TGD section 12).</summary>
    public class HiddenState : IMonsterState
    {
        private const float Duration = 1.5f;

        private readonly MonsterBrain brain;
        private float timer;

        public HiddenState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            timer = 0f;
            brain.Navigator.Stop();
        }

        public void Tick(float deltaTime)
        {
            timer += deltaTime;
            if (timer >= Duration) brain.ChangeState(brain.StalkState);
        }

        public void Exit() { }
    }
}
