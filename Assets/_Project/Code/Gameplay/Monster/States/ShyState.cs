using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Spotted and fleeing (tutorial's "shy"). Runs to a reachable corner outside the player's
    /// unobstructed camera view. A fresh raw gaze burns the exposed destination immediately and sends
    /// it toward a different hidden corner without stopping. Gaze can extend the flee phase but can
    /// never turn this retreat into a staredown or an aggression roll.
    /// </summary>
    public class ShyState : IMonsterState
    {
        private const float FaceTurnSpeed = 360f;

        private readonly MonsterBrain brain;
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
            // If Shy was entered because the Brain already saw a raw gaze edge, its freshly selected
            // escape corner has not been exposed before selection and must not be burned again on the
            // first Tick. Later false-to-true gaze edges still force normal relocation.
            wasGazedLastFrame = brain.IsGazedAtNow;
            triedFakeRetreat = false;
        }

        public void Tick(float deltaTime)
        {
            bool gazedNow = brain.IsGazedAtNow;
            brain.Navigator.FaceTowards(brain.Player.position, FaceTurnSpeed, deltaTime);

            if (gazedNow && !wasGazedLastFrame)
            {
                if (currentTarget != null) exposedTarget = currentTarget;
                relocationPending = true;
                TrySelectHiddenCorner(exposedTarget);
            }

            bool gazeConfirmed = brain.IsGazeConfirmed;
            if (gazeConfirmed)
            {
                // Only a deliberate confirmed stare preserves the legacy timer reset. Raw gaze
                // still relocates on its first frame, but cannot pause or refill the phase lifetime.
                fleeTimer = brain.Config.ShyFleeDuration;
            }

            if (relocationPending)
            {
                relocationRetryTimer -= deltaTime;
                if (relocationRetryTimer <= 0f) TrySelectHiddenCorner(exposedTarget);
            }

            if (!gazeConfirmed)
            {
                if (!relocationPending && brain.Navigator.HasArrived)
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
