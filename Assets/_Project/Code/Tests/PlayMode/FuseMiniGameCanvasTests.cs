using System.Collections;
using System;
using System.Linq;
using System.Reflection;
using Game.Gameplay.Player;
using Game.Gameplay.Objectives;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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

        [UnityTest]
        public IEnumerator SetupWithLegacyController_RebuildsWorldSpaceUi()
        {
            GameObject legacyCanvasObject = new GameObject("FuseMiniGameCanvas", typeof(Canvas));
            Canvas legacyCanvas = legacyCanvasObject.GetComponent<Canvas>();
            legacyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject panel = new GameObject("FusePanel", typeof(RectTransform));
            panel.transform.SetParent(legacyCanvasObject.transform, false);

            GameObject legacyControllerObject = new GameObject("FuseMiniGameController");
            FuseMiniGameController legacyController = legacyControllerObject.AddComponent<FuseMiniGameController>();
            WireLegacyPanel(legacyController, panel);

            GameObject cameraObject = new GameObject("MainCamera", typeof(Camera));
            Camera playerCamera = cameraObject.GetComponent<Camera>();
            cameraObject.tag = "MainCamera";
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            try
            {
                MethodInfo ensureUi = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("Game.Editor.MainSceneFuseSetup"))
                    .FirstOrDefault(type => type != null)
                    .GetMethod("EnsureFuseMiniGameUI", BindingFlags.NonPublic | BindingFlags.Static);

                FuseMiniGameController rebuilt = (FuseMiniGameController)ensureUi.Invoke(null, null);
                SerializedObject serialized = new SerializedObject(rebuilt);
                Canvas worldCanvas = (Canvas)serialized.FindProperty("worldCanvas").objectReferenceValue;

                Assert.That(rebuilt, Is.Not.SameAs(legacyController));
                Assert.That(worldCanvas, Is.Not.Null);
                Assert.That(worldCanvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
                Assert.That(worldCanvas.worldCamera, Is.SameAs(playerCamera));
                Assert.That(serialized.FindProperty("crosshairRoot").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("pointer").objectReferenceValue, Is.Not.Null);

                Object.DestroyImmediate(rebuilt.gameObject);
                Object.DestroyImmediate(worldCanvas.gameObject);
                Object.DestroyImmediate((serialized.FindProperty("crosshairRoot").objectReferenceValue as GameObject));
                Object.DestroyImmediate((serialized.FindProperty("pointer").objectReferenceValue as FuseScreenPointer).gameObject);
            }
            finally
            {
                if (legacyControllerObject != null) Object.DestroyImmediate(legacyControllerObject);
                if (legacyCanvasObject != null) Object.DestroyImmediate(legacyCanvasObject);
                Object.DestroyImmediate(eventSystemObject);
                Object.DestroyImmediate(cameraObject);
            }

            yield return null;
        }

        private static void WireLegacyPanel(FuseMiniGameController controller, GameObject panel)
        {
            Button[] buttons = new Button[4];
            Image[] images = new Image[4];
            for (int i = 0; i < buttons.Length; i++)
            {
                GameObject buttonObject = new GameObject("Button_" + i, typeof(RectTransform));
                buttonObject.transform.SetParent(panel.transform, false);
                images[i] = buttonObject.AddComponent<Image>();
                buttons[i] = buttonObject.AddComponent<Button>();
                buttons[i].targetGraphic = images[i];
            }

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("panelRoot").objectReferenceValue = panel;
            SerializedProperty buttonsProperty = serialized.FindProperty("buttons");
            SerializedProperty imagesProperty = serialized.FindProperty("buttonImages");
            buttonsProperty.arraySize = buttons.Length;
            imagesProperty.arraySize = images.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttonsProperty.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                imagesProperty.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
