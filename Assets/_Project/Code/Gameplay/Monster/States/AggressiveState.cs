using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Relentless chase (tutorial's "aggressive" / wiki's "enraged"). Chases the player directly —
    /// this is the one state where the monster goes straight for its target, no stealth. Anger
    /// drains 1/sec while chasing; reaching 0 reverts to Shy (the only way anger resets — wiki).
    /// Losing the player entirely also reverts to Shy, not Patrol — it just made an attempt and
    /// failed, it doesn't calmly go back to idle wandering.
    /// </summary>
    public class AggressiveState : IMonsterState
    {
        private const float RetargetDistanceThreshold = 0.5f;

        private readonly MonsterBrain brain;
        private float angerDecayAccumulator;
        private Vector3 lastTargetedPosition;

        public AggressiveState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            if (brain.IsFinalHunt)
            {
                brain.ChangeState(brain.RepositionState);
                return;
            }

            brain.Navigator.SetAutoRotate(true);
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Config.AggressiveSpeed);
            angerDecayAccumulator = 0f;
            lastTargetedPosition = brain.Player.position;
            // Validated, not raw SetDestination: this is the one state that goes straight at the
            // player, so it's also the most likely to be asked to path to a point that has drifted
            // just off the NavMesh (player hugging a wall/ledge). An unvalidated destination there
            // makes the agent grind against geometry instead of just waiting for the next retarget
            // (see chat — same wall-hugging class of bug already fixed for Flank/Reposition/CutOff,
            // but missed here since this is the one state that never went through StalkPointScorer).
            brain.Navigator.TrySetValidatedDestination(lastTargetedPosition);
        }

        public void Tick(float deltaTime)
        {
            angerDecayAccumulator += brain.Config.AggressiveAngerDecayPerSecond * deltaTime;
            while (angerDecayAccumulator >= 1f)
            {
                brain.Anger.AddAnger(-1);
                angerDecayAccumulator -= 1f;
            }

            if (brain.Anger.Anger <= 0)
            {
                brain.ChangeState(brain.ShyState);
                return;
            }

            if (!brain.Perception.IsPlayerWithinRadius(brain.Config.LoseAggroRadius))
            {
                brain.ChangeState(brain.ShyState);
                return;
            }

            // Re-pathing every single frame causes visible micro-stutter as NavMeshAgent recomputes
            // constantly; only retarget once the player has actually moved a meaningful distance.
            if (Vector3.Distance(lastTargetedPosition, brain.Player.position) > RetargetDistanceThreshold)
            {
                Vector3 candidate = brain.Player.position;
                // Only commit to the new target if it actually validates. On failure keep chasing the
                // last good target instead of snapping onto an invalid point (which is what produced
                // the wall-grinding — see Enter()'s comment).
                if (brain.Navigator.TrySetValidatedDestination(candidate))
                {
                    lastTargetedPosition = candidate;
                }
            }
        }

        public void Exit() { }
    }
}
