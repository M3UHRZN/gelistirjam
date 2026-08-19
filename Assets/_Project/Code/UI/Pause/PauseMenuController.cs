using Game.Gameplay.Objectives;
using Game.Gameplay.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI.Pause
{
    /// <summary>
    /// Corrupted Terminal pause menu: owns pause state, cursor hand-off, scene exit and the
    /// Master/player/monster mixer controls used by the gameplay scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour
    {
        public const string MasterVolumeParameter = "MasterVolume";
        public const string PlayerVolumeParameter = "PlayerVolume";
        public const string MonsterVolumeParameter = "MonsterVolume";

        private const string MasterVolumeKey = "Audio.MasterVolume";
        private const string PlayerVolumeKey = "Audio.PlayerVolume";
        private const string MonsterVolumeKey = "Audio.MonsterVolume";
        private const float MinimumLinearVolume = 0.0001f;

        [Header("Pause UI")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider playerSlider;
        [SerializeField] private Slider monsterSlider;
        [SerializeField] private TMP_Text masterValueLabel;
        [SerializeField] private TMP_Text playerValueLabel;
        [SerializeField] private TMP_Text monsterValueLabel;

        [Header("Gameplay")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private FuseMiniGameController fuseMiniGame;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private bool isPaused;
        private bool playerWasEnabled;
        private float previousTimeScale = 1f;

        public bool IsPaused => isPaused;

        private void Awake()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            panelRoot.SetActive(false);
            resumeButton.onClick.AddListener(Resume);
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
            playerSlider.onValueChanged.AddListener(SetPlayerVolume);
            monsterSlider.onValueChanged.AddListener(SetMonsterVolume);

            InitializeVolume(masterSlider, masterValueLabel, MasterVolumeParameter, MasterVolumeKey);
            InitializeVolume(playerSlider, playerValueLabel, PlayerVolumeParameter, PlayerVolumeKey);
            InitializeVolume(monsterSlider, monsterValueLabel, MonsterVolumeParameter, MonsterVolumeKey);
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

            if (isPaused)
            {
                Resume();
                return;
            }

            // Escape belongs to the fuse interaction while its terminal is open.
            if (fuseMiniGame != null && fuseMiniGame.IsActive) return;
            if (playerController != null && !playerController.enabled) return;

            Pause();
        }

        public void Pause()
        {
            if (isPaused) return;

            isPaused = true;
            previousTimeScale = Time.timeScale;
            playerWasEnabled = playerController != null && playerController.enabled;
            if (playerWasEnabled) playerController.SetControlEnabled(false);

            Time.timeScale = 0f;
            panelRoot.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            resumeButton.Select();
        }

        public void Resume()
        {
            if (!isPaused) return;

            panelRoot.SetActive(false);
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            isPaused = false;

            if (playerWasEnabled && playerController != null)
            {
                playerController.SetControlEnabled(true);
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void ReturnToMainMenu()
        {
            PlayerPrefs.Save();
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            isPaused = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        public void SetMasterVolume(float value) =>
            SetVolume(MasterVolumeParameter, MasterVolumeKey, value, masterValueLabel);

        public void SetPlayerVolume(float value) =>
            SetVolume(PlayerVolumeParameter, PlayerVolumeKey, value, playerValueLabel);

        public void SetMonsterVolume(float value) =>
            SetVolume(MonsterVolumeParameter, MonsterVolumeKey, value, monsterValueLabel);

        private void InitializeVolume(Slider slider, TMP_Text valueLabel, string parameter,
            string playerPrefsKey)
        {
            float defaultValue = 1f;
            if (audioMixer.GetFloat(parameter, out float currentDb))
            {
                defaultValue = DecibelsToLinear(currentDb);
            }

            float value = Mathf.Clamp01(PlayerPrefs.GetFloat(playerPrefsKey, defaultValue));
            slider.SetValueWithoutNotify(value);
            SetVolume(parameter, playerPrefsKey, value, valueLabel, false);
        }

        private void SetVolume(string parameter, string playerPrefsKey, float value,
            TMP_Text valueLabel, bool savePreference = true)
        {
            float normalized = Mathf.Clamp01(value);
            float decibels = normalized <= 0f
                ? -80f
                : Mathf.Log10(Mathf.Max(normalized, MinimumLinearVolume)) * 20f;

            if (!audioMixer.SetFloat(parameter, decibels))
            {
                Debug.LogWarning($"[Pause Menu] AudioMixer parameter '{parameter}' is not exposed.",
                    audioMixer);
            }

            valueLabel.text = $"{Mathf.RoundToInt(normalized * 100f):000}%";
            if (savePreference) PlayerPrefs.SetFloat(playerPrefsKey, normalized);
        }

        private static float DecibelsToLinear(float decibels) =>
            Mathf.Clamp01(Mathf.Pow(10f, decibels / 20f));

        private bool ValidateConfiguration()
        {
            bool valid = panelRoot != null && resumeButton != null && mainMenuButton != null &&
                         audioMixer != null && masterSlider != null && playerSlider != null &&
                         monsterSlider != null && masterValueLabel != null &&
                         playerValueLabel != null && monsterValueLabel != null;

            if (!valid)
            {
                Debug.LogError("[Pause Menu] Required UI or AudioMixer references are missing.", this);
            }

            return valid;
        }

        private void OnDestroy()
        {
            if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
            if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
            if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(SetMasterVolume);
            if (playerSlider != null) playerSlider.onValueChanged.RemoveListener(SetPlayerVolume);
            if (monsterSlider != null) monsterSlider.onValueChanged.RemoveListener(SetMonsterVolume);

            if (isPaused) Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            PlayerPrefs.Save();
        }
    }
}
