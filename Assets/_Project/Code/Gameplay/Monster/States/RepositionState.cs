using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Breaks off from directly stalking the player's back to seek a better StalkPoint (TGD sections
    /// 12, 14, 16) — the "thinking" state between plain Interested-stalking and a real ambush attempt.
    /// Candidates are re-scored on RepositionInterval instead of once on entry: the player keeps
    /// moving and turning while the monster walks, so a point chosen three seconds ago is often inside
    /// their FOV by the time it arrives. TGD section 14 asks for exactly this — periodic 0.4-0.8s
    /// re-evaluation, explicitly not per-frame.
    /// </summary>
    public class RepositionState : IMonsterState
    {
        private readonly MonsterBrain brain;
        private StalkPoint targetPoint;
        private float rescoreTimer;

        public RepositionState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.SetAutoRotate(true);
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Config.RepositionSpeed);
            targetPoint = null;
            rescoreTimer = 0f;

            // TGD section 20: an angry monster converts more of its repositions into outright
            // cut-offs ("cut-off artar" at 6-8, "aggressive cut-off" at 9+) rather than setting up a
            // patient flank/ambush. Rolled once per reposition, not per re-score.
            if (Random.value < brain.Tuning.CutOffPreference)
            {
                brain.SetDecisionReason($"CutOff: anger-stage roll ({brain.Tuning.CutOffPreference:P0} chance at {brain.Anger.Stage})");
                brain.ChangeState(brain.CutOffState);
                return;
            }

            if (!TryRescore())
            {
                brain.SetDecisionReason("Reposition->Interested: no valid StalkPoint candidate on entry");
                brain.ChangeState(brain.InterestedState);
            }
        }

        public void Tick(float deltaTime)
        {
            if (brain.IsGazeConfirmed)
            {
                brain.ChangeState(brain.ObservedState);
                return;
            }

            if (!brain.IsFinalHunt
                && !brain.Perception.IsPlayerWithinRadius(brain.Config.StalkAbandonRadius))
            {
                brain.ChangeState(brain.PatrolState);
                return;
            }

            rescoreTimer -= deltaTime;
            if (rescoreTimer <= 0f && !TryRescore())
            {
                // Every candidate went invalid (unreachable, or the player moved such that the whole
                // set scores badly). Fall back to plain behind-stalking rather than standing still.
                brain.SetDecisionReason("Reposition->Interested: every candidate went invalid on rescore");
                brain.ChangeState(brain.InterestedState);
                return;
            }

            if (targetPoint == null || !brain.Navigator.HasArrived) return;

            // Arrived. A point flagged as a legal ambush spot (TGD section 15's AmbushAllowed) that is
            // also a natural dead-end or corner is worth waiting at; anything else becomes a flank
            // attempt to get lateral of the player's route without being spotted.
            if (targetPoint.AmbushAllowed && (targetPoint.IsDeadEnd || targetPoint.IsNearCorner))
            {
                brain.SetDecisionReason($"Ambush: arrived at {targetPoint.name} (dead-end/corner, ambush-allowed)");
                brain.ChangeState(brain.AmbushState);
            }
            else
            {
                brain.SetDecisionReason($"Flank: arrived at {targetPoint.name} (not a legal ambush spot)");
                brain.ChangeState(brain.FlankState);
            }
        }

        public void Exit() => targetPoint = null;

        /// <summary>Scores every candidate and paths to the winner. False when nothing valid and reachable exists.</summary>
        private bool TryRescore()
        {
            AngerStageTuning tuning = brain.Tuning;

            // TGD section 38: "Reposition cooldown azalır" during FinalChase — layered on top of the
            // anger-stage multiplier, not instead of it (a relentless monster is also necessarily at
            // Furious anger already, but the two multipliers are separate documented mechanisms).
            float intervalMultiplier = tuning.RepositionIntervalMultiplier;
            if (brain.IsFinalHunt) intervalMultiplier *= brain.Config.FinalChaseRepositionIntervalMultiplier;
            rescoreTimer = brain.Config.RepositionInterval * intervalMultiplier;

            // TGD section 38: "Monster daha yakın candidate point'leri seçer" during FinalChase.
            float distanceWeightMultiplier = brain.IsFinalHunt ? brain.Config.FinalChaseDistanceWeightMultiplier : 1f;

            Vector3 predicted = brain.Perception.PredictedPlayerPosition(brain.Config.PredictionSeconds);

            StalkPoint best = StalkPointScorer.PickBest(
                brain.StalkPoints,
                brain.Player.position,
                predicted,
                brain.Perception.PlayerFacingDirection,
                brain.transform.position,
                brain.VisionModel,
                brain.Navigator,
                tuning.VisibilityRiskTolerance,
                distanceWeightMultiplier: distanceWeightMultiplier,
                // Hysteresis (see chat): bias toward the point already targeted so a near-tied rescore
                // doesn't yank the destination to a different candidate every RepositionInterval tick.
                stickyPoint: targetPoint,
                stickyBonus: brain.Config.RepositionStickyBonus);

            if (best == null) return false;

            // Validated, not raw: PickBest already dropped unreachable candidates, but the snap onto
            // the NavMesh still has to happen here so the agent never targets a point floating just
            // off the walkable surface (see chat — raw SetDestination is what caused wall-grinding).
            if (!brain.Navigator.TrySetValidatedDestination(best.Position)) return false;

            targetPoint = best;
            return true;
        }
    }
}
