using Game.Core;
using Game.Gameplay;
using Game.Gameplay.Objectives;
using Game.Gameplay.World;
using Gloomveil.MapGeneration;
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

            fuseGate.Bind(registry, gameFlow, objectiveManager, miniGame);
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

        // ---------- Fuse Mini Game UI (adapted from MonsterAITestSceneBuilder.BuildFuseMiniGameUI —
        // see chat: same "Bruised Industrial" panel, legacy UnityEngine.UI.Text (no TextMeshPro
        // package installed), coroutine-driven feedback (no DOTween installed). Kept as a separate
        // copy rather than a shared utility so this additive setup never risks the test scene's
        // from-scratch builder, and vice versa. ----------

        private static FuseMiniGameController EnsureFuseMiniGameUI()
        {
            FuseMiniGameController existing = Object.FindFirstObjectByType<FuseMiniGameController>();
            if (existing != null) return existing;

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<InputSystemUIInputModule>();
            }

            GameObject canvasGO = new GameObject("FuseMiniGameCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject overlayGO = NewUIObject("DarkenOverlay", canvasGO.transform);
            StretchFull(overlayGO);
            Image overlayImage = overlayGO.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 1f);
            CanvasGroup overlayGroup = overlayGO.AddComponent<CanvasGroup>();
            overlayGroup.alpha = 0f;
            overlayGroup.blocksRaycasts = false;
            overlayGroup.interactable = false;

            GameObject panelGO = NewUIObject("FusePanel", canvasGO.transform);
            RectTransform panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 360f);
            panelRect.anchoredPosition = Vector2.zero;
            Image panelBg = panelGO.AddComponent<Image>();
            panelBg.color = new Color(0.10f, 0.10f, 0.11f, 0.95f);
            CanvasGroup panelGroup = panelGO.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f;

            Text progressText = NewText("ProgressText", panelGO.transform, uiFont, 22, TextAnchor.UpperCenter, new Color(0.78f, 0.82f, 0.85f));
            RectTransform progressRect = progressText.GetComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0f, 1f);
            progressRect.anchorMax = new Vector2(1f, 1f);
            progressRect.pivot = new Vector2(0.5f, 1f);
            progressRect.sizeDelta = new Vector2(-40f, 60f);
            progressRect.anchoredPosition = new Vector2(0f, -20f);

            Text statusText = NewText("StatusText", panelGO.transform, uiFont, 20, TextAnchor.MiddleCenter, new Color(0.95f, 0.75f, 0.2f));
            RectTransform statusRect = statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.sizeDelta = new Vector2(-40f, 40f);
            statusRect.anchoredPosition = new Vector2(0f, 24f);

            GameObject buttonGridGO = NewUIObject("ButtonGrid", panelGO.transform);
            RectTransform buttonGridRect = buttonGridGO.GetComponent<RectTransform>();
            buttonGridRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonGridRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonGridRect.pivot = new Vector2(0.5f, 0.5f);
            buttonGridRect.sizeDelta = new Vector2(232f, 232f);
            buttonGridRect.anchoredPosition = new Vector2(0f, -10f);
            GridLayoutGroup layout = buttonGridGO.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(108f, 108f);
            layout.spacing = new Vector2(16f, 16f);
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

            GameObject controllerGO = new GameObject("FuseMiniGameController");
            FuseMiniGameController controller = controllerGO.AddComponent<FuseMiniGameController>();
            BindFuseMiniGameController(controller, panelGO, overlayGroup, panelGroup, progressText, statusText, buttons, buttonImages);

            return controller;
        }

        private static GameObject NewUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFull(GameObject go)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text NewText(string name, Transform parent, Font font, int fontSize, TextAnchor alignment, Color color)
        {
            GameObject go = NewUIObject(name, parent);
            Text text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.text = string.Empty;
            return text;
        }

        private static void BindFuseMiniGameController(FuseMiniGameController controller, GameObject panelRoot,
            CanvasGroup darkenOverlay, CanvasGroup panelGroup, Text progressText, Text statusText,
            Button[] buttons, Image[] buttonImages)
        {
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("panelRoot").objectReferenceValue = panelRoot;
            so.FindProperty("darkenOverlay").objectReferenceValue = darkenOverlay;
            so.FindProperty("panelGroup").objectReferenceValue = panelGroup;
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
