using System;
using System.Collections;
using Game.Core;
using Game.Gameplay.Monster;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Gameplay.Death
{
    /// <summary>
    /// Bruised-industrial death presentation. Gameplay authority remains in DeathState; this
    /// component only owns the kill camera, sound and persistent Retry/Main Menu overlay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathSequenceController : MonoBehaviour
    {
        private const string KillCameraAnchorName = "KillCameraAnchor";

        private static readonly Color OverlayColor = new(0.025f, 0.018f, 0.022f, 0.96f);
        private static readonly Color PanelColor = new(0.075f, 0.045f, 0.055f, 0.98f);
        private static readonly Color DangerColor = new(0.72f, 0.08f, 0.10f, 1f);
        private static readonly Color TextColor = new(0.86f, 0.80f, 0.78f, 1f);

        [SerializeField] private GameFlowController gameFlow;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private GameObject deathUiRoot;
        [SerializeField] private AudioSource deathAudioSource;
        [SerializeField] private AudioClip deathClip;
        [SerializeField, Min(0f)] private float panelDelaySeconds = 0.65f;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private MonsterBrain boundMonster;
        private Transform killCameraAnchor;
        private Action retryAction;
        private Action mainMenuAction;
        private Coroutine sequenceRoutine;
        private bool subscribed;

        public bool IsPlaying { get; private set; }
        public MonsterBrain BoundMonster => boundMonster;
        public Transform KillCameraAnchor => killCameraAnchor;
        public GameObject DeathUiRoot => deathUiRoot;

        public void Configure(GameFlowController flow, Camera camera, AudioClip clip,
            float panelDelay, string menuSceneName)
        {
            Unsubscribe();
            gameFlow = flow;
            playerCamera = camera != null ? camera : Camera.main;
            deathClip = clip;
            panelDelaySeconds = Mathf.Max(0f, panelDelay);
            if (!string.IsNullOrWhiteSpace(menuSceneName)) mainMenuSceneName = menuSceneName;
            Subscribe();

            if (gameFlow != null && gameFlow.CurrentStateName == nameof(DeathState))
                BeginSequence();
        }

        public void BindMonster(MonsterBrain monster)
        {
            boundMonster = monster;
            killCameraAnchor = FindKillCameraAnchor(monster);
        }

        /// <summary>Allows another scene-loading policy to be supplied without coupling the UI to it.</summary>
        public void BindSceneActions(Action retry, Action mainMenu)
        {
            retryAction = retry;
            mainMenuAction = mainMenu;
        }

        public void Retry() => (retryAction ?? ReloadActiveScene).Invoke();

        public void MainMenu() => (mainMenuAction ?? LoadMainMenu).Invoke();

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            if (sequenceRoutine != null)
            {
                StopCoroutine(sequenceRoutine);
                sequenceRoutine = null;
            }
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || gameFlow == null || subscribed) return;
            gameFlow.OnStateChanged += HandleStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || gameFlow == null) return;
            gameFlow.OnStateChanged -= HandleStateChanged;
            subscribed = false;
        }

        private void HandleStateChanged(string stateName)
        {
            if (stateName == nameof(DeathState)) BeginSequence();
        }

        private void BeginSequence()
        {
            if (IsPlaying) return;
            IsPlaying = true;

            EnsureDeathUi();
            deathUiRoot.SetActive(false);
            LockCameraToMonster();
            PlayDeathSound();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            sequenceRoutine = StartCoroutine(RevealPanelAfterAttackBeat());
        }

        private IEnumerator RevealPanelAfterAttackBeat()
        {
            if (panelDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(panelDelaySeconds);

            deathUiRoot.SetActive(true);
            sequenceRoutine = null;
        }

        private void LockCameraToMonster()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            if (playerCamera == null) return;

            CinemachineBrain brain = playerCamera.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = false;

            killCameraAnchor = FindKillCameraAnchor(boundMonster);
            if (killCameraAnchor == null) return;

            Transform cameraTransform = playerCamera.transform;
            cameraTransform.SetParent(killCameraAnchor, false);
            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;
        }

        private void PlayDeathSound()
        {
            if (deathClip == null) return;
            if (deathAudioSource == null)
            {
                deathAudioSource = GetComponent<AudioSource>();
                if (deathAudioSource == null) deathAudioSource = gameObject.AddComponent<AudioSource>();
                deathAudioSource.playOnAwake = false;
                deathAudioSource.spatialBlend = 0f;
            }

            deathAudioSource.PlayOneShot(deathClip);
        }

        private static Transform FindKillCameraAnchor(MonsterBrain monster)
        {
            if (monster == null) return null;

            Transform[] transforms = monster.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
            {
                if (candidate.name == KillCameraAnchorName) return candidate;
            }

            return null;
        }

        private void EnsureDeathUi()
        {
            if (deathUiRoot != null) return;

            TMP_FontAsset deathFont = FindLoadedFont("CascadiaMono SDF");

            deathUiRoot = NewUiObject("DeathScreen", transform);
            Canvas canvas = deathUiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = deathUiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            deathUiRoot.AddComponent<GraphicRaycaster>();

            RectTransform overlay = NewRect("Blackout", deathUiRoot.transform);
            Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = OverlayColor;

            RectTransform dangerBand = NewRect("DangerBand", overlay);
            dangerBand.anchorMin = new Vector2(0f, 0.49f);
            dangerBand.anchorMax = new Vector2(1f, 0.51f);
            dangerBand.offsetMin = Vector2.zero;
            dangerBand.offsetMax = Vector2.zero;
            dangerBand.gameObject.AddComponent<Image>().color = new Color(DangerColor.r, DangerColor.g, DangerColor.b, 0.3f);

            RectTransform panel = NewRect("DeathPanel", overlay);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(720f, 470f);
            panel.gameObject.AddComponent<Image>().color = PanelColor;

            AddLabel(panel, "Title", "ÖLDÜN", 86f, new Vector2(0f, 105f), new Vector2(640f, 120f), DangerColor, deathFont);
            AddLabel(panel, "Subtitle", "SİNYAL KESİLDİ", 25f, new Vector2(0f, 35f), new Vector2(640f, 55f), TextColor, deathFont);
            AddButton(panel, "RetryButton", "RETRY", new Vector2(0f, -70f), Retry, deathFont);
            AddButton(panel, "MainMenuButton", "MAIN MENU", new Vector2(0f, -160f), MainMenu, deathFont);
            EnsureEventSystem();
        }

        private static TMP_FontAsset FindLoadedFont(string fontName)
        {
            TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (TMP_FontAsset font in fonts)
            {
                if (font != null && font.name == fontName) return font;
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static GameObject NewUiObject(string objectName, Transform parent)
        {
            var uiObject = new GameObject(objectName, typeof(RectTransform));
            uiObject.transform.SetParent(parent, false);
            return uiObject;
        }

        private static RectTransform NewRect(string objectName, Transform parent) =>
            NewUiObject(objectName, parent).GetComponent<RectTransform>();

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AddLabel(RectTransform parent, string objectName, string value,
            float fontSize, Vector2 position, Vector2 size, Color color, TMP_FontAsset font)
        {
            RectTransform rect = NewRect(objectName, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = value;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
        }

        private static void AddButton(RectTransform parent, string objectName, string label,
            Vector2 position, UnityEngine.Events.UnityAction action, TMP_FontAsset font)
        {
            RectTransform rect = NewRect(objectName, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(390f, 66f);

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.20f, 0.06f, 0.075f, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1.25f, 0.7f, 0.72f, 1f),
                pressedColor = new Color(0.72f, 0.38f, 0.40f, 1f),
                selectedColor = new Color(1.15f, 0.65f, 0.68f, 1f),
                disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
            button.onClick.AddListener(action);

            RectTransform textRect = NewRect("Label", rect);
            Stretch(textRect);
            TextMeshProUGUI text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = label;
            text.fontSize = 27f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = TextColor;
            text.raycastTarget = false;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var eventSystemObject = new GameObject(
                "DeathScreenEventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(transform, false);
        }

        private static void ReloadActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
        }

        private void LoadMainMenu() => SceneManager.LoadScene(mainMenuSceneName);
    }
}
