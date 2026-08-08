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
        public IEnumerator OnMapReady_BuildsOneZonePerLitModule_AndExemptsAllSafeZonesFromShutdown()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Bootstrap.Generate(2024);

                Assert.That(fixture.Gate.SpawnedLightZones.Count, Is.GreaterThan(0));
                int expectedSafeZoneCount = fixture.Registry.Current.SafeZoneAnchors.Count;
                Assert.That(fixture.Gate.SafeZoneLightZones.Count, Is.EqualTo(expectedSafeZoneCount),
                    "every generated SafeZone-role room carries a LightAnchor, so every SafeZone must have a LightZone");

                foreach (LightZone safeZoneZone in fixture.Gate.SafeZoneLightZones)
                {
                    Assert.That(fixture.Gate.SpawnedLightZones, Does.Contain(safeZoneZone));
                }

                fixture.LightDirector.ShutdownAll();

                foreach (LightZone zone in fixture.Gate.SafeZoneLightZones)
                {
                    Assert.That(zone.State, Is.EqualTo(LightZoneState.Active),
                        "every SafeZone's own LightZone must survive Final Chase shutdown (TGD section 38)");
                }

                foreach (LightZone zone in fixture.Gate.SpawnedLightZones)
                {
                    if (fixture.Gate.SafeZoneLightZones.Contains(zone)) continue;
                    Assert.That(zone.State, Is.EqualTo(LightZoneState.Disabled));
                }
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator OnMapReady_OmitsDoorTransitionEntirely_NeverKeepsTwoAdjacentCorridorFixtures_NeverThinsRoomOrStair()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Bootstrap.Generate(2024);

                var modulesWithAnchors = new List<GeneratedMapModule>();
                foreach (Transform anchor in fixture.Registry.Current.LightAnchors)
                {
                    if (anchor == null) continue;
                    GeneratedMapModule module = anchor.GetComponentInParent<GeneratedMapModule>();
                    if (module != null && !modulesWithAnchors.Contains(module)) modulesWithAnchors.Add(module);
                }

                var doorTransitionModules = modulesWithAnchors
                    .Where(m => m.Category == MapModuleCategory.Corridor && m.CatalogEntryId.Contains("DoorTransition"))
                    .ToList();
                var otherCorridorModules = modulesWithAnchors
                    .Where(m => m.Category == MapModuleCategory.Corridor && !m.CatalogEntryId.Contains("DoorTransition"))
                    .ToList();
                var nonCorridorModules = modulesWithAnchors.Where(m => m.Category != MapModuleCategory.Corridor).ToList();

                // A real generated map must actually exercise all three groups, otherwise the
                // assertions below would trivially pass on an empty set.
                Assert.That(doorTransitionModules.Count, Is.GreaterThan(0), "test setup sanity: expected DoorTransition modules in the generated map");
                Assert.That(otherCorridorModules.Count, Is.GreaterThan(0), "test setup sanity: expected non-DoorTransition corridor modules");
                Assert.That(nonCorridorModules.Count, Is.GreaterThan(0), "test setup sanity: expected Room/Stair modules");

                var spawnedZoneNames = new HashSet<string>();
                foreach (LightZone zone in fixture.Gate.SpawnedLightZones) spawnedZoneNames.Add(zone.name);

                bool WasSpawned(GeneratedMapModule m) =>
                    spawnedZoneNames.Contains($"LightZone_{m.CatalogEntryId}_{m.PlacementId}");

                Assert.That(doorTransitionModules.Any(WasSpawned), Is.False,
                    "DoorTransition (the room<->corridor connector) must never keep a fixture");

                int nonCorridorZonesSpawned = nonCorridorModules.Count(WasSpawned);
                Assert.That(nonCorridorZonesSpawned, Is.EqualTo(nonCorridorModules.Count),
                    "every Room/Stair category module (fuse rooms, exit, safezones, start, stairs) must keep its fixture");

                var litCorridorModules = otherCorridorModules.Where(WasSpawned).ToList();
                Assert.That(litCorridorModules.Count, Is.GreaterThan(0),
                    "test setup sanity: expected at least one non-DoorTransition corridor to keep its fixture");

                for (int i = 0; i < litCorridorModules.Count; i++)
                {
                    for (int j = i + 1; j < litCorridorModules.Count; j++)
                    {
                        Assert.That(AreAdjacent(litCorridorModules[i], litCorridorModules[j]), Is.False,
                            $"lit corridor modules {litCorridorModules[i].name} and {litCorridorModules[j].name} " +
                            "are spatially adjacent -- no two adjacent corridor segments should both keep a fixture");
                    }
                }
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        /// <summary>Mirrors MapLightGate's private adjacency test, kept independent here on purpose
        /// (a bug in the production check should not also be baked into its own test's oracle).</summary>
        private static bool AreAdjacent(GeneratedMapModule a, GeneratedMapModule b)
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

        [UnityTest]
        public IEnumerator OnMapReady_SameSeedProducesSameShutdownOrder_DifferentSeedProducesDifferentOrder()
        {
            Fixture fixtureA = CreateFixture();
            Fixture fixtureB = CreateFixture();
            Fixture fixtureC = CreateFixture();

            try
            {
                fixtureA.Bootstrap.Generate(2024);
                fixtureB.Bootstrap.Generate(2024);
                fixtureC.Bootstrap.Generate(1234);

                string[] orderA = ZoneNamesExcludingSafeZone(fixtureA.Gate);
                string[] orderB = ZoneNamesExcludingSafeZone(fixtureB.Gate);
                string[] orderC = ZoneNamesExcludingSafeZone(fixtureC.Gate);

                Assert.That(orderA, Is.EqualTo(orderB), "same seed must produce the same shutdown order");
                Assert.That(orderA, Is.Not.EqualTo(orderC), "different seeds should produce different shutdown orders");
            }
            finally
            {
                fixtureA.Dispose();
                fixtureB.Dispose();
                fixtureC.Dispose();
            }

            yield break;
        }

        private static string[] ZoneNamesExcludingSafeZone(MapLightGate gate) =>
            gate.SpawnedLightZones
                .Where(zone => !gate.SafeZoneLightZones.Contains(zone))
                .Select(zone => zone.name)
                .ToArray();

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
