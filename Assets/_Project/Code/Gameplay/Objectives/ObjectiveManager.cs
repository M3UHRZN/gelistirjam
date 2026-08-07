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

        public void SetRequiredCount(int count) => requiredCount = count;

        public void Register(FuseBox fuseBox)
        {
            if (registered.Contains(fuseBox)) return;
            registered.Add(fuseBox);
            fuseBox.OnCompleted += HandleFuseCompleted;
        }

        public void Unregister(FuseBox fuseBox)
        {
            if (!registered.Remove(fuseBox)) return;
            fuseBox.OnCompleted -= HandleFuseCompleted;
        }

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
