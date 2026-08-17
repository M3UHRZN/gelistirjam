using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// A player is nearby: silently trails to a reachable point behind their current facing
    /// direction (the hunting behaviour from the supplied Bracken transcript). Raw player gaze is a
    /// Brain-level priority interruption shared by all calm/stalking states, so this state only owns
    /// its stalking navigation and distance/reposition transitions.
    /// </summary>
    public class InterestedState : IMonsterState
    {
        private readonly MonsterBrain brain;
        private float repathTimer;

        public InterestedState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.SetAutoRotate(true);
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Config.InterestedSpeed);
            brain.Navigator.SetStalkDestinationBehind(brain.Player, StalkDistanceForCurrentFacing());
            // Was 0f, which re-rolled the RepositionChance check on the very next Tick. That mattered
            // most exactly when Interested is (re-)entered as the fallback from a failed
            // Reposition/Flank/CutOff attempt — the monster would immediately try again, and if the
            // same candidate set kept failing (e.g. the player is standing somewhere with no good
            // StalkPoint nearby) it visibly bounced Interested<->Reposition every frame instead of
            // just calmly stalking for a beat (see chat — reported as "state geçişlerinde bozukluk").
            // Starting the timer at the normal interval still repaths immediately via the
            // SetStalkDestinationBehind call above; it only delays the *next* reposition roll.
            repathTimer = brain.Config.StalkRepathInterval;
        }

        public void Tick(float deltaTime)
        {
            if (!brain.IsFinalHunt
                && !brain.Perception.IsPlayerWithinRadius(brain.Config.LoseInterestRadius))
            {
                brain.ChangeState(brain.PatrolState);
                return;
            }

            if (brain.Navigator.HasArrived)
            {
                brain.Navigator.SetStalkDestinationBehind(brain.Player, StalkDistanceForCurrentFacing());
            }

            repathTimer -= deltaTime;
            if (repathTimer <= 0f)
            {
                // TGD section 12: occasionally break off from plain behind-stalking to attempt a real
                // ambush via StalkPoint scoring, instead of always trailing the player directly.
                // Skipped entirely when no StalkPoints are bound — falls back to the original
                // Bracken-only behavior (see chat).
                // Chance is scaled by the anger bracket (TGD section 20: "düşük flank oranı" when calm
                // rising to "daha fazla flank / daha fazla ambush" as anger climbs).
                float repositionChance = brain.Config.RepositionChance * brain.Tuning.RepositionChanceMultiplier;
                if (brain.StalkPoints != null && brain.StalkPoints.Items.Count > 0 && Random.value < repositionChance)
                {
                    brain.ChangeState(brain.RepositionState);
                    return;
                }

                brain.Navigator.SetStalkDestinationBehind(brain.Player, StalkDistanceForCurrentFacing());
                repathTimer = brain.Config.StalkRepathInterval;
            }
        }

        public void Exit() { }

        // TGD section 68 — "the AI reacts to which way the player is looking": if the player is
        // generally facing toward the monster's current position (even without triggering a full
        // gaze-confirm on the eyes specifically), it hangs back further; if the player is looking
        // away, it closes the distance. This is what makes the stalk distance feel reactive to the
        // player's own orientation instead of a constant number.
        private float StalkDistanceForCurrentFacing()
        {
            Vector3 playerToMonster = brain.transform.position - brain.Player.position;
            playerToMonster.y = 0f;
            if (playerToMonster.sqrMagnitude < 0.0001f) return brain.Config.StalkBehindDistance;

            float facingDot = Vector3.Dot(brain.Perception.PlayerFacingDirection, playerToMonster.normalized);
            // facingDot > 0 means the player is oriented roughly toward the monster's side of the
            // world even if not staring straight at it. Scale distance between 0.6x (closing in while
            // unwatched) and 1.6x (backing off while roughly faced) of the base config value.
            float t = Mathf.InverseLerp(-1f, 1f, facingDot);
            float facingScale = Mathf.Lerp(0.6f, 1.6f, t);

            // Anger bracket then shifts the whole band (TGD section 20: "daha uzaktan stalk" while
            // calm, "yakın approach" when furious).
            return brain.Config.StalkBehindDistance * facingScale * brain.Tuning.StalkDistanceMultiplier;
        }
    }
}
