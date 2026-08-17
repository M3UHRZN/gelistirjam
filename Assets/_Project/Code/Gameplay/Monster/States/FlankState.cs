using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Moves to a point lateral to the player's current facing direction — never crossing directly in
    /// front of them — to get ahead without walking into their FOV (TGD section 12, 17). Once there,
    /// hands off to Ambush to wait for the moment to strike.
    /// </summary>
    public class FlankState : IMonsterState
    {
        // Tried in order: straight out to the monster's own side first, then swept progressively
        // backwards, with the opposite side as a last resort. A single geometric offset lands inside a
        // wall most of the time in a corridor maze, so this sweeps for one that is actually reachable.
        private static readonly float[] SweepAngles = { 0f, -35f, 35f, -70f, 70f, 180f };

        private readonly MonsterBrain brain;

        public FlankState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.SetAutoRotate(true);
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Config.RepositionSpeed);

            if (!TrySetFlankDestination()) brain.ChangeState(brain.InterestedState);
        }

        public void Tick(float deltaTime)
        {
            if (brain.IsGazeConfirmed)
            {
                brain.ChangeState(brain.ObservedState);
                return;
            }

            if (!brain.Perception.IsPlayerWithinRadius(brain.Config.StalkAbandonRadius))
            {
                brain.ChangeState(brain.PatrolState);
                return;
            }

            if (brain.Navigator.HasArrived)
            {
                brain.ChangeState(brain.AmbushState);
            }
        }

        public void Exit() { }

        /// <summary>
        /// Sweeps candidate lateral offsets until one lands on reachable NavMesh outside a SafeZone.
        /// Every candidate goes through TrySetValidatedDestination: a raw SetDestination on a purely
        /// geometric point makes the agent walk as far as it can and grind against a wall — the exact
        /// wall-hugging behaviour already fixed once for Patrol/Interested/Shy (see chat).
        /// </summary>
        private bool TrySetFlankDestination()
        {
            Vector3 facing = brain.Perception.PlayerFacingDirection;
            Vector3 right = Vector3.Cross(Vector3.up, facing);

            // Pick whichever side the monster is already on so it never crosses the player's path.
            Vector3 toMonster = brain.transform.position - brain.Player.position;
            toMonster.y = 0f;
            float side = Vector3.Dot(toMonster, right) >= 0f ? 1f : -1f;
            Vector3 baseDirection = right * side;

            float distance = brain.Config.FlankOffsetDistance;

            for (int i = 0; i < SweepAngles.Length; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, SweepAngles[i], 0f) * baseDirection;
                Vector3 candidate = brain.Player.position + direction * distance;

                if (brain.Navigator.TrySetValidatedDestination(candidate)) return true;
            }

            return false;
        }
    }
}
