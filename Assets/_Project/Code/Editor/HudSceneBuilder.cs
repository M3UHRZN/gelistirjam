using Game.Core;
using Game.Gameplay.Interaction;
using Game.Gameplay.Objectives;
using Game.Gameplay.Player;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Reproducibly builds the HUD hierarchy into the active scene.
    /// </summary>
    public static class HudSceneBuilder
    {
        private const string RootName = "HudCanvas";
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float CornerMarginX = 48f;
        private const float CornerMarginY = 44f;
        private const float PromptBottomOffset = 190f;
        private const float CrosshairTickDistance = 10f;

        [MenuItem("Tools/Gloomveil/UI/Build HUD")]
        public static void BuildHud()
        {
            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject(RootName,
                typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup),
                typeof(HudCameraMotion), typeof(HudVisibility));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            CanvasGroup canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            HudCameraMotion motion = canvasObject.GetComponent<HudCameraMotion>();
            Camera playerCamera = Camera.main;
            if (playerCamera != null)
            {
                motion.Bind(playerCamera.transform);
            }
            else
            {
                Debug.LogWarning(
                    "[HUD Builder] No MainCamera found — HUD sway will rest until HudCameraMotion is bound.");
            }

            GameFlowController flow = Object.FindFirstObjectByType<GameFlowController>();
            if (flow != null)
            {
                canvasObject.GetComponent<HudVisibility>().Bind(flow, canvasGroup);
            }
            else
            {
                Debug.LogWarning(
                    "[HUD Builder] No GameFlowController found — HUD visibility control will stay disabled.");
            }

            RectTransform swayRoot = CreateFullScreenChild(canvasObject.transform, "SwayRoot");
            swayRoot.gameObject.AddComponent<HudRootSway>().Bind(motion);

            RectTransform fixedRoot = CreateFullScreenChild(canvasObject.transform, "FixedRoot");

            BuildObjective(swayRoot, motion, font);
            BuildStamina(swayRoot, motion);
            BuildFlashlight(swayRoot, motion, font);
            BuildPrompt(swayRoot, motion, font);
            BuildCrosshair(fixedRoot, motion);

            Undo.RegisterCreatedObjectUndo(canvasObject, "Build HUD");
            EditorSceneManager.MarkSceneDirty(canvasObject.scene);
            Selection.activeGameObject = canvasObject;
            Debug.Log(
                $"[HUD Builder] Built '{RootName}' in scene '{canvasObject.scene.name}'. Save the scene to keep it.");
        }

        private static RectTransform CreateFullScreenChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform CreateChild(Transform parent, string name, Vector2 anchor,
            Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private static Text CreateText(RectTransform parent, string name, Font font, int fontSize,
            TextAnchor alignment)
        {
            RectTransform rect = CreateChild(parent, name, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, parent.sizeDelta);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Image CreateImage(RectTransform parent, string name, Vector2 anchor,
            Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            RectTransform rect = CreateChild(parent, name, anchor, pivot, anchoredPosition, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void BuildObjective(RectTransform parent, HudCameraMotion motion, Font font)
        {
            RectTransform rect = CreateChild(parent, "ObjectiveWidget", new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(CornerMarginX, -CornerMarginY),
                new Vector2(420f, 34f));

            HudSway sway = rect.gameObject.AddComponent<HudSway>();
            sway.Bind(motion);
            AssignSerialized(sway, "pixelsPerDegreePerSecond", new Vector2(-0.055f, 0.038f));

            Text label = CreateText(rect, "Label", font, 24, TextAnchor.UpperLeft);
            label.text = string.Empty;

            ObjectiveWidget widget = rect.gameObject.AddComponent<ObjectiveWidget>();
            ObjectiveManager objectives = Object.FindFirstObjectByType<ObjectiveManager>();
            if (objectives != null)
            {
                widget.Bind(objectives);
            }
            else
            {
                Debug.Log(
                    "[HUD Builder] No ObjectiveManager in this scene — the fuse counter will stand down until one exists.");
            }

            AssignSerialized(widget, "label", label);
        }

        private static void BuildStamina(RectTransform parent, HudCameraMotion motion)
        {
            RectTransform rect = CreateChild(parent, "StaminaWidget", Vector2.zero,
                Vector2.zero, new Vector2(CornerMarginX, CornerMarginY),
                new Vector2(260f, 14f));

            HudSway sway = rect.gameObject.AddComponent<HudSway>();
            sway.Bind(motion);
            AssignSerialized(sway, "pixelsPerDegreePerSecond", new Vector2(-0.07f, 0.048f));

            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            Image background = CreateImage(rect, "Background", Vector2.zero, Vector2.zero,
                Vector2.zero, new Vector2(260f, 14f), new Color(0f, 0f, 0f, 0.45f));
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = Vector2.zero;
            background.rectTransform.offsetMax = Vector2.zero;

            Image fill = CreateImage(rect, "Fill", Vector2.zero, Vector2.zero,
                Vector2.zero, new Vector2(260f, 14f),
                new Color(0.85f, 0.85f, 0.82f, 0.9f));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(2f, 2f);
            fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            StaminaWidget widget = rect.gameObject.AddComponent<StaminaWidget>();
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                widget.Bind(player);
            }
            else
            {
                Debug.LogWarning(
                    "[HUD Builder] No PlayerController found — the stamina bar will stand down.");
            }

            AssignSerialized(widget, "fill", fill);
            AssignSerialized(widget, "group", group);
        }

        private static void BuildFlashlight(RectTransform parent, HudCameraMotion motion, Font font)
        {
            RectTransform rect = CreateChild(parent, "FlashlightWidget", new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(-CornerMarginX, CornerMarginY),
                new Vector2(34f, 34f));

            HudSway sway = rect.gameObject.AddComponent<HudSway>();
            sway.Bind(motion);
            AssignSerialized(sway, "pixelsPerDegreePerSecond", new Vector2(-0.065f, 0.044f));

            Text icon = CreateText(rect, "Icon", font, 22, TextAnchor.MiddleCenter);
            icon.text = "(F)";
            icon.color = new Color(0.5f, 0.5f, 0.5f, 0.45f);

            FlashlightWidget widget = rect.gameObject.AddComponent<FlashlightWidget>();
            FlashlightController flashlight =
                Object.FindFirstObjectByType<FlashlightController>();
            if (flashlight != null)
            {
                widget.Bind(flashlight);
            }
            else
            {
                Debug.LogWarning(
                    "[HUD Builder] No FlashlightController found — the flashlight icon will stand down.");
            }

            AssignSerialized(widget, "icon", icon);
        }

        private static void BuildPrompt(RectTransform parent, HudCameraMotion motion, Font font)
        {
            RectTransform rect = CreateChild(parent, "InteractionPromptWidget",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, PromptBottomOffset), new Vector2(700f, 36f));

            HudSway sway = rect.gameObject.AddComponent<HudSway>();
            sway.Bind(motion);
            AssignSerialized(sway, "pixelsPerDegreePerSecond", new Vector2(-0.03f, 0.02f));

            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;

            Text label = CreateText(rect, "Label", font, 22, TextAnchor.MiddleCenter);
            label.text = string.Empty;

            InteractionPromptWidget widget =
                rect.gameObject.AddComponent<InteractionPromptWidget>();
            PlayerInteraction interaction = Object.FindFirstObjectByType<PlayerInteraction>();
            if (interaction != null)
            {
                widget.Bind(interaction);
            }
            else
            {
                Debug.LogWarning(
                    "[HUD Builder] No PlayerInteraction found — the interaction prompt will stand down.");
            }

            AssignSerialized(widget, "label", label);
            AssignSerialized(widget, "group", group);
        }

        private static void BuildCrosshair(RectTransform parent, HudCameraMotion motion)
        {
            RectTransform rect = CreateChild(parent, "CrosshairWidget",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(64f, 64f));

            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            Color color = Color.white;
            Image dot = CreateImage(rect, "Dot", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 2f), color);

            var ticks = new RectTransform[4];
            var graphics = new Graphic[5];
            graphics[0] = dot;

            Vector2[] directions =
            {
                Vector2.up,
                Vector2.down,
                Vector2.left,
                Vector2.right,
            };
            string[] names = { "TickUp", "TickDown", "TickLeft", "TickRight" };

            for (int i = 0; i < directions.Length; i++)
            {
                bool vertical = i < 2;
                Vector2 size = vertical ? new Vector2(2f, 7f) : new Vector2(7f, 2f);
                Image tick = CreateImage(rect, names[i], new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), directions[i] * CrosshairTickDistance,
                    size, color);
                ticks[i] = tick.rectTransform;
                graphics[i + 1] = tick;
            }

            CrosshairPulse pulse = rect.gameObject.AddComponent<CrosshairPulse>();
            pulse.Bind(motion);
            AssignSerialized(pulse, "ticks", ticks);
            AssignSerialized(pulse, "group", group);

            CrosshairWidget widget = rect.gameObject.AddComponent<CrosshairWidget>();
            PlayerInteraction interaction = Object.FindFirstObjectByType<PlayerInteraction>();
            if (interaction != null)
            {
                widget.Bind(interaction);
            }

            AssignSerialized(widget, "parts", graphics);
        }

        private static void AssignSerialized(Object target, string fieldName, object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError(
                    $"[HUD Builder] {target.GetType().Name} has no serialized field '{fieldName}'.");
                return;
            }

            switch (value)
            {
                case Object unityObject:
                    property.objectReferenceValue = unityObject;
                    break;
                case Vector2 vector:
                    property.vector2Value = vector;
                    break;
                case Object[] array:
                    property.arraySize = array.Length;
                    for (int i = 0; i < array.Length; i++)
                    {
                        property.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
                    }
                    break;
                default:
                    Debug.LogError(
                        $"[HUD Builder] Unsupported value type for '{fieldName}'.");
                    return;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
