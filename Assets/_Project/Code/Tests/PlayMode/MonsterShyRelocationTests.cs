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
            yield return fixture.WaitUntilTravelling();
            Vector3 destinationBeforeGaze = fixture.Agent.destination;

            yield return fixture.HoldConfirmedGaze();

            Assert.That(shy.CurrentTarget, Is.SameAs(firstTarget),
                "A held gaze may yell or stare down, but must not switch corners before gaze release.");
            Assert.That(Vector3.Distance(fixture.Agent.destination, destinationBeforeGaze), Is.LessThan(0.001f),
                "The in-flight destination must stay stable while gaze is held.");
            Assert.That(Vector3.Dot(
                fixture.MonsterTransform.forward,
                fixture.Agent.desiredVelocity.normalized), Is.LessThan(-0.25f),
                "Shy must keep facing the player while retreating backwards.");

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

        [UnityTest]
        public IEnumerator ActiveStaredown_AccumulatesAngerThenAggressesBeforeRelocation()
        {
            using var fixture = new RelocationFixture(
                includeSecondCorner: true,
                staredownChance: 1f,
                aggressionStaredownBonus: 1f,
                staredownAngerInterval: 0.02f);
            fixture.EnterShy();

            int angerBeforeGaze = fixture.Anger.Anger;
            yield return fixture.HoldConfirmedGaze(holdAfterConfirmationSeconds: 0.08f);

            Assert.That(fixture.Agent.isStopped, Is.True,
                "A successful Shy staredown must freeze the retreat.");
            Assert.That(fixture.Anger.Anger, Is.GreaterThan(angerBeforeGaze + fixture.Config.AngerPerSpotted),
                "Held staredown time must continue adding anger after the confirmed-gaze anger event.");

            yield return fixture.ReleaseGaze();

            Assert.That(fixture.Brain.CurrentStateName, Is.EqualTo(nameof(AggressiveState)),
                "The existing staredown aggression roll must run before any hidden-corner relocation.");
        }

        [UnityTest]
        public IEnumerator UnobservedMidpoint_PreservesFakeRetreatTransition()
        {
            using var fixture = new RelocationFixture(
                includeSecondCorner: true,
                shyFleeDuration: 0.2f,
                fakeRetreatChance: 1f);
            bool enteredFakeRetreat = false;
            fixture.Brain.OnStateChanged += state => enteredFakeRetreat |= state == nameof(FakeRetreatState);
            fixture.EnterShy();

            float deadline = Time.time + 0.5f;
            while (!enteredFakeRetreat && Time.time < deadline) yield return null;

            Assert.That(enteredFakeRetreat, Is.True,
                "The hidden-corner integration must preserve Shy's midpoint FakeRetreat roll.");
        }

        [UnityTest]
        public IEnumerator UnobservedLifetimeExpiry_PreservesPatrolReturn()
        {
            using var fixture = new RelocationFixture(
                includeSecondCorner: true,
                shyFleeDuration: 0.2f,
                fakeRetreatChance: 0f);
            bool returnedToPatrol = false;
            fixture.Brain.OnStateChanged += state => returnedToPatrol |= state == nameof(PatrolState);
            fixture.EnterShy();

            float deadline = Time.time + 0.5f;
            while (!returnedToPatrol && Time.time < deadline) yield return null;

            Assert.That(returnedToPatrol, Is.True,
                "The hidden-corner integration must preserve Shy's lifetime expiry back to Patrol.");
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

            public RelocationFixture(
                bool includeSecondCorner,
                float staredownChance = 0f,
                float aggressionStaredownBonus = 0.1f,
                float staredownAngerInterval = 1f,
                float shyFleeDuration = 24f,
                float fakeRetreatChance = 0.25f)
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
                SetConfig("staredownChance", staredownChance);
                SetConfig("staredownCheckInterval", 0.01f);
                SetConfig("staredownAngerInterval", staredownAngerInterval);
                SetConfig("aggressionStaredownBonus", aggressionStaredownBonus);
                SetConfig("shyFleeDuration", shyFleeDuration);
                SetConfig("fakeRetreatChance", fakeRetreatChance);
                Config.TuningFor(AngerStage.Calm).FakeRetreatChanceMultiplier = 1f;
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
            public MonsterAngerController Anger => monster.GetComponent<MonsterAngerController>();
            public StalkPoint FirstCorner { get; }
            public StalkPoint SecondCorner { get; }
            public Transform MonsterTransform => monster.transform;

            public void EnterShy()
            {
                camera.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Physics.SyncTransforms();
                Brain.ChangeState(Brain.ShyState);
            }

            public IEnumerator HoldConfirmedGaze()
                => HoldConfirmedGaze(0f);

            public IEnumerator HoldConfirmedGaze(float holdAfterConfirmationSeconds)
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

                float holdDeadline = Time.time + holdAfterConfirmationSeconds;
                while (Time.time < holdDeadline)
                {
                    camera.transform.LookAt(gazeTarget.position);
                    Physics.SyncTransforms();
                    yield return null;
                }
            }

            public IEnumerator WaitUntilTravelling()
            {
                float deadline = Time.time + 0.5f;
                while (Agent.desiredVelocity.sqrMagnitude < 0.01f && Time.time < deadline) yield return null;
                Assert.That(Agent.desiredVelocity.sqrMagnitude, Is.GreaterThan(0.01f),
                    "The regression fixture must exercise a real in-flight retreat.");
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

            private void SetConfig(string fieldName, object value)
            {
                FieldInfo field = typeof(MonsterConfig).GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, $"Missing MonsterConfig field {fieldName}.");
                field.SetValue(Config, value);
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
