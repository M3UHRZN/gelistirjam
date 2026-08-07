namespace Game.MonsterAI
{
    /// <summary>Core state: approach the player while staying out of the vision cone (TGD sections 12, 16-17).</summary>
    public class StalkState : IMonsterState
    {
        private readonly MonsterBrain brain;
        private float reevaluateTimer;

        public StalkState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Anger.CurrentSpeed);
            PickNewTarget();
        }

        public void Tick(float deltaTime)
        {
            brain.Navigator.SetSpeed(brain.Anger.CurrentSpeed); // anger can rise mid-state

            reevaluateTimer -= deltaTime;
            if (reevaluateTimer <= 0f || brain.Navigator.HasArrived)
            {
                PickNewTarget();
            }
        }

        public void Exit() { }

        private void PickNewTarget()
        {
            StalkPoint target = brain.PositionEvaluator.EvaluateBestCandidate();
            if (target != null) brain.Navigator.SetDestination(target.Position);
            reevaluateTimer = brain.Config.CandidateReevaluateInterval;
        }
    }
}
