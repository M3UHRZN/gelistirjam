using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Being watched while attempting a stealthy approach (Reposition/Flank/Ambush/CutOff) is a
    /// distinct reaction from Interested's own gaze-freeze — the plan is blown, not just paused (TGD
    /// section 12, 28). Freezes, stares back accumulating anger like the other gaze-freeze states,
    /// then on gaze break rolls the same aggro-chance used everywhere else: attack now, or peel off
    /// into Shy to reset and try again later.
    /// </summary>
    public class ObservedState : IMonsterState
    {
        private const float FaceTurnSpeed = 360f;

        private readonly MonsterBrain brain;

        public ObservedState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.Stop();
            brain.Navigator.SetAutoRotate(false);
            brain.Anger.AddAnger(brain.Config.AngerPerSpotted); // being caught mid-stalk is worse than a passive glance
        }

        public void Tick(float deltaTime)
        {
            brain.Navigator.FaceTowards(brain.Player.position, FaceTurnSpeed, deltaTime);

            if (brain.IsGazeConfirmed) return; // still being stared at — hold the freeze

            bool aggro = Random.value < brain.Anger.AggroChance;
            brain.ChangeState(aggro ? brain.AggressiveState : brain.ShyState);
        }

        public void Exit() { }
    }
}
