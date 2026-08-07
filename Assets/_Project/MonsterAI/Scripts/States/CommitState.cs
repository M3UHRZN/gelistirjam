namespace Game.MonsterAI
{
    /// <summary>
    /// Player got too close — gaze no longer works, the monster goes straight for the kill (TGD sections 12, 25).
    /// </summary>
    public class CommitState : IMonsterState
    {
        private readonly MonsterBrain brain;

        public CommitState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Anger.CommitSpeed);
        }

        public void Tick(float deltaTime)
        {
            if (brain.Player == null) return;

            brain.Navigator.SetDestination(brain.Player.position);
            if (brain.Perception.DistanceToPlayer <= brain.Config.AttackRange)
            {
                brain.ChangeState(brain.KillState);
            }
        }

        public void Exit() { }
    }
}
