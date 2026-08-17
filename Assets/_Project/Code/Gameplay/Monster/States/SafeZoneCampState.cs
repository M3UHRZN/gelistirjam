using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// The player is sheltering inside a SafeZone (TGD section 30). The monster cannot follow them in
    /// and cannot harm them there, but "Safe zone monster AI'ı tamamen devre dışı bırakmamalıdır" — so
    /// rather than losing interest and wandering off, it waits in the dark just outside, drifting
    /// between waiting spots so it isn't parked at a single predictable exit, and pounces the moment
    /// the player steps out. Patience is finite: it eventually gives up so a player isn't hard-locked.
    /// </summary>
    public class SafeZoneCampState : IMonsterState
    {
        private const float FaceTurnSpeed = 180f;

        private readonly MonsterBrain brain;
        private float campTimer;
        private float repositionTimer;

        public SafeZoneCampState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.SetAutoRotate(false);
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Config.InterestedSpeed);
            campTimer = brain.Config.SafeZoneCampDuration;
            repositionTimer = 0f;
        }

        public void Tick(float deltaTime)
        {
            // The player left the zone — resume the hunt immediately. This is the whole point of
            // camping, so it is checked before anything that could exit the state.
            if (!brain.IsPlayerInSafeZone)
            {
                brain.ChangeState(brain.InterestedState);
                return;
            }

            if (!brain.IsFinalHunt
                && !brain.Perception.IsPlayerWithinRadius(brain.Config.StalkAbandonRadius))
            {
                brain.ChangeState(brain.PatrolState);
                return;
            }

            brain.Navigator.FaceTowards(brain.Player.position, FaceTurnSpeed, deltaTime);

            repositionTimer -= deltaTime;
            if (repositionTimer <= 0f)
            {
                repositionTimer = brain.Config.SafeZoneCampRepositionInterval;
                PickWaitingSpot();
            }

            campTimer -= deltaTime;
            if (campTimer <= 0f)
            {
                brain.ChangeState(brain.IsFinalHunt ? brain.RepositionState : brain.PatrolState);
            }
        }

        public void Exit() { }

        /// <summary>
        /// Picks a reachable spot ringing the player's shelter at SafeZoneCampDistance. The navigator
        /// rejects any candidate that lands inside a SafeZone on its own, so this cannot accidentally
        /// place the monster in the zone it is besieging.
        /// </summary>
        private void PickWaitingSpot()
        {
            Vector3 center = brain.Player.position;
            float distance = brain.Config.SafeZoneCampDistance;
            float startAngle = Random.Range(0f, 360f);

            for (int i = 0; i < 8; i++)
            {
                float angle = startAngle + i * 45f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                if (brain.Navigator.TrySetValidatedDestination(center + offset)) return;
            }

            // Nowhere valid to wait (fully enclosed shelter) — hold position rather than thrash.
            brain.Navigator.Stop();
        }
    }
}
