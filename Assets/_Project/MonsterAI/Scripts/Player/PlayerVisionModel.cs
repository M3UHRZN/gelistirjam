using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>
    /// Player Vision Cone model consumed by the monster's perception (TGD section 13).
    /// Attach to the player camera.
    /// Uses a lazy accessor instead of relying on Awake() ordering — this is called from
    /// MonsterBrain.OnEnable() (via MonsterPerception/MonsterPositionEvaluator), and cross-object
    /// Awake/OnEnable ordering has repeatedly proven unreliable to depend on in this project
    /// (see chat / Editor.log; MonsterNavigator had the identical bug).
    /// </summary>
    public class PlayerVisionModel : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField, Tooltip("Full horizontal FOV in degrees.")]
        private float horizontalFov = 90f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        private Camera PlayerCamera => playerCamera != null ? playerCamera : (playerCamera = GetComponent<Camera>());

        /// <summary>Point lies inside the FOV cone (no occlusion check) — "Potentially Visible" (TGD section 13).</summary>
        public bool IsPotentiallyVisible(Vector3 worldPoint)
        {
            Transform cameraTransform = PlayerCamera.transform;
            Vector3 toPoint = worldPoint - cameraTransform.position;
            float angle = Vector3.Angle(cameraTransform.forward, toPoint);
            return angle <= horizontalFov * 0.5f;
        }

        /// <summary>Point is inside the FOV cone AND has an unobstructed line of sight — "Actually Visible" (TGD section 13).</summary>
        public bool IsActuallyVisible(Vector3 worldPoint)
        {
            if (!IsPotentiallyVisible(worldPoint)) return false;

            Vector3 origin = PlayerCamera.transform.position;
            Vector3 toPoint = worldPoint - origin;
            float distance = toPoint.magnitude;
            if (distance < 0.05f) return true;

            return !Physics.Raycast(origin, toPoint / distance, distance - 0.05f, obstructionMask, QueryTriggerInteraction.Ignore);
        }
    }
}
