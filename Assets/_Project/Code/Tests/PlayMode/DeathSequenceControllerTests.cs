using System.Collections;
using Game.Core;
using Game.Gameplay.Death;
using Game.Gameplay.Monster;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Gameplay.Tests
{
    public sealed class DeathSequenceControllerTests
    {
        [UnityTest]
        public IEnumerator DeathState_LocksCameraAndShowsPanelWithoutAutomaticSceneLoad()
        {
            using var fixture = new DeathSequenceFixture(withAnchor: true);

            fixture.Flow.ChangeState(fixture.Flow.DeathState);
            yield return null;

            Assert.That(fixture.Controller.IsPlaying, Is.True);
            Assert.That(fixture.Controller.KillCameraAnchor, Is.SameAs(fixture.Anchor));
            Assert.That(fixture.Camera.transform.parent, Is.SameAs(fixture.Anchor));
            Assert.That(fixture.Camera.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(fixture.Camera.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(fixture.Controller.DeathUiRoot, Is.Not.Null);
            Assert.That(fixture.Controller.DeathUiRoot.activeSelf, Is.True);
            Assert.That(fixture.RetryCount, Is.Zero);
            Assert.That(fixture.MainMenuCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator GeneratedButtons_InvokeRetryAndMainMenuActions()
        {
            using var fixture = new DeathSequenceFixture(withAnchor: false);

            fixture.Flow.ChangeState(fixture.Flow.DeathState);
            yield return null;

            Button[] buttons = fixture.Controller.DeathUiRoot.GetComponentsInChildren<Button>(true);
            Assert.That(buttons, Has.Length.EqualTo(2));

            buttons[0].onClick.Invoke();
            Assert.That(fixture.RetryCount, Is.EqualTo(1));
            Assert.That(fixture.MainMenuCount, Is.Zero);

            buttons[1].onClick.Invoke();
            Assert.That(fixture.RetryCount, Is.EqualTo(1));
            Assert.That(fixture.MainMenuCount, Is.EqualTo(1));
        }

        private sealed class DeathSequenceFixture : System.IDisposable
        {
            private readonly GameObject flowObject;
            private readonly GameObject cameraObject;
            private readonly GameObject monsterObject;
            private readonly GameObject controllerObject;

            public DeathSequenceFixture(bool withAnchor)
            {
                flowObject = new GameObject("DeathSequenceFixture_Flow");
                Flow = flowObject.AddComponent<GameFlowController>();

                cameraObject = new GameObject("DeathSequenceFixture_Camera", typeof(Camera));
                Camera = cameraObject.GetComponent<Camera>();

                monsterObject = new GameObject("DeathSequenceFixture_Monster");
                monsterObject.SetActive(false);
                MonsterBrain brain = monsterObject.AddComponent<MonsterBrain>();
                if (withAnchor)
                {
                    var anchorObject = new GameObject("KillCameraAnchor");
                    anchorObject.transform.SetParent(monsterObject.transform, false);
                    Anchor = anchorObject.transform;
                }

                controllerObject = new GameObject("DeathSequenceFixture_Controller");
                Controller = controllerObject.AddComponent<DeathSequenceController>();
                Controller.Configure(Flow, Camera, null, 0f, "MainMenu");
                Controller.BindMonster(brain);
                Controller.BindSceneActions(
                    () => RetryCount++,
                    () => MainMenuCount++);
            }

            public GameFlowController Flow { get; }
            public Camera Camera { get; }
            public Transform Anchor { get; }
            public DeathSequenceController Controller { get; }
            public int RetryCount { get; private set; }
            public int MainMenuCount { get; private set; }

            public void Dispose()
            {
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(monsterObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(flowObject);
            }
        }
    }
}
