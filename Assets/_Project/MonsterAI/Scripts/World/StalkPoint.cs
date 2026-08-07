using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>
    /// Hand-placed AI navigation candidate. See TECHNICAL GAME DESIGN DOCUMENT.md section 15.
    /// </summary>
    public class StalkPoint : MonoBehaviour
    {
        [SerializeField] private StalkPointRuntimeSet set;

        [Header("Metadata (TGD section 15)")]
        [SerializeField] private bool isNearCorner;
        [SerializeField] private bool isJunction;
        [SerializeField] private bool isDeadEnd;
        [SerializeField] private bool isSafeZoneAdjacent;
        [SerializeField] private bool ambushAllowed = true;
        [SerializeField, Range(0f, 1f), Tooltip("Used when no LightZone is linked.")]
        private float manualDarkness = 1f;
        [SerializeField, Tooltip("Optional — darkness follows this zone's active state instead of the manual value.")]
        private LightZone linkedLightZone;

        public Vector3 Position => transform.position;
        public bool IsNearCorner => isNearCorner;
        public bool IsJunction => isJunction;
        public bool IsDeadEnd => isDeadEnd;
        public bool IsSafeZoneAdjacent => isSafeZoneAdjacent;
        public bool AmbushAllowed => ambushAllowed;

        public float DarknessLevel => linkedLightZone != null
            ? (linkedLightZone.IsActive ? 0.1f : 1f)
            : manualDarkness;

        // Registers immediately in addition to OnEnable: the editor scene builder creates these
        // GameObjects already active, so OnEnable fires (with `set` still null) before Bind() runs.
        // Register() is idempotent (Contains-guarded), so this is safe however the two end up ordered.
        public void Bind(StalkPointRuntimeSet targetSet)
        {
            set = targetSet;
            if (isActiveAndEnabled) set.Register(this);
        }

        public void Configure(bool nearCorner, bool junction, bool deadEnd, bool safeZoneAdjacent, float darkness)
        {
            isNearCorner = nearCorner;
            isJunction = junction;
            isDeadEnd = deadEnd;
            isSafeZoneAdjacent = safeZoneAdjacent;
            manualDarkness = darkness;
        }

        private void OnEnable()
        {
            if (set != null) set.Register(this);
        }

        private void OnDisable()
        {
            if (set != null) set.Unregister(this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = isDeadEnd ? new Color(1f, 0.3f, 0.3f, 0.9f)
                : isJunction ? new Color(0.3f, 0.6f, 1f, 0.9f)
                : new Color(0.3f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
        }
    }
}
