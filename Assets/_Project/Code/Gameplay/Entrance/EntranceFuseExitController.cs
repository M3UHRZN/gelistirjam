using Game.Gameplay.Objectives;
using UnityEngine;

namespace Game.Gameplay.Entrance
{
    public sealed class EntranceFuseExitController : MonoBehaviour
    {
        [SerializeField] private FuseBox fuseBox;
        [SerializeField] private EntranceDoorActuator exitDoor;
        [SerializeField] private Collider portalCollider;

        private bool subscribed;

        public bool IsUnlocked { get; private set; }

        private void OnEnable()
        {
            Subscribe();
            SynchronizeExit();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Bind(FuseBox fuse, EntranceDoorActuator door, Collider portal)
        {
            Unsubscribe();
            fuseBox = fuse;
            exitDoor = door;
            portalCollider = portal;
            IsUnlocked = false;
            Subscribe();
            SynchronizeExit();
        }

        public void UnlockExit()
        {
            if (IsUnlocked) return;

            IsUnlocked = true;
            exitDoor?.SetOpen(true);
            if (portalCollider != null) portalCollider.enabled = true;
        }

        private void Subscribe()
        {
            if (subscribed || fuseBox == null) return;
            fuseBox.OnCompleted += UnlockExit;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (fuseBox != null) fuseBox.OnCompleted -= UnlockExit;
            subscribed = false;
        }

        private void SynchronizeExit()
        {
            if (fuseBox != null && fuseBox.State == FuseBoxState.Complete)
            {
                UnlockExit();
                return;
            }

            IsUnlocked = false;
            exitDoor?.SetOpen(false, immediate: true);
            if (portalCollider != null) portalCollider.enabled = false;
        }
    }
}
