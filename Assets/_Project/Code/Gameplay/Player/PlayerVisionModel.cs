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

        private readonly Vector3[] bodySamples = new Vector3[5];
        private Camera viewCamera;

        public void Bind(Transform playerTransform, MonsterPerception monsterPerception)
        {
            player = playerTransform;
            perception = monsterPerception;
        }

        public void Bind(Transform playerTransform, Camera playerCamera, MonsterPerception monsterPerception)
        {
            player = playerTransform;
            viewCamera = playerCamera;
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

        /// <summary>
        /// True when any representative point of a hypothetical monster capsule is inside the real
        /// camera viewport and has unobstructed line of sight. This is intentionally additive: the
        /// horizontal point checks above remain unchanged for tactical StalkPoint scoring.
        /// </summary>
        public bool IsMonsterBodyVisibleAt(Vector3 feetPosition, float height, float radius)
        {
            // Missing runtime wiring must fail closed for hiding: treating a point as hidden without a
            // camera or occlusion source would let Shy deliberately choose an unverified visible corner.
            if (viewCamera == null || perception == null) return true;

            float safeHeight = Mathf.Max(0.1f, height);
            float safeRadius = Mathf.Max(0f, radius);
            Vector3 torso = feetPosition + Vector3.up * (safeHeight * 0.55f);
            Vector3 cameraRight = viewCamera.transform.right * safeRadius;

            bodySamples[0] = feetPosition + Vector3.up * (safeHeight * 0.2f);
            bodySamples[1] = torso;
            bodySamples[2] = feetPosition + Vector3.up * (safeHeight * 0.9f);
            bodySamples[3] = torso - cameraRight;
            bodySamples[4] = torso + cameraRight;

            Vector3 cameraPosition = viewCamera.transform.position;
            for (int i = 0; i < bodySamples.Length; i++)
            {
                Vector3 sample = bodySamples[i];
                if (Vector3.Distance(cameraPosition, sample) > viewRange) continue;

                Vector3 viewport = viewCamera.WorldToViewportPoint(sample);
                if (viewport.z < viewCamera.nearClipPlane || viewport.z > viewCamera.farClipPlane
                    || viewport.x < 0f || viewport.x > 1f
                    || viewport.y < 0f || viewport.y > 1f)
                {
                    continue;
                }

                if (perception.HasClearLineOfSight(cameraPosition, sample)) return true;
            }

            return false;
        }
    }
}
