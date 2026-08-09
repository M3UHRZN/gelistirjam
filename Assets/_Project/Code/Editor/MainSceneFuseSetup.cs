using Game.Core;
using Game.Gameplay;
using Game.Gameplay.Objectives;
using Game.Gameplay.World;
using Gloomveil.MapGeneration;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// One-click setup that wires the Fuse Memory Minigame into the currently-open main procedural
    /// scene (see chat: Fuse Memory Minigame main-scene integration). Unlike
    /// MonsterAITestSceneBuilder, this is purely ADDITIVE — it never wipes or rebuilds the scene, only
    /// adds the objects the Fuse system needs and wires them to what's already there
    /// (GeneratedMapRegistry, GameFlowController, MapExitController). Safe to re-run: every step
    /// checks for an existing object/component first.
    /// </summary>
    public static class MainSceneFuseSetup
    {
        [MenuItem("Tools/Gloomveil/Setup Main Scene Fuse System")]
        public static void Run()
        {
            GeneratedMapRegistry registry = Object.FindFirstObjectByType<GeneratedMapRegistry>();
            GameFlowController gameFlow = Object.FindFirstObjectByType<GameFlowController>();
            MapExitController mapExit = Object.FindFirstObjectByType<MapExitController>();

            if (registry == null || gameFlow == null || mapExit == null)
            {
                Debug.LogError("Setup Main Scene Fuse System: missing GeneratedMapRegistry, " +
                                "GameFlowController, or MapExitController in the active scene. " +
                                "Open the main scene first.");
                return;
            }

            ObjectiveManager objectiveManager = EnsureObjectiveManager();
            FuseMiniGameController miniGame = EnsureFuseMiniGameUI();
            MapFuseGate fuseGate = EnsureFuseGate();
            MainSceneWiring wiring = EnsureMainSceneWiring();

            // Optional: a missing brain only costs the tablet's blend-duration override (MapFuseGate's
            // Bind contract), so this setup tool must not fail when the scene doesn't have one yet.
            CinemachineBrain brain = Object.FindFirstObjectByType<CinemachineBrain>();
            if (brain == null)
            {
                Debug.LogWarning("Setup Main Scene Fuse System: no CinemachineBrain in the scene. " +
                                 "Fuses will still work, but the camera will cut instead of blending. " +
                                 "Add a CinemachineBrain to PlayerCamera and re-run.");
            }

            fuseGate.Bind(registry, gameFlow, objectiveManager, miniGame, brain);
            wiring.Bind(gameFlow, objectiveManager, mapExit, miniGame);

            EditorUtility.SetDirty(fuseGate);
            EditorUtility.SetDirty(wiring);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("Main scene Fuse system wired: ObjectiveManager, FuseMiniGameController UI, " +
                       "MapFuseGate, MainSceneWiring. Enter Play Mode to test.");
        }

        private static ObjectiveManager EnsureObjectiveManager()
        {
            ObjectiveManager existing = Object.FindFirstObjectByType<ObjectiveManager>();
            if (existing != null) return existing;

            GameObject go = new GameObject("Objectives");
            return go.AddComponent<ObjectiveManager>();
        }

        private static MapFuseGate EnsureFuseGate()
        {
            MapFuseGate existing = Object.FindFirstObjectByType<MapFuseGate>();
            if (existing != null) return existing;

            GameObject go = new GameObject("FuseGate");
            return go.AddComponent<MapFuseGate>();
        }

        private static MainSceneWiring EnsureMainSceneWiring()
        {
            MainSceneWiring existing = Object.FindFirstObjectByType<MainSceneWiring>();
            if (existing != null) return existing;

            GameObject go = new GameObject("MainSceneWiring");
            return go.AddComponent<MainSceneWiring>();
        }

        // ---------- Fuse Mini Game UI. Builds the world-space canvas the minigame parents onto each
        // tablet's screen anchor, plus the screen-space crosshair overlay. TextMeshPro labels (legacy
        // Text is unreadable at world-space scale) and coroutine-driven button feedback; DOTween is
        // used only for the tablet lever, in FuseTabletPresenter. ----------

        private static FuseMiniGameController EnsureFuseMiniGameUI()
        {
            FuseMiniGameController existing = Object.FindFirstObjectByType<FuseMiniGameController>();
            if (existing != null)
            {
                if (HasCurrentFuseMiniGameUi(existing))
                {
                    BindWorldCanvasCamera(existing);
                    return existing;
                }

                RemoveLegacyFuseMiniGameUi(existing);
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<InputSystemUIInputModule>();
            }

            GameObject canvasGO = new GameObject("FuseMiniGameCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(400f, 300f);
            canvasGO.transform.localScale = Vector3.one * 0.0012f;
            GraphicRaycaster raycaster = canvasGO.AddComponent<GraphicRaycaster>();

            GameObject panelGO = NewUIObject("FusePanel", canvasGO.transform);
            RectTransform panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelBg = panelGO.AddComponent<Image>();
            panelBg.color = new Color(0.10f, 0.10f, 0.11f, 0.95f);
            CanvasGroup panelGroup = panelGO.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f;

            TMP_Text progressText = NewLabel("ProgressText", panelGO.transform, 18f, TextAlignmentOptions.Top, new Color(0.78f, 0.82f, 0.85f));
            RectTransform progressRect = progressText.GetComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0f, 1f);
            progressRect.anchorMax = new Vector2(1f, 1f);
            progressRect.pivot = new Vector2(0.5f, 1f);
            progressRect.sizeDelta = new Vector2(-20f, 48f);
            progressRect.anchoredPosition = new Vector2(0f, -10f);

            TMP_Text statusText = NewLabel("StatusText", panelGO.transform, 16f, TextAlignmentOptions.Center, new Color(0.95f, 0.75f, 0.2f));
            RectTransform statusRect = statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.sizeDelta = new Vector2(-20f, 32f);
            statusRect.anchoredPosition = new Vector2(0f, 14f);

            GameObject buttonGridGO = NewUIObject("ButtonGrid", panelGO.transform);
            RectTransform buttonGridRect = buttonGridGO.GetComponent<RectTransform>();
            buttonGridRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonGridRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonGridRect.pivot = new Vector2(0.5f, 0.5f);
            buttonGridRect.sizeDelta = new Vector2(180f, 180f);
            buttonGridRect.anchoredPosition = new Vector2(0f, -10f);
            GridLayoutGroup layout = buttonGridGO.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(82f, 82f);
            layout.spacing = new Vector2(12f, 12f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;

            string[] buttonNames = { "Button_Red", "Button_Blue", "Button_Green", "Button_Yellow" };
            Button[] buttons = new Button[buttonNames.Length];
            Image[] buttonImages = new Image[buttonNames.Length];
            for (int i = 0; i < buttonNames.Length; i++)
            {
                GameObject buttonGO = NewUIObject(buttonNames[i], buttonGridGO.transform);
                Image image = buttonGO.AddComponent<Image>();
                image.color = Color.white;
                Button button = buttonGO.AddComponent<Button>();
                button.targetGraphic = image;
                buttons[i] = button;
                buttonImages[i] = image;
            }

            GameObject crosshairCanvasGO = new GameObject("FuseCrosshairCanvas");
            Canvas crosshairCanvas = crosshairCanvasGO.AddComponent<Canvas>();
            crosshairCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            crosshairCanvas.sortingOrder = 100;
            CanvasScaler crosshairScaler = crosshairCanvasGO.AddComponent<CanvasScaler>();
            crosshairScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            crosshairScaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject crosshairGO = NewUIObject("Crosshair", crosshairCanvasGO.transform);
            RectTransform crosshairRect = crosshairGO.GetComponent<RectTransform>();
            crosshairRect.anchorMin = crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
            crosshairRect.pivot = new Vector2(0.5f, 0.5f);
            crosshairRect.sizeDelta = new Vector2(8f, 8f);
            crosshairRect.anchoredPosition = Vector2.zero;
            Image crosshairImage = crosshairGO.AddComponent<Image>();
            crosshairImage.color = new Color(0.95f, 0.95f, 0.95f, 0.85f);
            crosshairImage.raycastTarget = false;
            crosshairCanvasGO.SetActive(false);

            GameObject pointerGO = new GameObject("FuseScreenPointer");
            FuseScreenPointer pointer = pointerGO.AddComponent<FuseScreenPointer>();
            pointer.Bind(raycaster);

            GameObject controllerGO = new GameObject("FuseMiniGameController");
            FuseMiniGameController controller = controllerGO.AddComponent<FuseMiniGameController>();
            BindFuseMiniGameController(controller, canvas, panelGO, panelGroup, crosshairCanvasGO, pointer,
                progressText, statusText, buttons, buttonImages);

            return controller;
        }

        private static bool HasCurrentFuseMiniGameUi(FuseMiniGameController controller)
        {
            SerializedObject serialized = new SerializedObject(controller);
            Canvas canvas = serialized.FindProperty("worldCanvas").objectReferenceValue as Canvas;

            return canvas != null &&
                   canvas.renderMode == RenderMode.WorldSpace &&
                   serialized.FindProperty("panelRoot").objectReferenceValue != null &&
                   serialized.FindProperty("crosshairRoot").objectReferenceValue != null &&
                   serialized.FindProperty("pointer").objectReferenceValue != null &&
                   serialized.FindProperty("progressText").objectReferenceValue != null &&
                   serialized.FindProperty("statusText").objectReferenceValue != null;
        }

        private static void BindWorldCanvasCamera(FuseMiniGameController controller)
        {
            SerializedObject serialized = new SerializedObject(controller);
            Canvas canvas = serialized.FindProperty("worldCanvas").objectReferenceValue as Canvas;
            if (canvas != null) canvas.worldCamera = Camera.main;
        }

        private static void RemoveLegacyFuseMiniGameUi(FuseMiniGameController controller)
        {
            SerializedObject serialized = new SerializedObject(controller);
            GameObject panel = serialized.FindProperty("panelRoot").objectReferenceValue as GameObject;
            Canvas legacyCanvas = panel != null ? panel.GetComponentInParent<Canvas>() : null;
            GameObject crosshair = serialized.FindProperty("crosshairRoot").objectReferenceValue as GameObject;
            FuseScreenPointer pointer = serialized.FindProperty("pointer").objectReferenceValue as FuseScreenPointer;

            if (legacyCanvas != null) Object.DestroyImmediate(legacyCanvas.gameObject);
            if (crosshair != null) Object.DestroyImmediate(crosshair);
            if (pointer != null) Object.DestroyImmediate(pointer.gameObject);
            Object.DestroyImmediate(controller.gameObject);
        }

        private static GameObject NewUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text NewLabel(string name, Transform parent, float fontSize,
            TextAlignmentOptions alignment, Color color)
        {
            GameObject go = NewUIObject(name, parent);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.text = string.Empty;
            text.enableWordWrapping = false;
            return text;
        }

        private static void BindFuseMiniGameController(FuseMiniGameController controller, Canvas worldCanvas,
            GameObject panelRoot, CanvasGroup panelGroup, GameObject crosshairRoot, FuseScreenPointer pointer,
            TMP_Text progressText, TMP_Text statusText, Button[] buttons, Image[] buttonImages)
        {
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("worldCanvas").objectReferenceValue = worldCanvas;
            so.FindProperty("panelRoot").objectReferenceValue = panelRoot;
            so.FindProperty("panelGroup").objectReferenceValue = panelGroup;
            so.FindProperty("crosshairRoot").objectReferenceValue = crosshairRoot;
            so.FindProperty("pointer").objectReferenceValue = pointer;
            so.FindProperty("progressText").objectReferenceValue = progressText;
            so.FindProperty("statusText").objectReferenceValue = statusText;

            SerializedProperty buttonsProp = so.FindProperty("buttons");
            SerializedProperty imagesProp = so.FindProperty("buttonImages");
            buttonsProp.arraySize = buttons.Length;
            imagesProp.arraySize = buttonImages.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttonsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                imagesProp.GetArrayElementAtIndex(i).objectReferenceValue = buttonImages[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
