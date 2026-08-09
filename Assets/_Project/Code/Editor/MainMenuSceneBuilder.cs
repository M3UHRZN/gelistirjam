using System;
using Game.UI;
using Game.UI.MainMenu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// One-click setup that builds the main menu terminal: the World Space Canvas + rows + options
    /// page inside MainMenuTablet.prefab, and the camera/lighting/tablet placement in the currently
    /// open scene (expected to be MainMenu.unity). Additive and idempotent like
    /// MainSceneFuseSetup.cs — every step checks for an existing object first, safe to re-run.
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        private const string TabletPrefabPath = "Assets/prefabs/MainMenuTablet.prefab";

        [MenuItem("Tools/Gloomveil/Setup Main Menu Scene")]
        public static void Run()
        {
            TMP_FontAsset font = HudTerminalFactory.EnsureFontAsset();
            if (font == null)
            {
                Debug.LogError("Setup Main Menu Scene: Cascadia Mono font asset could not be " +
                                "ensured (see the error above from HudTerminalFactory). Aborting.");
                return;
            }

            string activeSceneName = EditorSceneManager.GetActiveScene().name;
            if (activeSceneName != "MainMenu")
            {
                Debug.LogError("Setup Main Menu Scene: the active scene is " +
                                $"'{activeSceneName}', not 'MainMenu'. Open MainMenu.unity before " +
                                "running this tool — it repositions the scene's Camera.main and " +
                                "saves the scene. Aborting.");
                return;
            }

            if (!BuildTabletPrefabUi(font, out _))
            {
                return;
            }

            BuildScene(font);

            Debug.Log("Main menu scene set up: tablet Canvas/rows/options page built into " +
                       "MainMenuTablet.prefab, camera/lighting/tablet placed in the open scene. " +
                       "Enter Play Mode to test.");
        }

        // ---------- Prefab: builds the Canvas/rows/options page inside MainMenuTablet.prefab ----------

        private static bool BuildTabletPrefabUi(TMP_FontAsset font, out GameObject tabletRoot)
        {
            tabletRoot = null;
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TabletPrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"Setup Main Menu Scene: prefab not found at '{TabletPrefabPath}'. " +
                                "Run the prefab-duplication step first.");
                return false;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(TabletPrefabPath);
            try
            {
                if (contents.transform.Find("ScreenAnchor") != null)
                {
                    Debug.Log("Setup Main Menu Scene: MainMenuTablet.prefab already has a " +
                              "ScreenAnchor — leaving its existing UI in place.");
                    tabletRoot = prefabAsset;
                    return true;
                }

                MeshFilter meshFilter = contents.GetComponentInChildren<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    Debug.LogError("Setup Main Menu Scene: MainMenuTablet.prefab has no mesh to " +
                                    "measure a screen placement from.");
                    return false;
                }

                Bounds localBounds = meshFilter.sharedMesh.bounds;

                // Assumption (documented in the design spec): the screen faces local +Z, matching
                // FuseTabletPresenter's screenAnchor convention ("+Z pointing out of the screen").
                // bezelInset leaves a margin so the Canvas doesn't overlap the tablet's frame.
                // BOTH of these are a starting point computed from the mesh's real bounds, not a
                // guess pulled from nowhere — but if the model's screen actually faces a different
                // local axis, or the bezel margin looks wrong, nudge ScreenAnchor's local position/
                // rotation/scale by hand in the Inspector. That is expected and fine — see the
                // design spec's "no hardcoded layout" requirement.
                const float bezelInset = 0.85f;
                var screenAnchorGo = new GameObject("ScreenAnchor");
                screenAnchorGo.transform.SetParent(contents.transform, false);
                screenAnchorGo.transform.localPosition =
                    new Vector3(localBounds.center.x, localBounds.center.y,
                        localBounds.max.z + localBounds.size.z * 0.01f);
                screenAnchorGo.transform.localRotation = Quaternion.identity;

                const float refWidthUnits = 800f;
                float scale = (localBounds.size.x * bezelInset) / refWidthUnits;
                if (scale <= 0f) scale = 0.001f; // guards a degenerate/zero-width mesh bounds case
                float refHeightUnits = (localBounds.size.y * bezelInset) / scale;

                GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform));
                canvasGo.transform.SetParent(screenAnchorGo.transform, false);
                canvasGo.transform.localScale = Vector3.one * scale;
                Canvas canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                RectTransform canvasRect = (RectTransform)canvasGo.transform;
                canvasRect.sizeDelta = new Vector2(refWidthUnits, refHeightUnits);
                canvasGo.AddComponent<GraphicRaycaster>();

                RectTransform screenRoot = HudTerminalFactory.CreateStretchChild(
                    canvasRect, "ScreenRoot", Vector2.zero, Vector2.zero);
                TerminalPanelGraphic background = HudTerminalFactory.CreatePanel(screenRoot);
                background.raycastTarget = false;
                CanvasGroup screenGroup = screenRoot.gameObject.AddComponent<CanvasGroup>();

                // One shared AudioSource for the whole menu's navigate/confirm SFX — every row
                // plays through this, none owns its own. 2D (spatialBlend 0): this is UI feedback,
                // not a positioned in-world sound. playOnAwake off so it only ever sounds on
                // PlayOneShot calls from MainMenuRow.
                GameObject audioGo = new GameObject("MenuAudioSource");
                audioGo.transform.SetParent(contents.transform, false);
                AudioSource audioSource = audioGo.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;

                (GameObject menuPage, MainMenuRow[] rows) =
                    BuildMenuPage(screenRoot, font, refWidthUnits, refHeightUnits, audioSource);
                GameObject optionsPage = BuildOptionsPage(screenRoot, font, refWidthUnits, refHeightUnits,
                    out Slider volumeSlider, out MasterVolumeController volumeController);
                optionsPage.SetActive(false);

                TMP_Text clockText = BuildClock(screenRoot, font, refWidthUnits, refHeightUnits);

                GameObject presenterGo = new GameObject("MainMenuPresenter");
                presenterGo.transform.SetParent(contents.transform, false);
                MainMenuPresenter presenter = presenterGo.AddComponent<MainMenuPresenter>();
                BindPresenter(presenter, rows, menuPage, optionsPage, clockText, screenGroup);

                WireVolumeSlider(volumeSlider, volumeController);

                PrefabUtility.SaveAsPrefabAsset(contents, TabletPrefabPath);
                tabletRoot = prefabAsset;
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ---------- Prefab: menu page rows ----------

        private static (GameObject page, MainMenuRow[] rows) BuildMenuPage(
            RectTransform screenRoot, TMP_FontAsset font, float refWidth, float refHeight,
            AudioSource audioSource)
        {
            RectTransform page = HudTerminalFactory.CreateStretchChild(
                screenRoot, "MenuPage", new Vector2(20f, 20f), new Vector2(-20f, -60f));

            VerticalLayoutGroup layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperCenter;

            (string label, bool interactable, string subLabel)[] rowDefs =
            {
                ("NEW GAME", true, null),
                ("CONTINUE", false, "(no save yet)"),
                ("OPTIONS", true, null),
                ("QUIT", true, null),
            };

            float rowHeight = (refHeight - 80f) / rowDefs.Length - layout.spacing;
            const float minRowHeight = 20f;
            if (rowHeight < minRowHeight)
            {
                Debug.LogWarning("Setup Main Menu Scene: computed rowHeight " +
                                  $"({rowHeight:F1}) collapsed below the minimum of {minRowHeight}. " +
                                  "Clamping — this usually means refHeight is smaller than expected " +
                                  "for this tablet mesh; check the screen-face assumption.");
            }
            rowHeight = Mathf.Max(rowHeight, minRowHeight);
            var rows = new MainMenuRow[rowDefs.Length];
            for (int i = 0; i < rowDefs.Length; i++)
            {
                rows[i] = BuildRow(page, font, rowDefs[i].label, rowDefs[i].interactable,
                    rowDefs[i].subLabel, rowHeight, audioSource);
            }

            return (page.gameObject, rows);
        }

        private static MainMenuRow BuildRow(RectTransform parent, TMP_FontAsset font, string label,
            bool interactable, string subLabel, float height, AudioSource audioSource)
        {
            GameObject rowGo = new GameObject($"Row_{label.Replace(' ', '_')}", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            RectTransform rowRect = (RectTransform)rowGo.transform;
            LayoutElement layoutElement = rowGo.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;
            layoutElement.minHeight = height;

            RectTransform borderRect = HudTerminalFactory.CreateStretchChild(
                rowRect, "Border", Vector2.zero, Vector2.zero);
            Image border = borderRect.gameObject.AddComponent<Image>();
            border.sprite = HudTerminalFactory.RequireFilledImageSprite();
            border.type = Image.Type.Sliced;
            border.color = HudTerminalFactory.MutedPhosphor;

            RectTransform cursorRect = HudTerminalFactory.CreateChild(
                rowRect, "Cursor", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(12f, 0f), new Vector2(24f, height));
            TMP_Text cursor = HudTerminalFactory.AddText(cursorRect, font, 20f,
                TextAlignmentOptions.MidlineLeft, HudTerminalFactory.Phosphor, ">");

            RectTransform titleRect = HudTerminalFactory.CreateChild(
                rowRect, "Title", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(44f, 0f), new Vector2(-64f, height));
            string text = subLabel != null ? $"{label}  <size=70%>{subLabel}</size>" : label;
            TMP_Text title = HudTerminalFactory.AddText(titleRect, font, 20f,
                TextAlignmentOptions.MidlineLeft, interactable ? HudTerminalFactory.Phosphor : HudTerminalFactory.MutedPhosphor,
                text);
            title.raycastTarget = false;

            MainMenuRow row = rowGo.AddComponent<MainMenuRow>();
            HudTerminalFactory.AssignSerialized(row, "highlightBorder", border);
            HudTerminalFactory.AssignSerialized(row, "cursorLabel", cursor);
            HudTerminalFactory.AssignSerialized(row, "audioSource", audioSource);
            // selectSound/confirmSound are deliberately left unassigned here — see Task 2's
            // Interfaces note: the user assigns those clips by hand once they have them.
            // `interactable` and colors keep the component's own inspector defaults; only the flag
            // needs setting per-row.
            var so = new SerializedObject(row);
            so.FindProperty("interactable").boolValue = interactable;
            so.ApplyModifiedPropertiesWithoutUndo();

            return row;
        }

        // ---------- Prefab: options page and clock ----------

        private static GameObject BuildOptionsPage(RectTransform screenRoot, TMP_FontAsset font,
            float refWidth, float refHeight, out Slider volumeSlider, out MasterVolumeController volumeController)
        {
            RectTransform page = HudTerminalFactory.CreateStretchChild(
                screenRoot, "OptionsPage", new Vector2(20f, 20f), new Vector2(-20f, -60f));

            RectTransform header = HudTerminalFactory.CreateChild(page, "Header",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(refWidth - 40f, 28f));
            HudTerminalFactory.AddText(header, font, 18f, TextAlignmentOptions.TopLeft,
                HudTerminalFactory.Phosphor, "OPTIONS");

            RectTransform controls = HudTerminalFactory.CreateChild(page, "Controls",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -36f), new Vector2(refWidth - 40f, 150f));
            string controlsText =
                "MOVE       WASD / ARROW KEYS\n" +
                "LOOK       MOUSE\n" +
                "SPRINT     LEFT SHIFT\n" +
                "INTERACT   E\n" +
                "FLASHLIGHT F\n" +
                "JUMP       SPACE";
            TMP_Text controlsLabel = HudTerminalFactory.AddText(controls, font, 15f,
                TextAlignmentOptions.TopLeft, HudTerminalFactory.MutedPhosphor, controlsText);
            controlsLabel.enableWordWrapping = false;

            RectTransform volumeLabelRect = HudTerminalFactory.CreateChild(page, "VolumeLabel",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -196f), new Vector2(refWidth - 40f, 24f));
            HudTerminalFactory.AddText(volumeLabelRect, font, 15f, TextAlignmentOptions.TopLeft,
                HudTerminalFactory.Phosphor, "MASTER VOLUME");

            RectTransform sliderRect = HudTerminalFactory.CreateChild(page, "VolumeSlider",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -228f), new Vector2(refWidth - 40f, 20f));
            Slider slider = sliderRect.gameObject.AddComponent<Slider>();
            RectTransform sliderBackground = HudTerminalFactory.CreateStretchChild(
                sliderRect, "Background", Vector2.zero, Vector2.zero);
            Image sliderBgImage = sliderBackground.gameObject.AddComponent<Image>();
            sliderBgImage.color = HudTerminalFactory.MutedPhosphor;

            RectTransform fillArea = HudTerminalFactory.CreateStretchChild(
                sliderRect, "Fill Area", Vector2.zero, Vector2.zero);
            RectTransform fill = HudTerminalFactory.CreateStretchChild(fillArea, "Fill", Vector2.zero, Vector2.zero);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = HudTerminalFactory.Phosphor;
            slider.fillRect = fill;
            slider.targetGraphic = fillImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            GameObject controllerGo = new GameObject("MasterVolumeController");
            controllerGo.transform.SetParent(page, false);
            MasterVolumeController controller = controllerGo.AddComponent<MasterVolumeController>();

            volumeSlider = slider;
            volumeController = controller;
            return page.gameObject;
        }

        private static TMP_Text BuildClock(RectTransform screenRoot, TMP_FontAsset font,
            float refWidth, float refHeight)
        {
            RectTransform clockRect = HudTerminalFactory.CreateChild(screenRoot, "Clock",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -12f), new Vector2(140f, 24f));
            TMP_Text clock = HudTerminalFactory.AddText(clockRect, font, 15f,
                TextAlignmentOptions.TopRight, HudTerminalFactory.MutedPhosphor, "00:00:00");
            return clock;
        }

        private static void BindPresenter(MainMenuPresenter presenter, MainMenuRow[] rows,
            GameObject menuPage, GameObject optionsPage, TMP_Text clockText, CanvasGroup screenGroup)
        {
            var so = new SerializedObject(presenter);
            SerializedProperty rowsProp = so.FindProperty("rows");
            rowsProp.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                rowsProp.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
            }

            so.FindProperty("menuPage").objectReferenceValue = menuPage;
            so.FindProperty("optionsPage").objectReferenceValue = optionsPage;
            so.FindProperty("clockText").objectReferenceValue = clockText;
            so.FindProperty("screenGroup").objectReferenceValue = screenGroup;
            so.ApplyModifiedPropertiesWithoutUndo();

            // OPTIONS row's onConfirmed -> presenter.OpenOptions(). NEW GAME/QUIT rows are wired to
            // their own action components in BuildScene instead (they don't exist inside the
            // prefab, and a prefab can't hold a reference to a scene object — see BuildScene's
            // WireSceneOnlyRowActions).
            foreach (MainMenuRow row in rows)
            {
                if (row.name != "Row_OPTIONS") continue;
                UnityEditor.Events.UnityEventTools.AddPersistentListener(
                    row.EditorOnConfirmedEvent, presenter.OpenOptions);
            }
        }

        private static void WireVolumeSlider(Slider slider, MasterVolumeController controller)
        {
            slider.value = controller.CurrentVolume;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                slider.onValueChanged, controller.SetVolume);
        }

        // ---------- Scene: places the tablet, frames it with a camera, lights it ----------

        private static void BuildScene(TMP_FontAsset font)
        {
            EnsureEventSystem();

            GameObject tabletInstance = FindOrInstantiateTablet();
            WireSceneOnlyRowActions(tabletInstance);

            Camera camera = EnsureMenuCamera(tabletInstance);
            EnsureAmbientLight(tabletInstance);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject FindOrInstantiateTablet()
        {
            GameObject existing = GameObject.Find("MainMenuTablet");
            if (existing != null) return existing;

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TabletPrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            instance.transform.position = new Vector3(0f, 1f, 0f);
            instance.transform.rotation = Quaternion.identity;
            return instance;
        }

        private static void WireSceneOnlyRowActions(GameObject tabletInstance)
        {
            MainMenuRow[] rows = tabletInstance.GetComponentsInChildren<MainMenuRow>(true);
            MainMenuRow newGameRow = Array.Find(rows, r => r.name == "Row_NEW_GAME");
            MainMenuRow quitRow = Array.Find(rows, r => r.name == "Row_QUIT");

            if (newGameRow != null && CountPersistentListeners(newGameRow) == 0)
            {
                GameObject loaderGo = new GameObject("NewGameLoader");
                loaderGo.transform.SetParent(tabletInstance.transform, false);
                NewGameLoader loader = loaderGo.AddComponent<NewGameLoader>();
                UnityEditor.Events.UnityEventTools.AddPersistentListener(
                    newGameRow.EditorOnConfirmedEvent, loader.Load);
                PrefabUtility.RecordPrefabInstancePropertyModifications(newGameRow);
                EditorUtility.SetDirty(newGameRow);
            }

            if (quitRow != null && CountPersistentListeners(quitRow) == 0)
            {
                GameObject quitGo = new GameObject("QuitGameAction");
                quitGo.transform.SetParent(tabletInstance.transform, false);
                QuitGameAction quit = quitGo.AddComponent<QuitGameAction>();
                UnityEditor.Events.UnityEventTools.AddPersistentListener(
                    quitRow.EditorOnConfirmedEvent, quit.Quit);
                PrefabUtility.RecordPrefabInstancePropertyModifications(quitRow);
                EditorUtility.SetDirty(quitRow);
            }
        }

        private static int CountPersistentListeners(MainMenuRow row) =>
            row.EditorOnConfirmedEvent.GetPersistentEventCount();

        private static Camera EnsureMenuCamera(GameObject tabletInstance)
        {
            Camera existing = Camera.main;
            if (existing != null && existing.GetComponent<MainMenuCameraSway>() != null)
            {
                return existing;
            }

            GameObject cameraGo = existing != null ? existing.gameObject : new GameObject("MainMenuCamera");
            Camera camera = cameraGo.GetComponent<Camera>();
            if (camera == null) camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";

            if (cameraGo.GetComponent<AudioListener>() == null)
            {
                cameraGo.AddComponent<AudioListener>();
            }

            // Frame the tablet's screen from a short distance in front of it, roughly centered.
            Transform screenAnchor = tabletInstance.transform.Find("ScreenAnchor");
            Vector3 lookTarget = screenAnchor != null ? screenAnchor.position : tabletInstance.transform.position;
            Vector3 forward = screenAnchor != null ? screenAnchor.TransformDirection(Vector3.forward) : Vector3.forward;
            cameraGo.transform.position = lookTarget + forward * 0.6f;
            cameraGo.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);

            if (cameraGo.GetComponent<MainMenuCameraSway>() == null)
            {
                cameraGo.AddComponent<MainMenuCameraSway>();
            }

            return camera;
        }

        private static void EnsureAmbientLight(GameObject tabletInstance)
        {
            if (UnityEngine.Object.FindFirstObjectByType<MainMenuAmbientFlicker>() != null) return;

            GameObject lightGo = new GameObject("MenuKeyLight");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.85f, 0.65f);
            light.intensity = 3f;
            light.range = 4f;
            lightGo.transform.position = tabletInstance.transform.position + new Vector3(0.3f, 0.6f, 0.4f);

            GameObject flickerGo = new GameObject("MainMenuAmbientFlicker");
            MainMenuAmbientFlicker flicker = flickerGo.AddComponent<MainMenuAmbientFlicker>();
            var so = new SerializedObject(flicker);
            SerializedProperty lightsProp = so.FindProperty("lights");
            lightsProp.arraySize = 1;
            lightsProp.GetArrayElementAtIndex(0).objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
