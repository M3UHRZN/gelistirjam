using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>
    /// Lit area the monster cannot enter or select as a destination (TGD section 29-30).
    /// Bounds are read from an attached trigger Collider.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SafeZone : MonoBehaviour
    {
        [SerializeField] private SafeZoneRuntimeSet set;
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

        public bool Contains(Vector3 worldPoint) => ZoneVolume.bounds.Contains(worldPoint);

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
            if (!(ZoneVolume is BoxCollider box)) return;

            Gizmos.color = new Color(1f, 0.95f, 0.4f, 0.25f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
