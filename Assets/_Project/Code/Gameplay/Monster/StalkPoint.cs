using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// A hand-placed candidate ambush/approach position (TGD section 15-16). Carries the static
    /// metadata StalkPointScorer needs to rank points — corner/junction/dead-end topology is set by
    /// hand per marker (cheap to author, expensive to infer), while darkness and SafeZone adjacency
    /// are resolved live so lights turning off mid-game or SafeZone layout changes stay accurate.
    /// Reintroduced after the Bracken rewrite deleted this system outright (see chat: TGD comparison).
    /// </summary>
    public class StalkPoint : MonoBehaviour
    {
        [SerializeField] private bool isNearCorner;
        [SerializeField] private bool isJunction;
        [SerializeField] private bool isDeadEnd;
        [SerializeField, Tooltip("TGD section 15: whether this point is a legal place to stand still and wait. Open corridor mid-sections make poor ambush spots even when they score well as approach routes.")]
        private bool ambushAllowed = true;
        [SerializeField, Tooltip("Fallback darkness (0=lit,1=dark) when no LightZone is assigned.")]
        private float staticDarknessLevel = 0.5f;
        [SerializeField, Tooltip("If assigned, darkness is derived from this zone's on/off state instead of the static value.")]
        private LightZone lightZone;
        [SerializeField] private SafeZoneRuntimeSet safeZones;
        [SerializeField] private float safeZoneAdjacencyRadius = 3f;
        [SerializeField] private StalkPointRuntimeSet registry;

        public Vector3 Position => transform.position;
        public bool IsNearCorner => isNearCorner;
        public bool IsJunction => isJunction;
        public bool IsDeadEnd => isDeadEnd;
        public bool AmbushAllowed => ambushAllowed;
        public float DarknessLevel => lightZone != null ? (lightZone.IsActive ? 0f : 1f) : staticDarknessLevel;

        public bool IsSafeZoneAdjacent
        {
            get
            {
                if (safeZones == null) return false;
                var zones = safeZones.Items;
                for (int i = 0; i < zones.Count; i++)
                {
                    if (zones[i] == null) continue;
                    if (zones[i].Contains(Position)) return true;
                    // Contains() only tests the exact point; also flag points hovering just outside a
                    // zone's edge, which is where "safe zone adjacent" actually matters for scoring.
                    if (Vector3.Distance(zones[i].transform.position, Position) <= safeZoneAdjacencyRadius) return true;
                }
                return false;
            }
        }

        /// <summary>Sets the hand-authored topology flags from code (composition root) instead of only via the Inspector.</summary>
        public void ConfigureTopology(bool nearCorner, bool junction, bool deadEnd, bool allowAmbush = true)
        {
            isNearCorner = nearCorner;
            isJunction = junction;
            isDeadEnd = deadEnd;
            ambushAllowed = allowAmbush;
        }

        public void ConfigureStaticDarkness(float darkness) =>
            staticDarknessLevel = Mathf.Clamp01(darkness);

        public void Bind(SafeZoneRuntimeSet zones, LightZone zone, StalkPointRuntimeSet stalkRegistry)
        {
            safeZones = zones;
            lightZone = zone;
            registry = stalkRegistry;
            if (isActiveAndEnabled) registry.Register(this);
        }

        // Same OnEnable+Start idempotent-registration fallback used by SafeZone/FuseBox — Editor
        // scene-builder ordering isn't reliable (see chat).
        private void OnEnable()
        {
            if (registry != null) registry.Register(this);
        }

        private void Start()
        {
            if (registry != null) registry.Register(this);
        }

        private void OnDisable()
        {
            if (registry != null) registry.Unregister(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isDeadEnd ? Color.red : isJunction ? Color.yellow : isNearCorner ? Color.cyan : Color.white;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
