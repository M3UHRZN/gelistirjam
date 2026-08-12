using Game.Gameplay.Player;
using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Selects Shy's next hiding corner. Unlike tactical StalkPoint scoring, every eligibility rule
    /// here is a hard filter: a retreat destination may never trade visibility for anger-driven risk.
    /// </summary>
    public static class ShyEscapePointSelector
    {
        private const float ComparisonTolerance = 0.001f;

        public static StalkPoint PickBest(
            StalkPointRuntimeSet points,
            Vector3 playerPosition,
            Vector3 monsterPosition,
            PlayerVisionModel visionModel,
            MonsterNavigator navigator,
            StalkPoint excluded)
        {
            if (points == null || visionModel == null || navigator == null) return null;

            StalkPoint best = null;
            float bestPlayerDistance = float.NegativeInfinity;
            float bestDarkness = float.NegativeInfinity;
            float bestMonsterDistance = float.PositiveInfinity;
            var items = points.Items;

            for (int i = 0; i < items.Count; i++)
            {
                StalkPoint candidate = items[i];
                if (candidate == null || candidate == excluded || !candidate.IsNearCorner) continue;
                if (visionModel.IsMonsterBodyVisibleAt(
                        candidate.Position,
                        navigator.AgentHeight,
                        navigator.AgentRadius))
                {
                    continue;
                }

                if (!navigator.IsReachable(candidate.Position)) continue;

                float playerDistance = Vector3.Distance(candidate.Position, playerPosition);
                float darkness = candidate.DarknessLevel;
                float monsterDistance = Vector3.Distance(candidate.Position, monsterPosition);

                bool fartherFromPlayer = playerDistance > bestPlayerDistance + ComparisonTolerance;
                bool samePlayerDistance = Mathf.Abs(playerDistance - bestPlayerDistance) <= ComparisonTolerance;
                bool darker = darkness > bestDarkness + ComparisonTolerance;
                bool sameDarkness = Mathf.Abs(darkness - bestDarkness) <= ComparisonTolerance;
                bool closerToMonster = monsterDistance < bestMonsterDistance - ComparisonTolerance;

                if (best != null && !fartherFromPlayer
                    && !(samePlayerDistance && darker)
                    && !(samePlayerDistance && sameDarkness && closerToMonster))
                {
                    continue;
                }

                best = candidate;
                bestPlayerDistance = playerDistance;
                bestDarkness = darkness;
                bestMonsterDistance = monsterDistance;
            }

            return best;
        }
    }
}
