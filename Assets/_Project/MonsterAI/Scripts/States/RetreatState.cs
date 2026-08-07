namespace Game.MonsterAI
{
    /// <summary>Withdraw after being Observed until far enough away or hidden again (TGD section 12).</summary>
    public class RetreatState : IMonsterState
    {
        private readonly MonsterBrain brain;

        public RetreatState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Anger.CurrentSpeed * brain.Config.RetreatSpeedMultiplier);

            StalkPoint target = brain.PositionEvaluator.EvaluateRetreatCandidate();
            if (target != null) brain.Navigator.SetDestination(target.Position);
        }

        public void Tick(float deltaTime)
        {
            bool farEnough = brain.Perception.DistanceToPlayer >= brain.Config.RetreatDistance;
            if (brain.Navigator.HasArrived || farEnough)
            {
                brain.ChangeState(brain.HiddenState);
            }
        }

        public void Exit() { }
    }
}
