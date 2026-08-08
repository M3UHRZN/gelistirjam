using Game.Gameplay.Monster;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// Reintroduced after the Bracken rewrite deleted it outright (TGD section 13, see chat: TGD
    /// comparison). Distinguishes "Potentially Visible" — a candidate point is within the player's
    /// FOV cone and range, regardless of geometry — from "Actually Visible" — that plus an unobstructed
    /// line of sight (delegated to MonsterPerception.IsPlayerVisibleFrom, which already does the
    /// occlusion linecast). This does not replace the Bracken-style omniscient detection radius; it
    /// exists purely so StalkPointScorer can penalize points the player could currently see, per TGD's
    /// original "stay hidden while stalking" intent, layered on top of the Bracken behaviors.
    /// </summary>
    public class PlayerVisionModel : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField, Range(1f, 179f)] private float fieldOfViewDegrees = 100f;
        [SerializeField] private float viewRange = 20f;
        [SerializeField] private MonsterPerception perception;

        public void Bind(Transform playerTransform, MonsterPerception monsterPerception)
        {
            player = playerTransform;
            perception = monsterPerception;
        }

        /// <summary>Within the player's FOV cone and range — no occlusion check.</summary>
        public bool IsPotentiallyVisible(Vector3 worldPoint)
        {
            if (player == null) return false;

            Vector3 toPoint = worldPoint - player.position;
            toPoint.y = 0f;
            float distance = toPoint.magnitude;
            if (distance > viewRange || distance < 0.0001f) return distance <= viewRange;

            Vector3 facing = player.forward;
            facing.y = 0f;
            facing.Normalize();

            float angle = Vector3.Angle(facing, toPoint.normalized);
            return angle <= fieldOfViewDegrees * 0.5f;
        }

        /// <summary>Potentially visible AND an unobstructed line of sight actually reaches the point.</summary>
        public bool IsActuallyVisible(Vector3 worldPoint)
        {
            if (!IsPotentiallyVisible(worldPoint)) return false;
            if (perception == null) return true;
            return perception.IsPlayerVisibleFrom(worldPoint);
        }
    }
}
