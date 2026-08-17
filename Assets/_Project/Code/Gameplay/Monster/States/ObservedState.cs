namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Compatibility handoff for older stealth-state transitions. The Brain now intercepts raw gaze
    /// before those states Tick, but any direct/legacy entry still routes deterministically to Shy and
    /// can never freeze or roll into aggression.
    /// </summary>
    public class ObservedState : IMonsterState
    {
        private readonly MonsterBrain brain;

        public ObservedState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.Resume();
        }

        public void Tick(float deltaTime)
        {
            brain.SetDecisionReason("Shy: observed during stalking, retreat without confrontation");
            brain.ChangeState(brain.ShyState);
        }

        public void Exit() { }
    }
}
