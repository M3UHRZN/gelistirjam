using System;
using System.Collections;
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
