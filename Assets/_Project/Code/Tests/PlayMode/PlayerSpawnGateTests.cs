using System;
using System.Collections;
using Game.Gameplay.Player;
using Gloomveil.MapGeneration;
using NUnit.Framework;
using Unity.AI.Navigation;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class PlayerSpawnGateTests
    {
        private const string CatalogPath =
            "Assets/Gloomveil/MapGeneration/Data/GrayboxMapModuleCatalog.asset";

        [UnityTest]
        public IEnumerator OnMapReady_TeleportsPlayerToStartAnchor()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Bootstrap.Generate(2024);
                yield return null;

                Assert.That(fixture.Registry.IsReady, Is.True);
                Transform startAnchor = fixture.Registry.Current.StartAnchor;
                Assert.That(startAnchor, Is.Not.Null);

                Vector3 playerPosition = fixture.PlayerRigidbody.position;
                Assert.That(
                    Vector3.Distance(playerPosition, startAnchor.position),
                    Is.LessThan(0.01f));
                Assert.That(
                    Mathf.Abs(
                        Mathf.DeltaAngle(
                            fixture.PlayerController.Yaw,
                            startAnchor.eulerAngles.y)),
                    Is.LessThan(0.01f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static Fixture CreateFixture()
        {
#if UNITY_EDITOR
            MapModuleCatalog catalog =
                AssetDatabase.LoadAssetAtPath<MapModuleCatalog>(CatalogPath);
#else
            MapModuleCatalog catalog = null;
#endif
            Assert.That(catalog, Is.Not.Null, CatalogPath);

            MapGenerationConfig config = ScriptableObject.CreateInstance<MapGenerationConfig>();
            config.ConfigureGeneratedContent(catalog, 2);

            var mapRoot = new GameObject("PlayerSpawnGateFixture_Map");
            GeneratedMapRegistry registry = mapRoot.AddComponent<GeneratedMapRegistry>();
            var surfaceObject = new GameObject("GeneratedGeometrySurface");
            surfaceObject.transform.SetParent(mapRoot.transform, false);
            NavMeshSurface surface = surfaceObject.AddComponent<NavMeshSurface>();
            GeneratedNavMeshController navMeshController =
                mapRoot.AddComponent<GeneratedNavMeshController>();
            navMeshController.Configure(surface);
            MapGenerationBootstrap bootstrap = mapRoot.AddComponent<MapGenerationBootstrap>();
            bootstrap.Configure(config, navMeshController, registry);

            var playerObject = new GameObject(
                "PlayerSpawnGateFixture_Player",
                typeof(Rigidbody),
                typeof(CapsuleCollider));
            CapsuleCollider capsule = playerObject.GetComponent<CapsuleCollider>();
            capsule.radius = 0.35f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            PlayerController playerController = playerObject.AddComponent<PlayerController>();
            Rigidbody playerRigidbody = playerObject.GetComponent<Rigidbody>();

            var gateObject = new GameObject("PlayerSpawnGateFixture_Gate");
            PlayerSpawnGate gate = gateObject.AddComponent<PlayerSpawnGate>();
            gate.Bind(registry, playerController);

            return new Fixture(
                mapRoot,
                config,
                playerObject,
                gateObject,
                bootstrap,
                registry,
                playerController,
                playerRigidbody);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject mapRoot;
            private readonly MapGenerationConfig config;
            private readonly GameObject playerObject;
            private readonly GameObject gateObject;

            public MapGenerationBootstrap Bootstrap { get; }
            public GeneratedMapRegistry Registry { get; }
            public PlayerController PlayerController { get; }
            public Rigidbody PlayerRigidbody { get; }

            public Fixture(
                GameObject mapRoot,
                MapGenerationConfig config,
                GameObject playerObject,
                GameObject gateObject,
                MapGenerationBootstrap bootstrap,
                GeneratedMapRegistry registry,
                PlayerController playerController,
                Rigidbody playerRigidbody)
            {
                this.mapRoot = mapRoot;
                this.config = config;
                this.playerObject = playerObject;
                this.gateObject = gateObject;
                Bootstrap = bootstrap;
                Registry = registry;
                PlayerController = playerController;
                PlayerRigidbody = playerRigidbody;
            }

            public void Dispose()
            {
                Bootstrap.ClearGeneratedMap();
                UnityEngine.Object.DestroyImmediate(gateObject);
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(mapRoot);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
