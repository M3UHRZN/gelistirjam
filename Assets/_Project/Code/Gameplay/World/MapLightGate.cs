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
        private bool subscribed;

        public IReadOnlyList<LightZone> SpawnedLightZones => spawnedZones;
        public IReadOnlyList<LightZone> SafeZoneLightZones { get; private set; } = Array.Empty<LightZone>();

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
                return;
            }

            var moduleOrder = new List<GeneratedMapModule>();
            var lightsByModule = new Dictionary<GeneratedMapModule, List<Light>>();
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

                Light light = anchor.GetComponentInChildren<Light>();
                if (light == null) light = BuildDefaultLight(anchor);

                if (!lightsByModule.TryGetValue(module, out List<Light> lights))
                {
                    lights = new List<Light>();
                    lightsByModule[module] = lights;
                    moduleOrder.Add(module);
                }
                lights.Add(light);

                if (module.RoomRole == RoomRole.SafeZone) safeZoneModules.Add(module);
            }

            var shutdownCandidates = new List<LightZone>();
            var safeZoneZones = new List<LightZone>();

            foreach (GeneratedMapModule module in moduleOrder)
            {
                LightZone zone = BuildLightZone(module, lightsByModule[module]);
                spawnedZones.Add(zone);

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

        // Fisher-Yates with a seed-derived RNG: same seed -> same shutdown order (TGD section 43).
        private static void Shuffle(List<LightZone> zones, int seed)
        {
            var random = new System.Random(seed);
            for (int i = zones.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (zones[i], zones[j]) = (zones[j], zones[i]);
            }
        }

        private void ClearSpawned()
        {
            foreach (LightZone zone in spawnedZones)
            {
                if (zone != null) Destroy(zone.gameObject);
            }
            spawnedZones.Clear();
            SafeZoneLightZones = Array.Empty<LightZone>();
        }
    }
}
