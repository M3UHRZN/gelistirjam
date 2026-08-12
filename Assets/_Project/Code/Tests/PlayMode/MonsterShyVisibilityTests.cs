using System;
using Game.Gameplay.Monster;
using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace Game.Gameplay.Tests
{
    public sealed class MonsterShyVisibilityTests
    {
        [Test]
        public void BodyVisibility_RejectsFullOcclusionButDetectsPartialExposure()
        {
            using var fixture = new VisibilityFixture();
            Vector3 candidateFeet = new Vector3(0f, 0f, 6f);

            Assert.That(fixture.Vision.IsMonsterBodyVisibleAt(candidateFeet, 2.2f, 0.35f), Is.True,
                "An unobstructed body inside the real camera viewport must be visible.");

            GameObject wall = fixture.CreateOccluder(
                "FullWall",
                new Vector3(0f, 1.1f, 3f),
                new Vector3(4f, 3f, 0.2f));

            Assert.That(fixture.Vision.IsMonsterBodyVisibleAt(candidateFeet, 2.2f, 0.35f), Is.False,
                "A wall covering every body sample must hide the candidate.");

            wall.transform.localScale = new Vector3(0.2f, 3f, 0.2f);
            Physics.SyncTransforms();

            Assert.That(fixture.Vision.IsMonsterBodyVisibleAt(candidateFeet, 2.2f, 0.35f), Is.True,
                "One unobstructed side-torso sample must expose a partially visible candidate.");
        }

        [Test]
        public void BodyVisibility_FloorOrCeilingOccludesAnotherStorey()
        {
            using var fixture = new VisibilityFixture();
            Vector3 candidateFeet = new Vector3(0f, 4f, 6f);
            fixture.Camera.transform.LookAt(candidateFeet + Vector3.up * 1.1f);
            Physics.SyncTransforms();

            Assert.That(fixture.Vision.IsMonsterBodyVisibleAt(candidateFeet, 2.2f, 0.35f), Is.True,
                "The upper-storey body must be visible before geometry separates the floors.");

            fixture.CreateOccluder(
                "FloorSlab",
                new Vector3(0f, 3f, 3f),
                new Vector3(5f, 0.25f, 8f));

            Assert.That(fixture.Vision.IsMonsterBodyVisibleAt(candidateFeet, 2.2f, 0.35f), Is.False,
                "A floor or ceiling slab between camera and body must occlude every sample.");
        }

        [Test]
        public void BodyVisibility_UsesHorizontalAndVerticalCameraViewport()
        {
            using var fixture = new VisibilityFixture();

            Assert.That(fixture.Vision.IsMonsterBodyVisibleAt(
                new Vector3(8f, 0f, 6f), 2.2f, 0.35f), Is.False,
                "A body beyond the camera's horizontal FOV must not count as visible.");
            Assert.That(fixture.Vision.IsMonsterBodyVisibleAt(
                new Vector3(0f, 8f, 6f), 2.2f, 0.35f), Is.False,
                "A body beyond the camera's vertical FOV must not count as visible.");
        }

        [Test]
        public void BodyVisibility_DoesNotTreatTheCurrentMonsterBodyAsEnvironmentOcclusion()
        {
            using var fixture = new VisibilityFixture();
            fixture.CreateCurrentMonsterBody(new Vector3(0f, 0f, 2.5f));

            Assert.That(fixture.Vision.IsMonsterBodyVisibleAt(
                new Vector3(0f, 0f, 6f), 2.2f, 0.35f), Is.True,
                "The monster's current body must not hide its hypothetical destination from the player camera.");
        }

        private sealed class VisibilityFixture : IDisposable
        {
            private readonly GameObject root;
            private readonly GameObject perceptionObject;

            public VisibilityFixture()
            {
                root = new GameObject("ShyVisibilityFixture");

                var player = new GameObject("Player");
                player.transform.SetParent(root.transform, false);
                Vision = player.AddComponent<PlayerVisionModel>();

                var cameraObject = new GameObject("PlayerCamera");
                cameraObject.transform.SetParent(player.transform, false);
                cameraObject.transform.localPosition = Vector3.up * 1.1f;
                Camera = cameraObject.AddComponent<Camera>();
                Camera.enabled = false;
                Camera.fieldOfView = 60f;
                Camera.aspect = 16f / 9f;
                Camera.nearClipPlane = 0.01f;
                Camera.farClipPlane = 50f;

                perceptionObject = new GameObject("MonsterPerception");
                perceptionObject.transform.SetParent(root.transform, false);
                MonsterPerception perception = perceptionObject.AddComponent<MonsterPerception>();
                perception.Bind(player.transform);

                Vision.Bind(player.transform, Camera, perception);
                Physics.SyncTransforms();
            }

            public Camera Camera { get; }
            public PlayerVisionModel Vision { get; }

            public GameObject CreateOccluder(string name, Vector3 position, Vector3 scale)
            {
                GameObject occluder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                occluder.name = name;
                occluder.transform.SetParent(root.transform, false);
                occluder.transform.position = position;
                occluder.transform.localScale = scale;
                Physics.SyncTransforms();
                return occluder;
            }

            public void CreateCurrentMonsterBody(Vector3 position)
            {
                perceptionObject.transform.position = position;
                CapsuleCollider body = perceptionObject.AddComponent<CapsuleCollider>();
                body.center = Vector3.up * 1.1f;
                body.height = 2.2f;
                body.radius = 0.35f;
                Physics.SyncTransforms();
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
