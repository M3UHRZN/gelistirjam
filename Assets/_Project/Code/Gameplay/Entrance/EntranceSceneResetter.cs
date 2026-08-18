using System.Collections;
using Game.Gameplay.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.Entrance
{
    public sealed class EntranceSceneResetter : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField, Min(0f)] private float fadeDuration = 0.25f;
        [SerializeField, Min(0f)] private float resetDelay = 0.5f;

        private IEntranceSceneLoader sceneLoader;
        private bool isResetting;

        public bool IsResetting => isResetting;

        private void Awake()
        {
            sceneLoader ??= new UnityEntranceSceneLoader();
        }

        public void Bind(PlayerController player, CanvasGroup group, IEntranceSceneLoader loader)
        {
            playerController = player;
            fadeGroup = group;
            sceneLoader = loader;
        }

        public bool BeginReset()
        {
            if (!isActiveAndEnabled || isResetting) return false;

            isResetting = true;
            playerController?.SetControlEnabled(false);
            sceneLoader ??= new UnityEntranceSceneLoader();
            StartCoroutine(ResetScene());
            return true;
        }

        private IEnumerator ResetScene()
        {
            if (fadeGroup != null)
            {
                float startAlpha = fadeGroup.alpha;
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    fadeGroup.alpha = Mathf.Lerp(startAlpha, 1f, Mathf.Clamp01(elapsed / fadeDuration));
                    yield return null;
                }

                fadeGroup.alpha = 1f;
            }

            float delayElapsed = 0f;
            while (delayElapsed < resetDelay)
            {
                delayElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            sceneLoader.Load(SceneManager.GetActiveScene().name);
        }

    }
}
