using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// One row of the main menu terminal (e.g. "NEW GAME"). Owns its own selection/confirm
    /// animation and fires its own UnityEvent when confirmed — MainMenuPresenter only tells a row
    /// when to select/deselect/confirm, it never knows what a specific row *does*. What a row does
    /// is wired in the Inspector (onConfirmed -> NewGameLoader.Load(), QuitGameAction.Quit(), etc.),
    /// so adding/reordering/removing rows never touches MainMenuPresenter's code.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuRow : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [Header("Wiring")]
        [SerializeField] private Image highlightBorder;
        [SerializeField] private TMP_Text cursorLabel;
        [SerializeField] private bool interactable = true;
        [SerializeField] private UnityEvent onConfirmed;

        [Header("Audio")]
        [Tooltip("Shared AudioSource the whole menu plays through (one per tablet, assigned by " +
                 "MainMenuSceneBuilder — see Task 6). Left null-safe: if either this or a clip " +
                 "below is unassigned, that sound simply doesn't play, no error.")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Played on Select() — i.e. arrow-key navigation landing on this row, or mouse " +
                 "hover once hover wiring exists (see Task 7's noted gap). Left empty here; the " +
                 "user assigns the actual clip in the Inspector once they have one.")]
        [SerializeField] private AudioClip selectSound;
        [Tooltip("Played on Confirm() — Enter or click. Left empty here for the same reason.")]
        [SerializeField] private AudioClip confirmSound;

        [Header("Animation")]
        [SerializeField] private float selectDuration = 0.15f;
        [SerializeField] private float confirmPulseDuration = 0.35f;
        [SerializeField] private Color idleBorderColor = new Color(0.086f, 0.27f, 0.157f, 1f);
        [SerializeField] private Color selectedBorderColor = new Color(0.31f, 1f, 0.541f, 1f);
        [SerializeField] private Color confirmFlashColor = new Color(0.31f, 1f, 0.541f, 0.9f);

        /// <summary>False for rows like CONTINUE that render but never respond to selection/confirm.</summary>
        public bool Interactable => interactable;

        internal event Action<MainMenuRow> PointerEntered;

#if UNITY_EDITOR
        /// <summary>Editor-only escape hatch so MainMenuSceneBuilder (Task 6) can wire this row's
        /// event via UnityEventTools, which needs a live UnityEvent reference, not a
        /// SerializedProperty. Nothing at Play-time should ever read this — there is no public
        /// runtime API for "trigger another row's confirm logic" by design.</summary>
        internal UnityEvent EditorOnConfirmedEvent => onConfirmed;
#endif

        private Tween activeSequence;

        private void Awake()
        {
            if (cursorLabel != null) cursorLabel.gameObject.SetActive(false);
            if (highlightBorder != null) highlightBorder.color = idleBorderColor;
        }

        public void Select()
        {
            if (!interactable) return;

            activeSequence?.Kill();
            if (cursorLabel != null) cursorLabel.gameObject.SetActive(true);
            if (highlightBorder != null)
            {
                activeSequence = DOTween.To(() => highlightBorder.color, c => highlightBorder.color = c,
                    selectedBorderColor, selectDuration).SetUpdate(true);
            }

            PlayOneShot(selectSound);
        }

        public void Deselect()
        {
            if (!interactable) return;

            activeSequence?.Kill();
            if (cursorLabel != null) cursorLabel.gameObject.SetActive(false);
            if (highlightBorder != null)
            {
                activeSequence = DOTween.To(() => highlightBorder.color, c => highlightBorder.color = c,
                    idleBorderColor, selectDuration).SetUpdate(true);
            }
        }

        /// <summary>Plays the confirm pulse, then fires <see cref="onConfirmed"/>.</summary>
        public void Confirm()
        {
            if (!interactable) return;

            PlayOneShot(confirmSound);

            if (highlightBorder == null)
            {
                onConfirmed?.Invoke();
                return;
            }

            activeSequence?.Kill();
            Color returnTo = selectedBorderColor;
            activeSequence = DOTween.Sequence()
                .Append(DOTween.To(() => highlightBorder.color, c => highlightBorder.color = c,
                    confirmFlashColor, confirmPulseDuration * 0.3f))
                .Append(DOTween.To(() => highlightBorder.color, c => highlightBorder.color = c,
                    returnTo, confirmPulseDuration * 0.7f))
                .SetUpdate(true)
                .OnComplete(() => onConfirmed?.Invoke());
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (interactable) PointerEntered?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            Confirm();
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }

        private void OnDisable() => activeSequence?.Kill();
    }
}
