using System;
using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>
    /// Detects sustained player gaze on a MonsterGazeReceiver via a small raycast cluster (TGD section 22-24).
    /// Attach to the player camera.
    /// </summary>
    public class PlayerGazeDetector : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private MonsterConfig config;
        [SerializeField] private LayerMask gazeHitMask = ~0;

        // Fixed 5-point cluster around the viewport center (TGD section 23); scaled by the config's cone angle.
        private static readonly Vector2[] ClusterOffsets =
        {
            Vector2.zero, Vector2.up, Vector2.down, Vector2.left, Vector2.right
        };

        private float continuousGazeSeconds;
        private MonsterGazeReceiver currentTarget;

        public event Action<MonsterGazeReceiver> OnGazeConfirmed;

        /// <summary>The receiver currently under the ray cluster this frame, or null. Updated every Update().</summary>
        public MonsterGazeReceiver CurrentTarget => currentTarget;

        public void Bind(Camera camera, MonsterConfig monsterConfig)
        {
            playerCamera = camera;
            config = monsterConfig;
        }

        private Camera PlayerCamera => playerCamera != null ? playerCamera : (playerCamera = GetComponent<Camera>());

        private void Update()
        {
            if (PlayerCamera == null || config == null) return;

            MonsterGazeReceiver target = DetectTarget();
            if (target != null)
            {
                currentTarget = target;
                continuousGazeSeconds += Time.deltaTime;
                if (continuousGazeSeconds >= config.GazeConfirmDuration)
                {
                    OnGazeConfirmed?.Invoke(currentTarget);
                    continuousGazeSeconds = 0f;
                }
            }
            else
            {
                currentTarget = null;
                continuousGazeSeconds = 0f;
            }
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
