using System.Collections;
using Game.Gameplay.Entrance;
using Game.Gameplay.Objectives;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class EntranceDoorAndExitTests
    {
        private GameObject parent;
        private GameObject movingObject;
        private GameObject blockerObject;
        private GameObject portalObject;
        private GameObject fuseObject;
        private GameObject controllerObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent);
            Object.DestroyImmediate(blockerObject);
            Object.DestroyImmediate(portalObject);
            Object.DestroyImmediate(fuseObject);
            Object.DestroyImmediate(controllerObject);
        }

        [Test]
        public void ImmediateOpenAndClose_ApplyLocalPosesAndBlockers()
        {
            var door = CreateDoor(out var moving, out var blocker);
            var closedPosition = moving.localPosition;
            var closedRotation = moving.localRotation;

            door.SetOpen(true, immediate: true);

            Assert.That(moving.localPosition, Is.EqualTo(closedPosition + new Vector3(0f, 3f, 0f)));
            Assert.That(Quaternion.Angle(moving.localRotation, closedRotation * Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f));
            Assert.That(blocker.enabled, Is.False);

            door.SetOpen(false, immediate: true);

            Assert.That(moving.localPosition, Is.EqualTo(closedPosition));
            Assert.That(Quaternion.Angle(moving.localRotation, closedRotation), Is.LessThan(0.01f));
            Assert.That(blocker.enabled, Is.True);
        }

        [Test]
        public void UnlockExit_IsIdempotentAndEnablesThePortal()
        {
            var door = CreateDoor(out _, out _);
            fuseObject = new GameObject("Fuse");
            var fuse = fuseObject.AddComponent<FuseBox>();
            portalObject = new GameObject("Portal");
            var portal = portalObject.AddComponent<BoxCollider>();
            portal.enabled = true;
            controllerObject = new GameObject("Exit Controller");
            var exit = controllerObject.AddComponent<EntranceFuseExitController>();
            exit.Bind(fuse, door, portal);

            exit.UnlockExit();
            exit.UnlockExit();

            Assert.That(exit.IsUnlocked, Is.True);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(portal.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator TimedMotion_KeepsBlockersDisabledUntilClosePoseIsReached()
        {
            var door = CreateDoor(out var moving, out var blocker, duration: 0.01f);
            var closedPosition = moving.localPosition;

            door.SetOpen(true);
            Assert.That(blocker.enabled, Is.False);
            yield return new WaitForSecondsRealtime(0.05f);
            Assert.That(moving.localPosition, Is.EqualTo(closedPosition + new Vector3(0f, 3f, 0f)));

            door.SetOpen(false);
            Assert.That(blocker.enabled, Is.False);
            yield return new WaitForSecondsRealtime(0.05f);

            Assert.That(moving.localPosition, Is.EqualTo(closedPosition));
            Assert.That(blocker.enabled, Is.True);
        }

        [Test]
        public void FuseCompletion_UnlocksAfterDisableAndEnable()
        {
            var door = CreateDoor(out _, out _);
            var fuse = CreateFuse("Fuse");
            var portal = CreatePortal();
            var exit = CreateExitController();
            exit.Bind(fuse, door, portal);

            controllerObject.SetActive(false);
            controllerObject.SetActive(true);
            CompleteFuse(fuse);

            Assert.That(exit.IsUnlocked, Is.True);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(portal.enabled, Is.True);
        }

        [Test]
        public void RebindingAfterBoundFuseIsDestroyed_SubscribesToReplacementFuse()
        {
            var door = CreateDoor(out _, out _);
            var firstFuse = CreateFuse("First Fuse");
            var portal = CreatePortal();
            var exit = CreateExitController();
            exit.Bind(firstFuse, door, portal);

            Object.DestroyImmediate(fuseObject);
            fuseObject = null;
            var replacementFuse = CreateFuse("Replacement Fuse");
            exit.Bind(replacementFuse, door, portal);
            CompleteFuse(replacementFuse);

            Assert.That(exit.IsUnlocked, Is.True);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(portal.enabled, Is.True);
        }

        private EntranceDoorActuator CreateDoor(out Transform moving, out Collider blocker, float duration = 0f)
        {
            parent = new GameObject("Door Parent");
            movingObject = new GameObject("Moving Door");
            movingObject.transform.SetParent(parent.transform);
            movingObject.transform.localPosition = new Vector3(1f, 2f, 3f);
            movingObject.transform.localRotation = Quaternion.Euler(12f, 34f, 56f);
            blockerObject = new GameObject("Blocker");
            blocker = blockerObject.AddComponent<BoxCollider>();
            var door = parent.AddComponent<EntranceDoorActuator>();
            door.Bind(movingObject.transform, new Vector3(0f, 3f, 0f), new Vector3(0f, 90f, 0f), duration, new[] { blocker });
            moving = movingObject.transform;
            return door;
        }

        private FuseBox CreateFuse(string name)
        {
            fuseObject = new GameObject(name);
            return fuseObject.AddComponent<FuseBox>();
        }

        private Collider CreatePortal()
        {
            portalObject = new GameObject("Portal");
            return portalObject.AddComponent<BoxCollider>();
        }

        private EntranceFuseExitController CreateExitController()
        {
            controllerObject = new GameObject("Exit Controller");
            return controllerObject.AddComponent<EntranceFuseExitController>();
        }

        private static void CompleteFuse(FuseBox fuse)
        {
            fuse.SendMessage("HandleFullyComplete", SendMessageOptions.RequireReceiver);
        }
    }
}
