using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Objectives
{
    /// <summary>Tracks fuse-box completion and raises OnAllComplete once every required one is done (TGD section 33, 37).</summary>
    public class ObjectiveManager : MonoBehaviour
    {
        [SerializeField] private int requiredCount = 2;

        private readonly List<FuseBox> registered = new List<FuseBox>();
        private int completedCount;
        private bool allCompleteRaised;

        public int CompletedCount => completedCount;
        public int RequiredCount => requiredCount;
        public int RegisteredCount => registered.Count;
        public event Action OnAllComplete;
        public event Action OnAnyFuseCompleted;

        /// <summary>Forwarded from FuseBox (TGD section 36) so the monster can react to fuse work in progress.</summary>
        public event Action<Vector3> OnFuseInteractionStarted;
        public event Action OnFuseInteractionStopped;

        public void SetRequiredCount(int count) => requiredCount = count;

        public void Register(FuseBox fuseBox)
        {
            if (registered.Contains(fuseBox)) return;
            registered.Add(fuseBox);
            fuseBox.OnCompleted += HandleFuseCompleted;
            fuseBox.OnInteractionStarted += HandleFuseInteractionStarted;
            fuseBox.OnInteractionStopped += HandleFuseInteractionStopped;
        }

        public void Unregister(FuseBox fuseBox)
        {
            if (!registered.Remove(fuseBox)) return;
            fuseBox.OnCompleted -= HandleFuseCompleted;
            fuseBox.OnInteractionStarted -= HandleFuseInteractionStarted;
            fuseBox.OnInteractionStopped -= HandleFuseInteractionStopped;
        }

        private void HandleFuseInteractionStarted(Vector3 position) => OnFuseInteractionStarted?.Invoke(position);
        private void HandleFuseInteractionStopped() => OnFuseInteractionStopped?.Invoke();

        private void HandleFuseCompleted()
        {
            completedCount++;
            OnAnyFuseCompleted?.Invoke();
            if (completedCount >= requiredCount && !allCompleteRaised)
            {
                allCompleteRaised = true;
                OnAllComplete?.Invoke();
            }
        }
    }
}
