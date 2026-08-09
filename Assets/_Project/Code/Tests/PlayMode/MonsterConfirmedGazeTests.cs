using System;
using System.Collections;
using System.Reflection;
using Game.Gameplay.Monster;
using Game.Gameplay.Player;
using Game.Gameplay.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class MonsterConfirmedGazeTests
    {
        private const string ControllerPath = "Assets/_Project/Animations/MonsterAnimator.controller";

        public sealed class VelocitySourceStub : MonoBehaviour, IPlayerVelocitySource
        {
            public Vector3 Velocity => Vector3.zero;
        }

        [UnityTest]
        public IEnumerator ContinuousConfirmedGaze_AddsOneAngerRaisesOneEventAndEntersYell()
        {
            using var fixture = new GazeFixture();
            Assert.That(fixture.Initialize(), Is.True, "The complete inactive monster rig must initialize.");

            EventInfo gazeStartedEvent = typeof(MonsterBrain).GetEvent("OnConfirmedGazeStarted");
            int gazeStartedCount = 0;
            Action handler = () => gazeStartedCount++;
            gazeStartedEvent?.AddEventHandler(fixture.Brain, handler);

            fixture.Root.SetActive(true);
            Physics.SyncTransforms();

            float confirmDeadline = Time.time + 1.5f;
            while (!fixture.Brain.IsGazeConfirmed && Time.time < confirmDeadline)
            {
                yield return null;
            }

            bool enteredYell = false;
            for (int frame = 0; frame < 15 && !enteredYell; frame++)
            {
                if (fixture.Animator.isInitialized)
                {
                    AnimatorStateInfo current = fixture.Animator.GetCurrentAnimatorStateInfo(0);
                    AnimatorStateInfo next = fixture.Animator.GetNextAnimatorStateInfo(0);
                    enteredYell = current.IsName("Yell") || next.IsName("Yell");
                }

                if (!enteredYell) yield return null;
            }

            float holdDeadline = Time.time + 0.35f;
            while (Time.time < holdDeadline) yield return null;

            Assert.That(gazeStartedEvent, Is.Not.Null,
                "MonsterBrain must publish the confirmed-gaze rising edge.");
            Assert.That(fixture.Brain.IsGazeConfirmed, Is.True,
                "The real gaze detector must keep the gaze confirmed while the camera holds.");
            Assert.That(fixture.Anger.Anger, Is.EqualTo(1),
                "One continuous confirmed gaze must add spotted anger exactly once.");
            Assert.That(gazeStartedCount, Is.EqualTo(1),
                "One continuous confirmed gaze must publish exactly one start event.");
            Assert.That(enteredYell, Is.True,
                "The confirmed-gaze event must drive the production Animator into Yell.");

            gazeStartedEvent?.RemoveEventHandler(fixture.Brain, handler);
        }

        private sealed class GazeFixture : IDisposable
        {
            private readonly VelocitySourceStub velocitySource;
            private readonly PlayerGazeDetector gazeDetector;
            private readonly PlayerVisionModel visionModel;
            private readonly MonsterRuntimeRig rig;

            public GazeFixture()
            {
                Root = new GameObject("ConfirmedGazeMonster");
                Root.SetActive(false);

                NavMeshAgent agent = Root.AddComponent<NavMeshAgent>();
                agent.enabled = false;
                Root.AddComponent<MonsterNavigator>();
                Root.AddComponent<MonsterPerception>();
                Anger = Root.AddComponent<MonsterAngerController>();
                Brain = Root.AddComponent<MonsterBrain>();
                Animator = Root.AddComponent<Animator>();
                Animator.runtimeAnimatorController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
                Root.AddComponent<MonsterAnimationController>();
                Root.AddComponent<MonsterVisualDriver>();
                rig = Root.AddComponent<MonsterRuntimeRig>();

                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(Root.transform, false);
                body.transform.localPosition = Vector3.up;
                body.GetComponent<Collider>().enabled = false;

                GameObject eye = new GameObject("Eye");
                eye.transform.SetParent(Root.transform, false);
                eye.transform.localPosition = Vector3.up * 1.6f;
                SphereCollider eyeCollider = eye.AddComponent<SphereCollider>();
                eyeCollider.isTrigger = true;
                eyeCollider.radius = 0.75f;
                eye.AddComponent<MonsterGazeReceiver>();

                Player = new GameObject("ConfirmedGazePlayer");
                Player.transform.position = new Vector3(0f, 0f, -20f);
                velocitySource = Player.AddComponent<VelocitySourceStub>();
                visionModel = Player.AddComponent<PlayerVisionModel>();

                GameObject cameraObject = new GameObject("PlayerCamera");
                cameraObject.transform.SetParent(Player.transform, false);
                cameraObject.transform.localPosition = Vector3.up * 1.6f;
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                gazeDetector = cameraObject.AddComponent<PlayerGazeDetector>();

                Config = ScriptableObject.CreateInstance<MonsterConfig>();
                SafeZones = ScriptableObject.CreateInstance<SafeZoneRuntimeSet>();
                StalkPoints = ScriptableObject.CreateInstance<StalkPointRuntimeSet>();
            }

            public GameObject Root { get; }
            public GameObject Player { get; }
            public MonsterBrain Brain { get; }
            public MonsterAngerController Anger { get; }
            public Animator Animator { get; }
            public MonsterConfig Config { get; }
            public SafeZoneRuntimeSet SafeZones { get; }
            public StalkPointRuntimeSet StalkPoints { get; }

            public bool Initialize() => rig.Initialize(
                Config,
                Player.transform,
                velocitySource,
                gazeDetector,
                SafeZones,
                StalkPoints,
                visionModel);

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(Player);
                UnityEngine.Object.DestroyImmediate(Config);
                UnityEngine.Object.DestroyImmediate(SafeZones);
                UnityEngine.Object.DestroyImmediate(StalkPoints);
            }
        }
    }
}
