using System;
using System.Reflection;
using Game.Core;
using Game.Gameplay.Monster;
using Game.Gameplay.Objectives;
using Game.Gameplay.Player;
using Game.Gameplay.World;
using Gloomveil.MapGeneration;
using NUnit.Framework;
using UnityEngine;

namespace Game.Gameplay.Tests
{
    public sealed class MainSceneWiringMonsterTests
    {
        public sealed class VelocitySourceStub : MonoBehaviour, IPlayerVelocitySource
        {
            public Vector3 Velocity => Vector3.zero;
        }

        [Test]
        public void BoundMonsterKill_EntersDeathState()
        {
            using var fixture = new WiringFixture();
            MonsterBrain brain = fixture.CreateInactiveBrain("Monster");

            fixture.Wiring.BindMonster(brain);
            brain.RaiseKill();

            Assert.That(fixture.Flow.CurrentStateName, Is.EqualTo(nameof(DeathState)));
        }

        [Test]
        public void BindMonster_ReplacesKillSubscription()
        {
            using var fixture = new WiringFixture();
            MonsterBrain first = fixture.CreateInactiveBrain("First");
            MonsterBrain second = fixture.CreateInactiveBrain("Second");

            fixture.Wiring.BindMonster(first);
            fixture.Wiring.BindMonster(second);
            fixture.Flow.ChangeState(fixture.Flow.ExplorationState);

            first.RaiseKill();
            Assert.That(fixture.Flow.CurrentStateName, Is.EqualTo(nameof(ExplorationState)));

            second.RaiseKill();
            Assert.That(fixture.Flow.CurrentStateName, Is.EqualTo(nameof(DeathState)));
        }

        [Test]
        public void UnbindMonster_ReleasesOnlyTheCurrentMonster()
        {
            using var fixture = new WiringFixture();
            MonsterBrain first = fixture.CreateInactiveBrain("First");
            MonsterBrain second = fixture.CreateInactiveBrain("Second");

            fixture.Wiring.BindMonster(first);
            fixture.Wiring.UnbindMonster(second);
            Assert.That(fixture.Wiring.BoundMonster, Is.SameAs(first));

            fixture.Wiring.UnbindMonster(first);
            first.RaiseKill();

            Assert.That(fixture.Wiring.BoundMonster, Is.Null);
            Assert.That(fixture.Flow.CurrentStateName, Is.Not.EqualTo(nameof(DeathState)));
        }

        [Test]
        public void FuseEvents_AffectOnlyCurrentMonster()
        {
            using var fixture = new WiringFixture();
            MonsterBrain first = fixture.CreateInactiveBrain("First");
            MonsterBrain second = fixture.CreateInactiveBrain("Second");
            Vector3 fusePosition = new Vector3(4f, 1f, 9f);

            fixture.Wiring.BindMonster(first);
            fixture.Wiring.BindMonster(second);
            RaiseFuseStarted(fixture.Objectives, fusePosition);

            Assert.That(first.IsAwareOfFuse, Is.False);
            Assert.That(second.IsAwareOfFuse, Is.True);
            Assert.That(second.FusePosition, Is.EqualTo(fusePosition));

            RaiseFuseStopped(fixture.Objectives);
            Assert.That(second.IsAwareOfFuse, Is.False);
        }

        [Test]
        public void FinalChase_AppliesToCurrentMonster()
        {
            using var fixture = new WiringFixture();
            using var monster = new ActiveMonsterFixture();

            fixture.Wiring.BindMonster(monster.Brain);
            fixture.Flow.ChangeState(fixture.Flow.FinalChaseState);

            Assert.That(monster.Brain.IsRelentless, Is.True);
            Assert.That(monster.Brain.CurrentStateName, Is.EqualTo(nameof(AggressiveState)));
        }

        [Test]
        public void BindMonster_AfterFinalChaseAppliesStateImmediately()
        {
            using var fixture = new WiringFixture();
            using var monster = new ActiveMonsterFixture();
            fixture.Flow.ChangeState(fixture.Flow.FinalChaseState);

            fixture.Wiring.BindMonster(monster.Brain);

            Assert.That(monster.Brain.IsRelentless, Is.True);
            Assert.That(monster.Brain.CurrentStateName, Is.EqualTo(nameof(AggressiveState)));
        }

        private static void RaiseFuseStarted(ObjectiveManager objectives, Vector3 position) =>
            GetObjectiveHandler("HandleFuseInteractionStarted").Invoke(objectives, new object[] { position });

        private static void RaiseFuseStopped(ObjectiveManager objectives) =>
            GetObjectiveHandler("HandleFuseInteractionStopped").Invoke(objectives, null);

        private static MethodInfo GetObjectiveHandler(string name) =>
            typeof(ObjectiveManager).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(ObjectiveManager).FullName, name);

        private sealed class WiringFixture : IDisposable
        {
            private readonly System.Collections.Generic.List<GameObject> monsters = new();

            public WiringFixture()
            {
                var flowObject = new GameObject("GameFlow");
                Flow = flowObject.AddComponent<GameFlowController>();

                var objectivesObject = new GameObject("Objectives");
                Objectives = objectivesObject.AddComponent<ObjectiveManager>();

                var registryObject = new GameObject("Registry");
                Registry = registryObject.AddComponent<GeneratedMapRegistry>();

                var exitObject = new GameObject("MapExit");
                Exit = exitObject.AddComponent<MapExitController>();
                Exit.Bind(Registry, Flow);

                var miniGameObject = new GameObject("FuseMiniGame");
                MiniGame = miniGameObject.AddComponent<FuseMiniGameController>();

                var wiringObject = new GameObject("Wiring");
                Wiring = wiringObject.AddComponent<MainSceneWiring>();
                Wiring.Bind(Flow, Objectives, Exit, MiniGame);
            }

            public GameFlowController Flow { get; }
            public ObjectiveManager Objectives { get; }
            public GeneratedMapRegistry Registry { get; }
            public MapExitController Exit { get; }
            public FuseMiniGameController MiniGame { get; }
            public MainSceneWiring Wiring { get; }

            public MonsterBrain CreateInactiveBrain(string name)
            {
                var root = new GameObject(name);
                root.SetActive(false);
                monsters.Add(root);
                return root.AddComponent<MonsterBrain>();
            }

            public void Dispose()
            {
                foreach (GameObject monster in monsters) UnityEngine.Object.DestroyImmediate(monster);
                UnityEngine.Object.DestroyImmediate(Wiring.gameObject);
                UnityEngine.Object.DestroyImmediate(MiniGame.gameObject);
                UnityEngine.Object.DestroyImmediate(Exit.gameObject);
                UnityEngine.Object.DestroyImmediate(Registry.gameObject);
                UnityEngine.Object.DestroyImmediate(Objectives.gameObject);
                UnityEngine.Object.DestroyImmediate(Flow.gameObject);
            }
        }

        private sealed class ActiveMonsterFixture : IDisposable
        {
            private readonly GameObject player;
            private readonly MonsterConfig config;
            private readonly SafeZoneRuntimeSet safeZones;
            private readonly StalkPointRuntimeSet stalkPoints;

            public ActiveMonsterFixture()
            {
                var root = new GameObject("Monster");
                root.SetActive(false);
                Root = root;

                root.AddComponent<UnityEngine.AI.NavMeshAgent>();
                root.AddComponent<MonsterNavigator>();
                root.AddComponent<MonsterPerception>();
                root.AddComponent<MonsterAngerController>();
                Brain = root.AddComponent<MonsterBrain>();
                root.AddComponent<Animator>();
                root.AddComponent<MonsterAnimationController>();
                root.AddComponent<MonsterVisualDriver>();
                MonsterRuntimeRig rig = root.AddComponent<MonsterRuntimeRig>();

                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);

                var eye = new GameObject("Eye");
                eye.transform.SetParent(root.transform, false);
                eye.AddComponent<SphereCollider>().isTrigger = true;
                eye.AddComponent<MonsterGazeReceiver>();

                player = new GameObject("Player");
                var velocity = player.AddComponent<VelocitySourceStub>();
                var vision = player.AddComponent<PlayerVisionModel>();

                var cameraObject = new GameObject("PlayerCamera");
                cameraObject.transform.SetParent(player.transform, false);
                cameraObject.AddComponent<Camera>();
                var detector = cameraObject.AddComponent<PlayerGazeDetector>();

                config = ScriptableObject.CreateInstance<MonsterConfig>();
                safeZones = ScriptableObject.CreateInstance<SafeZoneRuntimeSet>();
                stalkPoints = ScriptableObject.CreateInstance<StalkPointRuntimeSet>();

                Assert.That(rig.Initialize(
                    config,
                    player.transform,
                    velocity,
                    detector,
                    safeZones,
                    stalkPoints,
                    vision), Is.True);

                root.SetActive(true);
            }

            public GameObject Root { get; }
            public MonsterBrain Brain { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(safeZones);
                UnityEngine.Object.DestroyImmediate(stalkPoints);
            }
        }
    }
}
