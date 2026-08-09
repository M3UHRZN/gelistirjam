using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Holds concealed at the reached StalkPoint/flank point waiting for the player to close in (TGD
    /// section 12). Not the same as freezing during a stare — the monster isn't being watched here
    /// (Observed handles that interruption). Triggering Commit is a one-way door.
    /// </summary>
    public class AmbushState : IMonsterState
    {
        private const float FaceTurnSpeed = 180f;

        private readonly MonsterBrain brain;
        private float waitTimer;

        public AmbushState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.Stop();
            brain.Navigator.SetAutoRotate(false);
            waitTimer = brain.Config.AmbushMaxDurationSeconds;
        }

        public void Tick(float deltaTime)
        {
            if (brain.IsGazeConfirmed)
            {
                brain.ChangeState(brain.ObservedState);
                return;
            }

            if (!brain.IsFinalHunt
                && !brain.Perception.IsPlayerWithinRadius(brain.Config.StalkAbandonRadius))
            {
                brain.ChangeState(brain.PatrolState);
                return;
            }

            if (brain.Perception.IsPlayerWithinRadius(brain.Config.AmbushTriggerRadius))
            {
                brain.ChangeState(brain.CommitState);
                return;
            }

            // Turn slowly to watch where the player is heading, so it is already looking the right way
            // when they round the corner. Slower than the staredown turn rate — this is lurking, not a
            // snap-to-target.
            brain.Navigator.FaceTowards(
                brain.Perception.PredictedPlayerPosition(brain.Config.PredictionSeconds),
                FaceTurnSpeed, deltaTime);

            // An ambush that is never sprung would otherwise stand still indefinitely: the player can
            // sit anywhere between AmbushTriggerRadius and StalkAbandonRadius forever without the
            // monster ever re-planning. Time it out and go set up somewhere better.
            waitTimer -= deltaTime;
            if (waitTimer <= 0f) brain.ChangeState(brain.RepositionState);
        }

        public void Exit() { }
    }
}
