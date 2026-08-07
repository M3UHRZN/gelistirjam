using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>
    /// Scores and selects StalkPoint candidates (TGD section 16 scoring table).
    /// Points contained by a SafeZone are excluded outright (-Invalid).
    /// </summary>
    public class MonsterPositionEvaluator : MonoBehaviour
    {
        [SerializeField] private StalkPointRuntimeSet stalkPoints;
        [SerializeField] private SafeZoneRuntimeSet safeZones;
        [SerializeField] private MonsterPerception perception;
        [SerializeField] private Transform player;
        [SerializeField] private Transform monster;

        public void Bind(StalkPointRuntimeSet points, SafeZoneRuntimeSet zones, MonsterPerception monsterPerception,
            Transform playerTransform, Transform monsterTransform)
        {
            stalkPoints = points;
            safeZones = zones;
            perception = monsterPerception;
            player = playerTransform;
            monster = monsterTransform;
        }

        /// <summary>Best stalk-and-approach point: hidden from the player, dark, close, behind, near a corner/junction.</summary>
        public StalkPoint EvaluateBestCandidate()
        {
            StalkPoint best = null;
            float bestScore = float.NegativeInfinity;

            var items = stalkPoints.Items;
            for (int i = 0; i < items.Count; i++)
            {
                StalkPoint candidate = items[i];
                if (IsInsideAnySafeZone(candidate.Position)) continue;

                float score = ScoreStalkCandidate(candidate);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>Best retreat point: far from the player, hidden, dark. Used while fleeing after Observed.</summary>
        public StalkPoint EvaluateRetreatCandidate()
        {
            StalkPoint best = null;
            float bestScore = float.NegativeInfinity;

            var items = stalkPoints.Items;
            for (int i = 0; i < items.Count; i++)
            {
                StalkPoint candidate = items[i];
                if (IsInsideAnySafeZone(candidate.Position)) continue;

                bool visible = perception != null && perception.IsPointActuallyVisibleToPlayer(candidate.Position);
                float distanceToPlayer = Vector3.Distance(candidate.Position, player.position);
                float score = distanceToPlayer + (visible ? -50f : 50f) + candidate.DarknessLevel * 20f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private bool IsInsideAnySafeZone(Vector3 point)
        {
            var zones = safeZones.Items;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].Contains(point)) return true;
            }
            return false;
        }

        private float ScoreStalkCandidate(StalkPoint point)
        {
            float score = 0f;

            bool visibleToPlayer = perception != null && perception.IsPointActuallyVisibleToPlayer(point.Position);
            score += visibleToPlayer ? -1000f : 300f; // FOV disinda +High / gorulebilir -Huge

            score += point.DarknessLevel * 250f; // karanlik +High

            float distanceToPlayer = Vector3.Distance(point.Position, player.position);
            score += Mathf.Clamp01(1f - distanceToPlayer / 15f) * 120f; // yakin +Medium

            Vector3 toPoint = (point.Position - player.position).normalized;
            if (Vector3.Dot(-player.forward, toPoint) > 0.3f) score += 100f; // oyuncunun arkasinda +Medium

            if (point.IsNearCorner || point.IsJunction) score += 90f; // corner/junction +Medium
            if (point.IsSafeZoneAdjacent) score += 60f; // safe zone exit yakininda +Low/Medium

            float distanceToMonster = Vector3.Distance(point.Position, monster.position);
            score -= Mathf.Clamp01(distanceToMonster / 20f) * 40f; // monster'in mevcut pozisyonundan gereksiz uzak -Low
            if (distanceToPlayer > 18f) score -= 150f; // player'a asiri uzak -Medium

            return score;
        }
    }
}
