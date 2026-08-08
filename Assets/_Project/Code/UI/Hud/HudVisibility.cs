using System;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Fades the HUD according to game-flow state without deactivating its hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    public class HudVisibility : MonoBehaviour
    {
        [SerializeField] private GameFlowController gameFlow;
        [SerializeField] private CanvasGroup hudGroup;
        [SerializeField] private float fadeSeconds = 0.15f;

        [SerializeField] private string[] visibleStates =
        {
            nameof(ExplorationState),
            nameof(FinalChaseState),
        };

        private float targetAlpha;

        public void Bind(GameFlowController flow, CanvasGroup group)
        {
            gameFlow = flow;
            hudGroup = group;
        }

        private void Awake()
        {
            if (gameFlow == null || hudGroup == null)
            {
                Debug.LogWarning(
                    $"[HUD] {name}: gameFlow/hudGroup not assigned — HUD visibility control disabled.",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (gameFlow == null || hudGroup == null) return;

            gameFlow.OnStateChanged += HandleStateChanged;
            HandleStateChanged(gameFlow.CurrentStateName);
            hudGroup.alpha = targetAlpha;
        }

        private void OnDisable()
        {
            if (gameFlow == null) return;
            gameFlow.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(string stateName)
        {
            targetAlpha = Array.IndexOf(visibleStates, stateName) >= 0 ? 1f : 0f;
        }

        private void Update()
        {
            hudGroup.alpha = fadeSeconds <= 0f
                ? targetAlpha
                : Mathf.MoveTowards(hudGroup.alpha, targetAlpha, Time.deltaTime / fadeSeconds);
        }
    }
}
