using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// A player is nearby: silently trails to a reachable point behind their current facing
    /// direction (the hunting behaviour from the supplied Bracken transcript). Any glance registers instantly (no minimum duration — Bracken reacts the
    /// moment it's looked at). While looked at it freezes and stares back, gaining Anger; when the
    /// player looks away it either flees (Shy) or, based on the accumulated Anger, sometimes attacks
    /// outright (Aggressive) — the wiki's probabilistic "aggro roll" on staredown exit. Staring
    /// continuously past GazeAggroHardCap forces Aggressive regardless of the roll ("staring too
    /// long WILL retaliate").
    /// </summary>
    public class InterestedState : IMonsterState
    {
        private const float FaceTurnSpeed = 360f;

        private readonly MonsterBrain brain;
        private bool wasGazedLastFrame;
        private float continuousStareSeconds;
        private float repathTimer;

        public InterestedState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.SetAutoRotate(true);
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Config.InterestedSpeed);
            brain.Navigator.SetStalkDestinationBehind(brain.Player, brain.Config.StalkBehindDistance);
            wasGazedLastFrame = false;
            continuousStareSeconds = 0f;
            repathTimer = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (!brain.Perception.IsPlayerWithinRadius(brain.Config.LoseInterestRadius))
            {
                brain.ChangeState(brain.PatrolState);
                return;
            }

            bool gazedNow = brain.IsGazedAtNow;

            if (gazedNow)
            {
                if (!wasGazedLastFrame)
                {
                    brain.Anger.AddAnger(brain.Config.AngerPerSpotted); // "the moment you look at it, this adds to anger" (wiki)
                    brain.Navigator.Stop();
                    brain.Navigator.SetAutoRotate(false);
                }

                brain.Navigator.FaceTowards(brain.Player.position, FaceTurnSpeed, deltaTime);
                continuousStareSeconds += deltaTime;

                if (continuousStareSeconds >= brain.Config.GazeAggroHardCap)
                {
                    brain.ChangeState(brain.AggressiveState);
                    return;
                }

                wasGazedLastFrame = gazedNow;
                return; // frozen and staring — no reason to keep repathing a destination it can't move toward
            }
            else if (wasGazedLastFrame)
            {
                // Gaze just broke — decide the reaction (tutorial's IsStaringTooLong, blended with
                // the wiki's anger-chance roll instead of a hard binary threshold).
                bool aggro = Random.value < brain.Anger.AggroChance;
                brain.ChangeState(aggro ? brain.AggressiveState : brain.ShyState);
                return;
            }
            else if (brain.Navigator.HasArrived)
            {
                brain.Navigator.SetStalkDestinationBehind(brain.Player, brain.Config.StalkBehindDistance);
            }

            repathTimer -= deltaTime;
            if (repathTimer <= 0f)
            {
                brain.Navigator.SetStalkDestinationBehind(brain.Player, brain.Config.StalkBehindDistance);
                repathTimer = brain.Config.StalkRepathInterval;
            }

            wasGazedLastFrame = gazedNow;
        }

        public void Exit() { }
    }
}
