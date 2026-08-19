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

        private readonly MinimapTabletToggle toggle = new MinimapTabletToggle();
        private GameInputActions actions;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            actions = new GameInputActions();
        }

        private void OnEnable()
        {
            actions.Player.Minimap.performed += OnMinimapPerformed;
            actions.Player.Minimap.Enable();
        }

        private void OnDisable()
        {
            actions.Player.Minimap.performed -= OnMinimapPerformed;
            actions.Player.Minimap.Disable();
        }

        private void OnDestroy() => actions?.Dispose();

        private void OnMinimapPerformed(InputAction.CallbackContext context)
        {
            MinimapTabletCommand command = toggle.Toggle();

            if (command == MinimapTabletCommand.Opening)
            {
                animator.ResetTrigger(ClosingId);
                animator.SetTrigger(OpeningId);
                return;
            }

            animator.ResetTrigger(OpeningId);
            animator.SetTrigger(ClosingId);
        }
    }
}
