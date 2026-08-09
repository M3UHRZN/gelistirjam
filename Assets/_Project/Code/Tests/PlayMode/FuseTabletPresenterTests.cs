using System;
using System.Collections;
using Game.Gameplay.Objectives;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class FuseTabletPresenterTests
    {
        [UnityTest]
        public IEnumerator Activate_RaisesFuseViewPriorityAndSetsBlendIn()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Presenter.Activate();

                Assert.That(fixture.FuseView.Priority.Value, Is.EqualTo(20));
                Assert.That(fixture.Brain.DefaultBlend.Time, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(fixture.Presenter.IsActive, Is.True);
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator Deactivate_RestoresIdlePriorityAndSetsBlendOut()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Presenter.Activate();
                fixture.Presenter.Deactivate();

                Assert.That(fixture.FuseView.Priority.Value, Is.EqualTo(0));
                Assert.That(fixture.Brain.DefaultBlend.Time, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(fixture.Presenter.IsActive, Is.False);
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator ApplyLook_ClampsYawAndPitchToConfiguredLimits()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Presenter.Activate();
                fixture.Presenter.ApplyLook(new Vector2(500f, -500f));

                Vector3 euler = fixture.FuseView.transform.localEulerAngles;
                float yaw = Mathf.DeltaAngle(0f, euler.y);
                float pitch = Mathf.DeltaAngle(0f, euler.x);

                Assert.That(yaw, Is.EqualTo(15f).Within(0.01f));
                Assert.That(pitch, Is.EqualTo(10f).Within(0.01f));
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator Activate_ResetsLookFromAPreviousInteraction()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Presenter.Activate();
                fixture.Presenter.ApplyLook(new Vector2(500f, 500f));
                fixture.Presenter.Deactivate();

                fixture.Presenter.Activate();

                Assert.That(
                    Quaternion.Angle(fixture.FuseView.transform.localRotation, Quaternion.identity),
                    Is.LessThan(0.01f));
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator PlayCompletion_RotatesLeverToTheOpenPose()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Presenter.Activate();
                yield return fixture.Presenter.StartCoroutine(fixture.Presenter.PlayCompletion());

                Assert.That(
                    Quaternion.Angle(
                        fixture.Lever.localRotation,
                        Quaternion.Euler(0f, 0f, -70f)),
                    Is.LessThan(0.5f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Activate_SnapsLeverBackToClosedAfterAPreviousCompletion()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Presenter.Activate();
                yield return fixture.Presenter.StartCoroutine(fixture.Presenter.PlayCompletion());

                fixture.Presenter.Activate();

                Assert.That(
                    Quaternion.Angle(fixture.Lever.localRotation, Quaternion.identity),
                    Is.LessThan(0.01f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        internal static Fixture CreateFixture()
        {
            var brainObject = new GameObject("FusePresenterFixture_Brain", typeof(Camera));
            CinemachineBrain brain = brainObject.AddComponent<CinemachineBrain>();

            var tablet = new GameObject("FusePresenterFixture_Tablet");

            var screenAnchor = new GameObject("Anchor_Screen");
            screenAnchor.transform.SetParent(tablet.transform, false);

            var cameraAnchor = new GameObject("Anchor_CameraView");
            cameraAnchor.transform.SetParent(tablet.transform, false);

            var fuseViewObject = new GameObject("CM_FuseView");
            fuseViewObject.transform.SetParent(cameraAnchor.transform, false);
            CinemachineCamera fuseView = fuseViewObject.AddComponent<CinemachineCamera>();
            fuseView.Priority = new PrioritySettings { Enabled = true, Value = 0 };

            var lever = new GameObject("Lever");
            lever.transform.SetParent(tablet.transform, false);

            FuseTabletPresenter presenter = tablet.AddComponent<FuseTabletPresenter>();
            var serialized = new UnityEditor.SerializedObject(presenter);
            serialized.FindProperty("screenAnchor").objectReferenceValue = screenAnchor.transform;
            serialized.FindProperty("fuseViewCamera").objectReferenceValue = fuseView;
            serialized.FindProperty("lever").objectReferenceValue = lever.transform;
            serialized.FindProperty("leverTweenDuration").floatValue = 0.05f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            presenter.Bind(brain);

            return new Fixture(brainObject, tablet, brain, fuseView, presenter, lever.transform);
        }

        internal sealed class Fixture : IDisposable
        {
            private readonly GameObject brainObject;
            private readonly GameObject tablet;

            public CinemachineBrain Brain { get; }
            public CinemachineCamera FuseView { get; }
            public FuseTabletPresenter Presenter { get; }
            public Transform Lever { get; }

            public Fixture(
                GameObject brainObject,
                GameObject tablet,
                CinemachineBrain brain,
                CinemachineCamera fuseView,
                FuseTabletPresenter presenter,
                Transform leverTransform)
            {
                this.brainObject = brainObject;
                this.tablet = tablet;
                Brain = brain;
                FuseView = fuseView;
                Presenter = presenter;
                Lever = leverTransform;
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(tablet);
                UnityEngine.Object.DestroyImmediate(brainObject);
            }
        }
    }
}
