using Game.Gameplay.Player;
using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Ranks StalkPoint candidates for approach/ambush behavior (TGD section 16). Pure static
    /// functions of their inputs — no MonoBehaviour state, nothing allocated per call — so this stays
    /// trivially unit-testable and safe to run on every reposition tick (MASTER_PROMPT.md
    /// zero-allocation discipline).
    /// </summary>
    public static class StalkPointScorer
    {
        /// <summary>Weights for the scoring terms, matching TGD section 16's Positive/Negative lists.</summary>
        [System.Serializable]
        public struct Weights
        {
            public float OutsideFov;        // "Player FOV dışında: +High"
            public float Darkness;          // "Karanlık: +High"
            public float BehindPlayer;      // "Player'ın arkasında: +Medium"
            public float Corner;            // "Corner/junction: +Medium"
            public float Junction;
            public float PredictedRoute;    // "Predicted route üzerinde: +Medium"
            public float DeadEndPenalty;
            public float SafeZonePenalty;   // "Safe zone exit yakınında: +Low/Medium" — see note below
            public float VisiblePenalty;    // "Player tarafından görülebilir: -Huge"
            public float PlayerDistancePenalty;  // "Player'a aşırı uzak: -Medium"
            public float MonsterDistancePenalty; // "Monster'ın mevcut pozisyonundan gereksiz uzak: -Low"

            public static Weights Default => new Weights
            {
                OutsideFov = 4f,
                Darkness = 3f,
                BehindPlayer = 2f,
                Corner = 1.5f,
                Junction = 1f,
                PredictedRoute = 2f,
                DeadEndPenalty = 3f,
                SafeZonePenalty = 2f,
                VisiblePenalty = 8f,
                PlayerDistancePenalty = 0.15f,
                MonsterDistancePenalty = 0.05f,
            };
        }

        /// <summary>
        /// Higher is better. Returns float.NegativeInfinity for candidates TGD marks "-Invalid"
        /// (inside a SafeZone, or with no complete NavMesh path) so callers can never select them —
        /// picking an unreachable point makes the agent grind against a wall (see chat).
        /// </summary>
        public static float Score(StalkPoint point, Vector3 playerPosition, Vector3 predictedPlayerPosition,
            Vector3 playerFacing, Vector3 monsterPosition, PlayerVisionModel visionModel,
            MonsterNavigator navigator, float visibilityRiskTolerance, Weights weights,
            StalkPoint stickyPoint = null, float stickyBonus = 0f)
        {
            // "-Invalid" terms first — these are disqualifications, not penalties.
            if (navigator != null && !navigator.IsReachable(point.Position)) return float.NegativeInfinity;

            Vector3 position = point.Position;
            float score = 0f;

            // Hysteresis: the point the monster is already committed to gets a flat bonus so a
            // marginally-better candidate (a fraction of a point, from the player simply taking one
            // more step) doesn't yank the destination away mid-approach every rescore tick. Without
            // this a close scoring race between two candidates reads as the monster flip-flopping
            // between two spots instead of committing to one (see chat).
            if (stickyPoint != null && point == stickyPoint) score += stickyBonus;

            score += point.DarknessLevel * weights.Darkness;
            if (point.IsNearCorner) score += weights.Corner;
            if (point.IsJunction) score += weights.Junction;
            if (point.IsDeadEnd) score -= weights.DeadEndPenalty; // dead ends trap the monster itself
            if (point.IsSafeZoneAdjacent) score -= weights.SafeZonePenalty;

            if (visionModel != null)
            {
                // Two distinct terms, matching TGD's two visibility rows: being outside the FOV cone at
                // all is a positive, while being genuinely visible (in-cone AND unobstructed) is the
                // "-Huge" penalty. Scoring only the latter — as this did before — meant a point right
                // in front of the player behind a thin pillar ranked identically to one safely behind
                // them (see chat).
                if (!visionModel.IsPotentiallyVisible(position)) score += weights.OutsideFov;

                if (visionModel.IsActuallyVisible(position))
                {
                    // TGD section 17: the avoidance must not be absolute — an angry monster accepts
                    // more risk of being seen, which is what stops it reading as robotic.
                    score -= weights.VisiblePenalty * (1f - Mathf.Clamp01(visibilityRiskTolerance));
                }
            }

            // "Player'ın arkasında: +Medium" — scaled by how far behind, so directly-behind beats
            // merely-off-to-the-side.
            Vector3 playerToPoint = position - playerPosition;
            playerToPoint.y = 0f;
            float playerDistance = playerToPoint.magnitude;
            if (playerDistance > 0.0001f)
            {
                float behindness = Vector3.Dot(-playerFacing, playerToPoint / playerDistance); // 1 = straight behind
                if (behindness > 0f) score += behindness * weights.BehindPlayer;
            }

            // "Predicted route üzerinde: +Medium" — closeness to where the player is heading, not to
            // where they currently stand. Falls out naturally as an inverse-distance bonus.
            float predictedDistance = Vector3.Distance(position, predictedPlayerPosition);
            score += weights.PredictedRoute / (1f + predictedDistance);

            // "Player'a aşırı uzak: -Medium" and "Monster'ın mevcut pozisyonundan gereksiz uzak: -Low".
            // The second term was missing entirely before, so the monster would happily commit to a
            // marginally-better point on the far side of the map.
            score -= playerDistance * weights.PlayerDistancePenalty;
            score -= Vector3.Distance(position, monsterPosition) * weights.MonsterDistancePenalty;

            return score;
        }

        /// <summary>
        /// Highest-scoring valid point, or null when the set is empty or every candidate is invalid.
        /// `excluded` lets a caller skip the point it is already sitting on.
        /// </summary>
        public static StalkPoint PickBest(StalkPointRuntimeSet points, Vector3 playerPosition,
            Vector3 predictedPlayerPosition, Vector3 playerFacing, Vector3 monsterPosition,
            PlayerVisionModel visionModel, MonsterNavigator navigator, float visibilityRiskTolerance,
            StalkPoint excluded = null, bool requireAmbushAllowed = false, float distanceWeightMultiplier = 1f,
            StalkPoint stickyPoint = null, float stickyBonus = 0f)
        {
            if (points == null) return null;

            StalkPoint best = null;
            float bestScore = float.NegativeInfinity;
            Weights weights = Weights.Default;

            // TGD section 38: "Monster daha yakın candidate point'leri seçer" during FinalChase —
            // scaling both distance-penalty terms up makes farther points lose relatively more,
            // biasing the winner toward whatever is closest without touching any of the concealment
            // terms (darkness/corner/visibility still matter exactly as much).
            weights.PlayerDistancePenalty *= distanceWeightMultiplier;
            weights.MonsterDistancePenalty *= distanceWeightMultiplier;

            var items = points.Items;

            for (int i = 0; i < items.Count; i++)
            {
                StalkPoint candidate = items[i];
                if (candidate == null || candidate == excluded) continue;
                if (requireAmbushAllowed && !candidate.AmbushAllowed) continue;

                float score = Score(candidate, playerPosition, predictedPlayerPosition, playerFacing,
                    monsterPosition, visionModel, navigator, visibilityRiskTolerance, weights,
                    stickyPoint, stickyBonus);

                if (float.IsNegativeInfinity(score)) continue; // "-Invalid"
                if (score <= bestScore) continue;

                bestScore = score;
                best = candidate;
            }

            return best;
        }

        /// <summary>Score of a single candidate using the current defaults — for debug gizmo labels (TGD section 53).</summary>
        public static float DebugScore(StalkPoint point, Vector3 playerPosition, Vector3 predictedPlayerPosition,
            Vector3 playerFacing, Vector3 monsterPosition, PlayerVisionModel visionModel, float visibilityRiskTolerance)
            => Score(point, playerPosition, predictedPlayerPosition, playerFacing, monsterPosition,
                visionModel, null, visibilityRiskTolerance, Weights.Default);
    }
}
