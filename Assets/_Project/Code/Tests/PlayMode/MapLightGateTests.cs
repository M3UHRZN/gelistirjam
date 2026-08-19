using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.World;
using Gloomveil.MapGeneration;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class MapLightGateTests
    {
        private const string CatalogPath =
            "Assets/Gloomveil/MapGeneration/Data/GrayboxMapModuleCatalog.asset";

        [UnityTest]
        public IEnumerator OnMapReady_ReportsCompletedBuildAndModuleLookupAfterZonesExist()
        {
            Fixture fixture = CreateFixture();
            GeneratedMapRuntimeData completedFor = null;
            int zonesVisibleInsideCallback = -1;

            try
            {
                fixture.Gate.BuildCompleted += data =>
                {
                    completedFor = data;
                    zonesVisibleInsideCallback = fixture.Gate.SpawnedLightZones.Count;
                };

                fixture.Bootstrap.Generate(2024);

                Assert.That(completedFor, Is.SameAs(fixture.Registry.Current));
                Assert.That(fixture.Gate.IsBuiltFor(fixture.Registry.Current), Is.True);
                Assert.That(zonesVisibleInsideCallback, Is.EqualTo(fixture.Gate.SpawnedLightZones.Count));
                Assert.That(zonesVisibleInsideCallback, Is.GreaterThan(0));

                int mappedModuleCount = 0;
                foreach (GeneratedMapModule module in fixture.Registry.Current.Modules)
                {
                    if (!fixture.Gate.TryGetLightZone(module, out LightZone zone)) continue;
                    mappedModuleCount++;
                    Assert.That(fixture.Gate.SpawnedLightZones, Does.Contain(zone));
                }

                Assert.That(mappedModuleCount, Is.EqualTo(fixture.Gate.SpawnedLightZones.Count));
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator OnMapReady_NoLightAnchorsStillReportsCompletedBuild()
        {
            var registryObject = new GameObject("Registry");
            var generatedRoot = new GameObject("GeneratedMap");
            var gateObject = new GameObject("LightGate");
            var wiringObject = new GameObject("Wiring");
            GeneratedMapRegistry registry = registryObject.AddComponent<GeneratedMapRegistry>();
            MapLightGate gate = gateObject.AddComponent<MapLightGate>();
            MainSceneWiring wiring = wiringObject.AddComponent<MainSceneWiring>();
            GeneratedMapRuntimeData data = CreateEmptyRuntimeData(generatedRoot);
            GeneratedMapRuntimeData completedFor = null;

            try
            {
                gate.Bind(registry, wiring);
                gate.BuildCompleted += completed => completedFor = completed;

                registry.Publish(data);

                Assert.That(completedFor, Is.SameAs(data));
                Assert.That(gate.IsBuiltFor(data), Is.True);
                Assert.That(gate.SpawnedLightZones, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gateObject);
                UnityEngine.Object.DestroyImmediate(wiringObject);
                UnityEngine.Object.DestroyImmediate(registryObject);
                UnityEngine.Object.DestroyImmediate(generatedRoot);
            }

            yield break;
        }

        private static GeneratedMapRuntimeData CreateEmptyRuntimeData(GameObject root) =>
            new GeneratedMapRuntimeData(
                root,
                Array.Empty<GeneratedMapModule>(),
                Array.Empty<Transform>(),
                Array.Empty<Transform>(),
                Array.Empty<Transform>(),
                Array.Empty<Transform>(),
                Array.Empty<Transform>(),
                null,
                null,
                null,
                null,
                1042,
                1,
                1,
                2,
                "empty-light-test");

        [UnityTest]
        public IEnumerator OnMapReady_BuildsLightZonesOnlyForSafeZoneModules()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Bootstrap.Generate(2024);

                List<GeneratedMapModule> safeZoneModules = fixture.Registry.Current.Modules
                    .Where(module => module.RoomRole == RoomRole.SafeZone)
                    .ToList();
                Assert.That(safeZoneModules.Count, Is.GreaterThan(0),
                    "test setup sanity: generated map must contain SafeZone modules");
                Assert.That(fixture.Gate.SpawnedLightZones.Count, Is.EqualTo(safeZoneModules.Count));
                Assert.That(fixture.Gate.SafeZoneLightZones.Count, Is.EqualTo(safeZoneModules.Count));

                foreach (GeneratedMapModule module in fixture.Registry.Current.Modules)
                {
                    bool hasZone = fixture.Gate.TryGetLightZone(module, out LightZone zone);
                    bool shouldHaveZone = module.RoomRole == RoomRole.SafeZone;
                    Assert.That(hasZone, Is.EqualTo(shouldHaveZone), module.name);
                    if (shouldHaveZone)
                    {
                        Assert.That(fixture.Gate.SpawnedLightZones, Does.Contain(zone));
                    }
                }

                fixture.LightDirector.ShutdownAll();

                foreach (LightZone zone in fixture.Gate.SpawnedLightZones)
                {
                    Assert.That(zone.State, Is.EqualTo(LightZoneState.Active),
                        "SafeZone lights must survive Final Chase shutdown");
                }
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator OnMapReady_InstantiatesAssignedPrefabAtEverySafeZoneLightAnchor()
        {
            var lightPrefab = new GameObject("SafeZoneLightPrefab");
            Light prefabLight = lightPrefab.AddComponent<Light>();
            prefabLight.range = 13.5f;
            Fixture fixture = CreateFixture(lightPrefab);

            try
            {
                fixture.Bootstrap.Generate(2024);

                List<Transform> safeZoneLightAnchors = fixture.Registry.Current.LightAnchors
                    .Where(anchor => anchor.GetComponentInParent<GeneratedMapModule>()?.RoomRole == RoomRole.SafeZone)
                    .ToList();
                Assert.That(safeZoneLightAnchors.Count, Is.GreaterThan(0),
                    "test setup sanity: generated SafeZones must contain Light anchors");

                foreach (Transform anchor in safeZoneLightAnchors)
                {
                    Light[] anchorLights = anchor.GetComponentsInChildren<Light>(true);
                    string lightDetails = string.Join(", ", anchorLights.Select(light =>
                        $"{light.gameObject.name} parent={light.transform.parent?.name} range={light.range}"));
                    Light[] spawnedPrefabLights = anchorLights
                        .Where(light => light.gameObject.name == "SafeZoneLightPrefab(Clone)")
                        .ToArray();
                    Assert.That(spawnedPrefabLights.Length, Is.EqualTo(1), $"{anchor.name}: {lightDetails}");

                    Light spawnedLight = spawnedPrefabLights[0];
                    Assert.That(spawnedLight.enabled, Is.True);
                    Assert.That(spawnedLight.range, Is.EqualTo(13.5f));
                    Assert.That(spawnedLight.transform.localPosition, Is.EqualTo(Vector3.zero));
                    Assert.That(spawnedLight.transform.localRotation, Is.EqualTo(Quaternion.identity));

                    foreach (Light authoredLight in anchorLights.Where(light => light != spawnedLight))
                    {
                        Assert.That(authoredLight.enabled, Is.False,
                            $"the selected SafeZone light prefab must be the only active light: {lightDetails}");
                    }
                }
            }
            finally
            {
                fixture.Dispose();
                UnityEngine.Object.DestroyImmediate(lightPrefab);
            }

            yield break;
        }

        private static Fixture CreateFixture(GameObject lightPrefab = null)
        {
#if UNITY_EDITOR
            MapModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<MapModuleCatalog>(CatalogPath);
#else
            MapModuleCatalog catalog = null;
#endif
            Assert.That(catalog, Is.Not.Null, CatalogPath);

            MapGenerationConfig config = ScriptableObject.CreateInstance<MapGenerationConfig>();
            config.ConfigureGeneratedContent(catalog, 2);

            var mapRoot = new GameObject("MapLightGateFixture_Map");
            GeneratedMapRegistry registry = mapRoot.AddComponent<GeneratedMapRegistry>();
            var surfaceObject = new GameObject("GeneratedGeometrySurface");
            surfaceObject.transform.SetParent(mapRoot.transform, false);
            var surface = surfaceObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            var navMeshController = mapRoot.AddComponent<GeneratedNavMeshController>();
            navMeshController.Configure(surface);
            var bootstrap = mapRoot.AddComponent<MapGenerationBootstrap>();
            bootstrap.Configure(config, navMeshController, registry);

            var gateObject = new GameObject("MapLightGateFixture_Gate");
            MapLightGate gate = gateObject.AddComponent<MapLightGate>();
#if UNITY_EDITOR
            if (lightPrefab != null)
            {
                var serializedGate = new SerializedObject(gate);
                SerializedProperty prefabProperty = serializedGate.FindProperty("safeZoneLightPrefab");
                Assert.That(prefabProperty, Is.Not.Null,
                    "MapLightGate must expose a serialized SafeZone light prefab field");
                prefabProperty.objectReferenceValue = lightPrefab;
                serializedGate.ApplyModifiedPropertiesWithoutUndo();
            }
#endif
            var wiringObject = new GameObject("Wiring");
            MainSceneWiring wiring = wiringObject.AddComponent<MainSceneWiring>();
            gate.Bind(registry, wiring);

            return new Fixture(mapRoot, config, gateObject, wiringObject, bootstrap, registry, gate, wiring);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject mapRoot;
            private readonly MapGenerationConfig config;
            private readonly GameObject gateObject;
            private readonly GameObject wiringObject;
            private readonly MainSceneWiring wiring;

            public MapGenerationBootstrap Bootstrap { get; }
            public GeneratedMapRegistry Registry { get; }
            public MapLightGate Gate { get; }
            public LightDirector LightDirector => wiring.LightDirector;

            public Fixture(GameObject mapRoot, MapGenerationConfig config, GameObject gateObject,
                GameObject wiringObject, MapGenerationBootstrap bootstrap, GeneratedMapRegistry registry,
                MapLightGate gate, MainSceneWiring wiring)
            {
                this.mapRoot = mapRoot;
                this.config = config;
                this.gateObject = gateObject;
                this.wiringObject = wiringObject;
                this.wiring = wiring;
                Bootstrap = bootstrap;
                Registry = registry;
                Gate = gate;
            }

            public void Dispose()
            {
                Bootstrap.ClearGeneratedMap();
                UnityEngine.Object.DestroyImmediate(gateObject);
                UnityEngine.Object.DestroyImmediate(wiringObject);
                UnityEngine.Object.DestroyImmediate(mapRoot);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
