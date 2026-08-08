# Fuse Tablet Interaction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pressing E on a fuse blends the camera to the wall-mounted tablet and runs the existing 4-button memory minigame on the tablet's own world-space screen, driven by a locked crosshair instead of a free mouse cursor.

**Architecture:** Cinemachine 3.x owns the camera transition — a POV virtual camera parented to the player's existing `CameraPivot`, and a fuse-view virtual camera on the tablet prefab whose priority the new `FuseTabletPresenter` raises and lowers. The single shared `FuseMiniGameController` keeps its state machine and visuals untouched; only its canvas moves to world space and reparents to the active tablet's screen anchor for the duration of an interaction. `FuseBox` talks to the tablet through an `IFuseViewPresenter` interface so it never references Cinemachine.

**Tech Stack:** Unity 6000.3.13f1 (URP), Cinemachine 3.x (`Unity.Cinemachine`), Unity Input System 1.19, uGUI + TextMeshPro (`Unity.TextMeshPro`, part of `com.unity.ugui` 2.0.0), DOTween (plain DLL at `Assets/Plugins/Demigiant/DOTween`, auto-referenced — no asmdef entry needed), Unity Test Framework (PlayMode).

## Global Constraints

- Source of truth for this work: `docs/superpowers/specs/2026-08-09-fuse-tablet-interaction-design.md`.
- The minigame's rules are frozen: sequence length 4, button count 4, existing colours, timings, penalty behaviour, round resumption, and the `Idle → ShowingSequence → WaitingForInput → Penalty → RoundComplete → FuseCompleted / Cancelled` state machine must not change.
- `FuseBox` must never reference Cinemachine, `FuseTabletPresenter`, or any concrete presentation type — only `IFuseViewPresenter`.
- Map generation stays one-directional: nothing added here may call back into `Gloomveil.MapGeneration`. Fuse placement keeps flowing through `GeneratedMapRegistry` → `MapFuseGate`.
- The cursor stays locked and hidden for the entire fuse interaction. After this work `PlayerController` is the only script that writes `Cursor.lockState` / `Cursor.visible`.
- New cross-system references are wired through `Bind(...)` calls from a composition root (`MapFuseGate` for spawned fuses, `MainSceneFuseSetup` for scene objects). No `FindObjectOfType` in runtime gameplay code.
- All new PlayMode tests live in `Assets/_Project/Code/Tests/PlayMode` and follow the existing style in `MapFuseGateTests.cs`: `[UnityTest] public IEnumerator Method_Scenario()`, a `Fixture : IDisposable` created by a static factory, `try`/`finally` disposal, `DestroyImmediate` teardown.
- Run tests through the Unity MCP `run_tests` tool (PlayMode) or the Editor's Test Runner window. There is no CLI test runner in this repo.
- Fixed values used throughout, copied verbatim from the spec: blend in `0.5s` EaseInOut, blend out `0.25s`, limited look `±15°` yaw and `±10°` pitch, canvas world size 400×300 UI units at local scale `0.0012` (≈ 0.48 × 0.36 m), camera anchor ≈ `0.65 m` in front of the screen, lever tween `0.6s`.

---

## File Structure

**Create:**

| File | Responsibility |
|---|---|
| `Assets/_Project/Code/Gameplay/Objectives/IFuseViewPresenter.cs` | The contract `FuseBox` uses to drive the tablet's presentation. |
| `Assets/_Project/Code/Gameplay/Objectives/FuseTabletPresenter.cs` | Concrete tablet presentation: fuse-view vcam priority, blend durations, clamped look, lever tween, screen anchor. |
| `Assets/_Project/Code/Gameplay/Objectives/FuseScreenPointer.cs` | Resolves the `Button` under the viewport centre through a `GraphicRaycaster`. Nothing else. |
| `Assets/_Project/Code/Tests/PlayMode/FuseTabletPresenterTests.cs` | Priority/blend/look/lever behaviour. |
| `Assets/_Project/Code/Tests/PlayMode/FuseBoxPresenterTests.cs` | `FuseBox` ↔ presenter ↔ game-flow interaction. |
| `Assets/_Project/Code/Tests/PlayMode/FuseMiniGameCanvasTests.cs` | Canvas reparenting and cursor invariants. |
| `Assets/_Project/Code/Tests/PlayMode/FuseScreenPointerTests.cs` | Crosshair hit resolution. |

**Modify:**

| File | Change |
|---|---|
| `Packages/manifest.json` | Add `com.unity.cinemachine`. |
| `Assets/_Project/Code/Gameplay/Game.Gameplay.asmdef` | Add `Unity.Cinemachine`, `Unity.TextMeshPro`. |
| `Assets/_Project/Code/Tests/PlayMode/Game.Gameplay.PlayModeTests.asmdef` | Add `Unity.Cinemachine`, `UnityEngine.UI`, `Unity.TextMeshPro`, `Unity.InputSystem`. |
| `Assets/_Project/Code/Gameplay/Player/PlayerController.cs` | Stop unlocking the cursor in `OnDisable`. |
| `Assets/_Project/Code/Gameplay/Objectives/FuseBox.cs` | Presenter in `Bind`, activate/deactivate, completion coroutine, `FinishInteraction` state guard. |
| `Assets/_Project/Code/Gameplay/World/MapFuseGate.cs` | Brain field, presenter resolution, force-cancel before clearing. |
| `Assets/_Project/Code/Gameplay/Objectives/FuseMiniGameController.cs` | World-space canvas + `screenAnchor` param, drop darken overlay and cursor code, TMP text, hover tint, crosshair toggle, pointer integration. |
| `Assets/_Project/Code/Editor/MainSceneFuseSetup.cs` | Build the world-space canvas, TMP text, crosshair canvas, pointer, and brain binding. |
| `Assets/_Project/Code/Tests/PlayMode/MapFuseGateTests.cs` | Update fixture for the new `Bind` signature. |
| `Assets/prefabs/fusetabler-game.prefab` | `Anchor_Screen`, `Anchor_CameraView`, `CinemachineCamera`, `FuseTabletPresenter`. |
| `Assets/_Project/Prefabs/Player.prefab` | `CinemachineBrain` on `PlayerCamera`, `CM_PlayerPOV` under `CameraPivot`. |

---

### Task 1: Install Cinemachine and wire assembly references

Nothing compiles against Cinemachine until the package exists. This task is only the dependency gate.

**Files:**
- Modify: `Packages/manifest.json`
- Modify: `Assets/_Project/Code/Gameplay/Game.Gameplay.asmdef`
- Modify: `Assets/_Project/Code/Tests/PlayMode/Game.Gameplay.PlayModeTests.asmdef`

**Interfaces:**
- Consumes: nothing.
- Produces: the `Unity.Cinemachine` namespace (`CinemachineCamera`, `CinemachineBrain`, `CinemachineBlendDefinition`, `PrioritySettings`) and the `TMPro` namespace, both usable from `Game.Gameplay` and from the PlayMode test assembly.

- [ ] **Step 1: Install the package**

In the Unity Editor: `Window > Package Manager > + > Install package by name...`, enter `com.unity.cinemachine`, leave the version blank so Package Manager resolves the newest 3.x compatible with Unity 6000.3.

Do not hand-edit a version string into `manifest.json` — let Package Manager write it, so the resolved version matches the editor.

- [ ] **Step 2: Verify the resolved version is 3.x**

Run: `grep cinemachine Packages/manifest.json`
Expected: a line like `"com.unity.cinemachine": "3.1.x",`. If Package Manager resolved a 2.x version, stop — the entire plan targets the Cinemachine 3 API (`CinemachineCamera`, not `CinemachineVirtualCamera`).

- [ ] **Step 3: Add the runtime assembly references**

`Assets/_Project/Code/Gameplay/Game.Gameplay.asmdef` — replace the `references` array with:

```json
    "references": [
        "Game.Core",
        "Unity.InputSystem",
        "Gloomveil.MapGeneration",
        "UnityEngine.UI",
        "Unity.Cinemachine",
        "Unity.TextMeshPro"
    ],
```

- [ ] **Step 4: Add the test assembly references**

`Assets/_Project/Code/Tests/PlayMode/Game.Gameplay.PlayModeTests.asmdef` — replace the `references` array with:

```json
  "references": [
    "Game.Gameplay",
    "Game.Core",
    "Gloomveil.MapGeneration",
    "Unity.AI.Navigation",
    "Unity.Cinemachine",
    "UnityEngine.UI",
    "Unity.TextMeshPro",
    "Unity.InputSystem"
  ],
```

- [ ] **Step 5: Verify compilation and that the existing suite still passes**

Use the Unity MCP `refresh_unity` tool, then `read_console` filtered to errors.
Expected: zero compile errors.

Then run the PlayMode suite (MCP `run_tests`, mode `PlayMode`).
Expected: all existing tests pass — the same set that passed before this task.

- [ ] **Step 6: Commit**

```bash
git add Packages/manifest.json Packages/packages-lock.json Assets/_Project/Code/Gameplay/Game.Gameplay.asmdef Assets/_Project/Code/Tests/PlayMode/Game.Gameplay.PlayModeTests.asmdef
git commit -m "build: add Cinemachine and TextMeshPro assembly references"
```

---

### Task 2: Make PlayerController the sole cursor owner

`PlayerController.OnDisable()` currently unlocks and shows the cursor whenever `manageCursor` is true. `FuseInteractionState.Enter()` disables the controller, so today the cursor pops free the instant a fuse interaction starts — which would defeat the crosshair scheme entirely.

**Files:**
- Modify: `Assets/_Project/Code/Gameplay/Player/PlayerController.cs:152-162`
- Test: `Assets/_Project/Code/Tests/PlayMode/FuseMiniGameCanvasTests.cs` (created here, extended in Task 6)

**Interfaces:**
- Consumes: nothing.
- Produces: the invariant that disabling `PlayerController` leaves `Cursor.lockState == CursorLockMode.Locked`.

- [ ] **Step 1: Write the failing test**

Create `Assets/_Project/Code/Tests/PlayMode/FuseMiniGameCanvasTests.cs`:

```csharp
using System.Collections;
using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class FuseMiniGameCanvasTests
    {
        [UnityTest]
        public IEnumerator DisablingPlayerController_LeavesCursorLocked()
        {
            var playerObject = new GameObject(
                "CursorOwnerFixture_Player",
                typeof(Rigidbody),
                typeof(CapsuleCollider));
            PlayerController controller = playerObject.AddComponent<PlayerController>();

            try
            {
                yield return null;
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));

                controller.SetControlEnabled(false);
                yield return null;

                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
                Assert.That(Cursor.visible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run the PlayMode suite filtered to `FuseMiniGameCanvasTests`.
Expected: FAIL — `Cursor.lockState` is `CursorLockMode.None` after `SetControlEnabled(false)`.

- [ ] **Step 3: Remove the unlock from OnDisable**

In `PlayerController.cs`, replace `OnDisable()`:

```csharp
        private void OnDisable()
        {
            actions.Player.Flashlight.performed -= OnFlashlightPerformed;
            actions.Player.Disable();
            HaltMotion();
        }
```

Update the `manageCursor` tooltip on line 59-60 so it still describes what the field does:

```csharp
        [SerializeField, Tooltip("Locks/hides the cursor when control is enabled. Nothing else in the game writes cursor state -- control being disabled (pause, fuse minigame) deliberately leaves the cursor locked. Turn this off if a pause menu should own cursor state instead.")]
        private bool manageCursor = true;
```

- [ ] **Step 4: Run the test to verify it passes**

Run the PlayMode suite filtered to `FuseMiniGameCanvasTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Code/Gameplay/Player/PlayerController.cs Assets/_Project/Code/Tests/PlayMode/FuseMiniGameCanvasTests.cs
git commit -m "fix: keep the cursor locked when player control is disabled"
```

---

### Task 3: IFuseViewPresenter and the fuse-view camera

**Files:**
- Create: `Assets/_Project/Code/Gameplay/Objectives/IFuseViewPresenter.cs`
- Create: `Assets/_Project/Code/Gameplay/Objectives/FuseTabletPresenter.cs`
- Test: `Assets/_Project/Code/Tests/PlayMode/FuseTabletPresenterTests.cs`

**Interfaces:**
- Consumes: `Unity.Cinemachine` from Task 1.
- Produces:
  - `interface IFuseViewPresenter { Transform ScreenAnchor { get; } void Activate(); void Deactivate(); IEnumerator PlayCompletion(); }` in namespace `Game.Gameplay.Objectives`.
  - `class FuseTabletPresenter : MonoBehaviour, IFuseViewPresenter` with `void Bind(CinemachineBrain brain)`, `bool IsActive { get; }`, `void ApplyLook(Vector2 scaledDelta)`.

> Note on naming: the spec calls the completion hook `PlayCompletionAsync()`. It is named `PlayCompletion()` here and everywhere below, returning `IEnumerator` — this codebase drives sequenced feedback with coroutines (see `FuseMiniGameController`), and an `Async` suffix on a coroutine would misdescribe it.

- [ ] **Step 1: Write the interface**

Create `Assets/_Project/Code/Gameplay/Objectives/IFuseViewPresenter.cs`:

```csharp
using System.Collections;
using UnityEngine;

namespace Game.Gameplay.Objectives
{
    /// <summary>
    /// How a fuse presents itself while the player is working it. FuseBox owns fuse *state* and talks
    /// to the world through this interface only, so it never references Cinemachine, DOTween, or the
    /// tablet prefab's concrete component — the same separation that already keeps FuseBox away from
    /// MonsterBrain. The single implementation today is FuseTabletPresenter.
    /// </summary>
    public interface IFuseViewPresenter
    {
        /// <summary>Where the minigame canvas parents itself for the duration of the interaction.</summary>
        Transform ScreenAnchor { get; }

        /// <summary>Player has started working this fuse: take over the camera.</summary>
        void Activate();

        /// <summary>Player is done, one way or another: hand the camera back.</summary>
        void Deactivate();

        /// <summary>
        /// Success-only flourish, played before Deactivate. Cancelled and death-interrupted fuses skip
        /// this entirely. Returns immediately (an empty enumerator) if the implementation has nothing
        /// to play.
        /// </summary>
        IEnumerator PlayCompletion();
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `Assets/_Project/Code/Tests/PlayMode/FuseTabletPresenterTests.cs`:

```csharp
using System;
using System.Collections;
using Game.Gameplay.Objectives;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class FuseTabletPresenterTests
    {
        [UnityTest]
        public IEnumerator Activate_RaisesFuseViewPriorityAndSetsBlendIn()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Presenter.Activate();

                Assert.That(fixture.FuseView.Priority.Value, Is.EqualTo(20));
                Assert.That(fixture.Brain.DefaultBlend.Time, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(fixture.Presenter.IsActive, Is.True);
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator Deactivate_RestoresIdlePriorityAndSetsBlendOut()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Presenter.Activate();
                fixture.Presenter.Deactivate();

                Assert.That(fixture.FuseView.Priority.Value, Is.EqualTo(0));
                Assert.That(fixture.Brain.DefaultBlend.Time, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(fixture.Presenter.IsActive, Is.False);
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator ApplyLook_ClampsYawAndPitchToConfiguredLimits()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Presenter.Activate();
                fixture.Presenter.ApplyLook(new Vector2(500f, -500f));

                Vector3 euler = fixture.FuseView.transform.localEulerAngles;
                float yaw = Mathf.DeltaAngle(0f, euler.y);
                float pitch = Mathf.DeltaAngle(0f, euler.x);

                Assert.That(yaw, Is.EqualTo(15f).Within(0.01f));
                Assert.That(pitch, Is.EqualTo(10f).Within(0.01f));
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator Activate_ResetsLookFromAPreviousInteraction()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Presenter.Activate();
                fixture.Presenter.ApplyLook(new Vector2(500f, 500f));
                fixture.Presenter.Deactivate();

                fixture.Presenter.Activate();

                Assert.That(
                    Quaternion.Angle(fixture.FuseView.transform.localRotation, Quaternion.identity),
                    Is.LessThan(0.01f));
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        internal static Fixture CreateFixture()
        {
            var brainObject = new GameObject("FusePresenterFixture_Brain", typeof(Camera));
            CinemachineBrain brain = brainObject.AddComponent<CinemachineBrain>();

            var tablet = new GameObject("FusePresenterFixture_Tablet");

            var screenAnchor = new GameObject("Anchor_Screen");
            screenAnchor.transform.SetParent(tablet.transform, false);

            var cameraAnchor = new GameObject("Anchor_CameraView");
            cameraAnchor.transform.SetParent(tablet.transform, false);

            var fuseViewObject = new GameObject("CM_FuseView");
            fuseViewObject.transform.SetParent(cameraAnchor.transform, false);
            CinemachineCamera fuseView = fuseViewObject.AddComponent<CinemachineCamera>();
            fuseView.Priority = new PrioritySettings { Enabled = true, Value = 0 };

            var lever = new GameObject("Lever");
            lever.transform.SetParent(tablet.transform, false);

            FuseTabletPresenter presenter = tablet.AddComponent<FuseTabletPresenter>();
            var serialized = new UnityEditor.SerializedObject(presenter);
            serialized.FindProperty("screenAnchor").objectReferenceValue = screenAnchor.transform;
            serialized.FindProperty("fuseViewCamera").objectReferenceValue = fuseView;
            serialized.FindProperty("lever").objectReferenceValue = lever.transform;
            serialized.FindProperty("leverTweenDuration").floatValue = 0.05f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            presenter.Bind(brain);

            return new Fixture(brainObject, tablet, brain, fuseView, presenter);
        }

        internal sealed class Fixture : IDisposable
        {
            private readonly GameObject brainObject;
            private readonly GameObject tablet;

            public CinemachineBrain Brain { get; }
            public CinemachineCamera FuseView { get; }
            public FuseTabletPresenter Presenter { get; }

            public Fixture(
                GameObject brainObject,
                GameObject tablet,
                CinemachineBrain brain,
                CinemachineCamera fuseView,
                FuseTabletPresenter presenter)
            {
                this.brainObject = brainObject;
                this.tablet = tablet;
                Brain = brain;
                FuseView = fuseView;
                Presenter = presenter;
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(tablet);
                UnityEngine.Object.DestroyImmediate(brainObject);
            }
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run the PlayMode suite filtered to `FuseTabletPresenterTests`.
Expected: FAIL to compile — `FuseTabletPresenter` does not exist.

- [ ] **Step 4: Write the presenter**

Create `Assets/_Project/Code/Gameplay/Objectives/FuseTabletPresenter.cs`. This step writes only the camera half; the lever half is Task 4, so `PlayCompletion` is a real no-op here rather than a placeholder.

```csharp
using System.Collections;
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

        public virtual IEnumerator PlayCompletion()
        {
            yield break;
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
    }
}
```

Note `ApplyLook` inverts pitch (`pitch - delta.y`) to match `PlayerController.HandleLook`'s convention, which is why the test feeds `-500f` on Y and expects `+10°`.

- [ ] **Step 5: Run the tests to verify they pass**

Run the PlayMode suite filtered to `FuseTabletPresenterTests`.
Expected: 4 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Code/Gameplay/Objectives/IFuseViewPresenter.cs Assets/_Project/Code/Gameplay/Objectives/FuseTabletPresenter.cs Assets/_Project/Code/Tests/PlayMode/FuseTabletPresenterTests.cs
git commit -m "feat: add fuse view presenter driving the tablet camera"
```

---

### Task 4: Lever completion tween

**Files:**
- Modify: `Assets/_Project/Code/Gameplay/Objectives/FuseTabletPresenter.cs`
- Test: `Assets/_Project/Code/Tests/PlayMode/FuseTabletPresenterTests.cs`

**Interfaces:**
- Consumes: `FuseTabletPresenter` and its fixture from Task 3.
- Produces: `PlayCompletion()` rotates the bound `lever` transform from `leverClosedEuler` to `leverOpenEuler` over `leverTweenDuration` and completes; `Activate()` snaps the lever back to `leverClosedEuler`.

- [ ] **Step 1: Write the failing tests**

Append these two tests to `FuseTabletPresenterTests.cs`, inside the class, before `CreateFixture`:

```csharp
        [UnityTest]
        public IEnumerator PlayCompletion_RotatesLeverToTheOpenPose()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Presenter.Activate();
                yield return fixture.Presenter.StartCoroutine(fixture.Presenter.PlayCompletion());

                Assert.That(
                    Quaternion.Angle(
                        fixture.Lever.localRotation,
                        Quaternion.Euler(0f, 0f, -70f)),
                    Is.LessThan(0.5f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Activate_SnapsLeverBackToClosedAfterAPreviousCompletion()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Presenter.Activate();
                yield return fixture.Presenter.StartCoroutine(fixture.Presenter.PlayCompletion());

                fixture.Presenter.Activate();

                Assert.That(
                    Quaternion.Angle(fixture.Lever.localRotation, Quaternion.identity),
                    Is.LessThan(0.01f));
            }
            finally
            {
                fixture.Dispose();
            }
        }
```

Add a `Lever` accessor to the fixture. In `CreateFixture`, change the returned object to include the lever transform, and in `Fixture` add:

```csharp
            public Transform Lever { get; }
```

Set it in the constructor — update `CreateFixture`'s return to `new Fixture(brainObject, tablet, brain, fuseView, presenter, lever.transform)` and the constructor signature to accept `Transform leverTransform` with `Lever = leverTransform;` as its last assignment.

- [ ] **Step 2: Run the tests to verify they fail**

Run the PlayMode suite filtered to `FuseTabletPresenterTests`.
Expected: FAIL — `PlayCompletion` currently yields immediately, so the lever stays at identity and the first assertion reports roughly a 70° gap.

- [ ] **Step 3: Implement the tween**

In `FuseTabletPresenter.cs`, add the DOTween import at the top:

```csharp
using DG.Tweening;
```

Replace `PlayCompletion` with:

```csharp
        public IEnumerator PlayCompletion()
        {
            if (lever == null) yield break;

            Tween tween = lever
                .DOLocalRotate(leverOpenEuler, leverTweenDuration)
                .SetEase(Ease.OutBack);

            yield return tween.WaitForCompletion();
        }
```

Add the lever reset to `Activate()`, immediately after the `pitch = 0f;` line:

```csharp
            ResetLever();
```

And add these two members:

```csharp
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
```

Note the `virtual` modifier on `PlayCompletion` from Task 3 is dropped — nothing subclasses this.

- [ ] **Step 4: Run the tests to verify they pass**

Run the PlayMode suite filtered to `FuseTabletPresenterTests`.
Expected: 6 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Code/Gameplay/Objectives/FuseTabletPresenter.cs Assets/_Project/Code/Tests/PlayMode/FuseTabletPresenterTests.cs
git commit -m "feat: play the tablet lever animation on fuse completion"
```

---

### Task 5: Wire FuseBox and MapFuseGate to the presenter

These two change together: `FuseBox.Bind` gains a parameter and `MapFuseGate` is its only caller.

This task also fixes a pre-existing ordering bug it would otherwise make worse. `HandleFullyComplete` raises `OnCompleted` (which can reach `ObjectiveManager.OnAllComplete → MapExitController.Open() → FinalChaseState`) and then calls `FinishInteraction()`, which pushes `ExplorationState` unless the flow is in `DeathState`. Completing the last fuse therefore drops the game straight back out of Final Chase. Adding a 0.6 s lever tween in front of `FinishInteraction` changes that race's timing without fixing it, so the guard is corrected here: only a flow still sitting in `FuseInteractionState` gets pushed back to Exploration.

**Files:**
- Modify: `Assets/_Project/Code/Gameplay/Objectives/FuseBox.cs`
- Modify: `Assets/_Project/Code/Gameplay/World/MapFuseGate.cs`
- Modify: `Assets/_Project/Code/Tests/PlayMode/MapFuseGateTests.cs`
- Test: `Assets/_Project/Code/Tests/PlayMode/FuseBoxPresenterTests.cs`

**Interfaces:**
- Consumes: `IFuseViewPresenter`, `FuseTabletPresenter.Bind(CinemachineBrain)` from Tasks 3–4.
- Produces:
  - `FuseBox.Bind(GameFlowController flow, ObjectiveManager objectives, FuseMiniGameController miniGameController, IFuseViewPresenter viewPresenter)`
  - `MapFuseGate.Bind(GeneratedMapRegistry mapRegistry, GameFlowController flow, ObjectiveManager objectives, FuseMiniGameController fuseMiniGame, CinemachineBrain cinemachineBrain)`

- [ ] **Step 1: Write the failing tests**

Create `Assets/_Project/Code/Tests/PlayMode/FuseBoxPresenterTests.cs`:

```csharp
using System;
using System.Collections;
using Game.Core;
using Game.Gameplay.Objectives;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class FuseBoxPresenterTests
    {
        [UnityTest]
        public IEnumerator BeginInteract_ActivatesThePresenter()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Box.BeginInteract();

                Assert.That(fixture.Presenter.ActivateCount, Is.EqualTo(1));
                Assert.That(fixture.Presenter.DeactivateCount, Is.EqualTo(0));
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator Cancel_DeactivatesWithoutPlayingCompletion()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Box.BeginInteract();
                fixture.MiniGame.ForceCancel();
                yield return null;

                Assert.That(fixture.Presenter.CompletionCount, Is.EqualTo(0));
                Assert.That(fixture.Presenter.DeactivateCount, Is.EqualTo(1));
                Assert.That(fixture.Box.State, Is.EqualTo(FuseBoxState.Incomplete));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator FinishInteraction_DoesNotLeaveFinalChase()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Box.BeginInteract();

                // Stand in for "the last fuse opened the exit": something else moved the flow on
                // while the fuse was finishing.
                fixture.GameFlow.ChangeState(fixture.GameFlow.FinalChaseState);

                fixture.MiniGame.ForceCancel();
                yield return null;

                Assert.That(fixture.GameFlow.CurrentStateName, Is.EqualTo(nameof(FinalChaseState)));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator FinishInteraction_ReturnsToExplorationFromFuseInteraction()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Box.BeginInteract();
                Assert.That(fixture.GameFlow.CurrentStateName, Is.EqualTo(nameof(FuseInteractionState)));

                fixture.MiniGame.ForceCancel();
                yield return null;

                Assert.That(fixture.GameFlow.CurrentStateName, Is.EqualTo(nameof(ExplorationState)));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static Fixture CreateFixture()
        {
            var gameFlowObject = new GameObject("FuseBoxPresenterFixture_GameFlow");
            GameFlowController gameFlow = gameFlowObject.AddComponent<GameFlowController>();

            var objectivesObject = new GameObject("FuseBoxPresenterFixture_Objectives");
            ObjectiveManager objectives = objectivesObject.AddComponent<ObjectiveManager>();

            var miniGameObject = new GameObject("FuseBoxPresenterFixture_MiniGame");
            FuseMiniGameController miniGame = miniGameObject.AddComponent<FuseMiniGameController>();

            var boxObject = new GameObject("FuseBoxPresenterFixture_Box", typeof(BoxCollider));
            FuseBox box = boxObject.AddComponent<FuseBox>();

            var presenter = new SpyPresenter();
            box.Bind(gameFlow, objectives, miniGame, presenter);

            return new Fixture(gameFlowObject, objectivesObject, miniGameObject, boxObject,
                gameFlow, miniGame, box, presenter);
        }

        /// <summary>
        /// Counts calls instead of exercising Cinemachine: this suite is about FuseBox's decisions,
        /// and FuseTabletPresenterTests already covers the real implementation.
        /// </summary>
        private sealed class SpyPresenter : IFuseViewPresenter
        {
            public int ActivateCount { get; private set; }
            public int DeactivateCount { get; private set; }
            public int CompletionCount { get; private set; }

            public Transform ScreenAnchor => null;

            public void Activate() => ActivateCount++;

            public void Deactivate() => DeactivateCount++;

            public IEnumerator PlayCompletion()
            {
                CompletionCount++;
                yield break;
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject gameFlowObject;
            private readonly GameObject objectivesObject;
            private readonly GameObject miniGameObject;
            private readonly GameObject boxObject;

            public GameFlowController GameFlow { get; }
            public FuseMiniGameController MiniGame { get; }
            public FuseBox Box { get; }
            public SpyPresenter Presenter { get; }

            public Fixture(
                GameObject gameFlowObject,
                GameObject objectivesObject,
                GameObject miniGameObject,
                GameObject boxObject,
                GameFlowController gameFlow,
                FuseMiniGameController miniGame,
                FuseBox box,
                SpyPresenter presenter)
            {
                this.gameFlowObject = gameFlowObject;
                this.objectivesObject = objectivesObject;
                this.miniGameObject = miniGameObject;
                this.boxObject = boxObject;
                GameFlow = gameFlow;
                MiniGame = miniGame;
                Box = box;
                Presenter = presenter;
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(boxObject);
                UnityEngine.Object.DestroyImmediate(miniGameObject);
                UnityEngine.Object.DestroyImmediate(objectivesObject);
                UnityEngine.Object.DestroyImmediate(gameFlowObject);
            }
        }
    }
}
```

The `SpyPresenter` is a plain class, not a `MonoBehaviour` — `IFuseViewPresenter` is deliberately not constrained to components.

> These tests drive cancellation through `FuseMiniGameController.ForceCancel()` rather than a synthetic ESC press. `BeginInteract` will bail early (logging an error and invoking `onCancelled`) because the fixture's minigame has no UI references wired — which is exactly the cancel path being asserted, and keeps this suite free of UI construction. Expect a `LogAssert.Expect(LogType.Error, ...)` to be needed once you see the exact message; add it at the top of each test that reaches `BeginInteract`:
>
> ```csharp
> LogAssert.Expect(LogType.Error, "[FuseMiniGameController] Missing required references (buttons/panel) — cannot start. See Inspector.");
> ```
>
> With that path, `onCancelled` fires synchronously inside `Begin`, so `ForceCancel()` is a no-op and the assertions hold on the frame after `BeginInteract`. Keep the `ForceCancel()` line anyway — it documents intent and stays correct once Task 6 wires real UI into this fixture.

- [ ] **Step 2: Run the tests to verify they fail**

Run the PlayMode suite filtered to `FuseBoxPresenterTests`.
Expected: FAIL to compile — `FuseBox.Bind` takes three arguments.

- [ ] **Step 3: Update FuseBox**

In `FuseBox.cs`, add `using System.Collections;` to the imports.

Add the presenter field next to the other serialized references:

```csharp
        // Not serialized: IFuseViewPresenter is an interface, and every FuseBox in the game is spawned
        // by MapFuseGate, which always binds one.
        private IFuseViewPresenter viewPresenter;
```

Replace `Bind`:

```csharp
        public void Bind(GameFlowController flow, ObjectiveManager objectives,
            FuseMiniGameController miniGameController, IFuseViewPresenter presenter)
        {
            gameFlow = flow;
            objectiveManager = objectives;
            miniGame = miniGameController;
            viewPresenter = presenter;
            if (isActiveAndEnabled) objectiveManager.Register(this);
        }
```

In `BeginInteract`, add the activation between the interaction event and the minigame start:

```csharp
            State = FuseBoxState.Active;
            gameFlow?.ChangeState(gameFlow.FuseInteractionState);
            OnInteractionStarted?.Invoke(transform.position);
            viewPresenter?.Activate();

            miniGame.Begin(roundsRequired, RoundsCompleted, name,
                onRoundComplete: HandleRoundComplete,
                onFullyComplete: HandleFullyComplete,
                onCancelled: HandleCancelled);
```

Replace `HandleFullyComplete` and `HandleCancelled`:

```csharp
        private void HandleFullyComplete()
        {
            RoundsCompleted = roundsRequired;
            State = FuseBoxState.Complete;
            OnCompleted?.Invoke();
            StartCoroutine(CompleteRoutine());
        }

        /// <summary>
        /// The success flourish runs between "the fuse is done" and "the player gets the camera back":
        /// OnCompleted has already fired, so objectives/exit/lights react while the lever animates.
        /// </summary>
        private IEnumerator CompleteRoutine()
        {
            if (viewPresenter != null) yield return StartCoroutine(viewPresenter.PlayCompletion());
            viewPresenter?.Deactivate();
            FinishInteraction();
        }

        private void HandleCancelled()
        {
            // RoundsCompleted is untouched here — only HandleRoundComplete increments it, so rounds
            // finished before this cancel are preserved (prompt section 15), only the in-progress round
            // was discarded (inside the minigame itself, before this callback fires).
            State = FuseBoxState.Incomplete;
            viewPresenter?.Deactivate();
            FinishInteraction();
        }
```

Replace `FinishInteraction` and its doc comment:

```csharp
        /// <summary>
        /// Shared "the player is no longer actively working this fuse" side effects. Only a flow still
        /// sitting in FuseInteractionState is pushed back to Exploration. Anything else already moved
        /// on and must not be overwritten: death (found live via Unity MCP testing — the monster
        /// killing the player mid-minigame used to be silently un-killed from the game flow's
        /// perspective) and, on the last fuse, Final Chase (OnCompleted above reaches
        /// ObjectiveManager.OnAllComplete -> MapExitController.Open(), which enters FinalChaseState
        /// before this method runs).
        /// </summary>
        private void FinishInteraction()
        {
            if (gameFlow != null && gameFlow.CurrentStateName == nameof(FuseInteractionState))
            {
                gameFlow.ChangeState(gameFlow.ExplorationState);
            }
            OnInteractionStopped?.Invoke();
        }
```

- [ ] **Step 4: Update MapFuseGate**

In `MapFuseGate.cs`, add `using Unity.Cinemachine;` to the imports.

Add the brain field after `miniGame`:

```csharp
        [SerializeField, Tooltip("The scene's CinemachineBrain, handed to each spawned tablet presenter. Spawned prefabs cannot hold scene references, so the gate passes it through.")]
        private CinemachineBrain cinemachineBrain;
```

Replace `Bind`:

```csharp
        public void Bind(GeneratedMapRegistry mapRegistry, GameFlowController flow,
            ObjectiveManager objectives, FuseMiniGameController fuseMiniGame, CinemachineBrain brain)
        {
            registry = mapRegistry;
            gameFlow = flow;
            objectiveManager = objectives;
            miniGame = fuseMiniGame;
            cinemachineBrain = brain;
            if (isActiveAndEnabled) Subscribe();
        }
```

`Subscribe()`'s null guard is deliberately left alone — a missing brain must not stop fuses from spawning, it only costs the blend-duration override.

In `SpawnAll`, replace the `fuseBox.Bind(...)` line and the two lines before it:

```csharp
                FuseBox fuseBox = instance.GetComponent<FuseBox>();
                if (fuseBox == null) fuseBox = instance.AddComponent<FuseBox>();

                FuseTabletPresenter presenter = instance.GetComponent<FuseTabletPresenter>();
                if (presenter != null) presenter.Bind(cinemachineBrain);

                int rounds = roundsPattern != null && roundsPattern.Length > 0
                    ? roundsPattern[i % roundsPattern.Length]
                    : 1;
                fuseBox.SetRoundsRequired(rounds);
                fuseBox.Bind(gameFlow, objectiveManager, miniGame, presenter);
                spawned.Add(fuseBox);
```

In `ClearSpawned`, force-cancel first so a regenerated map cannot destroy a tablet out from under an open minigame:

```csharp
        private void ClearSpawned()
        {
            // A minigame open on a fuse that is about to be destroyed would leave its canvas parented
            // to a dead transform and its coroutine waiting for input that can never arrive.
            if (miniGame != null) miniGame.ForceCancel();

            foreach (FuseBox box in spawned)
            {
                if (box != null) Destroy(box.gameObject);
            }
            spawned.Clear();
        }
```

- [ ] **Step 5: Update the existing MapFuseGate fixture**

In `Assets/_Project/Code/Tests/PlayMode/MapFuseGateTests.cs`, change the `gate.Bind` call in `CreateFixture` (line 206):

```csharp
            gate.Bind(registry, gameFlow, objectives, miniGame, null);
```

Passing `null` for the brain is the supported case — a fuse without a brain still spawns and binds.

- [ ] **Step 6: Run the tests to verify they pass**

Run the full PlayMode suite.
Expected: `FuseBoxPresenterTests` 4 PASS, `MapFuseGateTests` 6 PASS, everything else unchanged.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Code/Gameplay/Objectives/FuseBox.cs Assets/_Project/Code/Gameplay/World/MapFuseGate.cs Assets/_Project/Code/Tests/PlayMode/MapFuseGateTests.cs Assets/_Project/Code/Tests/PlayMode/FuseBoxPresenterTests.cs
git commit -m "feat: drive the fuse view presenter from FuseBox and MapFuseGate"
```

---

### Task 6: Move the minigame canvas into world space

**Files:**
- Modify: `Assets/_Project/Code/Gameplay/Objectives/FuseMiniGameController.cs`
- Modify: `Assets/_Project/Code/Gameplay/Objectives/FuseBox.cs` (the `Begin` call site)
- Test: `Assets/_Project/Code/Tests/PlayMode/FuseMiniGameCanvasTests.cs`

**Interfaces:**
- Consumes: `IFuseViewPresenter.ScreenAnchor` from Task 3.
- Produces: `FuseMiniGameController.Begin(int roundsRequired, int roundsAlreadyCompleted, string label, Transform screenAnchor, Action onRoundComplete, Action onFullyComplete, Action onCancelled)` — `screenAnchor` sits after `label` so the data arguments stay grouped ahead of the callbacks.

- [ ] **Step 1: Write the failing tests**

Add to `FuseMiniGameCanvasTests.cs` (created in Task 2), inside the class:

```csharp
        [UnityTest]
        public IEnumerator Begin_ParentsTheCanvasToTheScreenAnchor()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Begin();
                yield return null;

                Assert.That(fixture.Canvas.transform.parent, Is.EqualTo(fixture.ScreenAnchor));
                Assert.That(
                    Vector3.Distance(fixture.Canvas.transform.localPosition, Vector3.zero),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(fixture.Canvas.transform.localRotation, Quaternion.identity),
                    Is.LessThan(0.01f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Cancel_ReturnsTheCanvasToItsOriginalParent()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Begin();
                yield return null;

                fixture.MiniGame.ForceCancel();

                float timeout = 3f;
                while (fixture.MiniGame.IsActive && timeout > 0f)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }

                Assert.That(fixture.MiniGame.IsActive, Is.False);
                Assert.That(fixture.Canvas.transform.parent, Is.Null);
                Assert.That(fixture.CancelledCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Begin_KeepsTheCursorLocked()
        {
            Fixture fixture = CreateFixture();

            try
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                fixture.Begin();
                yield return null;

                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
                Assert.That(Cursor.visible, Is.False);
            }
            finally
            {
                fixture.Dispose();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
```

Add this fixture builder and class to the same file, after the tests:

```csharp
        internal static Fixture CreateFixture()
        {
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var eventSystemObject = new GameObject(
                    "FuseCanvasFixture_EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem));
                Object.DontDestroyOnLoad(eventSystemObject);
            }

            var canvasObject = new GameObject("FuseCanvasFixture_Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvasObject.transform, false);
            CanvasGroup panelGroup = panel.AddComponent<CanvasGroup>();

            var buttons = new UnityEngine.UI.Button[4];
            var images = new UnityEngine.UI.Image[4];
            for (int i = 0; i < 4; i++)
            {
                var buttonObject = new GameObject("Button_" + i, typeof(RectTransform));
                buttonObject.transform.SetParent(panel.transform, false);
                UnityEngine.UI.Image image = buttonObject.AddComponent<UnityEngine.UI.Image>();
                UnityEngine.UI.Button button = buttonObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = image;
                buttons[i] = button;
                images[i] = image;
            }

            var controllerObject = new GameObject("FuseCanvasFixture_Controller");
            Objectives.FuseMiniGameController miniGame =
                controllerObject.AddComponent<Objectives.FuseMiniGameController>();

            var serialized = new UnityEditor.SerializedObject(miniGame);
            serialized.FindProperty("worldCanvas").objectReferenceValue = canvas;
            serialized.FindProperty("panelRoot").objectReferenceValue = panel;
            serialized.FindProperty("panelGroup").objectReferenceValue = panelGroup;
            serialized.FindProperty("fadeDuration").floatValue = 0f;
            UnityEditor.SerializedProperty buttonsProp = serialized.FindProperty("buttons");
            UnityEditor.SerializedProperty imagesProp = serialized.FindProperty("buttonImages");
            buttonsProp.arraySize = 4;
            imagesProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                buttonsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                imagesProp.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var anchorObject = new GameObject("FuseCanvasFixture_Anchor");

            return new Fixture(canvasObject, controllerObject, anchorObject, canvas, miniGame,
                anchorObject.transform, buttons);
        }

        internal sealed class Fixture : System.IDisposable
        {
            private readonly GameObject canvasObject;
            private readonly GameObject controllerObject;
            private readonly GameObject anchorObject;

            public Canvas Canvas { get; }
            public Objectives.FuseMiniGameController MiniGame { get; }
            public Transform ScreenAnchor { get; }
            public UnityEngine.UI.Button[] Buttons { get; }
            public int CancelledCount { get; private set; }

            public Fixture(
                GameObject canvasObject,
                GameObject controllerObject,
                GameObject anchorObject,
                Canvas canvas,
                Objectives.FuseMiniGameController miniGame,
                Transform screenAnchor,
                UnityEngine.UI.Button[] buttons)
            {
                this.canvasObject = canvasObject;
                this.controllerObject = controllerObject;
                this.anchorObject = anchorObject;
                Canvas = canvas;
                MiniGame = miniGame;
                ScreenAnchor = screenAnchor;
                Buttons = buttons;
            }

            public void Begin() =>
                MiniGame.Begin(1, 0, "FIXTURE", ScreenAnchor, null, null, () => CancelledCount++);

            public void Dispose()
            {
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(anchorObject);
            }
        }
```

Add `using Game.Gameplay.Objectives;` is deliberately omitted — the fixture qualifies with `Objectives.` so the file's existing `Game.Gameplay.Player` import for Task 2's test stays unambiguous.

- [ ] **Step 2: Run the tests to verify they fail**

Run the PlayMode suite filtered to `FuseMiniGameCanvasTests`.
Expected: FAIL to compile — `Begin` takes six arguments and there is no `worldCanvas` serialized property.

- [ ] **Step 3: Update FuseMiniGameController**

In `FuseMiniGameController.cs`:

Replace the UI references header block:

```csharp
        [Header("UI References — assigned by the scene composition root")]
        [SerializeField, Tooltip("The world-space canvas root. Reparented to the active fuse tablet's screen anchor for the duration of an interaction, and returned here on close.")]
        private Canvas worldCanvas;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField, Tooltip("Screen-centre crosshair, shown only while the minigame is open. A separate ScreenSpaceOverlay canvas — the world-space canvas cannot host it.")]
        private GameObject crosshairRoot;
        [SerializeField] private Text progressText;
        [SerializeField] private Text statusText;
        [SerializeField] private Button[] buttons = new Button[ButtonCount];
        [SerializeField] private Image[] buttonImages = new Image[ButtonCount];
```

(`darkenOverlay` is gone. `progressText`/`statusText` stay legacy `Text` here — Task 7 swaps them.)

Delete the `darkenOverlayColor` field from the colours block.

Add a serialized screen scale to the timing block:

```csharp
        [SerializeField, Tooltip("Local scale applied to the canvas when it is parented to a tablet screen. The canvas is authored at 400x300 UI units, so 0.0012 maps it to roughly 0.48 x 0.36 m.")]
        private float screenScale = 0.0012f;
```

Add the home-parent field next to the other private state:

```csharp
        private Transform canvasHome;
```

In `Awake()`, capture it before the panel is hidden:

```csharp
            if (worldCanvas != null) canvasHome = worldCanvas.transform.parent;
            if (panelRoot != null) panelRoot.SetActive(false);
            if (crosshairRoot != null) crosshairRoot.SetActive(false);
```

Replace `Begin`'s signature and the `StartCoroutine` line:

```csharp
        public void Begin(int roundsRequired, int roundsAlreadyCompleted, string label,
            Transform screenAnchor, Action onRoundComplete, Action onFullyComplete, Action onCancelled)
        {
            if (IsActive)
            {
                Debug.LogWarning($"[FuseMiniGameController] Begin() called while already active — ignoring (label={label}).");
                return;
            }

            if (ValidateConfiguration() == false)
            {
                Debug.LogError("[FuseMiniGameController] Missing required references (canvas/buttons/panel) — cannot start. See Inspector.");
                onCancelled?.Invoke();
                return;
            }

            IsActive = true;
            StartCoroutine(RunFuse(Mathf.Max(1, roundsRequired), Mathf.Max(0, roundsAlreadyCompleted),
                label, screenAnchor, onRoundComplete, onFullyComplete, onCancelled));
        }
```

Add the canvas to `ValidateConfiguration`, as its first check:

```csharp
            if (worldCanvas == null) return false;
```

Thread `screenAnchor` through `RunFuse`:

```csharp
        private IEnumerator RunFuse(int roundsRequired, int roundsCompleted, string label,
            Transform screenAnchor, Action onRoundComplete, Action onFullyComplete, Action onCancelled)
        {
            cancelRequested = false;
            yield return OpenPanel(screenAnchor);
```

The rest of `RunFuse` is unchanged.

Replace `OpenPanel` and `ClosePanel`:

```csharp
        private IEnumerator OpenPanel(Transform screenAnchor)
        {
            AttachCanvasTo(screenAnchor);

            panelRoot.SetActive(true);
            if (crosshairRoot != null) crosshairRoot.SetActive(true);
            for (int i = 0; i < ButtonCount; i++) SetButtonColor(i, DimColor(i));
            SetStatusText(string.Empty);

            // Cursor state is deliberately untouched: it stays locked for the whole interaction and
            // PlayerController is the only script in the game that writes it.
            if (panelGroup != null) yield return FadeCanvasGroup(panelGroup, 0f, 1f, fadeDuration);
        }

        private IEnumerator ClosePanel()
        {
            SetButtonsInteractable(false);
            if (panelGroup != null) yield return FadeCanvasGroup(panelGroup, panelGroup.alpha, 0f, fadeDuration);

            panelRoot.SetActive(false);
            if (crosshairRoot != null) crosshairRoot.SetActive(false);
            DetachCanvas();
        }

        /// <summary>
        /// Parents the shared canvas onto the active tablet's screen. A missing anchor is survivable —
        /// the minigame still runs, just wherever the canvas already sits — so it warns instead of
        /// aborting an interaction the player already committed to.
        /// </summary>
        private void AttachCanvasTo(Transform screenAnchor)
        {
            if (screenAnchor == null)
            {
                Debug.LogWarning("[FuseMiniGameController] No screen anchor supplied — the minigame canvas stays where it is.", this);
                return;
            }

            Transform canvasTransform = worldCanvas.transform;
            canvasTransform.SetParent(screenAnchor, false);
            canvasTransform.localPosition = Vector3.zero;
            canvasTransform.localRotation = Quaternion.identity;
            canvasTransform.localScale = Vector3.one * screenScale;
        }

        /// <summary>
        /// Returns the canvas to the parent it had at Awake. Not cosmetic: MapFuseGate destroys fuse
        /// instances when a new map is published, and a canvas still parented under one dies with it.
        /// </summary>
        private void DetachCanvas()
        {
            if (worldCanvas == null) return;
            worldCanvas.transform.SetParent(canvasHome, false);
        }
```

Delete the `darkenOverlay` lines that remain in `RunFuse`'s open/close paths — there are none outside `OpenPanel`/`ClosePanel`, which the block above already replaced.

- [ ] **Step 4: Update the FuseBox call site**

In `FuseBox.BeginInteract`, pass the anchor:

```csharp
            miniGame.Begin(roundsRequired, RoundsCompleted, name, viewPresenter?.ScreenAnchor,
                onRoundComplete: HandleRoundComplete,
                onFullyComplete: HandleFullyComplete,
                onCancelled: HandleCancelled);
```

- [ ] **Step 5: Update the FuseBoxPresenterTests expected error string**

The validation message changed in Step 3. In `FuseBoxPresenterTests.cs`, every `LogAssert.Expect` must now read:

```csharp
                LogAssert.Expect(LogType.Error, "[FuseMiniGameController] Missing required references (canvas/buttons/panel) — cannot start. See Inspector.");
```

- [ ] **Step 6: Run the tests to verify they pass**

Run the full PlayMode suite.
Expected: `FuseMiniGameCanvasTests` 4 PASS (Task 2's cursor test plus the three added here), `FuseBoxPresenterTests` 4 PASS, everything else unchanged.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Code/Gameplay/Objectives/FuseMiniGameController.cs Assets/_Project/Code/Gameplay/Objectives/FuseBox.cs Assets/_Project/Code/Tests/PlayMode/FuseMiniGameCanvasTests.cs Assets/_Project/Code/Tests/PlayMode/FuseBoxPresenterTests.cs
git commit -m "feat: run the fuse minigame on a world-space canvas mounted to the tablet"
```

---

### Task 7: Swap the minigame text to TextMeshPro

Legacy `UnityEngine.UI.Text` is unreadably soft at 0.65 m in world space.

**Files:**
- Modify: `Assets/_Project/Code/Gameplay/Objectives/FuseMiniGameController.cs`
- Test: `Assets/_Project/Code/Tests/PlayMode/FuseMiniGameCanvasTests.cs`

**Interfaces:**
- Consumes: the `worldCanvas`/`panelRoot` wiring from Task 6.
- Produces: `progressText` and `statusText` are `TMPro.TMP_Text`.

- [ ] **Step 1: Write the failing test**

Add to `FuseMiniGameCanvasTests.cs`, inside the class:

```csharp
        [UnityTest]
        public IEnumerator Begin_WritesRoundProgressToTheTmpLabel()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Begin();
                yield return null;

                Assert.That(fixture.ProgressText.text, Does.Contain("FIXTURE"));
                Assert.That(fixture.ProgressText.text, Does.Contain("1 / 1"));
            }
            finally
            {
                fixture.Dispose();
            }
        }
```

In `CreateFixture`, build the label and bind it. After the buttons loop, before the controller object is created:

```csharp
            var progressObject = new GameObject("ProgressText", typeof(RectTransform));
            progressObject.transform.SetParent(panel.transform, false);
            TMPro.TMP_Text progressText = progressObject.AddComponent<TMPro.TextMeshProUGUI>();

            var statusObject = new GameObject("StatusText", typeof(RectTransform));
            statusObject.transform.SetParent(panel.transform, false);
            TMPro.TMP_Text statusText = statusObject.AddComponent<TMPro.TextMeshProUGUI>();
```

Then bind them alongside the other serialized properties:

```csharp
            serialized.FindProperty("progressText").objectReferenceValue = progressText;
            serialized.FindProperty("statusText").objectReferenceValue = statusText;
```

Add `TMPro.TMP_Text ProgressText { get; }` to `Fixture`, pass `progressText` as a new final constructor argument, and assign it.

- [ ] **Step 2: Run the test to verify it fails**

Run the PlayMode suite filtered to `FuseMiniGameCanvasTests`.
Expected: FAIL to compile — `progressText` is declared as `UnityEngine.UI.Text`, so the `TMP_Text` reference will not assign.

- [ ] **Step 3: Swap the field types**

In `FuseMiniGameController.cs`, add the import:

```csharp
using TMPro;
```

Change the two declarations:

```csharp
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text statusText;
```

`SetProgressText` and `SetStatusText` need no change — `TMP_Text` exposes the same `.text` property.

- [ ] **Step 4: Run the test to verify it passes**

Run the PlayMode suite filtered to `FuseMiniGameCanvasTests`.
Expected: 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Code/Gameplay/Objectives/FuseMiniGameController.cs Assets/_Project/Code/Tests/PlayMode/FuseMiniGameCanvasTests.cs
git commit -m "feat: render fuse minigame labels with TextMeshPro"
```

---

### Task 8: Crosshair targeting and click-to-press

**Files:**
- Create: `Assets/_Project/Code/Gameplay/Objectives/FuseScreenPointer.cs`
- Modify: `Assets/_Project/Code/Gameplay/Objectives/FuseMiniGameController.cs`
- Test: `Assets/_Project/Code/Tests/PlayMode/FuseScreenPointerTests.cs`

**Interfaces:**
- Consumes: the world-space canvas from Task 6.
- Produces: `class FuseScreenPointer : MonoBehaviour` with `void Bind(GraphicRaycaster raycaster)` and `Button ResolveButtonAtViewportCentre()`.

- [ ] **Step 1: Write the failing test**

Create `Assets/_Project/Code/Tests/PlayMode/FuseScreenPointerTests.cs`:

```csharp
using System;
using System.Collections;
using Game.Gameplay.Objectives;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Gameplay.Tests
{
    public sealed class FuseScreenPointerTests
    {
        [UnityTest]
        public IEnumerator ResolveButtonAtViewportCentre_ReturnsTheButtonUnderTheCrosshair()
        {
            Fixture fixture = CreateFixture();

            try
            {
                yield return null;

                Assert.That(fixture.Pointer.ResolveButtonAtViewportCentre(), Is.EqualTo(fixture.Button));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ResolveButtonAtViewportCentre_IgnoresNonInteractableButtons()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Button.interactable = false;
                yield return null;

                Assert.That(fixture.Pointer.ResolveButtonAtViewportCentre(), Is.Null);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ResolveButtonAtViewportCentre_ReturnsNullWhenNothingIsUnderTheCrosshair()
        {
            Fixture fixture = CreateFixture();

            try
            {
                // Push the button far off to one side; the crosshair now sees empty canvas.
                fixture.Button.GetComponent<RectTransform>().anchoredPosition = new Vector2(5000f, 0f);
                yield return null;

                Assert.That(fixture.Pointer.ResolveButtonAtViewportCentre(), Is.Null);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static Fixture CreateFixture()
        {
            var eventSystemObject = new GameObject("PointerFixture_EventSystem", typeof(EventSystem));

            var cameraObject = new GameObject("PointerFixture_Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 0f, -2f);
            cameraObject.transform.rotation = Quaternion.identity;

            var canvasObject = new GameObject("PointerFixture_Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(400f, 300f);
            canvasObject.transform.localScale = Vector3.one * 0.0012f;
            GraphicRaycaster raycaster = canvasObject.AddComponent<GraphicRaycaster>();

            var buttonObject = new GameObject("Button_Centre", typeof(RectTransform));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(120f, 120f);
            buttonRect.anchoredPosition = Vector2.zero;
            Image image = buttonObject.AddComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            var pointerObject = new GameObject("PointerFixture_Pointer");
            FuseScreenPointer pointer = pointerObject.AddComponent<FuseScreenPointer>();
            pointer.Bind(raycaster);

            return new Fixture(eventSystemObject, cameraObject, canvasObject, pointerObject,
                pointer, button);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject eventSystemObject;
            private readonly GameObject cameraObject;
            private readonly GameObject canvasObject;
            private readonly GameObject pointerObject;

            public FuseScreenPointer Pointer { get; }
            public Button Button { get; }

            public Fixture(
                GameObject eventSystemObject,
                GameObject cameraObject,
                GameObject canvasObject,
                GameObject pointerObject,
                FuseScreenPointer pointer,
                Button button)
            {
                this.eventSystemObject = eventSystemObject;
                this.cameraObject = cameraObject;
                this.canvasObject = canvasObject;
                this.pointerObject = pointerObject;
                Pointer = pointer;
                Button = button;
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(pointerObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(eventSystemObject);
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the PlayMode suite filtered to `FuseScreenPointerTests`.
Expected: FAIL to compile — `FuseScreenPointer` does not exist.

- [ ] **Step 3: Write the pointer**

Create `Assets/_Project/Code/Gameplay/Objectives/FuseScreenPointer.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Gameplay.Objectives
{
    /// <summary>
    /// Answers one question: which minigame button is under the screen-centre crosshair right now.
    /// The cursor stays locked for the whole fuse interaction, so there is no pointer position to feed
    /// the EventSystem — this fakes one at the viewport centre and asks the world-space canvas's own
    /// GraphicRaycaster. Deliberately owns no visuals and no input handling; FuseMiniGameController
    /// decides what a hover looks like and what a click means.
    /// </summary>
    public class FuseScreenPointer : MonoBehaviour
    {
        [SerializeField, Tooltip("The GraphicRaycaster on the minigame's world-space canvas.")]
        private GraphicRaycaster raycaster;

        private readonly List<RaycastResult> results = new List<RaycastResult>();

        public void Bind(GraphicRaycaster canvasRaycaster) => raycaster = canvasRaycaster;

        /// <summary>
        /// The interactable Button under the viewport centre, or null. Non-interactable buttons are
        /// skipped rather than returned, which makes hover feedback automatically follow the
        /// minigame's own input windows — buttons are only interactable while it is accepting input.
        /// </summary>
        public Button ResolveButtonAtViewportCentre()
        {
            if (raycaster == null || EventSystem.current == null) return null;

            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
            };

            results.Clear();
            raycaster.Raycast(pointerData, results);

            for (int i = 0; i < results.Count; i++)
            {
                Button button = results[i].gameObject.GetComponentInParent<Button>();
                if (button != null && button.interactable) return button;
            }

            return null;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the PlayMode suite filtered to `FuseScreenPointerTests`.
Expected: 3 tests PASS.

- [ ] **Step 5: Wire hover and click into the controller**

In `FuseMiniGameController.cs`, add the pointer reference to the UI references block:

```csharp
        [SerializeField, Tooltip("Resolves which button the crosshair is on. Bound to the world canvas's GraphicRaycaster.")]
        private FuseScreenPointer pointer;
```

Add the hover brightness to the colours block, between `highlightBrightness` and `dimBrightness`:

```csharp
        [SerializeField, Tooltip("Multiplier applied to a button's base color while the crosshair is on it. Sits between dim and highlight so hover reads as 'targeted', never as a sequence pulse.")]
        private float hoverBrightness = 1.1f;
```

Add the tracking state next to the other private fields:

```csharp
        private readonly bool[] buttonAnimating = new bool[ButtonCount];
        private int hoveredIndex = -1;
```

Replace `Update()`:

```csharp
        private void Update()
        {
            if (!IsActive) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) cancelRequested = true;

            UpdateCrosshairTargeting();
        }

        /// <summary>
        /// Hover tint plus click-to-press. The click goes through the Button's own onClick, so the
        /// index mapping wired once in Awake stays the single source of truth for which button is which.
        /// </summary>
        private void UpdateCrosshairTargeting()
        {
            Button hovered = pointer != null ? pointer.ResolveButtonAtViewportCentre() : null;
            int index = IndexOf(hovered);

            if (index != hoveredIndex)
            {
                if (hoveredIndex >= 0 && !buttonAnimating[hoveredIndex]) SetButtonColor(hoveredIndex, DimColor(hoveredIndex));
                hoveredIndex = index;
                if (hoveredIndex >= 0 && !buttonAnimating[hoveredIndex]) SetButtonColor(hoveredIndex, buttonColors[hoveredIndex] * hoverBrightness);
            }

            if (hovered != null && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                hovered.onClick.Invoke();
            }
        }

        private int IndexOf(Button button)
        {
            if (button == null) return -1;
            for (int i = 0; i < ButtonCount; i++)
            {
                if (buttons[i] == button) return i;
            }
            return -1;
        }
```

Guard the animation flag inside `LerpButtonVisual` so hover can never fight a pulse. Wrap its body:

```csharp
        private IEnumerator LerpButtonVisual(int index, Color fromColor, Color toColor, Vector3 fromScale, Vector3 toScale, float duration)
        {
            buttonAnimating[index] = true;

            if (duration <= 0f)
            {
                SetButtonColor(index, toColor);
                if (buttons[index] != null) buttons[index].transform.localScale = toScale;
                buttonAnimating[index] = false;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (cancelRequested) { buttonAnimating[index] = false; yield break; }
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                SetButtonColor(index, Color.Lerp(fromColor, toColor, t));
                if (buttons[index] != null) buttons[index].transform.localScale = Vector3.Lerp(fromScale, toScale, t);
                yield return null;
            }
            SetButtonColor(index, toColor);
            if (buttons[index] != null) buttons[index].transform.localScale = toScale;
            buttonAnimating[index] = false;
        }
```

Reset the hover state in `ClosePanel`, right after `SetButtonsInteractable(false);`:

```csharp
            hoveredIndex = -1;
```

Add the `Mouse` import — `UnityEngine.InputSystem` is already imported for `Keyboard`, so no new using is needed.

- [ ] **Step 6: Run the full suite**

Run the full PlayMode suite.
Expected: everything passes. The canvas fixture has no `pointer` bound, so `UpdateCrosshairTargeting` short-circuits to `hovered == null` and the existing tests are unaffected.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Code/Gameplay/Objectives/FuseScreenPointer.cs Assets/_Project/Code/Gameplay/Objectives/FuseMiniGameController.cs Assets/_Project/Code/Tests/PlayMode/FuseScreenPointerTests.cs
git commit -m "feat: press fuse minigame buttons with a locked crosshair"
```

---

### Task 9: Update the main-scene setup tool

`MainSceneFuseSetup` builds the minigame UI from scratch. It still builds a screen-space canvas with a darken overlay and legacy text, none of which the controller accepts any more.

**Files:**
- Modify: `Assets/_Project/Code/Editor/MainSceneFuseSetup.cs`

**Interfaces:**
- Consumes: every serialized field introduced in Tasks 5–8 (`worldCanvas`, `crosshairRoot`, `pointer`, TMP labels, `MapFuseGate.cinemachineBrain`).
- Produces: a one-click scene setup that matches the new runtime contract.

- [ ] **Step 1: Update the imports and the brain binding**

Add to the imports:

```csharp
using TMPro;
using Unity.Cinemachine;
```

In `Run()`, find the brain and pass it to the gate. Replace the `fuseGate.Bind(...)` line:

```csharp
            CinemachineBrain brain = Object.FindFirstObjectByType<CinemachineBrain>();
            if (brain == null)
            {
                Debug.LogWarning("Setup Main Scene Fuse System: no CinemachineBrain in the scene. " +
                                 "Fuses will still work, but the camera will cut instead of blending. " +
                                 "Add a CinemachineBrain to PlayerCamera and re-run.");
            }

            fuseGate.Bind(registry, gameFlow, objectiveManager, miniGame, brain);
```

- [ ] **Step 2: Rebuild the canvas as world-space**

Replace the canvas construction block in `EnsureFuseMiniGameUI` (the `canvasGO`/`Canvas`/`CanvasScaler` lines) with:

```csharp
            GameObject canvasGO = new GameObject("FuseMiniGameCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(400f, 300f);
            canvasGO.transform.localScale = Vector3.one * 0.0012f;
            GraphicRaycaster raycaster = canvasGO.AddComponent<GraphicRaycaster>();
```

The `CanvasScaler` is dropped — it has no effect on a world-space canvas.

Delete the whole `DarkenOverlay` block (the `overlayGO`, `overlayImage`, `overlayGroup` lines).

Change the panel's rect so it fills the 400×300 canvas:

```csharp
            GameObject panelGO = NewUIObject("FusePanel", canvasGO.transform);
            RectTransform panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelBg = panelGO.AddComponent<Image>();
            panelBg.color = new Color(0.10f, 0.10f, 0.11f, 0.95f);
            CanvasGroup panelGroup = panelGO.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f;
```

Shrink the button grid to fit: change `buttonGridRect.sizeDelta` to `new Vector2(180f, 180f)`, `layout.cellSize` to `new Vector2(82f, 82f)`, and `layout.spacing` to `new Vector2(12f, 12f)`.

- [ ] **Step 3: Build TMP labels instead of legacy Text**

Delete the `Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");` line and the `NewText` helper.

Add this helper in its place:

```csharp
        private static TMP_Text NewLabel(string name, Transform parent, float fontSize,
            TextAlignmentOptions alignment, Color color)
        {
            GameObject go = NewUIObject(name, parent);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.text = string.Empty;
            text.enableWordWrapping = false;
            return text;
        }
```

Replace the two `NewText` calls:

```csharp
            TMP_Text progressText = NewLabel("ProgressText", panelGO.transform, 18f, TextAlignmentOptions.Top, new Color(0.78f, 0.82f, 0.85f));
            RectTransform progressRect = progressText.GetComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0f, 1f);
            progressRect.anchorMax = new Vector2(1f, 1f);
            progressRect.pivot = new Vector2(0.5f, 1f);
            progressRect.sizeDelta = new Vector2(-20f, 48f);
            progressRect.anchoredPosition = new Vector2(0f, -10f);

            TMP_Text statusText = NewLabel("StatusText", panelGO.transform, 16f, TextAlignmentOptions.Center, new Color(0.95f, 0.75f, 0.2f));
            RectTransform statusRect = statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.sizeDelta = new Vector2(-20f, 32f);
            statusRect.anchoredPosition = new Vector2(0f, 14f);
```

- [ ] **Step 4: Build the crosshair canvas and the pointer**

Insert before the `controllerGO` creation:

```csharp
            GameObject crosshairCanvasGO = new GameObject("FuseCrosshairCanvas");
            Canvas crosshairCanvas = crosshairCanvasGO.AddComponent<Canvas>();
            crosshairCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            crosshairCanvas.sortingOrder = 100;
            CanvasScaler crosshairScaler = crosshairCanvasGO.AddComponent<CanvasScaler>();
            crosshairScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            crosshairScaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject crosshairGO = NewUIObject("Crosshair", crosshairCanvasGO.transform);
            RectTransform crosshairRect = crosshairGO.GetComponent<RectTransform>();
            crosshairRect.anchorMin = crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
            crosshairRect.pivot = new Vector2(0.5f, 0.5f);
            crosshairRect.sizeDelta = new Vector2(8f, 8f);
            crosshairRect.anchoredPosition = Vector2.zero;
            Image crosshairImage = crosshairGO.AddComponent<Image>();
            crosshairImage.color = new Color(0.95f, 0.95f, 0.95f, 0.85f);
            crosshairImage.raycastTarget = false;
            crosshairCanvasGO.SetActive(false);

            GameObject pointerGO = new GameObject("FuseScreenPointer");
            FuseScreenPointer pointer = pointerGO.AddComponent<FuseScreenPointer>();
            pointer.Bind(raycaster);
```

The crosshair GameObject handed to the controller is `crosshairCanvasGO`, so toggling it hides the whole overlay canvas.

- [ ] **Step 5: Update the controller binding**

Replace `BindFuseMiniGameController`'s signature and body:

```csharp
        private static void BindFuseMiniGameController(FuseMiniGameController controller, Canvas worldCanvas,
            GameObject panelRoot, CanvasGroup panelGroup, GameObject crosshairRoot, FuseScreenPointer pointer,
            TMP_Text progressText, TMP_Text statusText, Button[] buttons, Image[] buttonImages)
        {
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("worldCanvas").objectReferenceValue = worldCanvas;
            so.FindProperty("panelRoot").objectReferenceValue = panelRoot;
            so.FindProperty("panelGroup").objectReferenceValue = panelGroup;
            so.FindProperty("crosshairRoot").objectReferenceValue = crosshairRoot;
            so.FindProperty("pointer").objectReferenceValue = pointer;
            so.FindProperty("progressText").objectReferenceValue = progressText;
            so.FindProperty("statusText").objectReferenceValue = statusText;

            SerializedProperty buttonsProp = so.FindProperty("buttons");
            SerializedProperty imagesProp = so.FindProperty("buttonImages");
            buttonsProp.arraySize = buttons.Length;
            imagesProp.arraySize = buttonImages.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttonsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                imagesProp.GetArrayElementAtIndex(i).objectReferenceValue = buttonImages[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
```

And its call site:

```csharp
            BindFuseMiniGameController(controller, canvas, panelGO, panelGroup, crosshairCanvasGO, pointer,
                progressText, statusText, buttons, buttonImages);
```

- [ ] **Step 6: Update the stale header comment**

The comment above `EnsureFuseMiniGameUI` claims "no TextMeshPro package installed" and "no DOTween installed". Both are false now. Replace those parenthetical clauses with:

```csharp
        // ---------- Fuse Mini Game UI. Builds the world-space canvas the minigame parents onto each
        // tablet's screen anchor, plus the screen-space crosshair overlay. TextMeshPro labels (legacy
        // Text is unreadable at world-space scale) and coroutine-driven button feedback; DOTween is
        // used only for the tablet lever, in FuseTabletPresenter. ----------
```

- [ ] **Step 7: Verify in the Editor**

Refresh Unity and check the console for compile errors — expected: none.

Open the main procedural scene, delete any existing `FuseMiniGameCanvas`, `FuseMiniGameController`, `FuseCrosshairCanvas`, and `FuseScreenPointer` objects, then run `Tools > Gloomveil > Setup Main Scene Fuse System`.

Expected: the log line "Main scene Fuse system wired: ..." appears, a world-space `FuseMiniGameCanvas` exists with a `GraphicRaycaster`, `FuseCrosshairCanvas` exists and is inactive, and selecting `FuseMiniGameController` shows every reference slot filled — World Canvas, Panel Root, Panel Group, Crosshair Root, Pointer, Progress Text, Status Text, and four Buttons/Button Images.

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/Code/Editor/MainSceneFuseSetup.cs
git commit -m "chore: build the world-space fuse minigame UI from the setup tool"
```

---

### Task 10: Author the prefabs and verify live

The only remaining work is Unity Editor authoring — the code is complete and green, but nothing has run end-to-end.

**Files:**
- Modify: `Assets/_Project/Prefabs/Player.prefab`
- Modify: `Assets/prefabs/fusetabler-game.prefab`
- Modify: the main procedural scene (whichever is open for `Tools > Gloomveil > Setup Main Scene Fuse System`)

**Interfaces:**
- Consumes: everything above.
- Produces: a playable interaction.

- [ ] **Step 1: Set up the player camera rig**

Open `Assets/_Project/Prefabs/Player.prefab`.

Add a `CinemachineBrain` component to `PlayerCamera`. Leave `DefaultBlend` at its default — `FuseTabletPresenter` overwrites it at runtime.

Create an empty child of `CameraPivot` named `CM_PlayerPOV` at local position `(0,0,0)` and local rotation `(0,0,0)`. Add a `CinemachineCamera` component. Set `Priority > Value` to `10`. Remove any procedural components Unity adds by default so the vcam has no Body and no Aim — it must inherit the pivot's pose verbatim.

Expected after this step: entering Play Mode, the camera behaves exactly as before — mouse look, movement, everything. If the view is frozen or offset, the POV vcam has a procedural component it should not.

- [ ] **Step 2: Set up the tablet prefab**

Open `Assets/prefabs/fusetabler-game.prefab`.

Add an empty child named `Anchor_Screen`, positioned flush on the `Square` mesh's visible face with its local `+Z` pointing out of the screen into the room. This is where the 0.48 × 0.36 m canvas lands, so centre it on the screen area.

Add an empty child named `Anchor_CameraView`, positioned about `0.65 m` out along `Anchor_Screen`'s `+Z`, roughly level with the screen centre and angled slightly downward so the tablet reads as being looked *at*.

Add a `CinemachineCamera` child of `Anchor_CameraView` named `CM_FuseView`, local position and rotation zero, `Priority > Value` `0`, no Body and no Aim components.

Add the `FuseTabletPresenter` component to the prefab root and fill its slots:

| Field | Value |
|---|---|
| Screen Anchor | `Anchor_Screen` |
| Fuse View Camera | `CM_FuseView` |
| Lever | the `Lever` transform |
| Active / Idle Priority | `20` / `0` |
| Blend In / Out Duration | `0.5` / `0.25` |
| Look Sensitivity | `0.08` |
| Max Yaw / Pitch Degrees | `15` / `10` |
| Lever Closed Euler | the lever's current local euler |
| Lever Open Euler | closed euler with the hinge axis rotated to the "pulled" pose |
| Lever Tween Duration | `0.6` |

- [ ] **Step 3: Re-run the scene setup tool**

Open the main procedural scene and run `Tools > Gloomveil > Setup Main Scene Fuse System` so `MapFuseGate` picks up the `CinemachineBrain` reference from the player prefab instance.

Confirm in the Inspector that `MapFuseGate`'s `Cinemachine Brain` field is populated and `Fuse Box Prefab` still points at `fusetabler-game`.

- [ ] **Step 4: Run the full automated suite**

Run the full PlayMode and EditMode suites, plus `Tools/Gloomveil/Map/Validate Module Catalog`.
Expected: all green.

- [ ] **Step 5: Play Mode acceptance pass**

Enter Play Mode on the main scene and confirm each of these. Anything that fails here is a bug to fix before the plan is done — do not tick this step on partial results.

1. Walking up to a tablet shows the interaction prompt; pressing E blends the camera to the tablet over roughly half a second.
2. The tablet fills about 60% of the frame with wall and room still visible at the edges.
3. The minigame renders on the tablet's screen surface, not as a screen overlay, and the TMP labels are sharp.
4. Moving the mouse pans the view a little and stops at the limit — it never swings free.
5. The crosshair sits at screen centre; the button under it tints; left-click registers as that button's press; the sequence, penalty, and round progression behave exactly as before.
6. The cursor never becomes visible at any point.
7. Finishing all rounds plays the lever animation, then blends back and returns control.
8. ESC mid-round blends back immediately, keeps completed rounds, and re-entering the fuse resumes at the right round with the lever closed.
9. Letting the monster kill the player mid-minigame ends the minigame, returns the camera, and leaves the flow in Death — the camera must not be stuck at the tablet.
10. Completing the *last* fuse opens the exit and the flow stays in Final Chase — it does not drop back to Exploration.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Prefabs/Player.prefab Assets/prefabs/fusetabler-game.prefab Assets/Scenes
git commit -m "feat: author the tablet camera rig and fuse view anchors"
```

---

## Self-Review

**Spec coverage:**

| Spec requirement | Task |
|---|---|
| Install Cinemachine, add `Unity.Cinemachine` reference | 1 |
| `CinemachineBrain` on `PlayerCamera`, 0.5s/0.25s blends | 1, 3, 10 |
| `CM_PlayerPOV` under `CameraPivot`, priority 10, no procedural components | 10 |
| `CM_FuseView` under `Anchor_CameraView`, priority 0/20 | 3, 10 |
| Clamped ±15°/±10° look written by hand, not `CinemachineInputAxisController` | 3 |
| ~0.65 m camera anchor, ~60% framing | 10 |
| World-space canvas, `worldCamera` set | 9, 10 |
| `Begin()` takes `screenAnchor`, reparents, fixed 0.0012 scale | 6 |
| `ClosePanel` returns the canvas to its scene-root parent | 6 |
| `darkenOverlay` removed | 6 |
| TMP text swap in the controller and the editor builder | 7, 9 |
| Minigame rules/colours/timings untouched | enforced by Global Constraints; no task edits them |
| Cursor stays locked; `PlayerController` sole owner | 2, 6 |
| `FuseScreenPointer` crosshair raycast, `onClick.Invoke()` | 8 |
| Hover = colour-only tint between dim and highlight, never overriding a pulse | 8 |
| Crosshair on its own overlay canvas | 8, 9 |
| ESC unchanged, no monster warning | untouched by design — `Update()`'s ESC branch is preserved verbatim in Task 8 |
| `IFuseViewPresenter` with `ScreenAnchor`/`Activate`/`Deactivate`/completion | 3 |
| `MapFuseGate` resolves the presenter and passes it to `FuseBox.Bind` | 5 |
| Lever DOTween on completion only | 4, 5 |
| Control restored as the return blend starts | 5 (`CompleteRoutine` deactivates, then `FinishInteraction` re-enables control) |
| Death path via `ForceCancel` → `HandleCancelled` → `Deactivate` | 5 |
| `ForceCancel()` before `ClearSpawned` | 5 |
| `Anchor_Screen` / `Anchor_CameraView` prefab children | 10 |
| Emissive screen unchanged, no idle screen state | no task touches the screen material |
| Test list from the spec | 3, 4, 5, 6, 8 (all five spec bullets covered) |

Two additions beyond the spec, both inside the code being modified: the `FinishInteraction` state guard (Task 5 — the spec's flow diagram promises Final Chase survives fuse completion, which today's guard does not deliver), and `MapFuseGateTests` fixture maintenance (Task 5, forced by the `Bind` signature change).

**Placeholder scan:** No TBD/TODO. Every code step carries the actual code. The one "figure it out on the fly" note (the `LogAssert.Expect` string in Task 5) states the exact expected message and Task 6 Step 5 updates it when the message changes.

**Type consistency:** `IFuseViewPresenter.PlayCompletion()` (not `PlayCompletionAsync`) is used identically in Tasks 3, 4, 5, and the spy in Task 5. `FuseTabletPresenter.Bind(CinemachineBrain)` matches its call in Task 5. `FuseMiniGameController.Begin(int, int, string, Transform, Action, Action, Action)` matches the `FuseBox` call site in Task 6 and the fixture in Task 6. `MapFuseGate.Bind(..., CinemachineBrain)` matches both the editor tool (Task 9) and the updated `MapFuseGateTests` (Task 5). Serialized property names used in `SerializedObject` lookups (`worldCanvas`, `panelRoot`, `panelGroup`, `crosshairRoot`, `pointer`, `progressText`, `statusText`, `buttons`, `buttonImages`, `screenAnchor`, `fuseViewCamera`, `lever`, `leverTweenDuration`, `fadeDuration`) all match their declarations.
