namespace Game.MonsterAI
{
    /// <summary>
    /// Default idle state: wanders randomly around its spawn anchor, making no sound, until a
    /// player comes within DetectionRadius (Bracken tutorial part 1 — "patrolling").
    /// </summary>
    public class PatrolState : IMonsterState
    {
        private readonly MonsterBrain brain;

        public PatrolState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.SetAutoRotate(true);
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Config.PatrolSpeed);
            brain.Navigator.SetRandomDestinationAround(brain.PatrolAnchor, brain.Config.PatrolRadius);
        }

        public void Tick(float deltaTime)
        {
            if (brain.Perception.IsPlayerWithinRadius(brain.Config.DetectionRadius))
            {
                brain.ChangeState(brain.InterestedState);
                return;
            }

            if (brain.Navigator.HasArrived)
            {
                brain.Navigator.SetRandomDestinationAround(brain.PatrolAnchor, brain.Config.PatrolRadius);
            }
        }

        public void Exit() { }
    }
}
