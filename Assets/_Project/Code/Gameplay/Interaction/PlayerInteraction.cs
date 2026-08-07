using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Interaction
{
    /// <summary>
    /// Finds and hold-interacts with IInteractable objects via a forward raycast from the player
    /// camera (TGD section 49 — "PlayerInteraction: interaction ray"). Hold E while aiming.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float interactRange = 2.5f;
        [SerializeField] private LayerMask interactableMask = ~0;

        private IInteractable currentTarget;
        private bool isHolding;

        /// <summary>Prompt text for the currently-aimed, interactable target, or null.</summary>
        public string CurrentPrompt => currentTarget != null && currentTarget.CanInteract ? currentTarget.InteractionPrompt : null;

        public void Bind(Camera camera) => playerCamera = camera;

        private void Update()
        {
            IInteractable target = DetectTarget();

            if (target != currentTarget)
            {
                StopHolding();
                currentTarget = target;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || currentTarget == null)
            {
                StopHolding();
                return;
            }

            // While a hold is already in progress, only an explicit key release (or losing the
            // target/keyboard, handled above) should end it. CanInteract legitimately flips false
            // the instant BeginInteract() sets the target's own state to "in progress" — re-checking
            // it here every frame during an active hold cancelled every interaction one frame after
            // it started, making the fuse impossible to ever complete (see chat).
            if (isHolding)
            {
                if (keyboard.eKey.wasReleasedThisFrame) StopHolding();
                return;
            }

            if (!currentTarget.CanInteract) return;

            if (keyboard.eKey.wasPressedThisFrame)
            {
                currentTarget.BeginInteract();
                isHolding = true;
            }
        }

        private void StopHolding()
        {
            if (!isHolding) return;
            currentTarget?.EndInteract();
            isHolding = false;
        }

        private IInteractable DetectTarget()
        {
            if (playerCamera == null) return null;

            if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward,
                    out RaycastHit hit, interactRange, interactableMask, QueryTriggerInteraction.Collide))
            {
                hit.collider.TryGetComponent(out IInteractable interactable);
                return interactable;
            }

            return null;
        }
    }
}
