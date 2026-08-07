using UnityEngine;

using Game.Gameplay.Monster;
namespace Game.Gameplay.Player
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
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class PlayerGazeDetector : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private MonsterConfig config;
        [SerializeField] private LayerMask gazeHitMask = ~0;
        [SerializeField, Tooltip("Seconds a lost hit is still reported as gazed before CurrentTarget actually clears.")]
        private float gazeLossGraceSeconds = 0.15f;

        // Fixed 5-point cluster around the optical center. Offsets are converted to actual
        // degrees below, rather than treating viewport units as degrees.
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

        private void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponent<Camera>();
        }

        private void Update()
        {
            MonsterGazeReceiver hitNow = (playerCamera != null && config != null) ? DetectTarget() : null;

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
            Transform cameraTransform = playerCamera.transform;
            int rayCount = Mathf.Min(config.GazeRayCount, ClusterOffsets.Length);
            for (int i = 0; i < rayCount; i++)
            {
                Vector2 offset = ClusterOffsets[i] * config.GazeConeHalfAngleDegrees;
                Vector3 direction = Quaternion.AngleAxis(offset.x, cameraTransform.up)
                    * Quaternion.AngleAxis(-offset.y, cameraTransform.right) * cameraTransform.forward;
                Ray ray = new Ray(cameraTransform.position, direction);
                if (Physics.Raycast(ray, out RaycastHit hit, config.GazeMaxDistance, gazeHitMask, QueryTriggerInteraction.Collide))
                {
                    if (hit.collider.TryGetComponent(out MonsterGazeReceiver receiver))
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
