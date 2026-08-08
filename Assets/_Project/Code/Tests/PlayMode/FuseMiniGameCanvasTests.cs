using System.Collections;
using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class FuseMiniGameCanvasTests
    {
        [UnityTest]
        public IEnumerator DisablingPlayerController_LeavesCursorLocked()
        {
            var playerObject = new GameObject(
                "CursorOwnerFixture_Player",
                typeof(Rigidbody),
                typeof(CapsuleCollider));
            PlayerController controller = playerObject.AddComponent<PlayerController>();

            try
            {
                yield return null;
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));

                controller.SetControlEnabled(false);
                yield return null;

                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
                Assert.That(Cursor.visible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
