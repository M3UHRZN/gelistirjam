# Main Menu Terminal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a diegetic main menu — a new `Assets/Scenes/MainMenu.unity` scene where the camera looks at a tablet prop showing a retro green-phosphor CRT terminal (NEW GAME / CONTINUE / OPTIONS / QUIT), navigated with keyboard or mouse, matching the two reference images and the brainstorming-approved "Retro TUI with animation" design.

**Architecture:** Small, single-purpose MonoBehaviours (`MainMenuRow` owns one row's visuals/animation and fires its own `UnityEvent` on confirm; `MainMenuPresenter` owns only navigation/selection state and input; `NewGameLoader`/`QuitGameAction`/`MasterVolumeController` are the tiny action targets rows' events call) plus one Editor-only builder tool (`MainMenuSceneBuilder`, `[MenuItem]`-driven, run once inside Unity) that constructs the actual scene/prefab hierarchy in code — following this codebase's own established convention (`MainSceneFuseSetup.cs`, `HudSceneBuilder.cs`) for building Unity content programmatically rather than hand-authoring `.unity`/`.prefab` YAML from scratch, because no Unity Editor/MCP session is attached in this execution environment and a from-scratch scene/Canvas hierarchy is far riskier to hand-author blind than the small surgical prefab edits done in the recent lamp-lighting-fix work. Visuals reuse the project's existing CRT/terminal building blocks (`TerminalPanelGraphic`, `HudTerminalFactory`'s Cascadia Mono SDF font and phosphor-green palette) rather than inventing a parallel visual system.

**Tech Stack:** Unity 6000.3.13f1, URP, TextMeshPro, `UnityEngine.InputSystem`, DOTween (already a project dependency, used the same way `FuseTabletPresenter` already uses it).

## Global Constraints

- No Unity Editor/MCP session is attached in this execution environment. Every step is a plain-text file edit (new `.cs` files, one hand-edited prefab duplicate). The Editor-only builder script (Task 6) is verified structurally (it must compile, per the project's own conventions) — its actual scene-construction behavior can only be exercised by the user running it inside Unity, which is why Task 7 is an explicit manual checklist, not something this plan can close out itself.
- **Do not run any `git` command** (no `git add`, `git commit`, `git push`, branch creation). The user handles all git/GitHub operations themselves. Every task in this plan ends with "leave the changes unstaged" instead of a commit step — this deviates from this plan-writing skill's normal template (which always ends a task with a commit), by explicit instruction from the user for this plan.
- Do not touch `FuseTabletPresenter.cs`, `Assets/model/fusetablet.prefab`, or anything under the fuse minigame system.
- CONTINUE stays disabled (no save system exists in this project) — do not wire it to anything.
- OPTIONS is minimal: a static controls readout + one Master Volume slider via `AudioListener.volume`. No audio mixer, no rebindable controls.
- Menu row layout, the clock readout, and the options page's contents must be real scene-authored `GameObject`s/`RectTransform`s the user can freely reposition afterward in the Hierarchy/Inspector — `MainMenuPresenter` and its sibling components reference them via `[SerializeField]`, they never construct or position them from runtime code. (The Editor builder tool in Task 6 *does* construct them — that's a one-time authoring step, equivalent to a human dragging objects into the Hierarchy by hand. The distinction is: nothing at Play-time computes a row's position.)
- Real control-scheme bindings for the OPTIONS readout, taken verbatim from `Assets/InputSystem_Actions.inputactions`' `Player` action map: Move = WASD / Arrow Keys, Look = Mouse, Sprint = Left Shift, Interact = E, Flashlight = F, Jump = Space.

---

### Task 1: Duplicate the tablet prefab

**Files:**
- Create: `Assets/prefabs/MainMenuTablet.prefab`
- Create: `Assets/prefabs/MainMenuTablet.prefab.meta`

**Interfaces:**
- Consumes: `Assets/prefabs/tablet.prefab` (read-only source; do not modify it).
- Produces: `MainMenuTablet.prefab`, a standalone duplicate (own guid, own fileIDs left as-is since they only need to be unique *within* this file, which they already are, having been copied from a single-object prefab) that Task 6 adds a `ScreenAnchor` + `Canvas` child to. Its root GameObject transform (`m_LocalPosition`, `m_LocalScale`, `m_LocalRotation`) and the mesh/material/collider are otherwise unchanged from the source.

- [ ] **Step 1: Read the source prefab**

Run: `cat -n /Users/lee/Desktop/gelistirjam/Assets/prefabs/tablet.prefab`

Confirm it matches: single GameObject named `tablet` (fileID `5108628965234583825`) with a Transform (`m_LocalScale: {x: 168.06, y: 168.06, z: 168.06}`), MeshFilter (mesh guid `8f026d0c2bb09984fa782c8d95e53dac`), MeshRenderer, and BoxCollider (`m_Size: {x: 0.004, y: 0.0031132584, z: 0.0005927112}`).

- [ ] **Step 2: Copy the prefab file and rename the GameObject**

```bash
cp /Users/lee/Desktop/gelistirjam/Assets/prefabs/tablet.prefab /Users/lee/Desktop/gelistirjam/Assets/prefabs/MainMenuTablet.prefab
```

In the new file `Assets/prefabs/MainMenuTablet.prefab`, change:

```yaml
  m_Name: tablet
```

to:

```yaml
  m_Name: MainMenuTablet
```

(This is the only line that needs to change — the fileIDs inside this file only need to be unique within the file, and a straight copy already satisfies that. The mesh/material references keep pointing at the same source FBX-derived asset (guid `8f026d0c2bb09984fa782c8d95e53dac`) as the original tablet, which is intentional — both prefabs show the same physical tablet model, just as separate, independently-extensible prefab assets.)

- [ ] **Step 3: Create the new prefab's `.meta` file with a fresh, unique guid**

Check the candidate guid isn't already used anywhere in the project:

```bash
grep -rl "e7c4a29b6f183d5e9a0c2b8f4d6e1a73" /Users/lee/Desktop/gelistirjam/Assets 2>/dev/null
```

Expected: no output (guid unused). If it prints any file, pick a different guid and re-check before proceeding.

Write `Assets/prefabs/MainMenuTablet.prefab.meta`:

```yaml
fileFormatVersion: 2
guid: e7c4a29b6f183d5e9a0c2b8f4d6e1a73
PrefabImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

- [ ] **Step 4: Verify**

```bash
python3 - <<'EOF'
prefab = open("/Users/lee/Desktop/gelistirjam/Assets/prefabs/MainMenuTablet.prefab").read()
assert "m_Name: MainMenuTablet" in prefab
assert "m_Name: tablet" not in prefab
assert "guid: 8f026d0c2bb09984fa782c8d95e53dac" in prefab  # still points at the shared tablet mesh/material

meta = open("/Users/lee/Desktop/gelistirjam/Assets/prefabs/MainMenuTablet.prefab.meta").read()
assert "guid: e7c4a29b6f183d5e9a0c2b8f4d6e1a73" in meta

original = open("/Users/lee/Desktop/gelistirjam/Assets/prefabs/tablet.prefab").read()
assert "m_Name: tablet" in original  # source untouched
print("OK")
EOF
```

Expected: `OK`.

Leave the changes unstaged — do not run any `git` command (see Global Constraints).

---

### Task 2: `MainMenuRow` — one menu row's visuals and confirm event

**Files:**
- Create: `Assets/_Project/Code/UI/MainMenu/MainMenuRow.cs`

**Interfaces:**
- Consumes: nothing from other tasks (self-contained; the Editor builder in Task 6 assigns its serialized fields, and other rows' `onConfirmed` events are wired in the Inspector to Task 4's action components — none of that is a compile-time dependency).
- Produces: `public bool Interactable { get; }`, `public void Select()`, `public void Deselect()`, `public void Confirm()` — `MainMenuPresenter` (Task 3) calls these three methods by exact name and reads `Interactable`. `Confirm()` takes no parameters and no callback; it fires the row's own serialized `onConfirmed` `UnityEvent` once its animation finishes (or immediately, if the row isn't interactable — though `Interactable` should already prevent callers from calling `Confirm()` on such a row). Also produces serialized `audioSource`/`selectSound`/`confirmSound` fields — Task 6's builder assigns one shared `AudioSource` per tablet to every row's `audioSource` field, but leaves `selectSound`/`confirmSound` unassigned (`null`) on every row: the user is sourcing and assigning the actual navigate/confirm SFX clips themselves afterward, in the Inspector, per row (so e.g. CONTINUE, which never plays a sound since `Select`/`Confirm` both early-return on `!interactable`, doesn't need one at all). Both `PlayOneShot` calls are already null-safe against a missing source or clip — nothing needs to change in code once the user drags clips in.

- [ ] **Step 1: Write the component**

```csharp
using DG.Tweening;
using TMPro;
using UnityEngine;
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
    public class MainMenuRow : MonoBehaviour
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

#if UNITY_EDITOR
        /// <summary>Editor-only escape hatch so MainMenuSceneBuilder (Task 6) can wire this row's
        /// event via UnityEventTools, which needs a live UnityEvent reference, not a
        /// SerializedProperty. Nothing at Play-time should ever read this — there is no public
        /// runtime API for "trigger another row's confirm logic" by design.</summary>
        internal UnityEvent EditorOnConfirmedEvent => onConfirmed;
#endif

        private Sequence activeSequence;

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
                activeSequence = highlightBorder.DOColor(selectedBorderColor, selectDuration)
                    .SetUpdate(true);
            }

            PlayOneShot(selectSound);
        }

        public void Deselect()
        {
            activeSequence?.Kill();
            if (cursorLabel != null) cursorLabel.gameObject.SetActive(false);
            if (highlightBorder != null)
            {
                activeSequence = highlightBorder.DOColor(idleBorderColor, selectDuration)
                    .SetUpdate(true);
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
                .Append(highlightBorder.DOColor(confirmFlashColor, confirmPulseDuration * 0.3f))
                .Append(highlightBorder.DOColor(returnTo, confirmPulseDuration * 0.7f))
                .SetUpdate(true)
                .OnComplete(() => onConfirmed?.Invoke());
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }

        private void OnDisable() => activeSequence?.Kill();
    }
}
```

- [ ] **Step 2: Verify it compiles conceptually (no Editor to actually compile in this session)**

Run: `grep -n "class MainMenuRow" /Users/lee/Desktop/gelistirjam/Assets/_Project/Code/UI/MainMenu/MainMenuRow.cs`

Expected: one match. Also confirm the file has no unmatched braces:

```bash
python3 -c "
s = open('/Users/lee/Desktop/gelistirjam/Assets/_Project/Code/UI/MainMenu/MainMenuRow.cs').read()
assert s.count('{') == s.count('}')
print('OK: braces balanced,', s.count('{'), 'pairs')
"
```

Leave the changes unstaged — do not run any `git` command.

---

### Task 3: `MainMenuPresenter` — navigation, input, options-page toggle, clock

**Files:**
- Create: `Assets/_Project/Code/UI/MainMenu/MainMenuPresenter.cs`
- Test: `Assets/_Project/Code/Tests/EditMode/MainMenuPresenterTests.cs`

**Interfaces:**
- Consumes: `MainMenuRow.Interactable` (bool property), `MainMenuRow.Select()`/`Deselect()`/`Confirm()` (Task 2, exact names as above).
- Produces: `internal static int ComputeNextIndex(int current, int delta, bool[] interactableMask)` — a pure, static, side-effect-free function, deliberately factored out so it's testable without a live Unity scene (`FuseTabletPresenterTests.cs` established the pattern in this codebase of testing the pure/clamping parts of a tablet presenter without needing a full scene — follow that). No other task calls `ComputeNextIndex` directly; it exists for the test in this task and for `MainMenuPresenter`'s own `MoveSelection`.

- [ ] **Step 1: Write the failing test first**

```csharp
using Game.UI.MainMenu;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class MainMenuPresenterTests
    {
        [Test]
        public void ComputeNextIndex_MovesToNextRow_WhenAllInteractable()
        {
            bool[] mask = { true, true, true, true };

            int next = MainMenuPresenter.ComputeNextIndex(0, 1, mask);

            Assert.AreEqual(1, next);
        }

        [Test]
        public void ComputeNextIndex_SkipsDisabledRow()
        {
            // Row 1 (CONTINUE) is disabled; moving down from row 0 should land on row 2, not row 1.
            bool[] mask = { true, false, true, true };

            int next = MainMenuPresenter.ComputeNextIndex(0, 1, mask);

            Assert.AreEqual(2, next);
        }

        [Test]
        public void ComputeNextIndex_WrapsFromLastToFirst()
        {
            bool[] mask = { true, true, true, true };

            int next = MainMenuPresenter.ComputeNextIndex(3, 1, mask);

            Assert.AreEqual(0, next);
        }

        [Test]
        public void ComputeNextIndex_WrapsBackwardPastDisabledRow()
        {
            // From row 2, moving up (-1) should skip disabled row 1 and land on row 0.
            bool[] mask = { true, false, true, true };

            int next = MainMenuPresenter.ComputeNextIndex(2, -1, mask);

            Assert.AreEqual(0, next);
        }

        [Test]
        public void ComputeNextIndex_ReturnsCurrent_WhenNoRowIsInteractable()
        {
            bool[] mask = { false, false, false };

            int next = MainMenuPresenter.ComputeNextIndex(1, 1, mask);

            Assert.AreEqual(1, next);
        }

        [Test]
        public void ComputeNextIndex_ReturnsCurrent_WhenMaskIsEmpty()
        {
            int next = MainMenuPresenter.ComputeNextIndex(0, 1, new bool[0]);

            Assert.AreEqual(0, next);
        }
    }
}
```

- [ ] **Step 2: Confirm the test file is new and would fail to compile against a not-yet-existing method**

Run: `grep -n "ComputeNextIndex" /Users/lee/Desktop/gelistirjam/Assets/_Project/Code/UI/MainMenu/MainMenuPresenter.cs 2>/dev/null || echo "MainMenuPresenter.cs does not exist yet — expected at this point"`

Expected: the "does not exist yet" message (this test can't actually be *run* in this session — no attached Editor/Test Runner — so this step is a structural sanity check that the method doesn't already exist by coincidence, not a real red-bar run).

- [ ] **Step 3: Write `MainMenuPresenter`**

```csharp
using System;
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

        private int selectedIndex;
        private bool onOptionsPage;

        private void OnEnable()
        {
            if (rows == null || rows.Length == 0) return;

            selectedIndex = ComputeNextIndex(-1, 1, BuildInteractableMask());
            RefreshSelection();
            InvokeRepeating(nameof(RefreshClock), 0f, clockRefreshSeconds);
        }

        private void OnDisable()
        {
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
```

Note: `RefreshSelection` only calls `Select()` on the newly-current row — it relies on `MoveSelection` having already called `Deselect()` on the previous row, and on `Awake()`'s idle-visual reset covering every row before the first `OnEnable()` selection. This mirrors exactly how `MoveSelection` is used above; do not call `RefreshSelection()` from anywhere that hasn't first deselected the previous row.

- [ ] **Step 4: Verify structurally**

```bash
python3 -c "
s = open('/Users/lee/Desktop/gelistirjam/Assets/_Project/Code/UI/MainMenu/MainMenuPresenter.cs').read()
assert s.count('{') == s.count('}')
assert 'internal static int ComputeNextIndex' in s
print('OK')
"
```

Expected: `OK`. (The EditMode test in Step 1 runs for real once the user opens this in Unity's Test Runner — flag that explicitly in your final report, do not claim it passed without having run it.)

Leave the changes unstaged — do not run any `git` command.

---

### Task 4: Row action components — `NewGameLoader`, `QuitGameAction`, `MasterVolumeController`

**Files:**
- Create: `Assets/_Project/Code/UI/MainMenu/NewGameLoader.cs`
- Create: `Assets/_Project/Code/UI/MainMenu/QuitGameAction.cs`
- Create: `Assets/_Project/Code/UI/MainMenu/MasterVolumeController.cs`

**Interfaces:**
- Consumes: nothing (each is a standalone component with one public method, meant to be wired to a `MainMenuRow.onConfirmed` `UnityEvent`, or in `MasterVolumeController`'s case, to a `UnityEngine.UI.Slider.onValueChanged` event — both are Inspector-time wiring done in Task 6, not a compile-time dependency).
- Produces: `NewGameLoader.Load()` (no args — the scene name is a serialized field, not hardcoded), `QuitGameAction.Quit()` (no args), `MasterVolumeController.SetVolume(float)` (matches `Slider.onValueChanged`'s `float` signature) and `MasterVolumeController.CurrentVolume` (float, reads `AudioListener.volume` — Task 6 uses this to initialize the slider's starting value).

- [ ] **Step 1: `NewGameLoader.cs`**

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI.MainMenu
{
    /// <summary>Wired to the NEW GAME row's onConfirmed. Scene name is a field, not hardcoded, so
    /// retargeting it never needs a code change.</summary>
    public class NewGameLoader : MonoBehaviour
    {
        [SerializeField] private string sceneName = "MainGame";

        public void Load() => SceneManager.LoadScene(sceneName);
    }
}
```

- [ ] **Step 2: `QuitGameAction.cs`**

```csharp
using UnityEngine;

namespace Game.UI.MainMenu
{
    /// <summary>Wired to the QUIT row's onConfirmed. Exits Play Mode in the Editor (Application.Quit
    /// is a no-op there), quits the built application otherwise.</summary>
    public class QuitGameAction : MonoBehaviour
    {
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
```

- [ ] **Step 3: `MasterVolumeController.cs`**

```csharp
using UnityEngine;

namespace Game.UI.MainMenu
{
    /// <summary>Wired to the OPTIONS page's Master Volume Slider.onValueChanged. Single source of
    /// truth for reading/writing AudioListener.volume from UI, since that's a static property a
    /// UnityEvent can't bind to directly.</summary>
    public class MasterVolumeController : MonoBehaviour
    {
        public float CurrentVolume => AudioListener.volume;

        public void SetVolume(float value) => AudioListener.volume = value;
    }
}
```

- [ ] **Step 4: Verify all three compile structurally**

```bash
for f in NewGameLoader QuitGameAction MasterVolumeController; do
python3 -c "
s = open('/Users/lee/Desktop/gelistirjam/Assets/_Project/Code/UI/MainMenu/$f.cs').read()
assert s.count('{') == s.count('}'), '$f: unbalanced braces'
print('$f: OK')
"
done
```

Leave the changes unstaged — do not run any `git` command.

---

### Task 5: Idle camera sway and ambient light flicker

**Files:**
- Create: `Assets/_Project/Code/UI/MainMenu/MainMenuCameraSway.cs`
- Create: `Assets/_Project/Code/UI/MainMenu/MainMenuAmbientFlicker.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: nothing consumed by other tasks — Task 6 attaches these to the menu camera and a light respectively, and leaves their serialized defaults as-is (already sane defaults per the spec's "subtle, not distracting" and "themed flavor, not a jump-scare" requirements).

- [ ] **Step 1: `MainMenuCameraSway.cs`**

```csharp
using UnityEngine;

namespace Game.UI.MainMenu
{
    /// <summary>Very small idle sway so the main menu shot doesn't read as a frozen screenshot.
    /// Deliberately subtle (spec: "without being distracting") — do not raise the defaults without
    /// checking it in the Editor first.</summary>
    public class MainMenuCameraSway : MonoBehaviour
    {
        [SerializeField, Range(0f, 5f)] private float maxYawDegrees = 1.5f;
        [SerializeField, Range(0f, 5f)] private float maxPitchDegrees = 1f;
        [SerializeField] private float speed = 0.15f;

        private Quaternion baseRotation;
        private float seed;

        private void Awake()
        {
            baseRotation = transform.localRotation;
            seed = Random.Range(0f, 1000f);
        }

        private void Update()
        {
            float t = (Time.unscaledTime + seed) * speed;
            float yaw = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * maxYawDegrees;
            float pitch = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * maxPitchDegrees;
            transform.localRotation = baseRotation * Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
```

- [ ] **Step 2: `MainMenuAmbientFlicker.cs`**

```csharp
using System.Collections;
using UnityEngine;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// Independent ambient-light flicker for the main menu scene, in the same visual language as
    /// Game.Gameplay.World.LightZone's flicker (TGD section 31-32) but a separate component — the
    /// menu scene has no LightZone-relevant gameplay state (fuses, shutdown timers) to gate a
    /// flicker on, so this one is a standalone, always-on ambience loop instead of state-driven.
    /// </summary>
    public class MainMenuAmbientFlicker : MonoBehaviour
    {
        [SerializeField] private Light[] lights;
        [SerializeField] private float minIntervalSeconds = 2f;
        [SerializeField] private float maxIntervalSeconds = 6f;
        [SerializeField, Range(0f, 1f)] private float dimAmount = 0.35f;
        [SerializeField] private float flickerDurationSeconds = 0.08f;

        private float[] baseIntensities;

        private void OnEnable()
        {
            if (lights == null || lights.Length == 0) return;

            baseIntensities = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null) baseIntensities[i] = lights[i].intensity;
            }

            StartCoroutine(FlickerLoop());
        }

        private IEnumerator FlickerLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(minIntervalSeconds, maxIntervalSeconds));

                SetIntensityScale(1f - dimAmount);
                yield return new WaitForSeconds(flickerDurationSeconds);
                SetIntensityScale(1f);
            }
        }

        private void SetIntensityScale(float scale)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null) lights[i].intensity = baseIntensities[i] * scale;
            }
        }
    }
}
```

- [ ] **Step 3: Verify structurally**

```bash
for f in MainMenuCameraSway MainMenuAmbientFlicker; do
python3 -c "
s = open('/Users/lee/Desktop/gelistirjam/Assets/_Project/Code/UI/MainMenu/$f.cs').read()
assert s.count('{') == s.count('}'), '$f: unbalanced braces'
print('$f: OK')
"
done
```

Leave the changes unstaged — do not run any `git` command.

---

### Task 6: `MainMenuSceneBuilder` — Editor tool that constructs the prefab UI and the scene

**Files:**
- Create: `Assets/_Project/Code/Editor/MainMenuSceneBuilder.cs`
- Create: `Assets/Scenes/MainMenu.unity` (an empty scene — see Step 6; the builder populates it when the user runs the menu command inside Unity, it is not populated by this plan directly)
- Create: `Assets/Scenes/MainMenu.unity.meta`

**Interfaces:**
- Consumes: `MainMenuTablet.prefab` (Task 1), `MainMenuRow`/`MainMenuPresenter` (Tasks 2-3, exact serialized field names must match what this builder assigns via `SerializedObject`), `NewGameLoader`/`QuitGameAction`/`MasterVolumeController` (Task 4), `MainMenuCameraSway`/`MainMenuAmbientFlicker` (Task 5), `HudTerminalFactory`'s `EnsureFontAsset()`, `CreateChild`/`CreateStretchChild`/`AddText`/`CreatePanel`/`AssignSerialized` helpers and its `Phosphor`/`MutedPhosphor`/`PanelBackground` colors (`Assets/_Project/Code/Editor/HudTerminalFactory.cs` — same assembly, `internal` members are visible), `TerminalPanelGraphic` (`Assets/_Project/Code/UI/Hud/TerminalPanelGraphic.cs`).
- Produces: nothing consumed by a later task in this plan — this is the last authoring step; Task 7 is manual verification only.

- [ ] **Step 1: Create the empty scene file**

A brand new empty Unity scene is a small, well-known fixed YAML shape (just the default `OcclusionCullingSettings`/`RenderSettings`/`LightmapSettings`/`NavMeshSettings` boilerplate every empty scene has, no custom GameObjects) — write it directly rather than trying to construct it from the builder tool, since the builder needs a scene to already exist and be open to add content to it.

Write `Assets/Scenes/MainMenu.unity`:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!29 &1
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {fileID: 0}
--- !u!104 &2
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 9
  m_Fog: 0
  m_FogColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}
  m_FogMode: 3
  m_FogDensity: 0.01
  m_LinearFogStart: 0
  m_LinearFogEnd: 300
  m_AmbientSkyColor: {r: 0.02, g: 0.03, b: 0.025, a: 1}
  m_AmbientEquatorColor: {r: 0.02, g: 0.03, b: 0.025, a: 1}
  m_AmbientGroundColor: {r: 0.01, g: 0.015, b: 0.0125, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 3
  m_SubtractiveShadowColor: {r: 0.42, g: 0.478, b: 0.627, a: 1}
  m_SkyboxMaterial: {fileID: 0}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {fileID: 0}
  m_SpotCookie: {fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {fileID: 0}
  m_Sun: {fileID: 0}
  m_UseRadianceAmbientProbe: 0
--- !u!157 &3
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 13
  m_GIWorkflowMode: 1
  m_GISettings:
    serializedVersion: 2
    m_BounceScale: 1
    m_IndirectOutputScale: 1
    m_AlbedoBoost: 1
    m_IndirectOutputScale2: 1
    m_EnvironmentLightingMode: 0
    m_EnableBakedLightmaps: 0
    m_EnableRealtimeLightmaps: 0
  m_LightmapEditorSettings:
    serializedVersion: 12
    m_Resolution: 2
    m_BakeResolution: 40
    m_AtlasSize: 1024
    m_AO: 0
    m_AOMaxDistance: 1
    m_CompAOExponent: 1
    m_CompAOExponentDirect: 0
    m_ExtractAmbientOcclusion: 0
    m_Padding: 2
    m_LightmapParameters: {fileID: 0}
    m_LightmapsBakeMode: 1
    m_TextureCompression: 1
    m_FinalGather: 0
    m_FinalGatherFiltering: 1
    m_FinalGatherRayCount: 256
    m_ReflectionCompression: 2
    m_MixedBakeMode: 2
    m_BakeBackend: 1
    m_PVRSampling: 1
    m_PVRDirectSampleCount: 32
    m_PVRSampleCount: 500
    m_PVRBounces: 2
    m_PVREnvironmentSampleCount: 500
    m_PVREnvironmentReferencePointCount: 2048
    m_PVRFilteringMode: 1
    m_PVRDenoiserTypeDirect: 1
    m_PVRDenoiserTypeIndirect: 1
    m_PVRDenoiserTypeAO: 1
    m_PVRFilterTypeDirect: 0
    m_PVRFilterTypeIndirect: 0
    m_PVRFilterTypeAO: 0
    m_PVREnvironmentMIS: 1
    m_PVRCulling: 1
    m_PVRFilteringGaussRadiusDirect: 1
    m_PVRFilteringGaussRadiusIndirect: 5
    m_PVRFilteringGaussRadiusAO: 2
    m_PVRFilteringAtrousPositionSigmaDirect: 0.5
    m_PVRFilteringAtrousPositionSigmaIndirect: 2
    m_PVRFilteringAtrousPositionSigmaAO: 1
    m_ExportTrainingData: 0
    m_TrainingDataDestination: TrainingData
    m_LightProbeSampleCountMultiplier: 4
  m_LightingDataAsset: {fileID: 0}
  m_LightingSettings: {fileID: 0}
--- !u!196 &4
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 3
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    accuratePlacement: 0
    maxJobWorkers: 0
    preserveTilesOutsideBounds: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {fileID: 0}
```

Write `Assets/Scenes/MainMenu.unity.meta`:

```yaml
fileFormatVersion: 2
guid: 9b4e6f81c3a75d2e0f8b6a4c1d7e9f52
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

Before writing that guid, check it's unused:

```bash
grep -rl "9b4e6f81c3a75d2e0f8b6a4c1d7e9f52" /Users/lee/Desktop/gelistirjam/Assets 2>/dev/null
```

Expected: no output.

This scene has zero GameObjects — it is deliberately just the empty-scene boilerplate. Everything else (camera, tablet instance, lights, Canvas, rows, options page) is built by the Editor tool below, the first time the user opens this scene in Unity and runs its menu command. Say this plainly in your task report: **the scene is empty until the user runs `Tools/Gloomveil/Setup Main Menu Scene` inside Unity with this scene open** — that command cannot be run from this execution environment.

- [ ] **Step 2: Write the Editor builder — font, colors, and scaffolding**

```csharp
using System;
using Game.UI.MainMenu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// One-click setup that builds the main menu terminal: the World Space Canvas + rows + options
    /// page inside MainMenuTablet.prefab, and the camera/lighting/tablet placement in the currently
    /// open scene (expected to be MainMenu.unity). Additive and idempotent like
    /// MainSceneFuseSetup.cs — every step checks for an existing object first, safe to re-run.
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        private const string TabletPrefabPath = "Assets/prefabs/MainMenuTablet.prefab";

        [MenuItem("Tools/Gloomveil/Setup Main Menu Scene")]
        public static void Run()
        {
            TMP_FontAsset font = HudTerminalFactory.EnsureFontAsset();
            if (font == null)
            {
                Debug.LogError("Setup Main Menu Scene: Cascadia Mono font asset could not be " +
                                "ensured (see the error above from HudTerminalFactory). Aborting.");
                return;
            }

            if (!BuildTabletPrefabUi(font, out _))
            {
                return;
            }

            BuildScene(font);

            Debug.Log("Main menu scene set up: tablet Canvas/rows/options page built into " +
                       "MainMenuTablet.prefab, camera/lighting/tablet placed in the open scene. " +
                       "Enter Play Mode to test.");
        }

        // ---------- Prefab: builds the Canvas/rows/options page inside MainMenuTablet.prefab ----------

        private static bool BuildTabletPrefabUi(TMP_FontAsset font, out GameObject tabletRoot)
        {
            tabletRoot = null;
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TabletPrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"Setup Main Menu Scene: prefab not found at '{TabletPrefabPath}'. " +
                                "Run the prefab-duplication step first.");
                return false;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(TabletPrefabPath);
            try
            {
                if (contents.transform.Find("ScreenAnchor") != null)
                {
                    Debug.Log("Setup Main Menu Scene: MainMenuTablet.prefab already has a " +
                              "ScreenAnchor — leaving its existing UI in place.");
                    tabletRoot = prefabAsset;
                    return true;
                }

                MeshFilter meshFilter = contents.GetComponentInChildren<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    Debug.LogError("Setup Main Menu Scene: MainMenuTablet.prefab has no mesh to " +
                                    "measure a screen placement from.");
                    return false;
                }

                Bounds localBounds = meshFilter.sharedMesh.bounds;

                // Assumption (documented in the design spec): the screen faces local +Z, matching
                // FuseTabletPresenter's screenAnchor convention ("+Z pointing out of the screen").
                // bezelInset leaves a margin so the Canvas doesn't overlap the tablet's frame.
                // BOTH of these are a starting point computed from the mesh's real bounds, not a
                // guess pulled from nowhere — but if the model's screen actually faces a different
                // local axis, or the bezel margin looks wrong, nudge ScreenAnchor's local position/
                // rotation/scale by hand in the Inspector. That is expected and fine — see the
                // design spec's "no hardcoded layout" requirement.
                const float bezelInset = 0.85f;
                var screenAnchorGo = new GameObject("ScreenAnchor");
                screenAnchorGo.transform.SetParent(contents.transform, false);
                screenAnchorGo.transform.localPosition =
                    new Vector3(localBounds.center.x, localBounds.center.y, localBounds.max.z);
                screenAnchorGo.transform.localRotation = Quaternion.identity;

                const float refWidthUnits = 800f;
                float scale = (localBounds.size.x * bezelInset) / refWidthUnits;
                if (scale <= 0f) scale = 0.001f; // guards a degenerate/zero-width mesh bounds case
                float refHeightUnits = (localBounds.size.y * bezelInset) / scale;

                GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform));
                canvasGo.transform.SetParent(screenAnchorGo.transform, false);
                canvasGo.transform.localScale = Vector3.one * scale;
                Canvas canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                RectTransform canvasRect = (RectTransform)canvasGo.transform;
                canvasRect.sizeDelta = new Vector2(refWidthUnits, refHeightUnits);
                canvasGo.AddComponent<GraphicRaycaster>();

                RectTransform screenRoot = HudTerminalFactory.CreateStretchChild(
                    canvasRect, "ScreenRoot", Vector2.zero, Vector2.zero);
                TerminalPanelGraphic background = HudTerminalFactory.CreatePanel(screenRoot);
                background.raycastTarget = false;

                // One shared AudioSource for the whole menu's navigate/confirm SFX — every row
                // plays through this, none owns its own. 2D (spatialBlend 0): this is UI feedback,
                // not a positioned in-world sound. playOnAwake off so it only ever sounds on
                // PlayOneShot calls from MainMenuRow.
                GameObject audioGo = new GameObject("MenuAudioSource");
                audioGo.transform.SetParent(contents.transform, false);
                AudioSource audioSource = audioGo.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;

                (GameObject menuPage, MainMenuRow[] rows) =
                    BuildMenuPage(screenRoot, font, refWidthUnits, refHeightUnits, audioSource);
                GameObject optionsPage = BuildOptionsPage(screenRoot, font, refWidthUnits, refHeightUnits,
                    out Slider volumeSlider, out MasterVolumeController volumeController);
                optionsPage.SetActive(false);

                TMP_Text clockText = BuildClock(screenRoot, font, refWidthUnits, refHeightUnits);

                GameObject presenterGo = new GameObject("MainMenuPresenter");
                presenterGo.transform.SetParent(contents.transform, false);
                MainMenuPresenter presenter = presenterGo.AddComponent<MainMenuPresenter>();
                BindPresenter(presenter, rows, menuPage, optionsPage, clockText);

                WireVolumeSlider(volumeSlider, volumeController);

                PrefabUtility.SaveAsPrefabAsset(contents, TabletPrefabPath);
                tabletRoot = prefabAsset;
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
```

- [ ] **Step 3: Continue the builder — menu page rows**

Append (still inside the `MainMenuSceneBuilder` class):

```csharp
        private static (GameObject page, MainMenuRow[] rows) BuildMenuPage(
            RectTransform screenRoot, TMP_FontAsset font, float refWidth, float refHeight,
            AudioSource audioSource)
        {
            RectTransform page = HudTerminalFactory.CreateStretchChild(
                screenRoot, "MenuPage", new Vector2(20f, 20f), new Vector2(-20f, -60f));

            VerticalLayoutGroup layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperCenter;

            (string label, bool interactable, string subLabel)[] rowDefs =
            {
                ("NEW GAME", true, null),
                ("CONTINUE", false, "(no save yet)"),
                ("OPTIONS", true, null),
                ("QUIT", true, null),
            };

            float rowHeight = (refHeight - 60f) / rowDefs.Length - layout.spacing;
            var rows = new MainMenuRow[rowDefs.Length];
            for (int i = 0; i < rowDefs.Length; i++)
            {
                rows[i] = BuildRow(page, font, rowDefs[i].label, rowDefs[i].interactable,
                    rowDefs[i].subLabel, rowHeight, audioSource);
            }

            return (page.gameObject, rows);
        }

        private static MainMenuRow BuildRow(RectTransform parent, TMP_FontAsset font, string label,
            bool interactable, string subLabel, float height, AudioSource audioSource)
        {
            GameObject rowGo = new GameObject($"Row_{label.Replace(' ', '_')}", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            RectTransform rowRect = (RectTransform)rowGo.transform;
            LayoutElement layoutElement = rowGo.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;
            layoutElement.minHeight = height;

            RectTransform borderRect = HudTerminalFactory.CreateStretchChild(
                rowRect, "Border", Vector2.zero, Vector2.zero);
            Image border = borderRect.gameObject.AddComponent<Image>();
            border.sprite = HudTerminalFactory.RequireFilledImageSprite();
            border.type = Image.Type.Sliced;
            border.color = HudTerminalFactory.MutedPhosphor;

            RectTransform cursorRect = HudTerminalFactory.CreateChild(
                rowRect, "Cursor", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(12f, 0f), new Vector2(24f, height));
            TMP_Text cursor = HudTerminalFactory.AddText(cursorRect, font, 20f,
                TextAlignmentOptions.MidlineLeft, HudTerminalFactory.Phosphor, ">");

            RectTransform titleRect = HudTerminalFactory.CreateChild(
                rowRect, "Title", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(44f, 0f), new Vector2(-64f, height));
            string text = subLabel != null ? $"{label}  <size=70%>{subLabel}</size>" : label;
            TMP_Text title = HudTerminalFactory.AddText(titleRect, font, 20f,
                TextAlignmentOptions.MidlineLeft, interactable ? HudTerminalFactory.Phosphor : HudTerminalFactory.MutedPhosphor,
                text);
            title.raycastTarget = false;

            MainMenuRow row = rowGo.AddComponent<MainMenuRow>();
            HudTerminalFactory.AssignSerialized(row, "highlightBorder", border);
            HudTerminalFactory.AssignSerialized(row, "cursorLabel", cursor);
            HudTerminalFactory.AssignSerialized(row, "audioSource", audioSource);
            // selectSound/confirmSound are deliberately left unassigned here — see Task 2's
            // Interfaces note: the user assigns those clips by hand once they have them.
            // `interactable` and colors keep the component's own inspector defaults; only the flag
            // needs setting per-row.
            var so = new SerializedObject(row);
            so.FindProperty("interactable").boolValue = interactable;
            so.ApplyModifiedPropertiesWithoutUndo();

            return row;
        }
```

- [ ] **Step 4: Continue the builder — options page and clock**

Append:

```csharp
        private static GameObject BuildOptionsPage(RectTransform screenRoot, TMP_FontAsset font,
            float refWidth, float refHeight, out Slider volumeSlider, out MasterVolumeController volumeController)
        {
            RectTransform page = HudTerminalFactory.CreateStretchChild(
                screenRoot, "OptionsPage", new Vector2(20f, 20f), new Vector2(-20f, -60f));

            RectTransform header = HudTerminalFactory.CreateChild(page, "Header",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(refWidth - 40f, 28f));
            HudTerminalFactory.AddText(header, font, 18f, TextAlignmentOptions.TopLeft,
                HudTerminalFactory.Phosphor, "OPTIONS");

            RectTransform controls = HudTerminalFactory.CreateChild(page, "Controls",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -36f), new Vector2(refWidth - 40f, 150f));
            string controlsText =
                "MOVE       WASD / ARROW KEYS\n" +
                "LOOK       MOUSE\n" +
                "SPRINT     LEFT SHIFT\n" +
                "INTERACT   E\n" +
                "FLASHLIGHT F\n" +
                "JUMP       SPACE";
            TMP_Text controlsLabel = HudTerminalFactory.AddText(controls, font, 15f,
                TextAlignmentOptions.TopLeft, HudTerminalFactory.MutedPhosphor, controlsText);
            controlsLabel.enableWordWrapping = false;

            RectTransform volumeLabelRect = HudTerminalFactory.CreateChild(page, "VolumeLabel",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -196f), new Vector2(refWidth - 40f, 24f));
            HudTerminalFactory.AddText(volumeLabelRect, font, 15f, TextAlignmentOptions.TopLeft,
                HudTerminalFactory.Phosphor, "MASTER VOLUME");

            RectTransform sliderRect = HudTerminalFactory.CreateChild(page, "VolumeSlider",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -228f), new Vector2(refWidth - 40f, 20f));
            Slider slider = sliderRect.gameObject.AddComponent<Slider>();
            RectTransform sliderBackground = HudTerminalFactory.CreateStretchChild(
                sliderRect, "Background", Vector2.zero, Vector2.zero);
            Image sliderBgImage = sliderBackground.gameObject.AddComponent<Image>();
            sliderBgImage.color = HudTerminalFactory.MutedPhosphor;

            RectTransform fillArea = HudTerminalFactory.CreateStretchChild(
                sliderRect, "Fill Area", Vector2.zero, Vector2.zero);
            RectTransform fill = HudTerminalFactory.CreateStretchChild(fillArea, "Fill", Vector2.zero, Vector2.zero);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = HudTerminalFactory.Phosphor;
            slider.fillRect = fill;
            slider.targetGraphic = fillImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            GameObject controllerGo = new GameObject("MasterVolumeController");
            controllerGo.transform.SetParent(page, false);
            MasterVolumeController controller = controllerGo.AddComponent<MasterVolumeController>();

            volumeSlider = slider;
            volumeController = controller;
            return page.gameObject;
        }

        private static TMP_Text BuildClock(RectTransform screenRoot, TMP_FontAsset font,
            float refWidth, float refHeight)
        {
            RectTransform clockRect = HudTerminalFactory.CreateChild(screenRoot, "Clock",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -12f), new Vector2(140f, 24f));
            TMP_Text clock = HudTerminalFactory.AddText(clockRect, font, 15f,
                TextAlignmentOptions.TopRight, HudTerminalFactory.MutedPhosphor, "00:00:00");
            return clock;
        }

        private static void BindPresenter(MainMenuPresenter presenter, MainMenuRow[] rows,
            GameObject menuPage, GameObject optionsPage, TMP_Text clockText)
        {
            var so = new SerializedObject(presenter);
            SerializedProperty rowsProp = so.FindProperty("rows");
            rowsProp.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                rowsProp.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
            }

            so.FindProperty("menuPage").objectReferenceValue = menuPage;
            so.FindProperty("optionsPage").objectReferenceValue = optionsPage;
            so.FindProperty("clockText").objectReferenceValue = clockText;
            so.ApplyModifiedPropertiesWithoutUndo();

            // OPTIONS row's onConfirmed -> presenter.OpenOptions(). NEW GAME/QUIT rows are wired to
            // their own action components in BuildScene instead (they don't exist inside the
            // prefab, and a prefab can't hold a reference to a scene object — see BuildScene's
            // WireSceneOnlyRowActions).
            foreach (MainMenuRow row in rows)
            {
                if (row.name != "Row_OPTIONS") continue;
                UnityEditor.Events.UnityEventTools.AddPersistentListener(
                    row.EditorOnConfirmedEvent, presenter.OpenOptions);
            }
        }

        private static void WireVolumeSlider(Slider slider, MasterVolumeController controller)
        {
            slider.value = controller.CurrentVolume;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                slider.onValueChanged, controller.SetVolume);
        }
```

`UnityEventTools.AddPersistentListener` needs a live `UnityEvent` reference, not a `SerializedProperty` — that's exactly what Task 2's `MainMenuRow.EditorOnConfirmedEvent` (Editor-only, `#if UNITY_EDITOR`) exists for. If Task 2 was implemented as written, this compiles against it directly; nothing further to add.

- [ ] **Step 5: Continue the builder — the scene half (camera, lighting, tablet placement)**

Append (still inside the `MainMenuSceneBuilder` class):

```csharp
        // ---------- Scene: places the tablet, frames it with a camera, lights it ----------

        private static void BuildScene(TMP_FontAsset font)
        {
            EnsureEventSystem();

            GameObject tabletInstance = FindOrInstantiateTablet();
            WireSceneOnlyRowActions(tabletInstance);

            Camera camera = EnsureMenuCamera(tabletInstance);
            EnsureAmbientLight(tabletInstance);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject FindOrInstantiateTablet()
        {
            GameObject existing = GameObject.Find("MainMenuTablet");
            if (existing != null) return existing;

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TabletPrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            instance.transform.position = new Vector3(0f, 1f, 0f);
            instance.transform.rotation = Quaternion.identity;
            return instance;
        }

        private static void WireSceneOnlyRowActions(GameObject tabletInstance)
        {
            MainMenuRow[] rows = tabletInstance.GetComponentsInChildren<MainMenuRow>(true);
            MainMenuRow newGameRow = Array.Find(rows, r => r.name == "Row_NEW_GAME");
            MainMenuRow quitRow = Array.Find(rows, r => r.name == "Row_QUIT");

            if (newGameRow != null && CountPersistentListeners(newGameRow) == 0)
            {
                GameObject loaderGo = new GameObject("NewGameLoader");
                loaderGo.transform.SetParent(tabletInstance.transform, false);
                NewGameLoader loader = loaderGo.AddComponent<NewGameLoader>();
                UnityEditor.Events.UnityEventTools.AddPersistentListener(
                    newGameRow.EditorOnConfirmedEvent, loader.Load);
            }

            if (quitRow != null && CountPersistentListeners(quitRow) == 0)
            {
                GameObject quitGo = new GameObject("QuitGameAction");
                quitGo.transform.SetParent(tabletInstance.transform, false);
                QuitGameAction quit = quitGo.AddComponent<QuitGameAction>();
                UnityEditor.Events.UnityEventTools.AddPersistentListener(
                    quitRow.EditorOnConfirmedEvent, quit.Quit);
            }
        }

        private static int CountPersistentListeners(MainMenuRow row) =>
            row.EditorOnConfirmedEvent.GetPersistentEventCount();

        private static Camera EnsureMenuCamera(GameObject tabletInstance)
        {
            Camera existing = Camera.main;
            if (existing != null && existing.GetComponent<MainMenuCameraSway>() != null)
            {
                return existing;
            }

            GameObject cameraGo = existing != null ? existing.gameObject : new GameObject("MainMenuCamera");
            Camera camera = cameraGo.GetComponent<Camera>();
            if (camera == null) camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";

            // Frame the tablet's screen from a short distance in front of it, roughly centered.
            Transform screenAnchor = tabletInstance.transform.Find("ScreenAnchor");
            Vector3 lookTarget = screenAnchor != null ? screenAnchor.position : tabletInstance.transform.position;
            Vector3 forward = screenAnchor != null ? screenAnchor.TransformDirection(Vector3.forward) : Vector3.forward;
            cameraGo.transform.position = lookTarget + forward * 0.6f;
            cameraGo.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);

            if (cameraGo.GetComponent<MainMenuCameraSway>() == null)
            {
                cameraGo.AddComponent<MainMenuCameraSway>();
            }

            return camera;
        }

        private static void EnsureAmbientLight(GameObject tabletInstance)
        {
            if (UnityEngine.Object.FindFirstObjectByType<MainMenuAmbientFlicker>() != null) return;

            GameObject lightGo = new GameObject("MenuKeyLight");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.85f, 0.65f);
            light.intensity = 3f;
            light.range = 4f;
            lightGo.transform.position = tabletInstance.transform.position + new Vector3(0.3f, 0.6f, 0.4f);

            GameObject flickerGo = new GameObject("MainMenuAmbientFlicker");
            MainMenuAmbientFlicker flicker = flickerGo.AddComponent<MainMenuAmbientFlicker>();
            var so = new SerializedObject(flicker);
            SerializedProperty lightsProp = so.FindProperty("lights");
            lightsProp.arraySize = 1;
            lightsProp.GetArrayElementAtIndex(0).objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
```

- [ ] **Step 6: Assemble the full file and verify it structurally**

Concatenate Steps 2, 4, and 5 in that order into `Assets/_Project/Code/Editor/MainMenuSceneBuilder.cs`.

```bash
python3 -c "
s = open('/Users/lee/Desktop/gelistirjam/Assets/_Project/Code/Editor/MainMenuSceneBuilder.cs').read()
assert s.count('{') == s.count('}'), 'unbalanced braces'
assert '[MenuItem(\"Tools/Gloomveil/Setup Main Menu Scene\")]' in s
print('OK:', s.count(chr(10)), 'lines')
"
```

Also re-verify Task 2's file picked up the `EditorOnConfirmedEvent` addition:

```bash
grep -n "EditorOnConfirmedEvent" /Users/lee/Desktop/gelistirjam/Assets/_Project/Code/UI/MainMenu/MainMenuRow.cs
```

Expected: one match.

- [ ] **Step 7: State plainly what could not be verified in this session**

Write this into your task report verbatim (do not soften or omit it): *"This task's Editor tool has not been run inside Unity — there is no attached Editor/MCP session. It compiles-by-inspection and follows this codebase's existing `MainSceneFuseSetup.cs` pattern closely, but the actual scene/prefab construction it performs (Canvas sizing against the real mesh bounds, row layout, event wiring) is unverified until the user opens `MainMenu.unity` in Unity and runs `Tools/Gloomveil/Setup Main Menu Scene` — see Task 7."*

Leave the changes unstaged — do not run any `git` command.

---

### Task 7: Manual in-Editor verification

**Files:** none (verification only).

**Interfaces:**
- Consumes: everything from Tasks 1-6.
- Produces: nothing — last task.

- [ ] **Step 1: Run the builder**

Open `Assets/Scenes/MainMenu.unity` in Unity. Run `Tools/Gloomveil/Setup Main Menu Scene`. Confirm the Console shows the tool's success log and no errors.

- [ ] **Step 2: Check the tablet's screen fit**

Select `MainMenuTablet`'s `ScreenAnchor` child in the Hierarchy. In the Scene view, confirm the Canvas sits on the tablet's visible screen face, roughly matching its bezel — this is the part flagged in the design spec and Task 6 as a computed *starting point*, not a guaranteed-correct final fit. If it's on the wrong face or misaligned, adjust `ScreenAnchor`'s local position/rotation/scale by hand (per the spec, this must remain freely repositionable, and now you're doing exactly that).

- [ ] **Step 3: Enter Play mode and exercise the menu**

Confirm: Up/Down (or W/S) moves selection with the highlight-glow animation; CONTINUE is skipped by navigation and shows its "(no save yet)" sub-label in muted color; Enter (or mouse click, once mouse-hover wiring is confirmed working — `SelectRow` exists on `MainMenuPresenter` but Task 6's builder does not wire per-row `PointerEnter`/click handlers, since `Button`/`EventTrigger` wiring on each row was not included in Task 6 — **note this gap explicitly to the user**: hover/click support needs an `EventTrigger` (or `Button`) added to each row's `Border` object in the Editor, calling `MainMenuPresenter.SelectRow(int)` / `MainMenuRow.Confirm()`, since `MainMenuPresenter.SelectRow` takes an index that only the Editor-time row-building order can supply cleanly via the Inspector's `UnityEvent` int-argument field) confirms the selected row with the flash animation; NEW GAME loads `MainGame.unity`; QUIT exits Play mode; OPTIONS swaps to the controls/volume page and Escape returns to the menu; the volume slider audibly changes output.

- [ ] **Step 4: Check the clock and ambient flicker**

Confirm the clock updates once per second and is legible. Confirm the key light flickers occasionally, subtly, not distractingly.

- [ ] **Step 5: Report results**

Summarize what was checked, what worked, and explicitly flag: (a) the screen-fit accuracy from Step 2, (b) the missing mouse hover/click wiring noted in Step 3 as a known gap this plan did not close (scope note: the brainstorming discussion approved keyboard nav with mouse hover/click as a nice-to-have, not a hard requirement — Task 6 delivers keyboard nav completely; mouse support needs a short follow-up if wanted), (c) every row's `Select Sound`/`Confirm Sound` fields (on each `MainMenuRow` component, under `MainMenuTablet`'s row objects in the Hierarchy — e.g. `Row_NEW_GAME`, `Row_OPTIONS`) are empty by design — the user assigns their own navigate/confirm SFX clips there; the shared `MenuAudioSource` object and the null-safe `PlayOneShot` wiring are already in place, nothing else needs to change once clips are dropped in, (d) anything else that looked wrong. Do not mark this done silently if something is broken — flag it back to the user.
