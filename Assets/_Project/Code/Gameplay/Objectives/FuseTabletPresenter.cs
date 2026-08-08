using System.Collections;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Objectives
{
    /// <summary>
    /// The tablet's side of a fuse interaction: it hands the camera to the fuse-view virtual camera,
    /// grants a small amount of look freedom while the minigame runs, and plays the lever flourish on
    /// success. Lives on the fuse tablet prefab, so it cannot hold a scene reference to the
    /// CinemachineBrain — MapFuseGate binds that at spawn time, the same way it already binds every
    /// other dependency a spawned FuseBox needs.
    ///
    /// Look is driven here rather than by CinemachineInputAxisController: PlayerController disables
    /// its own GameInputActions clone while a fuse is active, and an InputActionReference resolves
    /// against the shared asset's separate instance instead — a working-but-invisible coupling. The
    /// rest of the gameplay code already reads devices directly, so this does too.
    /// </summary>
    [DisallowMultipleComponent]
    public class FuseTabletPresenter : MonoBehaviour, IFuseViewPresenter
    {
        [Header("Prefab wiring")]
        [SerializeField, Tooltip("Where the minigame canvas parents itself. Aligned to the tablet's screen surface with +Z pointing out of the screen.")]
        private Transform screenAnchor;
        [SerializeField, Tooltip("The fuse-view virtual camera, sitting under Anchor_CameraView.")]
        private CinemachineCamera fuseViewCamera;
        [SerializeField, Tooltip("The lever mesh rotated on successful completion.")]
        private Transform lever;

        [Header("Camera")]
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int idlePriority = 0;
        [SerializeField, Tooltip("Seconds to blend from the player POV to the tablet.")]
        private float blendInDuration = 0.5f;
        [SerializeField, Tooltip("Seconds to blend back. Shorter than the way in so bailing out feels responsive.")]
        private float blendOutDuration = 0.25f;

        [Header("Limited look")]
        [SerializeField, Tooltip("Degrees rotated per pixel of raw mouse delta while the minigame is open.")]
        private float lookSensitivity = 0.08f;
        [SerializeField] private float maxYawDegrees = 15f;
        [SerializeField] private float maxPitchDegrees = 10f;

        [Header("Lever")]
        [SerializeField] private Vector3 leverClosedEuler;
        [SerializeField] private Vector3 leverOpenEuler = new Vector3(0f, 0f, -70f);
        [SerializeField] private float leverTweenDuration = 0.6f;

        private CinemachineBrain brain;
        private float yaw;
        private float pitch;

        public Transform ScreenAnchor => screenAnchor;

        /// <summary>True between Activate and Deactivate. Drives whether look input is consumed.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Supplies the scene's CinemachineBrain. Called by MapFuseGate right after spawning.</summary>
        public void Bind(CinemachineBrain cinemachineBrain) => brain = cinemachineBrain;

        public void Activate()
        {
            IsActive = true;
            yaw = 0f;
            pitch = 0f;
            ResetLever();

            SetBlendDuration(blendInDuration);

            if (fuseViewCamera != null)
            {
                fuseViewCamera.transform.localRotation = Quaternion.identity;
                fuseViewCamera.Priority = new PrioritySettings { Enabled = true, Value = activePriority };
            }
        }

        public void Deactivate()
        {
            IsActive = false;

            // Blend duration is set before the priority drop so the brain picks up the shorter
            // curve for the transition this very call triggers.
            SetBlendDuration(blendOutDuration);

            if (fuseViewCamera != null)
            {
                fuseViewCamera.Priority = new PrioritySettings { Enabled = true, Value = idlePriority };
            }
        }

        public IEnumerator PlayCompletion()
        {
            if (lever == null) yield break;

            Tween tween = lever
                .DOLocalRotate(leverOpenEuler, leverTweenDuration)
                .SetEase(Ease.OutBack);

            yield return tween.WaitForCompletion();
        }

        /// <summary>
        /// Applies an already-sensitivity-scaled look delta, clamped to the configured cone. Public and
        /// separate from Update so the clamping is testable without synthesising Input System devices.
        /// </summary>
        public void ApplyLook(Vector2 scaledDelta)
        {
            if (fuseViewCamera == null) return;

            yaw = Mathf.Clamp(yaw + scaledDelta.x, -maxYawDegrees, maxYawDegrees);
            pitch = Mathf.Clamp(pitch - scaledDelta.y, -maxPitchDegrees, maxPitchDegrees);
            fuseViewCamera.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void Update()
        {
            if (!IsActive) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // Mouse.delta is already "pixels moved this frame" -- no Time.deltaTime, same rule as
            // PlayerController.HandleLook.
            ApplyLook(mouse.delta.ReadValue() * lookSensitivity);
        }

        private void SetBlendDuration(float seconds)
        {
            if (brain == null) return;
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, seconds);
        }

        /// <summary>
        /// Snaps the lever closed, killing any tween still running from a previous interaction. A fuse
        /// can be re-entered after an ESC cancel, and DOTween keeps mutating a transform after the
        /// coroutine that started it is gone.
        /// </summary>
        private void ResetLever()
        {
            if (lever == null) return;
            lever.DOKill();
            lever.localRotation = Quaternion.Euler(leverClosedEuler);
        }

        private void OnDisable()
        {
            if (lever != null) lever.DOKill();
        }
    }
}
