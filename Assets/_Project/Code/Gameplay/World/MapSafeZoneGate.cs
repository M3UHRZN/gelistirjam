using System.Collections.Generic;
using Gloomveil.MapGeneration;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Spawns a SafeZone at each of the generated map's Anchor_SafeZone markers when a map is
    /// published (same subscribe-to-MapReady pattern as MapFuseGate/MapLightGate). SafeZone.cs
    /// requires an attached Collider but nothing upstream in the generator publishes a room-bounds
    /// Collider (GeneratedMapModule only stores grid-unit footprint sizes) — this builds a BoxCollider
    /// from RotatedFootprintUnits * 4m, world-axis-aligned, since the map's hidden lattice only uses
    /// cardinal rotations. Anchor_SafeZone sits at a fixed local offset (0, 0, -0.5) from the module
    /// root, NOT the room's centre, so the box is positioned at the module's own transform instead of
    /// the anchor's — centring the box on the anchor would overhang past one wall and leave a strip of
    /// the room unprotected.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapSafeZoneGate : MonoBehaviour
    {
        // Mirrors MapGenerationConfig.unitSize's default — not read from config because
        // GeneratedMapRuntimeData doesn't republish it.
        private const float GridUnitMetres = 4f;

        [SerializeField] private GeneratedMapRegistry registry;
        [SerializeField] private SafeZoneRuntimeSet safeZoneSet;

        [SerializeField, Tooltip("Trigger volume height. Module footprints only carry floor-plan width/depth, not height (TGD kit: 3m walls).")]
        private float roomHeight = 3f;

        [SerializeField, Tooltip("Fallback footprint (metres) used if a SafeZone anchor has no parent GeneratedMapModule.")]
        private Vector2 fallbackFootprint = new Vector2(4f, 4f);

        private readonly List<SafeZone> spawned = new List<SafeZone>();
        private bool subscribed;

        public IReadOnlyList<SafeZone> SpawnedSafeZones => spawned;

        public void Bind(GeneratedMapRegistry mapRegistry, SafeZoneRuntimeSet set)
        {
            registry = mapRegistry;
            safeZoneSet = set;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void Start() => Subscribe();

        private void OnDisable()
        {
            if (!subscribed) return;
            registry.MapReady -= OnMapReady;
            subscribed = false;
        }

        private void Subscribe()
        {
            if (subscribed || registry == null || safeZoneSet == null) return;

            subscribed = true;
            registry.MapReady += OnMapReady;
            if (registry.TryGetCurrent(out GeneratedMapRuntimeData data))
            {
                SpawnAll(data);
            }
        }

        private void OnMapReady(GeneratedMapRuntimeData data) => SpawnAll(data);

        private void SpawnAll(GeneratedMapRuntimeData data)
        {
            // A freshly published map is a fresh run: old SafeZone instances must not carry over,
            // same "fresh run" reasoning as MapFuseGate.SpawnAll/MapExitController.BuildExit.
            ClearSpawned();

            if (data.SafeZoneAnchors.Count == 0)
            {
                Debug.LogWarning(
                    $"Generated map (seed {data.Seed}) published no SafeZone anchors — no SafeZones were built.",
                    this);
                return;
            }

            foreach (Transform anchor in data.SafeZoneAnchors)
            {
                if (anchor == null) continue;
                spawned.Add(BuildSafeZone(anchor));
            }
        }

        private SafeZone BuildSafeZone(Transform anchor)
        {
            GeneratedMapModule module = anchor.GetComponentInParent<GeneratedMapModule>();
            float width = module != null ? module.RotatedFootprintUnits.Width * GridUnitMetres : fallbackFootprint.x;
            float depth = module != null ? module.RotatedFootprintUnits.Depth * GridUnitMetres : fallbackFootprint.y;

            string suffix = module != null ? module.PlacementId.ToString() : anchor.GetInstanceID().ToString();
            var go = new GameObject($"SafeZone_{suffix}");
            go.transform.SetParent(transform, false);
            Vector3 position = module != null ? module.transform.position : anchor.position;
            go.transform.SetPositionAndRotation(position, Quaternion.identity);

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, roomHeight * 0.5f, 0f);
            box.size = new Vector3(width, roomHeight, depth);
            box.isTrigger = true;

            SafeZone safeZone = go.AddComponent<SafeZone>();
            safeZone.Bind(safeZoneSet);
            return safeZone;
        }

        private void ClearSpawned()
        {
            foreach (SafeZone zone in spawned)
            {
                if (zone != null) Destroy(zone.gameObject);
            }
            spawned.Clear();
        }
    }
}
