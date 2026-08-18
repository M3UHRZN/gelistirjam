using System;
using System.Collections;
using Game.Gameplay.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.Entrance
{
    public enum EntranceResetStartResult
    {
        Started,
        AlreadyRunning,
        Unavailable,
        MissingPlayer,
        PlayerMismatch,
    }

    public enum EntranceResetInterruptionReason
    {
        RoutineStartFailed,
        Disabled,
        Destroyed,
        SceneLoadFailed,
    }

    public interface IEntranceResetRoutineRunner
    {
        bool TryStart(IEnumerator routine);
    }

    public sealed class EntranceSceneResetter : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField, Min(0f)] private float fadeDuration = 0.25f;
        [SerializeField, Min(0f)] private float resetDelay = 0.5f;

        private IEntranceSceneLoader sceneLoader;
        private IEntranceResetRoutineRunner routineRunner;
        private PlayerController transactionPlayer;
        private Action<EntranceResetInterruptionReason> interruptionCallbacks;
        private bool isResetting;
        private bool insideSceneLoad;

        public bool IsResetting => isResetting;

        private void Awake()
        {
            sceneLoader ??= new UnityEntranceSceneLoader();
            routineRunner ??= new UnityEntranceResetRoutineRunner(this);
        }

        public void Bind(
            PlayerController player,
            CanvasGroup group,
            IEntranceSceneLoader loader,
            IEntranceResetRoutineRunner runner = null)
        {
            playerController = player;
            fadeGroup = group;
            sceneLoader = loader;
            routineRunner = runner ?? new UnityEntranceResetRoutineRunner(this);
        }

        public bool BeginReset()
        {
            return TryBeginReset(playerController, null) == EntranceResetStartResult.Started;
        }

        public bool CanReset(PlayerController requiredPlayer)
        {
            if (!isActiveAndEnabled || requiredPlayer == null || playerController == null) return false;
            if (playerController != requiredPlayer) return false;
            return !isResetting || transactionPlayer == requiredPlayer;
        }

        public EntranceResetStartResult TryBeginReset(
            PlayerController requiredPlayer,
            Action<EntranceResetInterruptionReason> onInterrupted)
        {
            if (requiredPlayer == null || playerController == null) return EntranceResetStartResult.MissingPlayer;
            if (playerController != requiredPlayer) return EntranceResetStartResult.PlayerMismatch;
            if (!isActiveAndEnabled) return EntranceResetStartResult.Unavailable;
            if (isResetting)
            {
                if (transactionPlayer != requiredPlayer) return EntranceResetStartResult.PlayerMismatch;

                interruptionCallbacks += onInterrupted;
                return EntranceResetStartResult.AlreadyRunning;
            }

            isResetting = true;
            transactionPlayer = requiredPlayer;
            interruptionCallbacks = onInterrupted;
            requiredPlayer.SetControlEnabled(false);
            sceneLoader ??= new UnityEntranceSceneLoader();
            routineRunner ??= new UnityEntranceResetRoutineRunner(this);

            bool started;
            try
            {
                started = routineRunner.TryStart(ResetScene());
            }
            catch
            {
                started = false;
            }

            if (started) return EntranceResetStartResult.Started;

            Interrupt(EntranceResetInterruptionReason.RoutineStartFailed);
            return EntranceResetStartResult.Unavailable;
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

            insideSceneLoad = true;
            try
            {
                sceneLoader.Load(SceneManager.GetActiveScene().name);
            }
            catch
            {
                insideSceneLoad = false;
                Interrupt(EntranceResetInterruptionReason.SceneLoadFailed);
                yield break;
            }

            insideSceneLoad = false;
            CompleteSuccessfulLoad();
        }

        private void OnDisable()
        {
            if (insideSceneLoad) return;
            Interrupt(EntranceResetInterruptionReason.Disabled);
        }

        private void OnDestroy()
        {
            if (insideSceneLoad) return;
            Interrupt(EntranceResetInterruptionReason.Destroyed);
        }

        private void CompleteSuccessfulLoad()
        {
            if (!isResetting) return;

            isResetting = false;
            transactionPlayer = null;
            interruptionCallbacks = null;
        }

        private void Interrupt(EntranceResetInterruptionReason reason)
        {
            if (!isResetting) return;

            PlayerController interruptedPlayer = transactionPlayer;
            Action<EntranceResetInterruptionReason> callbacks = interruptionCallbacks;
            isResetting = false;
            transactionPlayer = null;
            interruptionCallbacks = null;
            insideSceneLoad = false;

            if (interruptedPlayer != null) interruptedPlayer.SetControlEnabled(true);
            callbacks?.Invoke(reason);
        }

        private bool StartUnityRoutine(IEnumerator routine)
        {
            return StartCoroutine(routine) != null;
        }

        private sealed class UnityEntranceResetRoutineRunner : IEntranceResetRoutineRunner
        {
            private readonly EntranceSceneResetter owner;

            public UnityEntranceResetRoutineRunner(EntranceSceneResetter owner)
            {
                this.owner = owner;
            }

            public bool TryStart(IEnumerator routine)
            {
                return owner != null && owner.StartUnityRoutine(routine);
            }
        }
    }
}
