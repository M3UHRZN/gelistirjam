using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Interaction;
using Game.Gameplay.Objectives;
using Game.Gameplay.Player;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Reproducibly builds the Arızalı Tesis Terminali HUD into the active scene.
    /// </summary>
    public static class HudSceneBuilder
    {
        private const string RootName = "HudCanvas";
        private const string CandidateName = "__HudCanvas_Building";
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float CornerMarginX = 48f;
        private const float CornerMarginY = 44f;
        private const float PromptBottomOffset = 190f;
        private const float CrosshairTickDistance = 10f;

        [MenuItem("Tools/Gloomveil/UI/Build HUD")]
        public static void BuildHud()
        {
            TMP_FontAsset font = HudTerminalFactory.EnsureFontAsset();
            if (font == null) return;

            Scene scene = SceneManager.GetActiveScene();
            GameObject candidate = null;

            try
            {
                candidate = BuildCandidate(font);
                ValidateCandidate(candidate);
            }
            catch (Exception exception)
            {
                if (candidate != null)
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                }

                Debug.LogException(new InvalidOperationException(
                    "[HUD Builder] Retro terminal construction failed; " +
                    "existing HUD was preserved.", exception));
                return;
            }

            int undoGroup = -1;
            try
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Build Retro Terminal HUD");
                Undo.RegisterCreatedObjectUndo(candidate, "Build Retro Terminal HUD");
                Undo.RegisterFullObjectHierarchyUndo(candidate, "Build Retro Terminal HUD");

                List<GameObject> existingRoots = FindNamedRoots(scene, RootName);
                for (int i = 0; i < existingRoots.Count; i++)
                {
                    Undo.DestroyObjectImmediate(existingRoots[i]);
                }

                candidate.name = RootName;
                candidate.SetActive(true);

                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log(
                    $"[HUD Builder] Built retro terminal '{RootName}' in scene '{scene.name}'. " +
                    "Save the scene to keep it.");
            }
            catch (Exception exception)
            {
                bool rollbackSucceeded = false;
                try
                {
                    if (undoGroup >= 0)
                    {
                        Undo.RevertAllDownToGroup(undoGroup);
                    }
                    else if (candidate != null)
                    {
                        UnityEngine.Object.DestroyImmediate(candidate);
                    }

                    rollbackSucceeded = true;
                }
                catch (Exception rollbackException)
                {
                    Debug.LogException(new InvalidOperationException(
                        "[HUD Builder] Retro terminal rollback also failed.",
                        rollbackException));
                }

                if (candidate != null)
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                }

                Debug.LogException(new InvalidOperationException(
                    rollbackSucceeded
                        ? "[HUD Builder] Retro terminal build failed; existing HUD was preserved."
                        : "[HUD Builder] Retro terminal build and rollback failed; inspect the scene.",
                    exception));
            }
        }

        private static GameObject BuildCandidate(TMP_FontAsset font)
        {
            var canvasObject = new GameObject(CandidateName, typeof(RectTransform));
            try
            {
                PopulateCandidate(canvasObject, font);
                return canvasObject;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
                throw;
            }
        }

        private static void PopulateCandidate(GameObject canvasObject, TMP_FontAsset font)
        {
            canvasObject.SetActive(false);
            canvasObject.AddComponent<Canvas>();
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<CanvasGroup>();
            canvasObject.AddComponent<HudCameraMotion>();
            canvasObject.AddComponent<HudVisibility>();

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
                    "[HUD Builder] No MainCamera found — HUD sway will rest until bound.");
            }

            GameFlowController flow =
                UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
            if (flow != null)
            {
                canvasObject.GetComponent<HudVisibility>().Bind(flow, canvasGroup);
            }
            else
            {
                Debug.LogWarning(
                    "[HUD Builder] No GameFlowController found — HUD visibility stays disabled.");
            }

            RectTransform swayRoot =
                HudTerminalFactory.CreateFullScreenChild(canvasObject.transform, "SwayRoot");
            swayRoot.gameObject.AddComponent<HudRootSway>().Bind(motion);

            RectTransform fixedRoot =
                HudTerminalFactory.CreateFullScreenChild(canvasObject.transform, "FixedRoot");

            BuildObjective(swayRoot, motion, font);
            BuildStamina(swayRoot, motion, font);
            BuildFlashlight(swayRoot, motion, font);
            BuildPrompt(swayRoot, motion, font);
            BuildCrosshair(fixedRoot, motion);
        }

        private static void BuildObjective(RectTransform parent, HudCameraMotion motion,
            TMP_FontAsset font)
        {
            RectTransform rect = HudTerminalFactory.CreateChild(parent, "ObjectiveWidget",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(CornerMarginX, -CornerMarginY), new Vector2(460f, 66f));

            HudSway sway = rect.gameObject.AddComponent<HudSway>();
            sway.Bind(motion);
            HudTerminalFactory.AssignSerialized(sway, "pixelsPerDegreePerSecond",
                new Vector2(-0.055f, 0.038f));

            CanvasGroup group = AddPassiveCanvasGroup(rect, 1f);
            HudTerminalFactory.CreatePanel(rect);

            RectTransform headerRect = HudTerminalFactory.CreateChild(rect, "Header",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -7f), new Vector2(430f, 15f));
            HudTerminalFactory.AddText(headerRect, font, 11f,
                TextAlignmentOptions.TopLeft, HudTerminalFactory.MutedPhosphor,
                "GRID://POWER");

            RectTransform valueRect = HudTerminalFactory.CreateChild(rect, "Value",
                Vector2.zero, Vector2.zero, new Vector2(14f, 9f),
                new Vector2(430f, 31f));
            TextMeshProUGUI value = HudTerminalFactory.AddText(valueRect, font, 22f,
                TextAlignmentOptions.MidlineLeft, HudTerminalFactory.Phosphor, string.Empty);

            ObjectiveWidget widget = rect.gameObject.AddComponent<ObjectiveWidget>();
            ObjectiveManager objectives =
                UnityEngine.Object.FindFirstObjectByType<ObjectiveManager>();
            if (objectives != null)
            {
                widget.Bind(objectives);
            }
            else
            {
                group.alpha = 0f;
                Debug.Log(
                    "[HUD Builder] No ObjectiveManager — objective terminal is hidden.");
            }

            HudTerminalFactory.AssignSerialized(widget, "label", value);
            HudTerminalFactory.AssignSerialized(widget, "group", group);
        }

        private static void BuildStamina(RectTransform parent, HudCameraMotion motion,
            TMP_FontAsset font)
        {
            RectTransform rect = HudTerminalFactory.CreateChild(parent, "StaminaWidget",
                Vector2.zero, Vector2.zero,
                new Vector2(CornerMarginX, CornerMarginY), new Vector2(320f, 62f));

            HudSway sway = rect.gameObject.AddComponent<HudSway>();
            sway.Bind(motion);
            HudTerminalFactory.AssignSerialized(sway, "pixelsPerDegreePerSecond",
                new Vector2(-0.07f, 0.048f));

            CanvasGroup group = AddPassiveCanvasGroup(rect, 1f);
            HudTerminalFactory.CreatePanel(rect);

            RectTransform headerRect = HudTerminalFactory.CreateChild(rect, "Header",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -7f), new Vector2(292f, 14f));
            HudTerminalFactory.AddText(headerRect, font, 10f,
                TextAlignmentOptions.TopLeft, HudTerminalFactory.MutedPhosphor,
                "RUN://CAPACITY");

            RectTransform bar = HudTerminalFactory.CreateChild(rect, "Bar", Vector2.zero,
                Vector2.zero, new Vector2(14f, 10f), new Vector2(292f, 16f));

            Image background = HudTerminalFactory.CreateImage(bar, "Background",
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
                new Color32(121, 255, 155, 35));
            HudTerminalFactory.Stretch(background.rectTransform, Vector2.zero, Vector2.zero);

            Image fill = HudTerminalFactory.CreateImage(bar, "Fill", Vector2.zero,
                Vector2.zero, Vector2.zero, Vector2.zero, HudTerminalFactory.Phosphor);
            HudTerminalFactory.Stretch(fill.rectTransform, new Vector2(2f, 2f),
                new Vector2(-2f, -2f));
            fill.sprite = HudTerminalFactory.RequireFilledImageSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            for (int i = 1; i < 12; i++)
            {
                float normalized = i / 12f;
                float anchorX = Mathf.Lerp(2f / 292f, 290f / 292f, normalized);
                HudTerminalFactory.CreateImage(bar, $"Segment{i:00}",
                    new Vector2(anchorX, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(2f, 12f), HudTerminalFactory.Base);
            }

            StaminaWidget widget = rect.gameObject.AddComponent<StaminaWidget>();
            PlayerController player =
                UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                widget.Bind(player);
            }
            else
            {
                group.alpha = 0f;
                Debug.LogWarning(
                    "[HUD Builder] No PlayerController — stamina terminal is hidden.");
            }

            HudTerminalFactory.AssignSerialized(widget, "fill", fill);
            HudTerminalFactory.AssignSerialized(widget, "group", group);
        }

        private static void BuildFlashlight(RectTransform parent, HudCameraMotion motion,
            TMP_FontAsset font)
        {
            RectTransform rect = HudTerminalFactory.CreateChild(parent, "FlashlightWidget",
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-CornerMarginX, CornerMarginY), new Vector2(260f, 62f));

            HudSway sway = rect.gameObject.AddComponent<HudSway>();
            sway.Bind(motion);
            HudTerminalFactory.AssignSerialized(sway, "pixelsPerDegreePerSecond",
                new Vector2(-0.065f, 0.044f));

            CanvasGroup group = AddPassiveCanvasGroup(rect, 1f);
            HudTerminalFactory.CreatePanel(rect);

            RectTransform headerRect = HudTerminalFactory.CreateChild(rect, "Header",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -7f), new Vector2(232f, 14f));
            HudTerminalFactory.AddText(headerRect, font, 10f,
                TextAlignmentOptions.TopLeft, HudTerminalFactory.MutedPhosphor,
                "AUX://LIGHT");

            RectTransform keyRect = HudTerminalFactory.CreateChild(rect, "Key", Vector2.zero,
                Vector2.zero, new Vector2(14f, 8f), new Vector2(42f, 29f));
            TextMeshProUGUI key = HudTerminalFactory.AddText(keyRect, font, 18f,
                TextAlignmentOptions.Center, HudTerminalFactory.Phosphor, "[F]");

            RectTransform labelRect = HudTerminalFactory.CreateChild(rect, "Label", Vector2.zero,
                Vector2.zero, new Vector2(62f, 8f), new Vector2(90f, 29f));
            TextMeshProUGUI label = HudTerminalFactory.AddText(labelRect, font, 18f,
                TextAlignmentOptions.MidlineLeft, HudTerminalFactory.Phosphor, "FENER");

            RectTransform stateRect = HudTerminalFactory.CreateChild(rect, "State",
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-14f, 8f), new Vector2(84f, 29f));
            TextMeshProUGUI state = HudTerminalFactory.AddText(stateRect, font, 22f,
                TextAlignmentOptions.MidlineRight, HudTerminalFactory.MutedPhosphor,
                "KAPALI");

            FlashlightWidget widget = rect.gameObject.AddComponent<FlashlightWidget>();
            FlashlightController flashlight =
                UnityEngine.Object.FindFirstObjectByType<FlashlightController>();
            if (flashlight != null)
            {
                widget.Bind(flashlight);
            }
            else
            {
                group.alpha = 0f;
                Debug.LogWarning(
                    "[HUD Builder] No FlashlightController — flashlight terminal is hidden.");
            }

            HudTerminalFactory.AssignSerialized(widget, "stateLabel", state);
            HudTerminalFactory.AssignSerialized(widget, "visualParts",
                new Graphic[] { key, label, state });
            HudTerminalFactory.AssignSerialized(widget, "group", group);
        }

        private static void BuildPrompt(RectTransform parent, HudCameraMotion motion,
            TMP_FontAsset font)
        {
            RectTransform rect = HudTerminalFactory.CreateChild(parent,
                "InteractionPromptWidget", new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, PromptBottomOffset),
                new Vector2(720f, 52f));

            HudSway sway = rect.gameObject.AddComponent<HudSway>();
            sway.Bind(motion);
            HudTerminalFactory.AssignSerialized(sway, "pixelsPerDegreePerSecond",
                new Vector2(-0.03f, 0.02f));

            CanvasGroup group = AddPassiveCanvasGroup(rect, 0f);
            HudTerminalFactory.CreatePanel(rect);

            RectTransform headerRect = HudTerminalFactory.CreateChild(rect, "Header",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -6f), new Vector2(692f, 13f));
            HudTerminalFactory.AddText(headerRect, font, 9f,
                TextAlignmentOptions.TopLeft, HudTerminalFactory.MutedPhosphor,
                "INPUT://CONTEXT");

            RectTransform promptRect = HudTerminalFactory.CreateChild(rect, "Prompt",
                Vector2.zero, Vector2.zero, new Vector2(14f, 5f),
                new Vector2(692f, 29f));
            TextMeshProUGUI prompt = HudTerminalFactory.AddText(promptRect, font, 18f,
                TextAlignmentOptions.MidlineLeft, HudTerminalFactory.Phosphor, string.Empty);

            InteractionPromptWidget widget =
                rect.gameObject.AddComponent<InteractionPromptWidget>();
            PlayerInteraction interaction =
                UnityEngine.Object.FindFirstObjectByType<PlayerInteraction>();
            if (interaction != null)
            {
                widget.Bind(interaction);
            }
            else
            {
                Debug.LogWarning(
                    "[HUD Builder] No PlayerInteraction — prompt terminal is hidden.");
            }

            HudTerminalFactory.AssignSerialized(widget, "label", prompt);
            HudTerminalFactory.AssignSerialized(widget, "group", group);
        }

        private static void BuildCrosshair(RectTransform parent, HudCameraMotion motion)
        {
            RectTransform rect = HudTerminalFactory.CreateChild(parent, "CrosshairWidget",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(64f, 64f));

            CanvasGroup group = AddPassiveCanvasGroup(rect, 1f);

            Image dot = HudTerminalFactory.CreateImage(rect, "DotDiamond",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(3f, 3f), HudTerminalFactory.MutedPhosphor);
            dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

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
                Vector2 size = vertical ? new Vector2(1.5f, 8f) : new Vector2(8f, 1.5f);
                Image tick = HudTerminalFactory.CreateImage(rect, names[i],
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    directions[i] * CrosshairTickDistance, size,
                    HudTerminalFactory.MutedPhosphor);
                ticks[i] = tick.rectTransform;
                graphics[i + 1] = tick;
            }

            CrosshairPulse pulse = rect.gameObject.AddComponent<CrosshairPulse>();
            pulse.Bind(motion);
            HudTerminalFactory.AssignSerialized(pulse, "ticks", ticks);
            HudTerminalFactory.AssignSerialized(pulse, "group", group);

            CrosshairWidget widget = rect.gameObject.AddComponent<CrosshairWidget>();
            PlayerInteraction interaction =
                UnityEngine.Object.FindFirstObjectByType<PlayerInteraction>();
            if (interaction != null)
            {
                widget.Bind(interaction);
            }

            HudTerminalFactory.AssignSerialized(widget, "parts", graphics);
        }

        private static CanvasGroup AddPassiveCanvasGroup(RectTransform rect, float alpha)
        {
            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = alpha;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        private static List<GameObject> FindNamedRoots(Scene scene, string rootName)
        {
            var results = new List<GameObject>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == rootName) results.Add(roots[i]);
            }

            return results;
        }

        private static void ValidateCandidate(GameObject candidate)
        {
            if (candidate.GetComponentsInChildren<TerminalPanelGraphic>(true).Length != 4)
            {
                throw new InvalidOperationException(
                    "Candidate must contain exactly four TerminalPanelGraphic components.");
            }

            int segmentCount = 0;
            RectTransform[] rects = candidate.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].name.StartsWith("Segment", StringComparison.Ordinal))
                {
                    segmentCount++;
                }
            }

            if (segmentCount != 11)
            {
                throw new InvalidOperationException(
                    $"Candidate stamina bar has {segmentCount} separators; expected 11.");
            }

            if (candidate.GetComponentInChildren<GraphicRaycaster>(true) != null)
            {
                throw new InvalidOperationException("HUD must not contain GraphicRaycaster.");
            }

            Graphic[] graphics = candidate.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i].raycastTarget)
                {
                    throw new InvalidOperationException(
                        $"Graphic '{graphics[i].name}' has raycastTarget enabled.");
                }
            }

            CanvasGroup[] groups = candidate.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].blocksRaycasts)
                {
                    throw new InvalidOperationException(
                        $"CanvasGroup '{groups[i].name}' blocks raycasts.");
                }
            }

            Transform fixedRoot = candidate.transform.Find("FixedRoot");
            Transform crosshair = fixedRoot != null
                ? fixedRoot.Find("CrosshairWidget")
                : null;
            if (crosshair == null || crosshair.GetComponent<HudSway>() != null)
            {
                throw new InvalidOperationException(
                    "Crosshair must be under FixedRoot and must not have HudSway.");
            }

            RequireReference(candidate.GetComponentInChildren<ObjectiveWidget>(true),
                "label", "group");
            RequireReference(candidate.GetComponentInChildren<StaminaWidget>(true),
                "fill", "group");
            Transform fillTransform =
                candidate.transform.Find("SwayRoot/StaminaWidget/Bar/Fill");
            Image staminaFill = fillTransform != null
                ? fillTransform.GetComponent<Image>()
                : null;
            if (staminaFill == null || staminaFill.sprite == null ||
                staminaFill.type != Image.Type.Filled ||
                staminaFill.fillMethod != Image.FillMethod.Horizontal ||
                staminaFill.fillOrigin != (int)Image.OriginHorizontal.Left)
            {
                throw new InvalidOperationException(
                    "Stamina fill must use a persistent sprite and fill horizontally from left.");
            }
            RequireReference(candidate.GetComponentInChildren<FlashlightWidget>(true),
                "stateLabel", "group");
            RequireReference(candidate.GetComponentInChildren<InteractionPromptWidget>(true),
                "label", "group");
            RequireArray(candidate.GetComponentInChildren<FlashlightWidget>(true),
                "visualParts", 3);
            RequireArray(candidate.GetComponentInChildren<CrosshairWidget>(true),
                "parts", 5);
            RequireArray(candidate.GetComponentInChildren<CrosshairPulse>(true),
                "ticks", 4);
        }

        private static void RequireReference(UnityEngine.Object target, params string[] fields)
        {
            if (target == null) throw new InvalidOperationException("Required widget is missing.");

            var serialized = new SerializedObject(target);
            for (int i = 0; i < fields.Length; i++)
            {
                SerializedProperty property = serialized.FindProperty(fields[i]);
                if (property == null || property.objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        $"{target.GetType().Name}.{fields[i]} is not assigned.");
                }
            }
        }

        private static void RequireArray(UnityEngine.Object target, string field, int length)
        {
            if (target == null) throw new InvalidOperationException("Required widget is missing.");

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || !property.isArray || property.arraySize != length)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name}.{field} must have {length} entries.");
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        $"{target.GetType().Name}.{field}[{i}] is not assigned.");
                }
            }
        }
    }
}
