using UnityEngine;

namespace Game.MonsterAI
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
            brain.Navigator.SetAutoRotate(true);
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Config.AggressiveSpeed);
            angerDecayAccumulator = 0f;
            lastTargetedPosition = brain.Player.position;
            brain.Navigator.SetDestination(lastTargetedPosition);
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
                lastTargetedPosition = brain.Player.position;
                brain.Navigator.SetDestination(lastTargetedPosition);
            }
        }

        public void Exit() { }
    }
}
