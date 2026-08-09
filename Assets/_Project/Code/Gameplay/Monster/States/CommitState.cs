using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// The point of no return (TGD section 12, 25 — "Critical Distance"/committed rush): full speed,
    /// no more gaze-freeze interruptions, no more repositioning. Entered from Ambush/CutOff, or
    /// globally by MonsterBrain once the player is inside Critical Distance. Losing the player
    /// entirely reverts to Aggressive (still hostile, just no longer unbreakable) — only actual
    /// contact (handled globally by MonsterBrain) ends this the "intended" way.
    /// </summary>
    public class CommitState : IMonsterState
    {
        private const float RetargetDistanceThreshold = 0.5f;

        private readonly MonsterBrain brain;
        private Vector3 lastTargetedPosition;

        public CommitState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.SetAutoRotate(true);
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Config.CommitSpeed);

            // Deliberately does NOT raise Anger. Commit is reached from proximity (Critical Distance
            // is only 2.5m), so granting anger here pinned the meter to max within the first minute of
            // any run and made anger a function of *distance* rather than of looking — which directly
            // contradicts TGD section 19 ("her başarılı bakış +1") and guts design pillar 2.2,
            // "Information Has a Cost" (see chat). Anger is earned by gaze alone.
            lastTargetedPosition = brain.Player.position;
            // Validated, not raw: same wall-grinding risk as AggressiveState (see its Enter() comment)
            // — Commit is entered at close range where the player's exact footing relative to the
            // NavMesh edge matters even more.
            brain.Navigator.TrySetValidatedDestination(lastTargetedPosition);
        }

        public void Tick(float deltaTime)
        {
            if (!brain.Perception.IsPlayerWithinRadius(brain.Config.CommitLoseRadius))
            {
                brain.ChangeState(brain.IsFinalHunt ? brain.RepositionState : brain.AggressiveState);
                return;
            }

            // Same retarget threshold as AggressiveState — re-pathing every frame makes NavMeshAgent
            // recompute constantly and shows up as visible micro-stutter.
            if (Vector3.Distance(lastTargetedPosition, brain.Player.position) > RetargetDistanceThreshold)
            {
                Vector3 candidate = brain.Player.position;
                if (brain.Navigator.TrySetValidatedDestination(candidate))
                {
                    lastTargetedPosition = candidate;
                }
            }
        }

        public void Exit() { }
    }
}
