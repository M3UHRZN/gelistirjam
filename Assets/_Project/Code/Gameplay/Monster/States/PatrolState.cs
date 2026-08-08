using UnityEngine;

namespace Game.Gameplay.Monster
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
                // TGD section 36: while the player is off working a fuse, patrol drifts around that
                // fuse box instead of the spawn anchor — a passive "closing in" rather than obliviously
                // wandering somewhere else while the objective is actively in progress.
                Vector3 anchor = brain.IsAwareOfFuse ? brain.FusePosition : brain.PatrolAnchor;
                float radius = brain.IsAwareOfFuse ? brain.Config.FuseApproachRadius : brain.Config.PatrolRadius;
                brain.Navigator.SetRandomDestinationAround(anchor, radius);
            }
        }

        public void Exit() { }
    }
}
