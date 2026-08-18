using System.Collections;
using UnityEngine;

namespace Game.Gameplay.Entrance
{
    public sealed class EntranceDoorActuator : MonoBehaviour
    {
        [SerializeField] private Transform movingTransform;
        [SerializeField] private Vector3 openLocalPositionOffset;
        [SerializeField] private Vector3 openLocalEulerOffset;
        [SerializeField, Min(0f)] private float travelDuration = 0.5f;
        [SerializeField] private Collider[] blockers;

        private Coroutine motion;
        private Vector3 closedLocalPosition;
        private Quaternion closedLocalRotation;
        private bool hasClosedPose;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            CaptureClosedPose();
        }

        public void Bind(Transform moving, Vector3 positionOffset, Vector3 eulerOffset, float duration, Collider[] doorBlockers)
        {
            StopMotion();
            movingTransform = moving;
            openLocalPositionOffset = positionOffset;
            openLocalEulerOffset = eulerOffset;
            travelDuration = Mathf.Max(0f, duration);
            blockers = doorBlockers;
            hasClosedPose = false;
            CaptureClosedPose();
        }

        public void SetOpen(bool open, bool immediate = false)
        {
            if (!CaptureClosedPose()) return;

            StopMotion();
            IsOpen = open;
            if (open) SetBlockersEnabled(false);

            var targetPosition = open ? closedLocalPosition + openLocalPositionOffset : closedLocalPosition;
            var targetRotation = open ? closedLocalRotation * Quaternion.Euler(openLocalEulerOffset) : closedLocalRotation;
            if (immediate || travelDuration <= 0f)
            {
                ApplyPose(targetPosition, targetRotation);
                if (!open) SetBlockersEnabled(true);
                return;
            }

            motion = StartCoroutine(MoveTo(targetPosition, targetRotation, open));
        }

        private bool CaptureClosedPose()
        {
            if (hasClosedPose) return true;
            if (movingTransform == null) return false;

            closedLocalPosition = movingTransform.localPosition;
            closedLocalRotation = movingTransform.localRotation;
            hasClosedPose = true;
            return true;
        }

        private void StopMotion()
        {
            if (motion == null) return;
            StopCoroutine(motion);
            motion = null;
        }

        private IEnumerator MoveTo(Vector3 targetPosition, Quaternion targetRotation, bool open)
        {
            var startPosition = movingTransform.localPosition;
            var startRotation = movingTransform.localRotation;
            var elapsed = 0f;
            while (elapsed < travelDuration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / travelDuration);
                ApplyPose(Vector3.Lerp(startPosition, targetPosition, progress), Quaternion.Slerp(startRotation, targetRotation, progress));
                yield return null;
            }

            ApplyPose(targetPosition, targetRotation);
            motion = null;
            if (!open) SetBlockersEnabled(true);
        }

        private void ApplyPose(Vector3 localPosition, Quaternion localRotation)
        {
            movingTransform.localPosition = localPosition;
            movingTransform.localRotation = localRotation;
        }

        private void SetBlockersEnabled(bool enabled)
        {
            if (blockers == null) return;
            foreach (var blocker in blockers)
            {
                if (blocker != null) blocker.enabled = enabled;
            }
        }
    }
}
