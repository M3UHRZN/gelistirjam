using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Default idle state: wanders randomly around its spawn anchor, making no sound, until a
    /// player comes within DetectionRadius (Bracken tutorial part 1 — "patrolling"). While the player
    /// is off working a fuse (TGD section 36), this doubles as the "seek approach position" behavior
    /// instead of a dedicated state — see PickNextDestination.
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
            PickNextDestination();
        }

        public void Tick(float deltaTime)
        {
            if (brain.IsFinalHunt)
            {
                brain.ChangeState(brain.RepositionState);
                return;
            }

            if (brain.Perception.IsPlayerWithinRadius(brain.Config.DetectionRadius))
            {
                brain.ChangeState(brain.InterestedState);
                return;
            }

            if (brain.Navigator.HasArrived) PickNextDestination();
        }

        public void Exit() { }

        private void PickNextDestination()
        {
            if (brain.IsAwareOfFuse)
            {
                if (TrySeekFuseApproachPosition()) return;

                // No StalkPoints bound/reachable near the fuse — fall back to random sampling around
                // the fuse position rather than abandoning the approach entirely and wandering off to
                // the patrol anchor. Weaker than a scored position but still "closing in", same
                // graceful-degradation guarantee as every other StalkPoint consumer in this project.
                brain.Navigator.SetRandomDestinationAround(brain.FusePosition, brain.Config.FuseApproachRadius);
                return;
            }

            // Plain patrol: no fuse in progress — random NavMesh sampling is correct here, nothing in
            // the TGD asks patrol itself to be scored (unlike the fuse-approach case above).
            brain.Navigator.SetRandomDestinationAround(brain.PatrolAnchor, brain.Config.PatrolRadius);
        }

        /// <summary>
        /// TGD section 36: "doğrudan player'a koşmak yerine yüksek score'lu approach position arar" —
        /// while the player works a fuse, the monster must seek a *scored* concealed position near
        /// them (dark, cornered, unseen), not just wander to a random point within a radius of the
        /// fuse box. Player is treated as stationary (they're locked into the minigame), so their
        /// current position doubles as the "predicted" one. Falls back to the caller's plain random
        /// sampling when no StalkPoints are bound/reachable — same graceful-degradation guarantee as
        /// every other StalkPoint consumer in this project (see chat).
        /// </summary>
        private bool TrySeekFuseApproachPosition()
        {
            if (brain.StalkPoints == null || brain.StalkPoints.Items.Count == 0) return false;

            StalkPoint best = StalkPointScorer.PickBest(
                brain.StalkPoints,
                brain.Player.position,
                brain.Player.position,
                brain.Perception.PlayerFacingDirection,
                brain.transform.position,
                brain.VisionModel,
                brain.Navigator,
                brain.Tuning.VisibilityRiskTolerance);

            if (best == null) return false;

            return brain.Navigator.TrySetValidatedDestination(best.Position);
        }
    }
}
