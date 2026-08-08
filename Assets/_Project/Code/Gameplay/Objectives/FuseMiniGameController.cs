using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Gameplay.Objectives
{
    /// <summary>
    /// Generic 4-button memory-sequence minigame runner (Fuse Memory Minigame prompt, sections 7-14).
    /// Deliberately knows nothing about FuseBox, ObjectiveManager, MonsterBrain, or GameManager — it
    /// only exposes Begin(...) with plain data + callbacks, so any future caller (not just fuses) could
    /// reuse it (prompt section 4/45: no hard-reference from the fuse system down into gameplay
    /// systems). A single shared instance lives in the scene; FuseBox.BeginInteract() is what actually
    /// calls Begin() and translates the outcome into fuse-specific state.
    ///
    /// State machine (prompt section 32, enum+coroutine rather than class-per-state — these states are
    /// almost entirely timers/waiting, not rich individual behaviors like the Monster/GameManager
    /// FSMs, so a class-per-state split would just be ceremony without payoff — see chat):
    /// Idle -> ShowingSequence -> WaitingForInput -> (Correct -> next input | Wrong -> Penalty -> new
    /// sequence -> ShowingSequence) -> RoundComplete -> (more rounds -> ShowingSequence | FuseCompleted).
    /// ESC at any point during Showing/WaitingForInput/Penalty -> Cancelled -> Idle.
    /// </summary>
    public class FuseMiniGameController : MonoBehaviour
    {
        private enum MiniGameState { Idle, ShowingSequence, WaitingForInput, Penalty, RoundComplete, FuseCompleted, Cancelled }

        private const int SequenceLength = 4; // prompt section 9: fixed, not configurable — "tam olarak 4 input"
        private const int ButtonCount = 4;

        [Header("UI References — assigned by the scene composition root")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup darkenOverlay;
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private Text progressText;
        [SerializeField] private Text statusText;
        [SerializeField] private Button[] buttons = new Button[ButtonCount];
        [SerializeField] private Image[] buttonImages = new Image[ButtonCount];

        [Header("Colors — prompt section 7: fixed roles, configurable values")]
        [SerializeField] private Color[] buttonColors =
        {
            new Color(0.83f, 0.18f, 0.18f), // Red
            new Color(0.10f, 0.46f, 0.82f), // Blue
            new Color(0.22f, 0.56f, 0.24f), // Green
            new Color(0.98f, 0.75f, 0.18f), // Yellow
        };
        [SerializeField, Tooltip("Multiplier applied to a button's base color while highlighted/pressed (>1 brightens). Raised from 1.6 (see chat — against the idle/dim state, 1.6 didn't read as a clear enough on/off contrast).")]
        private float highlightBrightness = 2.2f;
        [SerializeField, Tooltip("Multiplier applied to a button's base color while idle/resting — dimmer than the highlight so the sequence pulse actually reads as lighting *up*, not just changing shade (prompt section 10's own example: dim -> highlight -> dim).")]
        private float dimBrightness = 0.45f;
        [SerializeField] private Color wrongColor = new Color(0.9f, 0.1f, 0.1f);
        [SerializeField] private Color darkenOverlayColor = new Color(0f, 0f, 0f, 0.7f);

        [Header("Timing — prompt section 11, all Inspector-configurable, no magic numbers")]
        [SerializeField] private float sequenceHighlightDuration = 0.5f;
        [SerializeField] private float sequenceGapDuration = 0.25f;
        [SerializeField] private float buttonPressFeedbackDuration = 0.18f;
        [SerializeField] private float penaltyDuration = 3f; // prompt section 11: DEFAULT = 3 seconds
        [SerializeField] private float fadeDuration = 0.25f;
        [SerializeField] private float roundCompleteFeedbackDuration = 0.5f;
        [SerializeField] private float fuseCompleteFeedbackDuration = 0.9f;
        [SerializeField, Tooltip("Scale multiplier applied during a highlight/press pulse. Raised from 1.15 (see chat — too subtle to read as a deliberate pulse at that scale).")]
        private float pulseScale = 1.35f;

        public bool IsActive { get; private set; }

        /// <summary>Current phase, for debug display (prompt section 40) — not consumed by any gameplay logic itself.</summary>
        public string DebugStateName => state.ToString();

        private MiniGameState state = MiniGameState.Idle;
        private int? pendingButtonPress;
        private bool cancelRequested;
        private readonly int[] sequenceBuffer = new int[SequenceLength];
        private readonly Vector3[] buttonBaseScale = new Vector3[ButtonCount];

        private void Awake()
        {
            // Wired exactly once here, never per-Begin() call — duplicate-listener safety (prompt
            // section 35): Begin() can be called many times across the scene's lifetime (once per
            // fuse interaction), but OnClick subscriptions must not stack.
            for (int i = 0; i < ButtonCount; i++)
            {
                int index = i; // capture by value for the closure
                if (buttons[i] != null) buttons[i].onClick.AddListener(() => OnButtonClicked(index));
                if (buttons[i] != null) buttonBaseScale[i] = buttons[i].transform.localScale;
            }

            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void Update()
        {
            if (!IsActive) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) cancelRequested = true;
        }

        /// <summary>
        /// External forced cancel — same graceful shutdown path as an ESC press (fade out, restore
        /// cursor, invoke onCancelled), just triggered by something other than the Escape key. Added
        /// for the case found live via Unity MCP testing (see chat): the monster killing the player
        /// mid-minigame left the panel open, cursor unlocked, and the coroutine waiting forever for
        /// input that would never come, fighting visually with the death sequence (prompt section 27
        /// explicitly warns against exactly this). The caller (SceneWiring, on GameManager entering
        /// DeathState) is responsible for deciding *when* to call this — this class still knows nothing
        /// about GameManager or death itself.
        /// </summary>
        public void ForceCancel()
        {
            if (!IsActive) return;
            cancelRequested = true;
        }

        /// <summary>
        /// Starts the minigame for `roundsRequired` total rounds, already `roundsAlreadyCompleted` of
        /// which are done (so a re-entered fuse resumes instead of restarting fully — prompt section
        /// 15). `label` is just a display string (e.g. the FuseBox's name) — this class never touches
        /// the caller's type. Exactly one of onFullyComplete/onCancelled fires, exactly once, when the
        /// minigame ends. No-op (logs a warning) if already active — enforces "only one fuse
        /// interaction at a time" from the caller's side too, though FuseBox already guards this.
        /// </summary>
        public void Begin(int roundsRequired, int roundsAlreadyCompleted, string label,
            Action onRoundComplete, Action onFullyComplete, Action onCancelled)
        {
            if (IsActive)
            {
                Debug.LogWarning($"[FuseMiniGameController] Begin() called while already active — ignoring (label={label}).");
                return;
            }

            if (ValidateConfiguration() == false)
            {
                Debug.LogError("[FuseMiniGameController] Missing required references (buttons/panel) — cannot start. See Inspector.");
                onCancelled?.Invoke();
                return;
            }

            IsActive = true;
            StartCoroutine(RunFuse(Mathf.Max(1, roundsRequired), Mathf.Max(0, roundsAlreadyCompleted),
                label, onRoundComplete, onFullyComplete, onCancelled));
        }

        private bool ValidateConfiguration()
        {
            if (panelRoot == null) return false;
            if (buttons == null || buttons.Length != ButtonCount) return false;
            if (buttonImages == null || buttonImages.Length != ButtonCount) return false;
            if (buttonColors == null || buttonColors.Length != ButtonCount) return false;

            for (int i = 0; i < ButtonCount; i++)
            {
                if (buttons[i] == null || buttonImages[i] == null) return false;

                // Duplicate reference check (prompt section 31) — the same Button/Image assigned to
                // two slots would make two "different" colored buttons act as one, silently breaking
                // the memory game.
                for (int j = i + 1; j < ButtonCount; j++)
                {
                    if (buttons[i] == buttons[j] || buttonImages[i] == buttonImages[j]) return false;
                }
            }
            return true;
        }

        private IEnumerator RunFuse(int roundsRequired, int roundsCompleted, string label,
            Action onRoundComplete, Action onFullyComplete, Action onCancelled)
        {
            cancelRequested = false;
            yield return OpenPanel();

            bool cancelled = false;
            int round = roundsCompleted;

            while (round < roundsRequired)
            {
                state = MiniGameState.ShowingSequence;
                SetProgressText(label, round + 1, roundsRequired);
                GenerateSequence();

                yield return ShowSequence();
                if (cancelRequested) { cancelled = true; break; }

                state = MiniGameState.WaitingForInput;
                bool roundWrong = false;
                yield return WaitForPlayerInput(result => roundWrong = result);
                if (cancelRequested) { cancelled = true; break; }

                if (roundWrong)
                {
                    state = MiniGameState.Penalty;
                    yield return PenaltyRoutine();
                    if (cancelRequested) { cancelled = true; break; }
                    continue; // same round, fresh sequence — completed rounds untouched
                }

                round++;
                state = MiniGameState.RoundComplete;
                onRoundComplete?.Invoke();
                yield return RoundCompleteFeedback();
            }

            if (cancelled)
            {
                state = MiniGameState.Cancelled;
                yield return ClosePanel();
                IsActive = false;
                state = MiniGameState.Idle;
                onCancelled?.Invoke();
                yield break;
            }

            state = MiniGameState.FuseCompleted;
            yield return FuseCompleteFeedback();
            yield return ClosePanel();
            IsActive = false;
            state = MiniGameState.Idle;
            onFullyComplete?.Invoke();
        }

        private void GenerateSequence()
        {
            // Unity Random, per prompt section 9's own fallback ("Aksi halde Unity Random
            // kullanılabilir") — the project's seeded-generation system is for procedural map layout,
            // not minigame RNG, so reusing it here would be a category mismatch, not a genuine reuse.
            for (int i = 0; i < SequenceLength; i++) sequenceBuffer[i] = UnityEngine.Random.Range(0, ButtonCount);
        }

        private IEnumerator ShowSequence()
        {
            SetButtonsInteractable(false);
            SetStatusText(string.Empty);

            for (int i = 0; i < SequenceLength; i++)
            {
                if (cancelRequested) yield break;
                int index = sequenceBuffer[i];
                yield return PulseButton(index, sequenceHighlightDuration, buttonColors[index] * highlightBrightness);
                yield return WaitSecondsOrCancel(sequenceGapDuration);
            }
        }

        private IEnumerator WaitForPlayerInput(Action<bool> reportWrong)
        {
            SetButtonsInteractable(true);
            pendingButtonPress = null;
            int inputIndex = 0;

            while (inputIndex < SequenceLength)
            {
                if (cancelRequested) { SetButtonsInteractable(false); yield break; }

                if (pendingButtonPress == null) { yield return null; continue; }

                int pressed = pendingButtonPress.Value;
                pendingButtonPress = null;

                if (pressed == sequenceBuffer[inputIndex])
                {
                    yield return PulseButton(pressed, buttonPressFeedbackDuration, buttonColors[pressed] * highlightBrightness);
                    inputIndex++;
                }
                else
                {
                    SetButtonsInteractable(false);
                    yield return FlashButton(pressed, wrongColor, buttonPressFeedbackDuration);
                    reportWrong(true);
                    yield break;
                }
            }

            SetButtonsInteractable(false);
            reportWrong(false);
        }

        private IEnumerator PenaltyRoutine()
        {
            SetStatusText("HATA — SIFIRLANIYOR");
            float remaining = penaltyDuration;
            while (remaining > 0f)
            {
                if (cancelRequested) yield break;
                SetStatusText($"HATA — {Mathf.CeilToInt(remaining)}");
                yield return WaitSecondsOrCancel(Mathf.Min(1f, remaining));
                remaining -= 1f;
            }
            SetStatusText(string.Empty);
        }

        private IEnumerator RoundCompleteFeedback()
        {
            SetStatusText("ROUND TAMAM");
            yield return WaitSecondsOrCancel(roundCompleteFeedbackDuration);
            SetStatusText(string.Empty);
        }

        private IEnumerator FuseCompleteFeedback()
        {
            SetStatusText("FUSE AKTİF");
            for (int i = 0; i < ButtonCount; i++) SetButtonColor(i, buttonColors[i] * highlightBrightness);
            yield return WaitSecondsOrCancel(fuseCompleteFeedbackDuration);
            for (int i = 0; i < ButtonCount; i++) SetButtonColor(i, DimColor(i));
        }

        private IEnumerator OpenPanel()
        {
            panelRoot.SetActive(true);
            for (int i = 0; i < ButtonCount; i++) SetButtonColor(i, DimColor(i));
            SetStatusText(string.Empty);

            // Unlock the cursor so the player can click buttons — TestPlayerController's Awake()
            // locks it for normal gameplay; this is the one place outside that controller that touches
            // Cursor state, restored symmetrically in ClosePanel().
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (darkenOverlay != null) yield return FadeCanvasGroup(darkenOverlay, 0f, darkenOverlayColor.a, fadeDuration);
            if (panelGroup != null) yield return FadeCanvasGroup(panelGroup, 0f, 1f, fadeDuration);
        }

        private IEnumerator ClosePanel()
        {
            SetButtonsInteractable(false);
            if (panelGroup != null) yield return FadeCanvasGroup(panelGroup, panelGroup.alpha, 0f, fadeDuration);
            if (darkenOverlay != null) yield return FadeCanvasGroup(darkenOverlay, darkenOverlay.alpha, 0f, fadeDuration);

            panelRoot.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (duration <= 0f) { group.alpha = to; yield break; }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            group.alpha = to;
        }

        /// <summary>Resting/idle appearance — dimmer than the true button color, so a pulse reads as visibly "lighting up" rather than just a slightly different shade.</summary>
        private Color DimColor(int index) => buttonColors[index] * dimBrightness;

        private IEnumerator PulseButton(int index, float duration, Color highlightColor)
        {
            yield return LerpButtonVisual(index, DimColor(index), highlightColor, buttonBaseScale[index], buttonBaseScale[index] * pulseScale, duration * 0.5f);
            if (cancelRequested) yield break;
            yield return LerpButtonVisual(index, highlightColor, DimColor(index), buttonBaseScale[index] * pulseScale, buttonBaseScale[index], duration * 0.5f);
        }

        private IEnumerator FlashButton(int index, Color flashColor, float duration)
        {
            yield return LerpButtonVisual(index, DimColor(index), flashColor, buttonBaseScale[index], buttonBaseScale[index] * pulseScale, duration * 0.5f);
            if (cancelRequested) yield break;
            yield return LerpButtonVisual(index, flashColor, DimColor(index), buttonBaseScale[index] * pulseScale, buttonBaseScale[index], duration * 0.5f);
        }

        private IEnumerator LerpButtonVisual(int index, Color fromColor, Color toColor, Vector3 fromScale, Vector3 toScale, float duration)
        {
            if (duration <= 0f)
            {
                SetButtonColor(index, toColor);
                if (buttons[index] != null) buttons[index].transform.localScale = toScale;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (cancelRequested) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                SetButtonColor(index, Color.Lerp(fromColor, toColor, t));
                if (buttons[index] != null) buttons[index].transform.localScale = Vector3.Lerp(fromScale, toScale, t);
                yield return null;
            }
            SetButtonColor(index, toColor);
            if (buttons[index] != null) buttons[index].transform.localScale = toScale;
        }

        private IEnumerator WaitSecondsOrCancel(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (cancelRequested) yield break;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void OnButtonClicked(int index) => pendingButtonPress = index;

        private void SetButtonsInteractable(bool interactable)
        {
            for (int i = 0; i < ButtonCount; i++)
            {
                if (buttons[i] != null) buttons[i].interactable = interactable;
            }
        }

        private void SetButtonColor(int index, Color color)
        {
            if (buttonImages[index] != null) buttonImages[index].color = color;
        }

        private void SetProgressText(string label, int currentRound, int totalRounds)
        {
            if (progressText != null) progressText.text = $"{label}\nRound {currentRound} / {totalRounds}";
        }

        private void SetStatusText(string text)
        {
            if (statusText != null) statusText.text = text;
        }
    }
}
