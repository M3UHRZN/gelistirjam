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
        }

        public void Tick(float deltaTime)
        {
            brain.Navigator.FaceTowards(brain.Player.position, FaceTurnSpeed, deltaTime);

            if (brain.IsGazeConfirmed) return; // still being stared at — hold the freeze

            float aggroChance = brain.ComputeAggressionProbability(followedStaredown: true);
            bool aggro = Random.value < aggroChance;
            brain.SetDecisionReason(aggro
                ? $"Aggressive: caught mid-stalk, aggro roll succeeded ({aggroChance:P0} chance)"
                : $"Shy: caught mid-stalk, aggro roll failed ({aggroChance:P0} chance)");
            brain.ChangeState(aggro ? brain.AggressiveState : brain.ShyState);
        }

        public void Exit() { }
    }
}
