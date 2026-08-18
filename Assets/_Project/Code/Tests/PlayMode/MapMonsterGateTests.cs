using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Gameplay.Audio;
using Game.Gameplay.Monster;
using Game.Gameplay.Objectives;
using Game.Gameplay.Player;
using Game.Gameplay.World;
using Gloomveil.MapGeneration;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

namespace Game.Gameplay.Tests
{
    public sealed class MapMonsterGateTests
    {
        private const string CatalogPath =
            "Assets/Gloomveil/MapGeneration/Data/GrayboxMapModuleCatalog.asset";
        private const string MonsterControllerPath =
            "Assets/_Project/Animations/MonsterAnimator.controller";
        private static readonly FloorSeedCase[] FloorSeedCases =
        {
            new FloorSeedCase(2, 1042),
            new FloorSeedCase(3, 1042),
            new FloorSeedCase(4, 1042),
            new FloorSeedCase(2, 2024),
        };

        [UnityTest]
        public IEnumerator GeneratedFloorCase_SpawnsOneReachableDeterministicMonster(
            [ValueSource(nameof(FloorSeedCases))] FloorSeedCase testCase)
        {
            Vector3 firstPosition;
            using (var first = new Fixture(floorCount: testCase.FloorCount))
            {
                first.Bootstrap.Generate(testCase.Seed);
                AssertValidSpawn(first, testCase);
                firstPosition = first.MonsterGate.SpawnedBrain.transform.position;
            }

            yield return null;

            using (var second = new Fixture(floorCount: testCase.FloorCount))
            {
                second.Bootstrap.Generate(testCase.Seed);
                AssertValidSpawn(second, testCase);
                Assert.That(Vector3.Distance(
                    firstPosition,
                    second.MonsterGate.SpawnedBrain.transform.position), Is.LessThanOrEqualTo(0.01f));
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator OnMapReady_WaitsForBothLightAndSafeZoneBuilds()
        {
            using (var fixture = new Fixture(lightGateActive: false, safeZoneGateActive: true))
            {
                fixture.Bootstrap.Generate(2024);
                Assert.That(fixture.MonsterGate.SpawnedBrain, Is.Null);

                fixture.LightGate.gameObject.SetActive(true);
                Assert.That(fixture.MonsterGate.SpawnedBrain, Is.Not.Null);
                Assert.That(fixture.MonsterGate.SpawnCount, Is.EqualTo(1));
            }

            yield return null;

            using (var fixture = new Fixture(lightGateActive: true, safeZoneGateActive: false))
            {
                fixture.Bootstrap.Generate(2024);
                Assert.That(fixture.MonsterGate.SpawnedBrain, Is.Null);

                fixture.SafeZoneGate.gameObject.SetActive(true);
                Assert.That(fixture.MonsterGate.SpawnedBrain, Is.Not.Null);
                Assert.That(fixture.MonsterGate.SpawnCount, Is.EqualTo(1));
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator FinalChaseBeforeMapPublication_SpawnsActiveAggressiveMonster()
        {
            using var fixture = new Fixture();
            fixture.Flow.ChangeState(fixture.Flow.FinalChaseState);

            fixture.Bootstrap.Generate(2024);

            MonsterBrain brain = fixture.MonsterGate.SpawnedBrain;
            Assert.That(brain, Is.Not.Null);
            Assert.That(brain.gameObject.activeSelf, Is.True);
            Assert.That(brain.IsRelentless, Is.True);
            Assert.That(brain.CurrentStateName, Is.EqualTo(nameof(AggressiveState)));
            yield break;
        }

        [UnityTest]
        public IEnumerator ContactKillRequiresThreeDimensionalProximity()
        {
            using var fixture = new Fixture();
            fixture.Bootstrap.Generate(2024);
            MonsterBrain brain = fixture.MonsterGate.SpawnedBrain;
            bool killRaised = false;
            brain.OnKill += () => killRaised = true;

            fixture.PlayerTransform.position = brain.transform.position + Vector3.up * 4f;
            yield return null;

            Assert.That(killRaised, Is.False, "a player on the floor above is not in physical contact");
            Assert.That(brain.CurrentStateName, Is.Not.EqualTo(nameof(KillState)));

            fixture.PlayerTransform.position = brain.transform.position;
            yield return null;

            Assert.That(killRaised, Is.True, "same-floor physical contact must still kill");
            Assert.That(brain.CurrentStateName, Is.EqualTo(nameof(KillState)));

            Animator animator = brain.GetComponentInChildren<Animator>();
            bool attackEntered = false;
            for (int frame = 0; frame < 10 && !attackEntered; frame++)
            {
                yield return null;
                attackEntered = animator.GetCurrentAnimatorStateInfo(0).IsName("Attack")
                    || animator.GetNextAnimatorStateInfo(0).IsName("Attack");
            }
            Assert.That(attackEntered, Is.True,
                "the authoritative kill event must trigger the attack animation");
        }

        [UnityTest]
        public IEnumerator GeneratedMap_ConvertsEveryStalkPointAndWiresAReachableMonster()
        {
            using var fixture = new Fixture();
            fixture.Bootstrap.Generate(2024);
            GeneratedMapRuntimeData data = fixture.Registry.Current;
            MonsterBrain brain = fixture.MonsterGate.SpawnedBrain;

            Assert.That(brain, Is.Not.Null);
            Assert.That(fixture.MonsterGate.SpawnedStalkPoints.Count, Is.EqualTo(data.StalkPoints.Count));
            Assert.That(fixture.StalkPoints.Items.Count, Is.EqualTo(data.StalkPoints.Count));
            Assert.That(brain.StalkPoints, Is.SameAs(fixture.StalkPoints));
            Assert.That(brain.Perception.HasVelocitySource, Is.True);
            Assert.That(brain.VisionModel, Is.SameAs(fixture.VisionModel));
            Assert.That(fixture.Wiring.BoundMonster, Is.SameAs(brain));
            Assert.That(fixture.MonsterGate.SelectedSpawnPathDistance, Is.GreaterThan(0f));

            StalkPoint litPoint = fixture.MonsterGate.SpawnedStalkPoints.FirstOrDefault(point =>
            {
                GeneratedMapModule module = point.GetComponentInParent<GeneratedMapModule>();
                return fixture.LightGate.TryGetLightZone(module, out _);
            });
            Assert.That(litPoint, Is.Not.Null, "fixture should include a stalk point inside a lit module");
            Assert.That(litPoint.DarknessLevel, Is.EqualTo(0f));

            StalkPoint omittedPoint = fixture.MonsterGate.SpawnedStalkPoints.FirstOrDefault(point =>
            {
                GeneratedMapModule module = point.GetComponentInParent<GeneratedMapModule>();
                return module != null && !fixture.LightGate.TryGetLightZone(module, out _);
            });
            Assert.That(omittedPoint, Is.Not.Null, "fixture should include a stalk point inside an omitted-light module");
            Assert.That(omittedPoint.DarknessLevel, Is.EqualTo(1f));

            NavMeshAgent agent = brain.GetComponent<NavMeshAgent>();
            Assert.That(agent.isOnNavMesh, Is.True);
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(
                data.StartAnchor.position,
                brain.transform.position,
                NavMesh.AllAreas,
                path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));

            yield break;
        }

        [UnityTest]
        public IEnumerator BelowMinimumSpawnSelection_LogsFallbackWarning()
        {
            using var fixture = new Fixture();
            FieldInfo distanceField = typeof(MapMonsterGate).GetField(
                "minimumInitialPathDistance",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(distanceField, Is.Not.Null);
            distanceField.SetValue(fixture.MonsterGate, 10000f);

            LogAssert.Expect(LogType.Warning, new Regex(
                "Generated monster spawn fallback for seed 2024: OrdinaryFarthest.*10000[,.]0m minimum"));
            fixture.Bootstrap.Generate(2024);

            Assert.That(fixture.MonsterGate.SpawnedBrain, Is.Not.Null);
            yield break;
        }

        [UnityTest]
        public IEnumerator ModulelessStalkAnchor_LogsWarningAndUsesDarknessFallback()
        {
            using var fixture = new Fixture(lightGateActive: false);
            fixture.Bootstrap.Generate(2024);
            Transform anchor = fixture.Registry.Current.StalkPoints[0];
            anchor.SetParent(fixture.Registry.Current.Root.transform, true);

            LogAssert.Expect(LogType.Warning,
                $"Generated stalk anchor '{anchor.name}' for seed 2024 has no parent " +
                "GeneratedMapModule; using full-darkness fallback.");
            fixture.LightGate.gameObject.SetActive(true);

            StalkPoint converted = anchor.GetComponent<StalkPoint>();
            Assert.That(converted, Is.Not.Null);
            Assert.That(converted.DarknessLevel, Is.EqualTo(1f));
            yield break;
        }

        [UnityTest]
        public IEnumerator SameSeed_SelectsSameSpawnPositionAcrossFreshBuilds()
        {
            Vector3 firstPosition;
            using (var first = new Fixture())
            {
                first.Bootstrap.Generate(2024);
                Assert.That(first.MonsterGate.SpawnedBrain, Is.Not.Null);
                firstPosition = first.MonsterGate.SpawnedBrain.transform.position;
            }

            yield return null;

            using (var second = new Fixture())
            {
                second.Bootstrap.Generate(2024);
                Assert.That(second.MonsterGate.SpawnedBrain, Is.Not.Null);
                Assert.That(second.MonsterGate.SpawnedBrain.transform.position,
                    Is.EqualTo(firstPosition).Using(Vector3ComparerWithEqualsOperator.Instance));
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator RegistryClear_CleansSpawnedComponentsAndAllowsSingleRegeneration()
        {
            using var fixture = new Fixture();
            fixture.Bootstrap.Generate(2024);
            Transform firstAnchor = fixture.Registry.Current.StalkPoints[0];

            fixture.Registry.Clear();

            Assert.That(fixture.MonsterGate.SpawnedBrain, Is.Null);
            Assert.That(fixture.MonsterGate.SpawnCount, Is.Zero);
            Assert.That(fixture.MonsterGate.SpawnedStalkPoints, Is.Empty);
            Assert.That(fixture.StalkPoints.Items, Is.Empty);
            Assert.That(fixture.Wiring.BoundMonster, Is.Null);

            yield return null;
            Assert.That(firstAnchor.GetComponent<StalkPoint>(), Is.Null);

            fixture.Bootstrap.Generate(2024);
            Assert.That(fixture.MonsterGate.SpawnedBrain, Is.Not.Null);
            Assert.That(fixture.MonsterGate.SpawnCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BootstrapClear_CleansMonsterStalkRegistryAndWiring()
        {
            using var fixture = new Fixture();
            fixture.Bootstrap.Generate(2024);
            Assert.That(fixture.MonsterGate.SpawnedBrain, Is.Not.Null);

            fixture.Bootstrap.ClearGeneratedMap();

            Assert.That(fixture.MonsterGate.SpawnedBrain, Is.Null);
            Assert.That(fixture.MonsterGate.SpawnedStalkPoints, Is.Empty);
            Assert.That(fixture.StalkPoints.Items, Is.Empty);
            Assert.That(fixture.Wiring.BoundMonster, Is.Null);

            yield return null;
        }

        [UnityTest]
        public IEnumerator DisableThenRegistryClear_ReenableReconcilesStaleMonster()
        {
            using var fixture = new Fixture();
            fixture.Bootstrap.Generate(2024);
            Assert.That(fixture.MonsterGate.SpawnedBrain, Is.Not.Null);

            fixture.MonsterGate.gameObject.SetActive(false);
            fixture.Registry.Clear();
            fixture.MonsterGate.gameObject.SetActive(true);

            Assert.That(fixture.MonsterGate.SpawnedBrain, Is.Null);
            Assert.That(fixture.MonsterGate.SpawnCount, Is.Zero);
            Assert.That(fixture.MonsterGate.SpawnedStalkPoints, Is.Empty);
            Assert.That(fixture.StalkPoints.Items, Is.Empty);
            Assert.That(fixture.Wiring.BoundMonster, Is.Null);
            yield break;
        }

        private static void AssertValidSpawn(Fixture fixture, FloorSeedCase testCase)
        {
            Assert.That(fixture.Registry.IsReady, Is.True, testCase.ToString());
            Assert.That(fixture.Registry.Current.FloorCount, Is.EqualTo(testCase.FloorCount));
            Assert.That(fixture.MonsterGate.SpawnedBrain, Is.Not.Null, testCase.ToString());
            Assert.That(fixture.MonsterGate.SpawnCount, Is.EqualTo(1), testCase.ToString());
            Assert.That(fixture.MonsterGate.SpawnedStalkPoints, Is.Not.Empty, testCase.ToString());

            NavMeshAgent agent = fixture.MonsterGate.SpawnedBrain.GetComponent<NavMeshAgent>();
            Assert.That(agent.isOnNavMesh, Is.True, testCase.ToString());
            Assert.That(fixture.MonsterGate.SelectedSpawnPathDistance, Is.GreaterThan(0f), testCase.ToString());

            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(
                fixture.Registry.Current.StartAnchor.position,
                fixture.MonsterGate.SpawnedBrain.transform.position,
                NavMesh.AllAreas,
                path), Is.True, testCase.ToString());
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), testCase.ToString());
        }

        public readonly struct FloorSeedCase
        {
            public FloorSeedCase(int floorCount, int seed)
            {
                FloorCount = floorCount;
                Seed = seed;
            }

            public int FloorCount { get; }
            public int Seed { get; }
            public override string ToString() => $"floors={FloorCount}, seed={Seed}";
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject mapRoot;
            private readonly GameObject lightGateObject;
            private readonly GameObject safeZoneGateObject;
            private readonly GameObject monsterGateObject;
            private readonly GameObject wiringObject;
            private readonly GameObject coreRoot;
            private readonly GameObject player;
            private readonly GameObject monsterPrefabRoot;
            private readonly MapGenerationConfig mapConfig;
            private readonly MonsterConfig monsterConfig;
            private readonly SafeZoneRuntimeSet safeZones;

            public Fixture(
                bool lightGateActive = true,
                bool safeZoneGateActive = true,
                int floorCount = 2)
            {
#if UNITY_EDITOR
                MapModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<MapModuleCatalog>(CatalogPath);
#else
                MapModuleCatalog catalog = null;
#endif
                Assert.That(catalog, Is.Not.Null, CatalogPath);

                mapConfig = ScriptableObject.CreateInstance<MapGenerationConfig>();
                mapConfig.ConfigureGeneratedContent(catalog, floorCount);

                mapRoot = new GameObject("MapMonsterGateFixture_Map");
                Registry = mapRoot.AddComponent<GeneratedMapRegistry>();
                var surfaceObject = new GameObject("GeneratedGeometrySurface");
                surfaceObject.transform.SetParent(mapRoot.transform, false);
                var surface = surfaceObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
                var navMeshController = mapRoot.AddComponent<GeneratedNavMeshController>();
                navMeshController.Configure(surface);
                Bootstrap = mapRoot.AddComponent<MapGenerationBootstrap>();
                Bootstrap.Configure(mapConfig, navMeshController, Registry);

                coreRoot = new GameObject("Core");
                Flow = coreRoot.AddComponent<GameFlowController>();
                ObjectiveManager objectives = coreRoot.AddComponent<ObjectiveManager>();
                MapExitController exit = coreRoot.AddComponent<MapExitController>();
                exit.Bind(Registry, Flow);
                FuseMiniGameController miniGame = coreRoot.AddComponent<FuseMiniGameController>();

                wiringObject = new GameObject("Wiring");
                Wiring = wiringObject.AddComponent<MainSceneWiring>();
                Wiring.Bind(Flow, objectives, exit, miniGame);

                safeZones = ScriptableObject.CreateInstance<SafeZoneRuntimeSet>();
                StalkPoints = ScriptableObject.CreateInstance<StalkPointRuntimeSet>();

                lightGateObject = new GameObject("LightGate");
                lightGateObject.SetActive(lightGateActive);
                LightGate = lightGateObject.AddComponent<MapLightGate>();
                LightGate.Bind(Registry, Wiring);

                safeZoneGateObject = new GameObject("SafeZoneGate");
                safeZoneGateObject.SetActive(safeZoneGateActive);
                SafeZoneGate = safeZoneGateObject.AddComponent<MapSafeZoneGate>();
                SafeZoneGate.Bind(Registry, safeZones);

                player = new GameObject("Player");
                player.SetActive(false);
                PlayerController playerController = player.AddComponent<PlayerController>();
                VisionModel = player.AddComponent<PlayerVisionModel>();
                var cameraObject = new GameObject("PlayerCamera");
                cameraObject.transform.SetParent(player.transform, false);
                cameraObject.AddComponent<Camera>();
                PlayerGazeDetector gazeDetector = cameraObject.AddComponent<PlayerGazeDetector>();

                monsterPrefabRoot = BuildMonsterPrefab(out MonsterRuntimeRig monsterPrefab);
                monsterConfig = ScriptableObject.CreateInstance<MonsterConfig>();

                monsterGateObject = new GameObject("MonsterGate");
                MonsterGate = monsterGateObject.AddComponent<MapMonsterGate>();
                MonsterGate.Bind(
                    Registry,
                    monsterPrefab,
                    monsterConfig,
                    playerController,
                    gazeDetector,
                    VisionModel,
                    safeZones,
                    StalkPoints,
                    LightGate,
                    SafeZoneGate,
                    Wiring);
            }

            public MapGenerationBootstrap Bootstrap { get; }
            public GeneratedMapRegistry Registry { get; }
            public GameFlowController Flow { get; }
            public Transform PlayerTransform => player.transform;
            public MapLightGate LightGate { get; }
            public MapSafeZoneGate SafeZoneGate { get; }
            public MapMonsterGate MonsterGate { get; }
            public MainSceneWiring Wiring { get; }
            public PlayerVisionModel VisionModel { get; }
            public StalkPointRuntimeSet StalkPoints { get; }

            public void Dispose()
            {
                Bootstrap.ClearGeneratedMap();
                UnityEngine.Object.DestroyImmediate(monsterGateObject);
                UnityEngine.Object.DestroyImmediate(monsterPrefabRoot);
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(safeZoneGateObject);
                UnityEngine.Object.DestroyImmediate(lightGateObject);
                UnityEngine.Object.DestroyImmediate(wiringObject);
                UnityEngine.Object.DestroyImmediate(coreRoot);
                UnityEngine.Object.DestroyImmediate(mapRoot);
                UnityEngine.Object.DestroyImmediate(monsterConfig);
                UnityEngine.Object.DestroyImmediate(safeZones);
                UnityEngine.Object.DestroyImmediate(StalkPoints);
                UnityEngine.Object.DestroyImmediate(mapConfig);
            }

            private static GameObject BuildMonsterPrefab(out MonsterRuntimeRig rig)
            {
                var root = new GameObject("MonsterPrefab");
                root.SetActive(false);
                root.AddComponent<NavMeshAgent>();
                root.AddComponent<MonsterNavigator>();
                root.AddComponent<MonsterPerception>();
                root.AddComponent<MonsterAngerController>();
                root.AddComponent<MonsterBrain>();
                Animator animator = root.AddComponent<Animator>();
#if UNITY_EDITOR
                animator.runtimeAnimatorController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MonsterControllerPath);
#endif
                root.AddComponent<MonsterAnimationController>();
                root.AddComponent<MonsterVisualDriver>();
                AudioSource footstepSource = root.AddComponent<AudioSource>();
                AudioSource voiceSource = root.AddComponent<AudioSource>();
                CharacterAudioEmitter emitter = root.AddComponent<CharacterAudioEmitter>();
                emitter.Bind(footstepSource, voiceSource);
                root.AddComponent<MonsterAudioController>();
                rig = root.AddComponent<MonsterRuntimeRig>();

                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);

                var eye = new GameObject("Eye");
                eye.transform.SetParent(root.transform, false);
                eye.AddComponent<SphereCollider>().isTrigger = true;
                eye.AddComponent<MonsterGazeReceiver>();
                return root;
            }
        }
    }
}
