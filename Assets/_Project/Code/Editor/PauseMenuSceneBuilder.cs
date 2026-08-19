using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Gameplay.Objectives;
using Game.Gameplay.Player;
using Game.UI;
using Game.UI.Pause;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>Builds the English corrupted-terminal pause menu in MainGame.</summary>
    public static class PauseMenuSceneBuilder
    {
        private const string MixerPath = "Assets/_Project/Audio/master.mixer";
        private const string CanvasName = "PauseMenuCanvas";

        private static readonly Color Overlay = new Color32(1, 5, 4, 220);
        private static readonly Color Phosphor = new Color32(121, 255, 155, 255);
        private static readonly Color Muted = new Color32(121, 255, 155, 120);
        private static readonly Color Dark = new Color32(6, 16, 12, 245);
        private static readonly Color Danger = new Color32(255, 90, 54, 255);

        [MenuItem("Tools/Gloomveil/Setup Pause Menu")]
        public static void Run()
        {
            if (EditorSceneManager.GetActiveScene().name != "MainGame")
            {
                Debug.LogError("[Pause Menu Builder] Open MainGame before running this tool.");
                return;
            }

            TMP_FontAsset font = HudTerminalFactory.EnsureFontAsset();
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (font == null || mixer == null)
            {
                Debug.LogError("[Pause Menu Builder] Cascadia Mono or master.mixer is missing.");
                return;
            }

            if (!ExposeVolumeParameters(mixer)) return;

            GameObject existing = GameObject.Find(CanvasName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

            BuildMenu(font, mixer);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[Pause Menu Builder] English pause menu and mixer routing saved to MainGame.");
        }

        private static void BuildMenu(TMP_FontAsset font, AudioMixer mixer)
        {
            GameObject canvasObject = new GameObject(CanvasName, typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            PauseMenuController controller = canvasObject.AddComponent<PauseMenuController>();
            RectTransform panelRoot = HudTerminalFactory.CreateFullScreenChild(
                canvasObject.transform, "PauseMenuRoot");

            Image overlay = panelRoot.gameObject.AddComponent<Image>();
            overlay.color = Overlay;
            overlay.raycastTarget = true;

            RectTransform panel = HudTerminalFactory.CreateChild(panelRoot, "TerminalPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(760f, 720f));
            panel.gameObject.AddComponent<TerminalPanelGraphic>();

            AddText(panel, font, "Header", new Vector2(0f, -34f), new Vector2(660f, 52f),
                34f, TextAlignmentOptions.Center, Phosphor, "// SYSTEM PAUSED");
            AddText(panel, font, "SubHeader", new Vector2(0f, -82f), new Vector2(660f, 28f),
                15f, TextAlignmentOptions.Center, Muted,
                "MAINTENANCE ACCESS  //  AUDIO ROUTING");

            CreateLine(panel, "HeaderLine", new Vector2(0f, -122f), new Vector2(650f, 2f), Phosphor);

            Slider masterSlider = CreateVolumeRow(panel, font, "Master", "MASTER OUTPUT",
                -175f, out TMP_Text masterValue);
            Slider playerSlider = CreateVolumeRow(panel, font, "Player", "PLAYER CHANNEL",
                -270f, out TMP_Text playerValue);
            Slider monsterSlider = CreateVolumeRow(panel, font, "Monster", "MONSTER CHANNEL",
                -365f, out TMP_Text monsterValue);

            CreateLine(panel, "ControlLine", new Vector2(0f, -435f), new Vector2(650f, 1f), Muted);

            Button resumeButton = CreateButton(panel, font, "ResumeButton", "RESUME",
                new Vector2(0f, -492f), Phosphor);
            Button mainMenuButton = CreateButton(panel, font, "MainMenuButton",
                "RETURN TO MAIN MENU", new Vector2(0f, -565f), Danger);

            AddText(panel, font, "Footer", new Vector2(0f, -642f), new Vector2(650f, 24f),
                14f, TextAlignmentOptions.Center, Muted, "ESC  RESUME");

            PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            FuseMiniGameController fuse =
                UnityEngine.Object.FindFirstObjectByType<FuseMiniGameController>();

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("panelRoot").objectReferenceValue = panelRoot.gameObject;
            serialized.FindProperty("resumeButton").objectReferenceValue = resumeButton;
            serialized.FindProperty("mainMenuButton").objectReferenceValue = mainMenuButton;
            serialized.FindProperty("audioMixer").objectReferenceValue = mixer;
            serialized.FindProperty("masterSlider").objectReferenceValue = masterSlider;
            serialized.FindProperty("playerSlider").objectReferenceValue = playerSlider;
            serialized.FindProperty("monsterSlider").objectReferenceValue = monsterSlider;
            serialized.FindProperty("masterValueLabel").objectReferenceValue = masterValue;
            serialized.FindProperty("playerValueLabel").objectReferenceValue = playerValue;
            serialized.FindProperty("monsterValueLabel").objectReferenceValue = monsterValue;
            serialized.FindProperty("playerController").objectReferenceValue = player;
            serialized.FindProperty("fuseMiniGame").objectReferenceValue = fuse;
            serialized.FindProperty("mainMenuSceneName").stringValue = "MainMenu";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            panelRoot.gameObject.SetActive(false);
            EditorUtility.SetDirty(controller);
        }

        private static Slider CreateVolumeRow(RectTransform parent, TMP_FontAsset font,
            string name, string label, float y, out TMP_Text valueLabel)
        {
            AddText(parent, font, $"{name}Label", new Vector2(-242f, y),
                new Vector2(280f, 28f), 17f, TextAlignmentOptions.MidlineLeft, Phosphor, label);
            valueLabel = AddText(parent, font, $"{name}Value", new Vector2(276f, y),
                new Vector2(100f, 28f), 17f, TextAlignmentOptions.MidlineRight, Phosphor, "100%");

            RectTransform sliderRect = HudTerminalFactory.CreateChild(parent, $"{name}Slider",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y - 38f),
                new Vector2(600f, 26f));
            Slider slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            RectTransform background = HudTerminalFactory.CreateStretchChild(
                sliderRect, "Background", new Vector2(0f, 7f), new Vector2(0f, -7f));
            Image backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color32(121, 255, 155, 45);
            backgroundImage.raycastTarget = false;

            RectTransform fillArea = HudTerminalFactory.CreateStretchChild(
                sliderRect, "Fill Area", new Vector2(4f, 8f), new Vector2(-4f, -8f));
            RectTransform fill = HudTerminalFactory.CreateStretchChild(
                fillArea, "Fill", Vector2.zero, Vector2.zero);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = Phosphor;
            fillImage.raycastTarget = false;

            RectTransform handleArea = HudTerminalFactory.CreateStretchChild(
                sliderRect, "Handle Slide Area", new Vector2(8f, 0f), new Vector2(-8f, 0f));
            RectTransform handle = HudTerminalFactory.CreateChild(handleArea, "Handle",
                new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(16f, 34f));
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Phosphor;

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static Button CreateButton(RectTransform parent, TMP_FontAsset font, string name,
            string label, Vector2 position, Color accent)
        {
            RectTransform rect = HudTerminalFactory.CreateChild(parent, name,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position,
                new Vector2(520f, 54f));
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Dark;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Dark;
            colors.highlightedColor = new Color(accent.r, accent.g, accent.b, 0.34f);
            colors.selectedColor = new Color(accent.r, accent.g, accent.b, 0.28f);
            colors.pressedColor = new Color(accent.r, accent.g, accent.b, 0.62f);
            colors.disabledColor = new Color32(20, 30, 25, 120);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            CreateLine(rect, "TopBorder", new Vector2(0f, -1f), new Vector2(520f, 2f), accent);
            CreateLine(rect, "BottomBorder", new Vector2(0f, -53f), new Vector2(520f, 2f), accent);
            AddText(rect, font, "Label", new Vector2(0f, -27f), new Vector2(500f, 42f),
                19f, TextAlignmentOptions.Center, accent, $"[  {label}  ]");
            return button;
        }

        private static TMP_Text AddText(RectTransform parent, TMP_FontAsset font, string name,
            Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment,
            Color color, string value)
        {
            RectTransform rect = HudTerminalFactory.CreateChild(parent, name,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, size);
            return HudTerminalFactory.AddText(rect, font, fontSize, alignment, color, value);
        }

        private static void CreateLine(RectTransform parent, string name, Vector2 position,
            Vector2 size, Color color)
        {
            RectTransform rect = HudTerminalFactory.CreateChild(parent, name,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static bool ExposeVolumeParameters(AudioMixer mixer)
        {
            Type controllerType = mixer.GetType();
            PropertyInfo exposedProperty = controllerType.GetProperty("exposedParameters",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (exposedProperty == null)
            {
                Debug.LogError("[Pause Menu Builder] Could not access mixer exposed parameters.");
                return false;
            }

            Type exposedType = controllerType.Assembly.GetType("UnityEditor.Audio.ExposedAudioParameter");
            FieldInfo guidField = exposedType?.GetField("guid",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo nameField = exposedType?.GetField("name",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (exposedType == null || guidField == null || nameField == null)
            {
                Debug.LogError("[Pause Menu Builder] Unity mixer parameter metadata is unavailable.");
                return false;
            }

            var requested = new Dictionary<string, string>
            {
                { "Master", PauseMenuController.MasterVolumeParameter },
                { "player", PauseMenuController.PlayerVolumeParameter },
                { "monster", PauseMenuController.MonsterVolumeParameter },
            };

            var entries = new List<object>();
            if (exposedProperty.GetValue(mixer) is IEnumerable existing)
            {
                entries.AddRange(existing.Cast<object>());
            }

            foreach (AudioMixerGroup group in mixer.FindMatchingGroups(string.Empty))
            {
                if (!requested.TryGetValue(group.name, out string parameterName)) continue;

                MethodInfo getVolumeGuid = group.GetType().GetMethod("GetGUIDForVolume",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object volumeGuid = getVolumeGuid?.Invoke(group, null);
                if (volumeGuid == null)
                {
                    Debug.LogError($"[Pause Menu Builder] Could not resolve {group.name} volume.");
                    return false;
                }

                object entry = entries.FirstOrDefault(item =>
                    string.Equals(nameField.GetValue(item) as string, parameterName,
                        StringComparison.Ordinal));
                if (entry == null)
                {
                    entry = Activator.CreateInstance(exposedType);
                    entries.Add(entry);
                }

                guidField.SetValue(entry, volumeGuid);
                nameField.SetValue(entry, parameterName);
            }

            if (requested.Values.Any(parameter => entries.All(entry =>
                    !string.Equals(nameField.GetValue(entry) as string, parameter,
                        StringComparison.Ordinal))))
            {
                Debug.LogError("[Pause Menu Builder] Master/player/monster mixer groups are incomplete.");
                return false;
            }

            Array exposedArray = Array.CreateInstance(exposedType, entries.Count);
            for (int i = 0; i < entries.Count; i++) exposedArray.SetValue(entries[i], i);
            exposedProperty.SetValue(mixer, exposedArray);
            EditorUtility.SetDirty(mixer);
            return true;
        }
    }
}
