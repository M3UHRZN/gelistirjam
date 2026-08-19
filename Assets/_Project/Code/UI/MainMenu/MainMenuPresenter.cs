using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// Drives the main menu terminal: row selection/navigation, the OPTIONS page swap, and the live
    /// clock readout. Reads Keyboard/Mouse directly via UnityEngine.InputSystem — the same
    /// convention FuseTabletPresenter already uses (see that file's comment) rather than an
    /// abstracted input service. Does not know what any individual row *does* when confirmed — see
    /// MainMenuRow's onConfirmed UnityEvent for that.
    /// </summary>
    public class MainMenuPresenter : MonoBehaviour
    {
        [Header("Rows (menu page)")]
        [SerializeField] private MainMenuRow[] rows;

        [Header("Pages")]
        [SerializeField] private GameObject menuPage;
        [SerializeField] private GameObject optionsPage;

        [Header("Clock")]
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private float clockRefreshSeconds = 1f;

        [Header("Boot")]
        [SerializeField] private CanvasGroup screenGroup;

        private int selectedIndex;
        private bool onOptionsPage;

        private void OnEnable()
        {
            BindPointerEvents();

            if (screenGroup != null) StartCoroutine(BootFlicker());

            if (rows == null || rows.Length == 0) return;

            selectedIndex = ComputeNextIndex(-1, 1, BuildInteractableMask());
            RefreshSelection();
            InvokeRepeating(nameof(RefreshClock), 0f, clockRefreshSeconds);
        }

        /// <summary>Short CRT power-on flicker (~0.4s) played on the tablet screen at scene start,
        /// before the menu settles fully visible. See the design spec's boot-flicker requirement.</summary>
        private IEnumerator BootFlicker()
        {
            float[] alphaSteps = { 0.2f, 0.8f, 0.1f, 1f };
            float stepDuration = 0.4f / alphaSteps.Length;
            foreach (float alpha in alphaSteps)
            {
                screenGroup.alpha = alpha;
                yield return new WaitForSecondsRealtime(stepDuration);
            }
            screenGroup.alpha = 1f;
        }

        private void OnDisable()
        {
            UnbindPointerEvents();
            CancelInvoke(nameof(RefreshClock));
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (onOptionsPage)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) CloseOptions();
                return;
            }

            if (rows == null || rows.Length == 0) return;

            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            {
                MoveSelection(-1);
            }
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            {
                MoveSelection(1);
            }
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                rows[selectedIndex].Confirm();
            }
        }

        /// <summary>Hovering a row selects it, same as pressing the arrow keys to reach it.</summary>
        public void SelectRow(int index)
        {
            if (rows == null || index < 0 || index >= rows.Length || index == selectedIndex) return;
            if (!rows[index].Interactable) return;

            rows[selectedIndex].Deselect();
            selectedIndex = index;
            RefreshSelection();
        }

        private void BindPointerEvents()
        {
            if (rows == null) return;

            foreach (MainMenuRow row in rows)
            {
                if (row == null) continue;
                row.PointerEntered -= OnRowPointerEntered;
                row.PointerEntered += OnRowPointerEntered;
            }
        }

        private void UnbindPointerEvents()
        {
            if (rows == null) return;

            foreach (MainMenuRow row in rows)
            {
                if (row != null) row.PointerEntered -= OnRowPointerEntered;
            }
        }

        private void OnRowPointerEntered(MainMenuRow row)
        {
            SelectRow(Array.IndexOf(rows, row));
        }

        /// <summary>Wired to the OPTIONS row's onConfirmed in the Inspector.</summary>
        public void OpenOptions()
        {
            onOptionsPage = true;
            if (menuPage != null) menuPage.SetActive(false);
            if (optionsPage != null) optionsPage.SetActive(true);
        }

        /// <summary>Wired to the options page's own "back"/Escape affordance, and called on Escape.</summary>
        public void CloseOptions()
        {
            onOptionsPage = false;
            if (optionsPage != null) optionsPage.SetActive(false);
            if (menuPage != null) menuPage.SetActive(true);
        }

        /// <summary>
        /// Computes the next selected index, skipping non-interactable rows and wrapping at the
        /// ends. Pure function (no MonoBehaviour/scene access) so it's unit-testable in isolation —
        /// see MainMenuPresenterTests.
        /// </summary>
        internal static int ComputeNextIndex(int current, int delta, bool[] interactableMask)
        {
            if (interactableMask == null || interactableMask.Length == 0) return current;

            int count = interactableMask.Length;
            int next = current;
            for (int i = 0; i < count; i++)
            {
                next = ((next + delta) % count + count) % count;
                if (interactableMask[next]) return next;
            }

            return current;
        }

        private void MoveSelection(int delta)
        {
            int next = ComputeNextIndex(selectedIndex, delta, BuildInteractableMask());
            if (next == selectedIndex) return;

            rows[selectedIndex].Deselect();
            selectedIndex = next;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (i == selectedIndex) rows[i].Select();
            }
        }

        private bool[] BuildInteractableMask()
        {
            var mask = new bool[rows.Length];
            for (int i = 0; i < rows.Length; i++) mask[i] = rows[i].Interactable;
            return mask;
        }

        private void RefreshClock()
        {
            if (clockText != null) clockText.text = DateTime.Now.ToString("HH:mm:ss");
        }
    }
}
