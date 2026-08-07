using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>
    /// Detects whether the player is currently aiming at a MonsterGazeReceiver via a small raycast
    /// cluster around screen center. Reacts instantly on first hit (no minimum duration) — Bracken
    /// "as soon as you look at it" registers the glance immediately. A short loss-grace window keeps
    /// CurrentTarget from flickering false for a single missed frame (rotation jitter, raycast noise
    /// at a collider's silhouette edge) — without it, every 1-frame miss read as "gaze broken" and
    /// caused constant state-machine thrashing (see chat). The monster states poll CurrentTarget
    /// every frame themselves instead of subscribing to a debounced event.
    /// Attach to the player camera.
    /// </summary>
    public class PlayerGazeDetector : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private MonsterConfig config;
        [SerializeField] private LayerMask gazeHitMask = ~0;
        [SerializeField, Tooltip("Seconds a lost hit is still reported as gazed before CurrentTarget actually clears.")]
        private float gazeLossGraceSeconds = 0.15f;

        // Fixed 5-point cluster around the viewport center; scaled by the config's cone angle.
        private static readonly Vector2[] ClusterOffsets =
        {
            Vector2.zero, Vector2.up, Vector2.down, Vector2.left, Vector2.right
        };

        private MonsterGazeReceiver lastConfirmedTarget;
        private float secondsSinceLastHit;

        /// <summary>The receiver currently under the ray cluster (with a small loss-grace window), or null.</summary>
        public MonsterGazeReceiver CurrentTarget { get; private set; }

        public void Bind(Camera camera, MonsterConfig monsterConfig)
        {
            playerCamera = camera;
            config = monsterConfig;
        }

        private Camera PlayerCamera => playerCamera != null ? playerCamera : (playerCamera = GetComponent<Camera>());

        private void Update()
        {
            MonsterGazeReceiver hitNow = (PlayerCamera != null && config != null) ? DetectTarget() : null;

            if (hitNow != null)
            {
                lastConfirmedTarget = hitNow;
                secondsSinceLastHit = 0f;
            }
            else
            {
                secondsSinceLastHit += Time.deltaTime;
                if (secondsSinceLastHit > gazeLossGraceSeconds) lastConfirmedTarget = null;
            }

            CurrentTarget = lastConfirmedTarget;
        }

        private MonsterGazeReceiver DetectTarget()
        {
            int hits = 0;
            MonsterGazeReceiver found = null;
            float spread = config.GazeConeHalfAngleDegrees / 90f; // rough viewport-space scale for the ray cluster

            int rayCount = Mathf.Min(config.GazeRayCount, ClusterOffsets.Length);
            for (int i = 0; i < rayCount; i++)
            {
                Vector3 viewportPoint = new Vector3(
                    0.5f + ClusterOffsets[i].x * spread,
                    0.5f + ClusterOffsets[i].y * spread,
                    0f);

                Ray ray = PlayerCamera.ViewportPointToRay(viewportPoint);
                if (Physics.Raycast(ray, out RaycastHit hit, config.GazeMaxDistance, gazeHitMask, QueryTriggerInteraction.Collide))
                {
                    MonsterGazeReceiver receiver = hit.collider.GetComponentInParent<MonsterGazeReceiver>();
                    if (receiver != null)
                    {
                        hits++;
                        found = receiver;
                    }
                }
            }

            return hits >= config.GazeRequiredHits ? found : null;
        }
    }
}
