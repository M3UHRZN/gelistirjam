using System;
using System.Collections.Generic;
using Game.Gameplay;
using Gloomveil.MapGeneration;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Spawns a LightZone for every generated map module that carries LightAnchors, when a map is
    /// published (same subscribe-to-MapReady pattern as MapFuseGate/MapExitController — map
    /// generation reaches gameplay only through GeneratedMapRegistry and neutral anchors). Anchors
    /// are grouped by their parent GeneratedMapModule since GeneratedMapRuntimeData.LightAnchors is a
    /// flat, ungrouped list (one room/corridor module = one LightZone). Every module tagged
    /// RoomRole.SafeZone (the default map config publishes more than one) is deliberately excluded
    /// from the shutdown order it hands to LightDirector — TGD section 29/38: SafeZone lighting is
    /// the "critical light" that must survive Final Chase.
    /// The remaining zones are shuffled with a seed-derived RNG so the shutdown order is part of the
    /// map's deterministic, per-seed randomization (TGD section 43).
    ///
    /// Fixture-count thinning (user-requested tuning, not itself a TGD-mandated rule, but consistent
    /// with TGD section 1's "dark industrial labyrinth" tone and the "don't let it get boring"
    /// note it came with): the DoorTransition corridor shape (the short connector strip between a
    /// room and a corridor) never gets a fixture at all — it sits between two already-lit spaces, so
    /// its own light reads as redundant. Among the remaining corridor modules, no two that are
    /// spatially adjacent (touching footprints, same floor) ever both keep a fixture — a run of
    /// several corridor segments in a row gets at most every-other-one lit, never a lit stretch.
    /// Selection order is a seed-derived shuffle so which one of an adjacent pair wins is
    /// deterministic per seed but not a rigid, visually-obvious pattern. Room and Stair category
    /// modules (fuse rooms, exit, safezones, start, stair landings) are never thinned — they're
    /// comparatively rare and functionally significant for safety/navigation/objective visibility.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapLightGate : MonoBehaviour
    {
        [SerializeField] private GeneratedMapRegistry registry;

        [SerializeField, Tooltip("Point light spawned at a LightAnchor that has no Light of its own.")]
        private float fallbackLightRange = 8f;
        [SerializeField] private float fallbackLightIntensity = 1.2f;

        [SerializeField] private MainSceneWiring wiring;
        private readonly List<LightZone> spawnedZones = new List<LightZone>();
        private readonly Dictionary<GeneratedMapModule, LightZone> zonesByModule =
            new Dictionary<GeneratedMapModule, LightZone>();
        private GeneratedMapRuntimeData builtFor;
        private bool subscribed;

        public event Action<GeneratedMapRuntimeData> BuildCompleted;
        public IReadOnlyList<LightZone> SpawnedLightZones => spawnedZones;
        public IReadOnlyList<LightZone> SafeZoneLightZones { get; private set; } = Array.Empty<LightZone>();

        public bool IsBuiltFor(GeneratedMapRuntimeData data) => ReferenceEquals(builtFor, data);

        public bool TryGetLightZone(GeneratedMapModule module, out LightZone zone)
        {
            if (module == null)
            {
                zone = null;
                return false;
            }

            return zonesByModule.TryGetValue(module, out zone);
        }

        public void Bind(GeneratedMapRegistry mapRegistry, MainSceneWiring sceneWiring)
        {
            registry = mapRegistry;
            wiring = sceneWiring;
            if (isActiveAndEnabled) Subscribe();
        }

        // Same OnEnable+Start double-attempt as MapFuseGate/MapExitController: whichever runs first
        // with both references present wins, the guard makes the second a no-op.
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
            if (subscribed || registry == null || wiring == null) return;

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
            // A freshly published map is a fresh run: old zones (and their shutdown state) must not
            // carry over, same "fresh run" reasoning as MapFuseGate.SpawnAll/MapExitController.BuildExit.
            ClearSpawned();

            if (data.LightAnchors.Count == 0)
            {
                Debug.LogWarning(
                    $"Generated map (seed {data.Seed}) published no Light anchors — no LightZones were built.",
                    this);
                wiring.LightDirector.SetShutdownOrder(Array.Empty<LightZone>());
                CompleteBuild(data);
                return;
            }

            var moduleOrder = new List<GeneratedMapModule>();
            var anchorsByModule = new Dictionary<GeneratedMapModule, List<Transform>>();
            var safeZoneModules = new HashSet<GeneratedMapModule>();

            foreach (Transform anchor in data.LightAnchors)
            {
                if (anchor == null) continue;

                GeneratedMapModule module = anchor.GetComponentInParent<GeneratedMapModule>();
                if (module == null)
                {
                    Debug.LogWarning(
                        $"Light anchor '{anchor.name}' has no parent GeneratedMapModule — skipped.", anchor);
                    continue;
                }

                if (!anchorsByModule.TryGetValue(module, out List<Transform> anchors))
                {
                    anchors = new List<Transform>();
                    anchorsByModule[module] = anchors;
                    moduleOrder.Add(module);
                }
                anchors.Add(anchor);

                if (module.RoomRole == RoomRole.SafeZone) safeZoneModules.Add(module);
            }

            HashSet<GeneratedMapModule> omittedCorridorModules = PickCorridorModulesToOmit(moduleOrder, data.Seed);

            var shutdownCandidates = new List<LightZone>();
            var safeZoneZones = new List<LightZone>();

            foreach (GeneratedMapModule module in moduleOrder)
            {
                if (omittedCorridorModules.Contains(module)) continue;

                LightZone zone = BuildLightZone(module, BuildLights(anchorsByModule[module]));
                spawnedZones.Add(zone);
                zonesByModule[module] = zone;

                if (safeZoneModules.Contains(module))
                {
                    safeZoneZones.Add(zone);
                }
                else
                {
                    shutdownCandidates.Add(zone);
                }
            }

            SafeZoneLightZones = safeZoneZones;

            if (safeZoneModules.Count == 0)
            {
                Debug.LogWarning(
                    $"Generated map (seed {data.Seed}) has no SafeZone-role module among its lit " +
                    "modules — no LightZone is exempt from shutdown.",
                    this);
            }

            Shuffle(shutdownCandidates, data.Seed);
            wiring.LightDirector.SetShutdownOrder(shutdownCandidates);
            CompleteBuild(data);
        }

        private void CompleteBuild(GeneratedMapRuntimeData data)
        {
            builtFor = data;
            BuildCompleted?.Invoke(data);
        }

        private List<Light> BuildLights(List<Transform> anchors)
        {
            var lights = new List<Light>(anchors.Count);
            foreach (Transform anchor in anchors)
            {
                Light light = anchor.GetComponentInChildren<Light>();
                if (light == null) light = BuildDefaultLight(anchor);
                lights.Add(light);
            }
            return lights;
        }

        private LightZone BuildLightZone(GeneratedMapModule module, List<Light> lights)
        {
            var go = new GameObject($"LightZone_{module.CatalogEntryId}_{module.PlacementId}");
            go.transform.SetParent(transform, false);
            LightZone zone = go.AddComponent<LightZone>();
            zone.Bind(lights.ToArray());
            zone.SetActive(true);
            return zone;
        }

        private Light BuildDefaultLight(Transform anchor)
        {
            var go = new GameObject("Light_Fallback");
            go.transform.SetParent(anchor, false);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = fallbackLightRange;
            light.intensity = fallbackLightIntensity;
            return light;
        }

        /// <summary>
        /// Picks which Corridor-category modules get no fixture at all (Room/Stair are never
        /// touched). DoorTransition modules are always omitted. Among the rest, a seed-shuffled
        /// greedy pass keeps a module only if it doesn't touch a module already kept — so no two
        /// adjacent corridor segments are ever both lit.
        /// </summary>
        private static HashSet<GeneratedMapModule> PickCorridorModulesToOmit(
            List<GeneratedMapModule> moduleOrder, int seed)
        {
            var omitted = new HashSet<GeneratedMapModule>();
            var candidates = new List<GeneratedMapModule>();

            foreach (GeneratedMapModule module in moduleOrder)
            {
                if (module.Category != MapModuleCategory.Corridor) continue;

                if (module.CatalogEntryId.Contains("DoorTransition"))
                {
                    omitted.Add(module);
                    continue;
                }

                candidates.Add(module);
            }

            // Distinct seed offset from the shutdown-order shuffle so the two selections don't correlate.
            Shuffle(candidates, seed + 1);

            var kept = new List<GeneratedMapModule>();
            foreach (GeneratedMapModule candidate in candidates)
            {
                bool touchesKeptModule = false;
                foreach (GeneratedMapModule keptModule in kept)
                {
                    if (!ModulesAreAdjacent(candidate, keptModule)) continue;
                    touchesKeptModule = true;
                    break;
                }

                if (touchesKeptModule)
                {
                    omitted.Add(candidate);
                }
                else
                {
                    kept.Add(candidate);
                }
            }

            return omitted;
        }

        /// <summary>True if both modules sit on the same floor and their grid footprints touch or overlap.</summary>
        private static bool ModulesAreAdjacent(GeneratedMapModule a, GeneratedMapModule b)
        {
            if (a.Origin.Floor != b.Origin.Floor) return false;

            GridSize2 sizeA = a.RotatedFootprintUnits;
            GridSize2 sizeB = b.RotatedFootprintUnits;

            bool touchesOrOverlapsX =
                a.Origin.X + sizeA.Width >= b.Origin.X && b.Origin.X + sizeB.Width >= a.Origin.X;
            bool touchesOrOverlapsZ =
                a.Origin.Z + sizeA.Depth >= b.Origin.Z && b.Origin.Z + sizeB.Depth >= a.Origin.Z;
            return touchesOrOverlapsX && touchesOrOverlapsZ;
        }

        // Fisher-Yates with a seed-derived RNG: same seed -> same result (TGD section 43).
        private static void Shuffle<T>(List<T> items, int seed)
        {
            var random = new System.Random(seed);
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        private void ClearSpawned()
        {
            foreach (LightZone zone in spawnedZones)
            {
                if (zone != null) Destroy(zone.gameObject);
            }
            spawnedZones.Clear();
            zonesByModule.Clear();
            SafeZoneLightZones = Array.Empty<LightZone>();
            builtFor = null;
        }
    }
}
