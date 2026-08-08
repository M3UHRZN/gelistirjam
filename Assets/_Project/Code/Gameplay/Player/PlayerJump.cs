using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// Self-contained jump module, deliberately a separate component from PlayerController so jump
    /// can be added to or removed from the Player prefab without touching movement code.
    /// PlayerController remains the sole writer of rb.linearVelocity -- both of its FixedUpdate
    /// branches hard-assign the full velocity every step (see PlayerController.ReadMoveAndApply), so
    /// a second component writing to the Rigidbody independently would either get silently
    /// overwritten or silently overwrite PlayerController's own write, depending on unspecified
    /// Unity script execution order between two separate FixedUpdate callbacks. To avoid that,
    /// PlayerController calls Evaluate(...) once per FixedUpdate, in the same step, right after its
    /// own ground probe and before it applies any velocity -- a direct, deterministic call, not two
    /// independently-scheduled components racing each other.
    ///
    /// The bug this exists to fix (first jump attempt, reverted): PlayerController's ground probe
    /// reaches groundSnapDistance (0.35m) below the capsule, but one fixed step of a jump launch
    /// only lifts the player a few centimeters. So the very next FixedUpdate, the probe reports
    /// "still grounded" again, and PlayerController's own grounded-branch snap correction yanks the
    /// launch velocity right back down -- every frame of the ascent, not just the first. On a slope
    /// that same snap has a horizontal component too, which reads as a teleport. The fix: keep
    /// telling PlayerController "treat this step as airborne" for the whole ascending phase, not
    /// just the launch frame, until vertical velocity stops being positive.
    /// </summary>
    public class PlayerJump : MonoBehaviour
    {
        [SerializeField, Tooltip("Peak jump height in meters. Converted to a launch speed via v = sqrt(2 * |gravity| * height) using PlayerController's own gravity value each call, so the two can never fall out of sync.")]
        private float jumpHeight = 1f;

        private GameInputActions actions;
        private bool jumpRequested;

        public bool IsAscending { get; private set; }

        private void Awake() => actions = new GameInputActions();

        private void OnEnable()
        {
            actions.Player.Enable();
            actions.Player.Jump.performed += OnJumpPerformed;
        }

        private void OnDisable()
        {
            actions.Player.Jump.performed -= OnJumpPerformed;
            actions.Player.Disable();
            ResetState();
        }

        private void OnDestroy() => actions?.Dispose();

        private void OnJumpPerformed(InputAction.CallbackContext context) => jumpRequested = true;

        /// <summary>
        /// Called by PlayerController once per FixedUpdate, immediately after its own ground probe
        /// and before it applies any velocity this step. groundedByProbe/currentVerticalVelocity are
        /// PlayerController's own values for THIS step; gravityMagnitude is PlayerController's
        /// gravity field value (same signed convention -- negative-down).
        ///
        /// Returns true if this step should be treated as airborne for movement-routing purposes
        /// (either just launched, or still rising from an earlier launch); the caller should skip
        /// its grounded/snap branch whenever this returns true. On the exact launch frame,
        /// overrideVerticalVelocity is the new launch speed the caller must assign into its own
        /// verticalVelocity before continuing; on every other frame it just echoes
        /// currentVerticalVelocity back unchanged.
        /// </summary>
        public bool Evaluate(bool groundedByProbe, float currentVerticalVelocity, float gravityMagnitude, out float overrideVerticalVelocity)
        {
            overrideVerticalVelocity = currentVerticalVelocity;

            if (IsAscending)
            {
                if (currentVerticalVelocity <= 0f)
                {
                    // Apex reached -- hand control back to PlayerController's normal ground/gravity logic.
                    IsAscending = false;
                }
                else
                {
                    return true; // still rising: keep forcing airborne treatment so nothing snaps us back down mid-air
                }
            }

            if (jumpRequested)
            {
                // Consumed either way -- no buffering across frames, matching this project's
                // no-smoothing, no-buffering movement philosophy (see PlayerController).
                jumpRequested = false;
                if (groundedByProbe)
                {
                    overrideVerticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravityMagnitude) * jumpHeight);
                    IsAscending = true;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Clears buffered/ascending state. PlayerController calls this from its own
        /// HaltMotion (control-gate disable, teleport) so a stale ascending flag can't survive a
        /// teleport or a control-disable/enable cycle.</summary>
        public void ResetState()
        {
            jumpRequested = false;
            IsAscending = false;
        }
    }
}
