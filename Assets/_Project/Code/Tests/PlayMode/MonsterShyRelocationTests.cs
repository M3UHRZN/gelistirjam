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
        public IEnumerator RawGazeAtHidingCorner_ImmediatelyRunsToADifferentHiddenCornerBeforeConfirmation()
        {
            using var fixture = new RelocationFixture(
                includeSecondCorner: true,
                gazeConfirmDuration: 1f);
            fixture.EnterShy();
            ShyState shy = (ShyState)fixture.Brain.ShyState;
            StalkPoint firstTarget = shy.CurrentTarget;

            Assert.That(firstTarget, Is.SameAs(fixture.FirstCorner));
            yield return fixture.WaitUntilHiddenAtCurrentTarget();
            Assert.That(fixture.Agent.isStopped, Is.True,
                "Shy must begin this regression scenario waiting at its first hiding corner.");

            yield return fixture.BeginRawGaze();

            Assert.That(shy.CurrentTarget, Is.SameAs(fixture.SecondCorner),
                "Raw gaze must burn the exposed hiding corner immediately, before the confirmation timer elapses.");
            Assert.That(shy.CurrentTarget, Is.Not.SameAs(firstTarget));
            Assert.That(fixture.Brain.IsGazeConfirmed, Is.False,
                "The retreat must start on the detector's raw gaze edge, not after gaze confirmation.");
            Assert.That(fixture.Agent.isStopped, Is.False,
                "After choosing a different hidden corner, Shy must keep retreating while watched.");
            Assert.That(fixture.Agent.desiredVelocity.sqrMagnitude, Is.GreaterThan(0.01f),
                "The resumed NavMesh retreat must produce real movement while gaze remains confirmed.");
            Vector3 directionToPlayer = fixture.PlayerPosition - fixture.MonsterTransform.position;
            directionToPlayer.y = 0f;
            Assert.That(Vector3.Dot(
                fixture.MonsterTransform.forward,
                directionToPlayer.normalized), Is.GreaterThan(0.9f),
                "Shy must keep facing the player while its retreat path moves toward the hidden corner.");

            yield return fixture.ReleaseGaze();

            Assert.That(shy.CurrentTarget, Is.SameAs(fixture.SecondCorner));
            Assert.That(shy.CurrentTarget, Is.Not.SameAs(firstTarget));
            Assert.That(fixture.Vision.IsMonsterBodyVisibleAt(
                fixture.Agent.destination,
                fixture.Navigator.AgentHeight,
                fixture.Navigator.AgentRadius), Is.False);
        }

        [UnityTest]
        public IEnumerator RawGazeFromInterested_EntersShyBeforeConfirmation()
        {
            using var fixture = new RelocationFixture(
                includeSecondCorner: true,
                gazeConfirmDuration: 1f);
            fixture.EnterInterested();

            yield return fixture.BeginRawGaze();

            Assert.That(fixture.Brain.IsGazeConfirmed, Is.False,
                "This regression must exercise the pre-confirmation gaze window.");
            Assert.That(fixture.Brain.CurrentStateName, Is.EqualTo(nameof(ShyState)),
                "A calm stalking state must hand off to Shy on the first raw gaze frame.");
            Assert.That(fixture.Agent.isStopped, Is.False,
                "The gaze reaction must flee instead of freezing into a staredown.");
        }

        [UnityTest]
        public IEnumerator RawGaze_DoesNotCancelAnAlreadyAggressiveChase()
        {
            using var fixture = new RelocationFixture(
                includeSecondCorner: true,
                gazeConfirmDuration: 1f);
            fixture.EnterAggressive();

            yield return fixture.BeginRawGaze();

            Assert.That(fixture.Brain.IsGazedAtNow, Is.True);
            Assert.That(fixture.Brain.CurrentStateName, Is.EqualTo(nameof(AggressiveState)),
                "Raw gaze may interrupt calm stalking states, but must not cancel an existing independent chase.");
        }

        [UnityTest]
        public IEnumerator ConfirmedGazeWhilePlayerIsSafe_PreservesOneStableSafeZoneCamp()
        {
            using var fixture = new RelocationFixture(
                includeSecondCorner: true,
                gazeConfirmDuration: 0.05f);
            fixture.AddSafeZoneAroundPlayer();

            int campEntries = 0;
            int shyEntries = 0;
            int observedEntries = 0;
            fixture.Brain.OnStateChanged += stateName =>
            {
                campEntries += stateName == nameof(SafeZoneCampState) ? 1 : 0;
                shyEntries += stateName == nameof(ShyState) ? 1 : 0;
                observedEntries += stateName == nameof(ObservedState) ? 1 : 0;
            };
            fixture.EnterInterested();

            yield return fixture.HoldConfirmedGaze(holdAfterConfirmationSeconds: 0.2f);

            Assert.That(fixture.Brain.IsPlayerInSafeZone, Is.True);
            Assert.That(fixture.Brain.CurrentStateName, Is.EqualTo(nameof(SafeZoneCampState)),
                "SafeZone camping must outrank gaze retreat while the player is invulnerable.");
            Assert.That(campEntries, Is.EqualTo(1),
                "Held gaze must not churn repeatedly through SafeZoneCamp.");
            Assert.That(shyEntries, Is.Zero,
                "SafeZone gaze must not interrupt camping with a Shy retreat.");
            Assert.That(observedEntries, Is.Zero,
                "Confirmed SafeZone gaze must not reintroduce the legacy Observed staredown path.");
        }

        [UnityTest]
        public IEnumerator UnconfirmedRawGaze_DoesNotResetTheShyLifetime()
        {
            using var fixture = new RelocationFixture(
                includeSecondCorner: true,
                shyFleeDuration: 0.3f,
                fakeRetreatChance: 0f,
                gazeConfirmDuration: 1f);
            bool returnedToPatrol = false;
            fixture.EnterShy();
            int shyReentries = 0;
            fixture.Brain.OnStateChanged += state =>
            {
                returnedToPatrol |= state == nameof(PatrolState);
                shyReentries += state == nameof(ShyState) ? 1 : 0;
            };

            float nearExpiryDeadline = Time.time + 0.6f;
            while (fixture.ShyFleeTimer > 0.08f && Time.time < nearExpiryDeadline) yield return null;
            Assert.That(fixture.ShyFleeTimer, Is.LessThanOrEqualTo(0.08f),
                "The fixture must reach Shy's near-expiry window before the incidental gaze.");

            yield return fixture.BeginRawGaze();
            Assert.That(fixture.Brain.IsGazeConfirmed, Is.False,
                "The lifetime regression must use an incidental gaze below confirmation time.");
            Assert.That(fixture.ShyFleeTimer, Is.LessThan(0.15f),
                "An unconfirmed raw gaze must not refill the configured 0.3 second flee lifetime.");

            yield return fixture.KeepHoldingRawGaze(0.12f);
            Assert.That(returnedToPatrol, Is.True,
                "An unconfirmed raw gaze must not pause Shy's countdown or postpone its expiry.");
            Assert.That(fixture.Brain.CurrentStateName, Is.Not.EqualTo(nameof(ShyState)),
                "One continuous raw gaze must not start a fresh Shy lifetime after expiry.");
            Assert.That(shyReentries, Is.Zero,
                "The global gaze interruption must fire once per continuous gaze, not once per state transition.");

            yield return fixture.ReleaseGaze();

            float patrolDeadline = Time.time + 0.25f;
            while (!returnedToPatrol && Time.time < patrolDeadline) yield return null;

            Assert.That(returnedToPatrol, Is.True,
                "An unconfirmed raw gaze may relocate Shy immediately, but must not refill its full flee lifetime.");
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
        public IEnumerator GazeRelease_DoesNotBypassTheFailedSelectionRetryCadence()
        {
            using var fixture = new RelocationFixture(includeSecondCorner: false);
            fixture.EnterShy();
            ShyState shy = (ShyState)fixture.Brain.ShyState;
            yield return fixture.WaitUntilHiddenAtCurrentTarget();

            yield return fixture.HoldConfirmedGaze();
            Assert.That(shy.CurrentTarget, Is.Null,
                "The exposed only corner must be rejected on the confirmed-gaze edge.");

            fixture.AddSecondHiddenCorner();
            yield return fixture.ReleaseGaze();

            Assert.That(shy.CurrentTarget, Is.Null,
                "Gaze release must not perform an extra selection before RepositionInterval expires.");
            Assert.That(fixture.Agent.isStopped, Is.True);

            float retryDeadline = Time.time + fixture.Config.RepositionInterval + 0.15f;
            while (shy.CurrentTarget == null && Time.time < retryDeadline) yield return null;

            Assert.That(shy.CurrentTarget, Is.SameAs(fixture.SecondCorner),
                "The normal bounded retry must select the newly available hidden corner.");
        }

        [UnityTest]
        public IEnumerator ConfirmedGazeDuringTravel_ReplacesTargetOnceWithoutHeldGazeChurn()
        {
            using var fixture = new RelocationFixture(includeSecondCorner: true);
            fixture.EnterShy();
            ShyState shy = (ShyState)fixture.Brain.ShyState;
            StalkPoint firstTarget = shy.CurrentTarget;
            yield return fixture.WaitUntilTravelling();

            yield return fixture.HoldConfirmedGaze(holdAfterConfirmationSeconds: 0.05f);

            Assert.That(shy.CurrentTarget, Is.SameAs(fixture.SecondCorner));
            Assert.That(shy.CurrentTarget, Is.Not.SameAs(firstTarget));
            Vector3 replacementDestination = fixture.Agent.destination;

            fixture.AddThirdHiddenCorner();
            yield return fixture.KeepHoldingGaze(0.2f);

            Assert.That(shy.CurrentTarget, Is.SameAs(fixture.SecondCorner),
                "A successful rising-edge relocation must not rescore each frame while gaze remains held.");
            Assert.That(shy.CurrentTarget, Is.Not.SameAs(fixture.ThirdCorner));
            Assert.That(Vector3.Distance(fixture.Agent.destination, replacementDestination), Is.LessThan(0.001f),
                "Held gaze must not churn an already accepted destination.");
        }

        [UnityTest]
        public IEnumerator MaximumAngerHeldGaze_NeverStopsOrAggressesAfterImmediateRelocation()
        {
            using var fixture = new RelocationFixture(
                includeSecondCorner: true,
                staredownChance: 1f,
                aggressionStaredownBonus: 1f,
                staredownAngerInterval: 0.02f);
            fixture.EnterShy();
            ShyState shy = (ShyState)fixture.Brain.ShyState;
            yield return fixture.WaitUntilHiddenAtCurrentTarget();
            fixture.Anger.AddAnger(fixture.Config.MaxAnger);

            yield return fixture.HoldConfirmedGaze(holdAfterConfirmationSeconds: 0.08f);

            Assert.That(fixture.Brain.CurrentStateName, Is.EqualTo(nameof(ShyState)));
            Assert.That(shy.CurrentTarget, Is.SameAs(fixture.SecondCorner));
            Assert.That(fixture.Agent.isStopped, Is.False,
                "Even a guaranteed legacy staredown roll at maximum anger must not stop the retreat.");

            yield return fixture.ReleaseGaze();

            Assert.That(fixture.Brain.CurrentStateName, Is.EqualTo(nameof(ShyState)),
                "Breaking gaze must never turn the gaze reaction into AggressiveState.");
            Assert.That(shy.CurrentTarget, Is.SameAs(fixture.SecondCorner));
            Assert.That(fixture.Agent.isStopped, Is.False,
                "The replacement retreat must continue after gaze release without target churn.");
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
                float fakeRetreatChance = 0.25f,
                int angerPerSpotted = 1,
                float aggressionDistanceWeight = 0.15f,
                float gazeConfirmDuration = 0.2f)
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
                SetConfig("angerPerSpotted", angerPerSpotted);
                SetConfig("aggressionDistanceWeight", aggressionDistanceWeight);
                SetConfig("gazeConfirmDurationSeconds", gazeConfirmDuration);
                Config.TuningFor(AngerStage.Calm).FakeRetreatChanceMultiplier = 1f;
                safeZones = ScriptableObject.CreateInstance<SafeZoneRuntimeSet>();
                stalkPoints = ScriptableObject.CreateInstance<StalkPointRuntimeSet>();
                FirstCorner = CreateCorner("FirstCorner", new Vector3(-4f, 0f, 6f));
                if (includeSecondCorner)
                {
                    SecondCorner = CreateCorner("SecondCorner", new Vector3(4f, 0f, 6f));
                    CreateVisionOnlyWall(
                        new Vector3(2f, 1.5f, 1f),
                        new Vector3(1f, 3f, 0.25f));
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
            public StalkPoint SecondCorner { get; private set; }
            public StalkPoint ThirdCorner { get; private set; }
            public Transform MonsterTransform => monster.transform;
            public Vector3 PlayerPosition => player.transform.position;
            public float ShyFleeTimer => (float)typeof(ShyState).GetField(
                "fleeTimer",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Brain.ShyState);

            public void AddSecondHiddenCorner()
            {
                Assert.That(SecondCorner, Is.Null,
                    "This helper models one newly available alternative corner exactly once.");
                SecondCorner = CreateCorner("SecondCorner_Late", new Vector3(4f, 0f, 6f));
            }

            public void AddThirdHiddenCorner()
            {
                Assert.That(ThirdCorner, Is.Null,
                    "This helper adds one newly eligible, higher-ranked hidden corner.");
                ThirdCorner = CreateCorner("ThirdCorner_Late", new Vector3(8f, 0f, 6f));
                CreateVisionOnlyWall(
                    new Vector3(4f, 1.5f, 1f),
                    new Vector3(1f, 3f, 0.25f));
            }

            public void AddSafeZoneAroundPlayer()
            {
                GameObject zoneObject = new GameObject("PlayerSafeZone");
                zoneObject.transform.SetParent(root.transform, false);
                zoneObject.transform.position = player.transform.position + Vector3.up;
                BoxCollider volume = zoneObject.AddComponent<BoxCollider>();
                volume.size = new Vector3(4f, 3f, 4f);
                SafeZone zone = zoneObject.AddComponent<SafeZone>();
                zone.Bind(safeZones);
                Physics.SyncTransforms();
            }

            public void EnterShy()
            {
                camera.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Physics.SyncTransforms();
                Brain.ChangeState(Brain.ShyState);
            }

            public void EnterInterested()
            {
                camera.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Physics.SyncTransforms();
                Brain.ChangeState(Brain.InterestedState);
            }

            public void EnterAggressive()
            {
                camera.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Physics.SyncTransforms();
                Anger.AddAnger(Config.MaxAnger);
                Brain.ChangeState(Brain.AggressiveState);
            }

            public IEnumerator BeginRawGaze()
            {
                float deadline = Time.time + 0.25f;
                while (!Brain.IsGazedAtNow && Time.time < deadline)
                {
                    camera.transform.LookAt(gazeTarget.position);
                    Physics.SyncTransforms();
                    yield return null;
                }

                Assert.That(Brain.IsGazedAtNow, Is.True,
                    "The real gaze detector must acquire the monster.");

                // Keep the ray locked for one more AI tick so the test does not depend on the
                // relative Update order of PlayerGazeDetector and MonsterBrain.
                camera.transform.LookAt(gazeTarget.position);
                Physics.SyncTransforms();
                yield return null;
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

            public IEnumerator KeepHoldingGaze(float seconds)
            {
                float deadline = Time.time + seconds;
                while (Time.time < deadline)
                {
                    camera.transform.LookAt(gazeTarget.position);
                    Physics.SyncTransforms();
                    yield return null;
                    Assert.That(Brain.IsGazeConfirmed, Is.True,
                        "The churn regression must keep one continuous confirmed gaze.");
                }
            }

            public IEnumerator KeepHoldingRawGaze(float seconds)
            {
                float deadline = Time.time + seconds;
                while (Time.time < deadline)
                {
                    camera.transform.LookAt(gazeTarget.position);
                    Physics.SyncTransforms();
                    yield return null;
                    Assert.That(Brain.IsGazedAtNow, Is.True,
                        "The lifetime regression must keep the raw gaze ray acquired.");
                    Assert.That(Brain.IsGazeConfirmed, Is.False,
                        "The raw gaze hold must remain below the configured confirmation duration.");
                }
            }

            public IEnumerator WaitUntilTravelling()
            {
                float deadline = Time.time + 0.5f;
                while (Agent.desiredVelocity.sqrMagnitude < 0.01f && Time.time < deadline) yield return null;
                Assert.That(Agent.desiredVelocity.sqrMagnitude, Is.GreaterThan(0.01f),
                    "The regression fixture must exercise a real in-flight retreat.");
            }

            public IEnumerator WaitUntilHiddenAtCurrentTarget()
            {
                float deadline = Time.time + 3f;
                while ((!Agent.isOnNavMesh || Agent.pathPending || !Agent.isStopped
                        || Agent.remainingDistance > Agent.stoppingDistance + 0.05f)
                    && Time.time < deadline)
                {
                    yield return null;
                }

                Assert.That(Agent.isOnNavMesh && !Agent.pathPending
                    && Agent.isStopped
                    && Agent.remainingDistance <= Agent.stoppingDistance + 0.05f, Is.True,
                    "The regression fixture must reach its first hiding corner before gaze begins.");
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

            private void CreateVisionOnlyWall(Vector3 position, Vector3 scale)
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "VisionOnlyWall";
                wall.transform.SetParent(root.transform, false);
                wall.transform.position = position;
                wall.transform.localScale = scale;
                Physics.SyncTransforms();
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
