using Game.Gameplay.Objectives;
using Game.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI.Minimap
{
    internal enum MinimapTabletCommand
    {
        Opening,
        Closing
    }

    internal sealed class MinimapTabletToggle
    {
        private bool isOpen;

        internal bool IsOpen => isOpen;

        internal MinimapTabletCommand Toggle()
        {
            isOpen = !isOpen;
            return isOpen ? MinimapTabletCommand.Opening : MinimapTabletCommand.Closing;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class MinimapTabletInput : MonoBehaviour
    {
        private static readonly int OpeningId = Animator.StringToHash("Opening");
        private static readonly int ClosingId = Animator.StringToHash("Closing");

        [SerializeField] private Animator animator;

        [Header("Charge")]
        [SerializeField] private ObjectiveManager objectiveManager;
        [SerializeField] private MinimapTabletGraphic chargeGraphic;
        [SerializeField, Min(0f)] private float initialChargeSeconds = 5f;
        [SerializeField, Min(0.1f)] private float maximumChargeSeconds = 12f;
        [SerializeField, Min(0f)] private float chargePerFuseSeconds = 8f;
        [SerializeField, Min(0f)] private float lowChargeThresholdSeconds = 2f;
        [SerializeField, Min(0f), Tooltip("Opening animation time; charge starts draining after this delay.")]
        private float openingDelaySeconds = 0.5f;

        private readonly MinimapTabletToggle toggle = new MinimapTabletToggle();
        private GameInputActions actions;
        private float currentChargeSeconds;
        private float openingDelayRemaining;
        private bool objectiveSubscribed;

        public float CurrentChargeSeconds => currentChargeSeconds;
        public float MaximumChargeSeconds => maximumChargeSeconds;
        public bool IsOpen => toggle.IsOpen;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (chargeGraphic == null)
            {
                chargeGraphic = GetComponentInChildren<MinimapTabletGraphic>(true);
            }

            currentChargeSeconds = Mathf.Clamp(
                initialChargeSeconds,
                0f,
                maximumChargeSeconds);
            actions = new GameInputActions();
            PublishCharge();
        }

        private void OnEnable()
        {
            actions.Player.Minimap.performed += OnMinimapPerformed;
            actions.Player.Minimap.Enable();
            SubscribeToObjectives();
            PublishCharge();
        }

        private void Start() => SubscribeToObjectives();

        private void Update()
        {
            if (!toggle.IsOpen)
            {
                return;
            }

            float drainSeconds = Time.deltaTime;
            if (openingDelayRemaining > 0f)
            {
                openingDelayRemaining -= drainSeconds;
                if (openingDelayRemaining > 0f)
                {
                    return;
                }

                drainSeconds = -openingDelayRemaining;
                openingDelayRemaining = 0f;
            }

            currentChargeSeconds = Mathf.Max(0f, currentChargeSeconds - drainSeconds);
            PublishCharge();
            if (currentChargeSeconds <= 0f)
            {
                CloseTablet();
            }
        }

        private void OnDisable()
        {
            actions.Player.Minimap.performed -= OnMinimapPerformed;
            actions.Player.Minimap.Disable();
            UnsubscribeFromObjectives();
        }

        private void OnDestroy()
        {
            UnsubscribeFromObjectives();
            actions?.Dispose();
        }

        private void OnMinimapPerformed(InputAction.CallbackContext context)
        {
            if (!toggle.IsOpen && currentChargeSeconds <= 0f)
            {
                PublishCharge();
                return;
            }

            MinimapTabletCommand command = toggle.Toggle();

            if (command == MinimapTabletCommand.Opening)
            {
                openingDelayRemaining = openingDelaySeconds;
                animator.ResetTrigger(ClosingId);
                animator.SetTrigger(OpeningId);
                return;
            }

            openingDelayRemaining = 0f;
            TriggerClosingAnimation();
        }

        private void CloseTablet()
        {
            if (!toggle.IsOpen)
            {
                return;
            }

            toggle.Toggle();
            openingDelayRemaining = 0f;
            TriggerClosingAnimation();
        }

        private void TriggerClosingAnimation()
        {
            animator.ResetTrigger(OpeningId);
            animator.SetTrigger(ClosingId);
        }

        private void SubscribeToObjectives()
        {
            if (objectiveSubscribed)
            {
                return;
            }

            if (objectiveManager == null)
            {
                objectiveManager = FindFirstObjectByType<ObjectiveManager>();
            }

            if (objectiveManager == null)
            {
                return;
            }

            objectiveManager.OnAnyFuseCompleted += HandleFuseCompleted;
            objectiveSubscribed = true;
        }

        private void UnsubscribeFromObjectives()
        {
            if (!objectiveSubscribed || objectiveManager == null)
            {
                objectiveSubscribed = false;
                return;
            }

            objectiveManager.OnAnyFuseCompleted -= HandleFuseCompleted;
            objectiveSubscribed = false;
        }

        private void HandleFuseCompleted()
        {
            bool finalFuse = objectiveManager.RequiredCount > 0 &&
                             objectiveManager.CompletedCount >= objectiveManager.RequiredCount;
            currentChargeSeconds = finalFuse
                ? maximumChargeSeconds
                : Mathf.Min(maximumChargeSeconds, currentChargeSeconds + chargePerFuseSeconds);
            PublishCharge();
        }

        private void PublishCharge()
        {
            if (chargeGraphic == null)
            {
                return;
            }

            float normalized = maximumChargeSeconds > 0f
                ? currentChargeSeconds / maximumChargeSeconds
                : 0f;
            bool isLow = currentChargeSeconds > 0f &&
                         currentChargeSeconds <= lowChargeThresholdSeconds;
            chargeGraphic.SetChargeState(normalized, isLow);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maximumChargeSeconds = Mathf.Max(0.1f, maximumChargeSeconds);
            initialChargeSeconds = Mathf.Clamp(initialChargeSeconds, 0f, maximumChargeSeconds);
            chargePerFuseSeconds = Mathf.Max(0f, chargePerFuseSeconds);
            lowChargeThresholdSeconds = Mathf.Clamp(
                lowChargeThresholdSeconds,
                0f,
                maximumChargeSeconds);
            openingDelaySeconds = Mathf.Max(0f, openingDelaySeconds);
        }
#endif
    }
}
