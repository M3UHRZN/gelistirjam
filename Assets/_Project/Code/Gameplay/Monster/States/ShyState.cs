using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Spotted and fleeing (tutorial's "shy"). Runs to the furthest reachable point away from the
    /// player. While fleeing, being looked at again freezes it into a staredown (wiki: "Brackens
    /// will stop retreating and enter a stare down... allowing anger to fill") which also keeps its
    /// flee timer pinned at max ("resets back to full if employees maintain eye contact"). When the
    /// timer runs out unlooked, it returns to Patrol. A staredown that ends can still roll into
    /// Aggressive, same as Interested.
    /// </summary>
    public class ShyState : IMonsterState
    {
        private const float FaceTurnSpeed = 360f;

        private readonly MonsterBrain brain;
        private float fleeTimer;
        private bool wasGazedLastFrame;
        private float staredownTimer;
        private float staredownCheckTimer;
        private bool isStaredown;

        public ShyState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            // A retreating Bracken runs backwards: it keeps its eyes on the closest player while
            // the NavMeshAgent follows its escape path.
            brain.Navigator.SetAutoRotate(false);
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Config.ShySpeed);
            brain.Navigator.SetFleeDestinationAwayFrom(brain.Player.position, brain.Config.ShyFleeRadius);
            fleeTimer = brain.Config.ShyFleeDuration;
            wasGazedLastFrame = false;
            staredownTimer = 0f;
            staredownCheckTimer = 0f;
            isStaredown = false;
        }

        public void Tick(float deltaTime)
        {
            bool gazedNow = brain.IsGazedAtNow;
            brain.Navigator.FaceTowards(brain.Player.position, FaceTurnSpeed, deltaTime);

            if (gazedNow)
            {
                fleeTimer = brain.Config.ShyFleeDuration; // eye contact resets phase-2's timer

                if (!isStaredown)
                {
                    staredownCheckTimer -= deltaTime;
                    if (staredownCheckTimer <= 0f)
                    {
                        staredownCheckTimer = brain.Config.StaredownCheckInterval;
                        if (Random.value < brain.Config.StaredownChance)
                        {
                            isStaredown = true;
                            staredownTimer = 0f;
                            brain.Navigator.Stop();
                        }
                    }
                }

                if (isStaredown)
                {
                    staredownTimer += deltaTime;
                    if (staredownTimer >= brain.Config.StaredownAngerInterval)
                    {
                        brain.Anger.AddAnger(1);
                        staredownTimer = 0f;
                    }
                }
            }
            else
            {
                if (wasGazedLastFrame)
                {
                    // Only an actual stopped staredown can fuel the anger roll. Merely watching a
                    // retreating monster keeps it away; it should not turn every glance into a chase.
                    if (isStaredown && Random.value < brain.Anger.AggroChance)
                    {
                        brain.ChangeState(brain.AggressiveState);
                        return;
                    }

                    brain.Navigator.Resume();
                    brain.Navigator.SetFleeDestinationAwayFrom(brain.Player.position, brain.Config.ShyFleeRadius);
                    isStaredown = false;
                    staredownCheckTimer = 0f;
                }
                else if (brain.Navigator.HasArrived)
                {
                    brain.Navigator.Stop(); // reached its hiding spot — wait here, still "in phase 2"
                }

                fleeTimer -= deltaTime;
                if (fleeTimer <= 0f)
                {
                    brain.ChangeState(brain.PatrolState);
                    return;
                }
            }

            wasGazedLastFrame = gazedNow;
        }

        public void Exit() { }
    }
}
