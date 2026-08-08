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
        [SerializeField, Tooltip("Draw the raycast cluster in the Scene view (TGD section 53 — AI/gaze debug visualization).")]
        private bool drawDebugRays = true;

        // Fixed 5-point cluster matching TGD section 23's own diagram literally (diamond/X — one
        // point per corner plus center):
        //   •       •
        //       •
        //   •       •
        // Each ray originates from its own point in this pattern, offset from the camera center in
        // the camera's local right/up plane by GazeRayOriginSpread meters, and travels straight ahead
        // (cameraTransform.forward) — parallel rays, not a single origin diverging at an angle (see
        // chat: the angled-cone version was rejected as not matching the diagram literally, even
        // though it was functionally equivalent).
        private static readonly Vector2[] ClusterOffsets =
        {
            Vector2.zero,
            new Vector2(-1f, 1f), new Vector2(1f, 1f),
            new Vector2(-1f, -1f), new Vector2(1f, -1f),
        };

        private MonsterGazeReceiver lastConfirmedTarget;
        private float secondsSinceLastHit;

        // Last frame's per-ray results, populated in DetectTarget(). Only used for the debug gizmo —
        // gameplay only ever reads the aggregate CurrentTarget.
        private readonly Vector3[] debugRayOrigins = new Vector3[ClusterOffsets.Length];
        private readonly Vector3[] debugRayEnds = new Vector3[ClusterOffsets.Length];
        private readonly bool[] debugRayHit = new bool[ClusterOffsets.Length];
        private int debugRayCount;

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
            debugRayCount = rayCount;

            Vector3 direction = cameraTransform.forward; // all 5 rays are parallel — see ClusterOffsets doc comment

            for (int i = 0; i < rayCount; i++)
            {
                Vector2 offset = ClusterOffsets[i] * config.GazeRayOriginSpread;
                Vector3 origin = cameraTransform.position
                    + cameraTransform.right * offset.x
                    + cameraTransform.up * offset.y;
                Ray ray = new Ray(origin, direction);

                bool rayHit = Physics.Raycast(ray, out RaycastHit hit, config.GazeMaxDistance, gazeHitMask, QueryTriggerInteraction.Collide);
                debugRayOrigins[i] = ray.origin;
                debugRayEnds[i] = rayHit ? hit.point : ray.origin + direction * config.GazeMaxDistance;
                debugRayHit[i] = false;

                if (rayHit && hit.collider.TryGetComponent(out MonsterGazeReceiver receiver))
                {
                    hits++;
                    found = receiver;
                    debugRayHit[i] = true;
                }
            }

            return hits >= config.GazeRequiredHits ? found : null;
        }

        private void OnDrawGizmos()
        {
            if (!drawDebugRays || !Application.isPlaying) return;

            for (int i = 0; i < debugRayCount; i++)
            {
                Gizmos.color = debugRayHit[i] ? Color.green : new Color(1f, 0f, 0f, 0.5f);
                Gizmos.DrawLine(debugRayOrigins[i], debugRayEnds[i]);
            }
        }
    }
}
