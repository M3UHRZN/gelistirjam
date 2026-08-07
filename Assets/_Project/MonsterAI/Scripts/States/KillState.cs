namespace Game.MonsterAI
{
    /// <summary>Terminal state: attack lands. A future GameManager should subscribe to MonsterBrain.OnKill (TGD section 12).</summary>
    public class KillState : IMonsterState
    {
        private readonly MonsterBrain brain;

        public KillState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.Stop();
            brain.RaiseKill();
        }

        public void Tick(float deltaTime) { }

        public void Exit() { }
    }
}
