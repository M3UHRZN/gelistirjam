using System;
using System.Collections.Generic;
using Game.Gameplay;
using Gloomveil.MapGeneration;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Builds lighting only for generated SafeZone modules. Each SafeZone's LightAnchors receive the
    /// Inspector-assigned prefab; authored lights already below those anchors are disabled so the
    /// selected prefab is the only active light source. When no prefab is assigned, a fallback point
    /// light is used. SafeZone zones are never registered for shutdown, so they remain active during
    /// fuse progression and Final Chase.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapLightGate : MonoBehaviour
    {
        [SerializeField] private GeneratedMapRegistry registry;

        [SerializeField, Tooltip("Prefab spawned at each generated SafeZone LightAnchor.")]
        private GameObject safeZoneLightPrefab;

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

                if (module.RoomRole != RoomRole.SafeZone) continue;

                if (!anchorsByModule.TryGetValue(module, out List<Transform> anchors))
                {
                    anchors = new List<Transform>();
                    anchorsByModule[module] = anchors;
                    moduleOrder.Add(module);
                }
                anchors.Add(anchor);
            }

            var safeZoneZones = new List<LightZone>();

            foreach (GeneratedMapModule module in moduleOrder)
            {
                LightZone zone = BuildLightZone(module, BuildLights(anchorsByModule[module]));
                spawnedZones.Add(zone);
                zonesByModule[module] = zone;
                safeZoneZones.Add(zone);
            }

            SafeZoneLightZones = safeZoneZones;

            if (moduleOrder.Count == 0)
            {
                Debug.LogWarning(
                    $"Generated map (seed {data.Seed}) has no SafeZone module with a Light anchor.",
                    this);
            }

            wiring.LightDirector.SetShutdownOrder(Array.Empty<LightZone>());
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
                if (safeZoneLightPrefab != null)
                {
                    foreach (Light authoredLight in anchor.GetComponentsInChildren<Light>(true))
                    {
                        authoredLight.enabled = false;
                    }

                    GameObject instance = Instantiate(safeZoneLightPrefab, anchor, false);
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    lights.AddRange(instance.GetComponentsInChildren<Light>(true));
                    continue;
                }

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
