namespace Game.MonsterAI
{
    /// <summary>
    /// Player made eye contact: freeze and stare back for as long as they keep looking — Bracken-
    /// inspired "stand and stare" (see chat). Breaking eye contact sends it into Retreat. Staring
    /// continuously past MonsterConfig.GazeStareEscalationDuration also forces a Retreat, but with an
    /// extra Anger point for pushing it that far — every successful/prolonged contact makes it
    /// permanently more dangerous (TGD section 2.2, 19), it never calms down mid-encounter.
    /// </summary>
    public class ObservedState : IMonsterState
    {
        private readonly MonsterBrain brain;
        private float continuousStareSeconds;

        public ObservedState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            continuousStareSeconds = 0f;
            brain.Navigator.Stop();
            // Hook point for scream/recoil animation and audio once art exists (TGD section 28).
        }

        public void Tick(float deltaTime)
        {
            if (!brain.IsGazedAtNow)
            {
                brain.ChangeState(brain.RetreatState); // player looked away — it bolts
                return;
            }

            continuousStareSeconds += deltaTime;
            if (continuousStareSeconds >= brain.Config.GazeStareEscalationDuration)
            {
                brain.Anger.AddAnger(1); // stared down too long — it gets bolder, not calmer
                brain.ChangeState(brain.RetreatState);
            }
        }

        public void Exit() { }
    }
}
