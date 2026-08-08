using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Lit area the monster cannot enter or select as a destination (TGD section 29-30).
    /// Bounds are read from an attached trigger Collider.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SafeZone : MonoBehaviour
    {
        [SerializeField] private SafeZoneRuntimeSet set;
        [SerializeField, Tooltip("Bounds are grown by this much in every direction before the containment test (TGD section 29: 'SafeZone içerisindeki player saldırıya uğrayamaz' is an absolute guarantee, not a best-effort one). A CharacterController's own grounded resting height sits a few centimeters below its collision surface (skin width) — verified live via Unity MCP: this project's TestPlayer settles at y=-0.02 on a floor whose SafeZone volumes start exactly at y=0.00, which made Contains() return false for a player standing normally inside the zone (see chat: Unity MCP validation pass). Small enough not to meaningfully change where a zone reads as 'entered', large enough to absorb normal floor-contact jitter.")]
        private float boundsTolerance = 0.1f;
        private Collider zoneVolume;

        // Registers immediately in addition to OnEnable: the editor scene builder creates this
        // GameObject already active, so OnEnable fires (with `set` still null) before Bind() runs.
        // Register() is idempotent (Contains-guarded), so this is safe however the two end up ordered.
        public void Bind(SafeZoneRuntimeSet targetSet)
        {
            set = targetSet;
            if (isActiveAndEnabled) set.Register(this);
        }

        private Collider ZoneVolume
        {
            get
            {
                if (zoneVolume == null)
                {
                    zoneVolume = GetComponent<Collider>();
                    zoneVolume.isTrigger = true;
                }
                return zoneVolume;
            }
        }

        public bool Contains(Vector3 worldPoint)
        {
            Bounds bounds = ZoneVolume.bounds;
            bounds.Expand(boundsTolerance * 2f); // Expand grows by the value in *each* direction (half per side)
            return bounds.Contains(worldPoint);
        }

        private void OnEnable()
        {
            if (set != null) set.Register(this);
        }

        // Fallback: OnEnable-time registration isn't reliable when a scene is loaded/reopened via an
        // Editor script rather than normal interactive Play Mode (same class of timing issue as the
        // Awake-ordering fix in MonsterNavigator — see chat). Start() runs after every object's
        // Awake/OnEnable has settled, and Register() is idempotent, so this is a safe no-op if
        // OnEnable already succeeded.
        private void Start()
        {
            if (set != null) set.Register(this);
        }

        private void OnDisable()
        {
            if (set != null) set.Unregister(this);
        }

        private void OnDrawGizmos()
        {
            if (!(ZoneVolume is BoxCollider box)) return;

            Gizmos.color = new Color(1f, 0.95f, 0.4f, 0.25f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
