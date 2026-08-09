using System;
using System.Collections;
using System.Linq;
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
    public sealed class MapSafeZoneGateTests
    {
        private const string CatalogPath =
            "Assets/Gloomveil/MapGeneration/Data/GrayboxMapModuleCatalog.asset";

        [UnityTest]
        public IEnumerator OnMapReady_ReportsCompletedBuildAfterSafeZonesExist()
        {
            Fixture fixture = CreateFixture();
            GeneratedMapRuntimeData completedFor = null;
            int zonesVisibleInsideCallback = -1;

            try
            {
                fixture.Gate.BuildCompleted += data =>
                {
                    completedFor = data;
                    zonesVisibleInsideCallback = fixture.Gate.SpawnedSafeZones.Count;
                };

                fixture.Bootstrap.Generate(2024);

                Assert.That(completedFor, Is.SameAs(fixture.Registry.Current));
                Assert.That(fixture.Gate.IsBuiltFor(fixture.Registry.Current), Is.True);
                Assert.That(zonesVisibleInsideCallback, Is.EqualTo(fixture.Gate.SpawnedSafeZones.Count));
                Assert.That(zonesVisibleInsideCallback, Is.GreaterThan(0));
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator ImmediateRegeneration_BuildCompletedSeesOnlyNewMapSafeZones()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Bootstrap.Generate(2024);
                SafeZone[] previousZones = fixture.Gate.SpawnedSafeZones.ToArray();
                bool inspectedReplacement = false;

                fixture.Gate.BuildCompleted += data =>
                {
                    if (data.Seed != 1042) return;

                    inspectedReplacement = true;
                    Assert.That(fixture.SafeZoneSet.Items.Count,
                        Is.EqualTo(fixture.Gate.SpawnedSafeZones.Count));
                    foreach (SafeZone previous in previousZones)
                    {
                        Assert.That(fixture.SafeZoneSet.Items.Contains(previous), Is.False);
                    }
                };

                fixture.Bootstrap.Generate(1042);
                Assert.That(inspectedReplacement, Is.True);
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator RegistryClear_UnregistersAndRemovesSpawnedSafeZonesImmediately()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Bootstrap.Generate(2024);
                GeneratedMapRuntimeData published = fixture.Registry.Current;
                Assert.That(fixture.SafeZoneSet.Items, Is.Not.Empty);

                fixture.Registry.Clear();

                Assert.That(fixture.Gate.IsBuiltFor(published), Is.False);
                Assert.That(fixture.Gate.SpawnedSafeZones, Is.Empty);
                Assert.That(fixture.SafeZoneSet.Items, Is.Empty);
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator OnMapReady_NoSafeZoneAnchorsStillReportsCompletedBuild()
        {
            var registryObject = new GameObject("Registry");
            var generatedRoot = new GameObject("GeneratedMap");
            var gateObject = new GameObject("SafeZoneGate");
            GeneratedMapRegistry registry = registryObject.AddComponent<GeneratedMapRegistry>();
            MapSafeZoneGate gate = gateObject.AddComponent<MapSafeZoneGate>();
            SafeZoneRuntimeSet safeZoneSet = ScriptableObject.CreateInstance<SafeZoneRuntimeSet>();
            GeneratedMapRuntimeData data = CreateEmptyRuntimeData(generatedRoot);
            GeneratedMapRuntimeData completedFor = null;

            try
            {
                gate.Bind(registry, safeZoneSet);
                gate.BuildCompleted += completed => completedFor = completed;

                registry.Publish(data);

                Assert.That(completedFor, Is.SameAs(data));
                Assert.That(gate.IsBuiltFor(data), Is.True);
                Assert.That(gate.SpawnedSafeZones, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gateObject);
                UnityEngine.Object.DestroyImmediate(registryObject);
                UnityEngine.Object.DestroyImmediate(generatedRoot);
                UnityEngine.Object.DestroyImmediate(safeZoneSet);
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
                "empty-safe-zone-test");

        [UnityTest]
        public IEnumerator OnMapReady_SpawnsOneSafeZonePerAnchor_RegisteredInRuntimeSet()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Bootstrap.Generate(2024);

                Assert.That(fixture.Registry.IsReady, Is.True);
                int expectedCount = fixture.Registry.Current.SafeZoneAnchors.Count;
                Assert.That(expectedCount, Is.GreaterThan(0), "default config has safeZoneCount = 2");

                Assert.That(fixture.Gate.SpawnedSafeZones.Count, Is.EqualTo(expectedCount));
                Assert.That(fixture.SafeZoneSet.Items.Count, Is.EqualTo(expectedCount));

                foreach (SafeZone zone in fixture.Gate.SpawnedSafeZones)
                {
                    BoxCollider box = zone.GetComponent<BoxCollider>();
                    Assert.That(box, Is.Not.Null);
                    Assert.That(box.isTrigger, Is.True);
                    Assert.That(box.size.x, Is.GreaterThan(0f));
                    Assert.That(box.size.z, Is.GreaterThan(0f));
                    Assert.That(fixture.SafeZoneSet.Items, Does.Contain(zone));

                    GeneratedMapModule module = zone.GetComponentInParent<GeneratedMapModule>();
                    if (module == null)
                    {
                        // Fixture always resolves a parent module for generated SafeZone anchors —
                        // module-less fallback path is exercised elsewhere, not here.
                        continue;
                    }

                    float expectedWidth = module.RotatedFootprintUnits.Width * 4f;
                    float expectedDepth = module.RotatedFootprintUnits.Depth * 4f;
                    Assert.That(box.size.x, Is.EqualTo(expectedWidth).Within(0.01f),
                        "box width must match the module's rotated footprint, not a hardcoded/anchor-derived size");
                    Assert.That(box.size.z, Is.EqualTo(expectedDepth).Within(0.01f),
                        "box depth must match the module's rotated footprint, not a hardcoded/anchor-derived size");

                    float distanceFromModule = Vector3.Distance(zone.transform.position, module.transform.position);
                    Assert.That(distanceFromModule, Is.LessThan(0.01f),
                        "SafeZone box must be centred on the module's own transform, not the off-centre " +
                        "Anchor_SafeZone marker (which sits at local offset (0,0,-0.5) from the module root)");
                }
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        private static Fixture CreateFixture()
        {
#if UNITY_EDITOR
            MapModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<MapModuleCatalog>(CatalogPath);
#else
            MapModuleCatalog catalog = null;
#endif
            Assert.That(catalog, Is.Not.Null, CatalogPath);

            MapGenerationConfig config = ScriptableObject.CreateInstance<MapGenerationConfig>();
            config.ConfigureGeneratedContent(catalog, 2);

            var mapRoot = new GameObject("MapSafeZoneGateFixture_Map");
            GeneratedMapRegistry registry = mapRoot.AddComponent<GeneratedMapRegistry>();
            var surfaceObject = new GameObject("GeneratedGeometrySurface");
            surfaceObject.transform.SetParent(mapRoot.transform, false);
            var surface = surfaceObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            var navMeshController = mapRoot.AddComponent<GeneratedNavMeshController>();
            navMeshController.Configure(surface);
            var bootstrap = mapRoot.AddComponent<MapGenerationBootstrap>();
            bootstrap.Configure(config, navMeshController, registry);

            SafeZoneRuntimeSet safeZoneSet = ScriptableObject.CreateInstance<SafeZoneRuntimeSet>();

            var gateObject = new GameObject("MapSafeZoneGateFixture_Gate");
            MapSafeZoneGate gate = gateObject.AddComponent<MapSafeZoneGate>();
            gate.Bind(registry, safeZoneSet);

            return new Fixture(mapRoot, config, gateObject, safeZoneSet, bootstrap, registry, gate);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject mapRoot;
            private readonly MapGenerationConfig config;
            private readonly GameObject gateObject;
            private readonly SafeZoneRuntimeSet safeZoneSet;

            public MapGenerationBootstrap Bootstrap { get; }
            public GeneratedMapRegistry Registry { get; }
            public MapSafeZoneGate Gate { get; }
            public SafeZoneRuntimeSet SafeZoneSet => safeZoneSet;

            public Fixture(GameObject mapRoot, MapGenerationConfig config, GameObject gateObject,
                SafeZoneRuntimeSet safeZoneSet, MapGenerationBootstrap bootstrap,
                GeneratedMapRegistry registry, MapSafeZoneGate gate)
            {
                this.mapRoot = mapRoot;
                this.config = config;
                this.gateObject = gateObject;
                this.safeZoneSet = safeZoneSet;
                Bootstrap = bootstrap;
                Registry = registry;
                Gate = gate;
            }

            public void Dispose()
            {
                Bootstrap.ClearGeneratedMap();
                UnityEngine.Object.DestroyImmediate(gateObject);
                UnityEngine.Object.DestroyImmediate(mapRoot);
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(safeZoneSet);
            }
        }
    }
}
