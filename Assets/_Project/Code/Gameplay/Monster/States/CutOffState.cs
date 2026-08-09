using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Paths straight to the player's predicted position instead of their current one (TGD section
    /// 12, 18) — a more direct, threatening variant of Reposition/Flank used when the monster is
    /// already close and doesn't need full stealth to get ahead. Re-predicts periodically rather than
    /// every frame to avoid NavMeshAgent repath churn (same reasoning as AggressiveState's threshold).
    /// </summary>
    public class CutOffState : IMonsterState
    {
        private const float RepredictInterval = 0.4f;

        private readonly MonsterBrain brain;
        private float repredictTimer;

        public CutOffState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.SetAutoRotate(true);
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Config.RepositionSpeed);

            if (!Repredict()) brain.ChangeState(brain.InterestedState);
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

            if (brain.Perception.IsPlayerWithinRadius(brain.Config.CommitDistance))
            {
                brain.ChangeState(brain.CommitState);
                return;
            }

            repredictTimer -= deltaTime;
            if (repredictTimer <= 0f && !Repredict())
            {
                brain.ChangeState(brain.InterestedState);
            }
        }

        public void Exit() { }

        /// <summary>
        /// Targets the predicted position, falling back to the player's actual position when the
        /// prediction lands off-mesh (a player running at a wall extrapolates straight through it).
        /// Both go through TrySetValidatedDestination — an unvalidated predicted point is the classic
        /// way to make the agent grind into geometry (see chat).
        /// </summary>
        private bool Repredict()
        {
            repredictTimer = RepredictInterval;

            Vector3 predicted = brain.Perception.PredictedPlayerPosition(brain.Config.PredictionSeconds);
            if (brain.Navigator.TrySetValidatedDestination(predicted)) return true;

            return brain.Navigator.TrySetValidatedDestination(brain.Player.position);
        }
    }
}
