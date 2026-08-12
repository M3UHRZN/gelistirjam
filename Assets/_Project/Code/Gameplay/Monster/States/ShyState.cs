using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Spotted and fleeing (tutorial's "shy"). Runs to a reachable corner outside the player's
    /// unobstructed camera view. While fleeing, being looked at again can freeze it into a staredown (wiki: "Brackens
    /// will stop retreating and enter a stare down... allowing anger to fill") which also keeps its
    /// flee timer pinned at max ("resets back to full if employees maintain eye contact"). When the
    /// timer runs out unlooked, it returns to Patrol. A staredown that ends can still roll into
    /// Aggressive, same as Interested.
    /// </summary>
    public class ShyState : IMonsterState
    {
        private const float FaceTurnSpeed = 360f;

        private readonly MonsterBrain brain;
        private readonly MonsterStaredownController staredown;
        private float fleeTimer;
        private bool wasGazedLastFrame;
        private bool triedFakeRetreat;
        private StalkPoint currentTarget;
        private StalkPoint exposedTarget;
        private bool relocationPending;
        private float relocationRetryTimer;

        public StalkPoint CurrentTarget => currentTarget;

        public ShyState(MonsterBrain brain)
        {
            this.brain = brain;
            staredown = new MonsterStaredownController(brain);
        }

        public void Enter()
        {
            // A retreating Bracken runs backwards: it keeps its eyes on the closest player while
            // the NavMeshAgent follows its escape path.
            brain.Navigator.SetAutoRotate(false);
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Config.ShySpeed);
            currentTarget = null;
            exposedTarget = null;
            relocationPending = false;
            relocationRetryTimer = 0f;
            TrySelectHiddenCorner(excluded: null);
            fleeTimer = brain.Config.ShyFleeDuration;
            wasGazedLastFrame = false;
            staredown.Reset();
            triedFakeRetreat = false;
        }

        public void Tick(float deltaTime)
        {
            bool gazedNow = brain.IsGazeConfirmed;
            brain.Navigator.FaceTowards(brain.Player.position, FaceTurnSpeed, deltaTime);

            if (gazedNow && !wasGazedLastFrame)
            {
                if (currentTarget != null) exposedTarget = currentTarget;
                relocationPending = true;
                TrySelectHiddenCorner(exposedTarget);
            }

            if (gazedNow)
            {
                fleeTimer = brain.Config.ShyFleeDuration; // eye contact resets phase-2's timer

                if (relocationPending)
                {
                    relocationRetryTimer -= deltaTime;
                    if (relocationRetryTimer <= 0f) TrySelectHiddenCorner(exposedTarget);
                }

                if (staredown.TickWhileGazed(deltaTime) || staredown.IsActive)
                {
                    brain.Navigator.Stop();
                }
            }
            else
            {
                if (wasGazedLastFrame)
                {
                    // Only an actual stopped staredown can fuel the anger roll. Merely watching a
                    // retreating monster keeps it away; it should not turn every glance into a chase.
                    if (staredown.IsActive && !brain.IsFinalHunt)
                    {
                        float aggroChance = brain.ComputeAggressionProbability(followedStaredown: true);
                        bool aggro = Random.value < aggroChance;
                        brain.SetDecisionReason(aggro
                            ? $"Aggressive: Shy staredown broken, aggro roll succeeded ({aggroChance:P0} chance)"
                            : $"Shy continues: staredown broken, aggro roll failed ({aggroChance:P0} chance)");
                        if (aggro)
                        {
                            brain.ChangeState(brain.AggressiveState);
                            return;
                        }
                    }

                    staredown.EndStaredown();
                    if (!relocationPending)
                    {
                        brain.Navigator.Resume();
                    }
                }
                else if (relocationPending)
                {
                    relocationRetryTimer -= deltaTime;
                    if (relocationRetryTimer <= 0f) TrySelectHiddenCorner(exposedTarget);
                }
                else if (brain.Navigator.HasArrived)
                {
                    brain.Navigator.Stop(); // reached its hiding spot — wait here, still "in phase 2"
                }

                fleeTimer -= deltaTime;

                // TGD section 12: at the flee timer's midpoint, unobserved, there's a chance of
                // doubling back (FakeRetreat) instead of always calmly returning to Patrol.
                if (!triedFakeRetreat && fleeTimer <= brain.Config.ShyFleeDuration * 0.5f)
                {
                    triedFakeRetreat = true;
                    // Scaled by anger bracket (TGD section 20: "daha fazla fake retreat" at 9+).
                    float fakeRetreatChance = brain.Config.FakeRetreatChance * brain.Tuning.FakeRetreatChanceMultiplier;
                    if (Random.value < fakeRetreatChance)
                    {
                        brain.ChangeState(brain.FakeRetreatState);
                        return;
                    }
                }

                if (fleeTimer <= 0f)
                {
                    brain.ChangeState(brain.IsFinalHunt ? brain.RepositionState : brain.PatrolState);
                    return;
                }
            }

            wasGazedLastFrame = gazedNow;
        }

        private bool TrySelectHiddenCorner(StalkPoint excluded)
        {
            StalkPoint selected = ShyEscapePointSelector.PickBest(
                brain.StalkPoints,
                brain.Player.position,
                brain.transform.position,
                brain.VisionModel,
                brain.Navigator,
                excluded,
                out Vector3 resolvedDestination);

            if (selected == null || !brain.Navigator.TrySetResolvedDestination(resolvedDestination))
            {
                currentTarget = null;
                relocationPending = true;
                relocationRetryTimer = brain.Config.RepositionInterval;
                brain.Navigator.Stop();
                brain.SetDecisionReason("Shy waiting: no different hidden reachable corner");
                return false;
            }

            currentTarget = selected;
            exposedTarget = null;
            relocationPending = false;
            relocationRetryTimer = 0f;
            brain.Navigator.Resume();
            brain.SetDecisionReason($"Shy retreat: hidden corner {selected.name}");
            return true;
        }

        public void Exit() { }
    }
}
