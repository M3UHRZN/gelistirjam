using Game.Gameplay.Monster;
using Game.Gameplay.Player;
using Game.Gameplay.World;
using NUnit.Framework;
using UnityEngine;

namespace Game.Gameplay.Tests
{
    public sealed class MonsterRuntimeRigTests
    {
        public sealed class VelocitySourceStub : MonoBehaviour, IPlayerVelocitySource
        {
            public Vector3 Velocity => new Vector3(1f, 0f, 0f);
        }

        [Test]
        public void PlayerController_ImplementsVelocitySource()
        {
            Assert.That(typeof(IPlayerVelocitySource).IsAssignableFrom(typeof(PlayerController)), Is.True);
        }

        [Test]
        public void Initialize_BindsCompleteInactiveRig()
        {
            using var fixture = new RigFixture(includeGazeReceiver: true);

            bool initialized = fixture.Rig.Initialize(
                fixture.Config,
                fixture.Player.transform,
                fixture.VelocitySource,
                fixture.GazeDetector,
                fixture.SafeZones,
                fixture.StalkPoints,
                fixture.VisionModel);

            Assert.That(initialized, Is.True);
            Assert.That(fixture.Rig.IsInitialized, Is.True);
            Assert.That(fixture.Rig.Brain.Player, Is.SameAs(fixture.Player.transform));
            Assert.That(fixture.Rig.Brain.StalkPoints, Is.SameAs(fixture.StalkPoints));
            Assert.That(fixture.Rig.Brain.VisionModel, Is.SameAs(fixture.VisionModel));
            Assert.That(fixture.Rig.Perception.HasVelocitySource, Is.True);
            Assert.That(fixture.GazeReceiver.Owner, Is.SameAs(fixture.Rig.Brain));
            Assert.That(fixture.Root.activeSelf, Is.False);
        }

        [Test]
        public void Initialize_MissingGazeReceiverFailsAndKeepsRootInactive()
        {
            using var fixture = new RigFixture(includeGazeReceiver: false);

            bool initialized = fixture.Rig.Initialize(
                fixture.Config,
                fixture.Player.transform,
                fixture.VelocitySource,
                fixture.GazeDetector,
                fixture.SafeZones,
                fixture.StalkPoints,
                fixture.VisionModel);

            Assert.That(initialized, Is.False);
            Assert.That(fixture.Rig.IsInitialized, Is.False);
            Assert.That(fixture.Root.activeSelf, Is.False);
        }

        private sealed class RigFixture : System.IDisposable
        {
            public RigFixture(bool includeGazeReceiver)
            {
                Root = new GameObject("Monster");
                Root.SetActive(false);

                Root.AddComponent<UnityEngine.AI.NavMeshAgent>();
                Root.AddComponent<MonsterNavigator>();
                Root.AddComponent<MonsterPerception>();
                Root.AddComponent<MonsterAngerController>();
                Root.AddComponent<MonsterBrain>();
                Root.AddComponent<MonsterVisualDriver>();
                Rig = Root.AddComponent<MonsterRuntimeRig>();

                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(Root.transform, false);

                if (includeGazeReceiver)
                {
                    var eye = new GameObject("Eye");
                    eye.transform.SetParent(Root.transform, false);
                    eye.AddComponent<SphereCollider>().isTrigger = true;
                    GazeReceiver = eye.AddComponent<MonsterGazeReceiver>();
                }

                Player = new GameObject("Player");
                VelocitySource = Player.AddComponent<VelocitySourceStub>();
                VisionModel = Player.AddComponent<PlayerVisionModel>();

                var cameraObject = new GameObject("PlayerCamera");
                cameraObject.transform.SetParent(Player.transform, false);
                cameraObject.AddComponent<Camera>();
                GazeDetector = cameraObject.AddComponent<PlayerGazeDetector>();

                Config = ScriptableObject.CreateInstance<MonsterConfig>();
                SafeZones = ScriptableObject.CreateInstance<SafeZoneRuntimeSet>();
                StalkPoints = ScriptableObject.CreateInstance<StalkPointRuntimeSet>();
            }

            public GameObject Root { get; }
            public GameObject Player { get; }
            public MonsterRuntimeRig Rig { get; }
            public MonsterConfig Config { get; }
            public SafeZoneRuntimeSet SafeZones { get; }
            public StalkPointRuntimeSet StalkPoints { get; }
            public VelocitySourceStub VelocitySource { get; }
            public PlayerGazeDetector GazeDetector { get; }
            public PlayerVisionModel VisionModel { get; }
            public MonsterGazeReceiver GazeReceiver { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                Object.DestroyImmediate(Player);
                Object.DestroyImmediate(Config);
                Object.DestroyImmediate(SafeZones);
                Object.DestroyImmediate(StalkPoints);
            }
        }
    }
}
