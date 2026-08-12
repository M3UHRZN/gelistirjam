using System;
using System.Collections;
using System.Reflection;
using Game.Gameplay.Monster;
using Game.Gameplay.Player;
using Game.Gameplay.World;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class MonsterShyRelocationTests
    {
        public sealed class VelocitySourceStub : MonoBehaviour, IPlayerVelocitySource
        {
            public Vector3 Velocity => Vector3.zero;
        }

        [UnityTest]
        public IEnumerator ConfirmedGazeRelease_ReplacesTheExposedInFlightCorner()
        {
            using var fixture = new RelocationFixture(includeSecondCorner: true);
            fixture.EnterShy();
            ShyState shy = (ShyState)fixture.Brain.ShyState;
            StalkPoint firstTarget = shy.CurrentTarget;

            Assert.That(firstTarget, Is.SameAs(fixture.FirstCorner));
            fixture.Navigator.Stop();

            yield return fixture.HoldConfirmedGaze();
            yield return fixture.ReleaseGaze();

            Assert.That(shy.CurrentTarget, Is.SameAs(fixture.SecondCorner));
            Assert.That(shy.CurrentTarget, Is.Not.SameAs(firstTarget),
                "A confirmed gaze must burn the exposed in-flight target for the next retreat.");
            Assert.That(fixture.Vision.IsMonsterBodyVisibleAt(
                shy.CurrentTarget.Position,
                fixture.Navigator.AgentHeight,
                fixture.Navigator.AgentRadius), Is.False);
        }

        [UnityTest]
        public IEnumerator NoAlternativeCorner_StopsAndRetriesWithoutVisibleFallback()
        {
            using var fixture = new RelocationFixture(includeSecondCorner: false);
            fixture.EnterShy();
            ShyState shy = (ShyState)fixture.Brain.ShyState;
            Assert.That(shy.CurrentTarget, Is.SameAs(fixture.FirstCorner));
            fixture.Navigator.Stop();

            yield return fixture.HoldConfirmedGaze();
            yield return fixture.ReleaseGaze();

            Assert.That(fixture.Brain.CurrentStateName, Is.EqualTo(nameof(ShyState)),
                "This fixture isolates the non-aggressive Shy continuation branch.");
            Assert.That(shy.CurrentTarget, Is.Null);
            Assert.That(fixture.Agent.isStopped, Is.True);

            float retryDeadline = Time.time + fixture.Config.RepositionInterval + 0.15f;
            while (Time.time < retryDeadline) yield return null;

            Assert.That(shy.CurrentTarget, Is.Null,
                "Retry must keep the exposed only corner excluded.");
            Assert.That(fixture.Agent.isStopped, Is.True,
                "No hidden alternative must never resume a stale or visible destination.");
        }

        private sealed class RelocationFixture : IDisposable
        {
            private readonly GameObject root;
            private readonly GameObject monster;
            private readonly GameObject player;
            private readonly Transform gazeTarget;
            private readonly Camera camera;
            private readonly NavMeshSurface surface;
            private readonly MonsterRuntimeRig rig;
            private readonly SafeZoneRuntimeSet safeZones;
            private readonly StalkPointRuntimeSet stalkPoints;
            private readonly VelocitySourceStub velocitySource;
            private readonly PlayerGazeDetector gazeDetector;

            public RelocationFixture(bool includeSecondCorner)
            {
                root = new GameObject("ShyRelocationFixture");

                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Floor";
                floor.transform.SetParent(root.transform, false);
                floor.transform.position = new Vector3(0f, -0.1f, 4f);
                floor.transform.localScale = new Vector3(30f, 0.2f, 30f);

                surface = root.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();

                player = new GameObject("Player");
                player.transform.SetParent(root.transform, false);
                player.transform.position = new Vector3(0f, 0f, -4f);
                velocitySource = player.AddComponent<VelocitySourceStub>();
                Vision = player.AddComponent<PlayerVisionModel>();

                GameObject cameraObject = new GameObject("PlayerCamera");
                cameraObject.transform.SetParent(player.transform, false);
                cameraObject.transform.localPosition = Vector3.up * 1.1f;
                cameraObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.fieldOfView = 60f;
                camera.aspect = 16f / 9f;
                gazeDetector = cameraObject.AddComponent<PlayerGazeDetector>();

                monster = new GameObject("Monster");
                monster.transform.SetParent(root.transform, false);
                monster.SetActive(false);
                monster.transform.position = Vector3.zero;
                Agent = monster.AddComponent<NavMeshAgent>();
                Agent.height = 2.2f;
                Agent.radius = 0.35f;
                Navigator = monster.AddComponent<MonsterNavigator>();
                monster.AddComponent<MonsterPerception>();
                monster.AddComponent<MonsterAngerController>();
                Brain = monster.AddComponent<MonsterBrain>();
                monster.AddComponent<Animator>();
                monster.AddComponent<MonsterAnimationController>();
                monster.AddComponent<MonsterVisualDriver>();
                rig = monster.AddComponent<MonsterRuntimeRig>();

                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(monster.transform, false);
                body.transform.localPosition = Vector3.up * 1.1f;
                body.GetComponent<Collider>().enabled = false;

                GameObject gazeVolume = new GameObject("GazeVolume");
                gazeVolume.transform.SetParent(monster.transform, false);
                gazeVolume.transform.localPosition = Vector3.up * 1.1f;
                SphereCollider gazeCollider = gazeVolume.AddComponent<SphereCollider>();
                gazeCollider.isTrigger = true;
                gazeCollider.radius = 0.8f;
                gazeVolume.AddComponent<MonsterGazeReceiver>();
                gazeTarget = gazeVolume.transform;

                Config = ScriptableObject.CreateInstance<MonsterConfig>();
                typeof(MonsterConfig)
                    .GetField("staredownChance", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(Config, 0f);
                safeZones = ScriptableObject.CreateInstance<SafeZoneRuntimeSet>();
                stalkPoints = ScriptableObject.CreateInstance<StalkPointRuntimeSet>();
                FirstCorner = CreateCorner("FirstCorner", new Vector3(-4f, 0f, 6f));
                if (includeSecondCorner)
                {
                    SecondCorner = CreateCorner("SecondCorner", new Vector3(4f, 0f, 6f));
                }

                Physics.SyncTransforms();
                Assert.That(rig.Initialize(
                    Config,
                    player.transform,
                    velocitySource,
                    gazeDetector,
                    safeZones,
                    stalkPoints,
                    Vision), Is.True);
                monster.SetActive(true);
                Physics.SyncTransforms();
                Assert.That(Agent.isOnNavMesh, Is.True,
                    "The atomically initialized agent must attach to the runtime NavMesh when activated.");
            }

            public MonsterBrain Brain { get; }
            public MonsterNavigator Navigator { get; }
            public NavMeshAgent Agent { get; }
            public PlayerVisionModel Vision { get; }
            public MonsterConfig Config { get; }
            public StalkPoint FirstCorner { get; }
            public StalkPoint SecondCorner { get; }

            public void EnterShy()
            {
                camera.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Physics.SyncTransforms();
                Brain.ChangeState(Brain.ShyState);
            }

            public IEnumerator HoldConfirmedGaze()
            {
                float deadline = Time.time + 1.5f;
                while (!Brain.IsGazeConfirmed && Time.time < deadline)
                {
                    camera.transform.LookAt(gazeTarget.position);
                    Physics.SyncTransforms();
                    yield return null;
                }

                Assert.That(Brain.IsGazeConfirmed, Is.True,
                    "The real gaze detector must confirm the exposed Shy monster.");
            }

            public IEnumerator ReleaseGaze()
            {
                camera.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Physics.SyncTransforms();
                float deadline = Time.time + 0.5f;
                while (Brain.IsGazeConfirmed && Time.time < deadline) yield return null;
                yield return null;
                Assert.That(Brain.IsGazeConfirmed, Is.False);
            }

            private StalkPoint CreateCorner(string name, Vector3 position)
            {
                var pointObject = new GameObject(name);
                pointObject.transform.SetParent(root.transform, false);
                pointObject.transform.position = position;
                StalkPoint point = pointObject.AddComponent<StalkPoint>();
                point.ConfigureTopology(nearCorner: true, junction: false, deadEnd: false);
                point.ConfigureStaticDarkness(0.8f);
                stalkPoints.Register(point);
                return point;
            }

            public void Dispose()
            {
                surface.RemoveData();
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(Config);
                UnityEngine.Object.DestroyImmediate(safeZones);
                UnityEngine.Object.DestroyImmediate(stalkPoints);
            }
        }
    }
}
