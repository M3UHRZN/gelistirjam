# Fuse Tablet Interaction — Design

Date: 2026-08-09
Status: Approved for implementation planning

## Context

Pressing E on a fuse currently opens the 4-button memory minigame as a `ScreenSpaceOverlay` canvas
pasted over the whole viewport. `FuseMiniGameController.OpenPanel()` dims the screen, unlocks the
cursor, and the player clicks the four coloured buttons with a free mouse pointer. The fuse visual
itself — `Assets/prefabs/fusetabler-game.prefab`, mounted on a closed wall by `MapFuseGate` — has a
`Square` mesh (an emissive screen surface) and a `Lever` mesh that no code touches.

The interaction should instead read as diegetic: the camera moves to the tablet and the minigame
plays *on the tablet's own screen*. The overlay canvas, the cursor unlock, and the screen dim all
work against that.

Relevant existing structure:

- The real player rig is `Player → CameraPivot → PlayerCamera`, driven by `PlayerController`
  (Rigidbody-based). The Rigidbody never rotates; yaw and pitch both live on `cameraPivot`.
  `TestPlayerController` is dead weight for the AI test scene and is not part of this work.
- `FuseInteractionState.Enter()` already calls `flow.SetPlayerControlEnabled(false)`, which disables
  `PlayerController` and therefore stops `HandleLook()` from writing to `cameraPivot`.
- A single shared `FuseMiniGameController` lives in the scene. `FuseBox.BeginInteract()` guards
  against a second concurrent interaction, so exactly one fuse is ever active.
- Cinemachine is **not** in `Packages/manifest.json`. DOTween **is** present as a plain DLL at
  `Assets/Plugins/Demigiant/DOTween`, auto-referenced by `Game.Gameplay` (`overrideReferences` is
  false), so it needs no asmdef change.

## Decisions

### Camera — Cinemachine

Install `com.unity.cinemachine` (3.x) and add `Unity.Cinemachine` to `Game.Gameplay.asmdef`.

- `PlayerCamera` gets a `CinemachineBrain`. Blends: **0.5s EaseInOut** into the fuse view, **0.25s**
  back out, via a custom blend asset.
- `CM_PlayerPOV` is a child of `CameraPivot` with an identity local transform and no procedural
  Body/Aim components, so it simply inherits the pivot's pose. Priority 10. `PlayerController`
  keeps driving `cameraPivot` exactly as it does today — the look system is not rewired.
- `CM_FuseView` lives on the tablet prefab under a new `Anchor_CameraView` child. Priority 0 while
  idle, 20 while active.
- Framing target: the tablet occupies roughly 60% of the viewport with wall and room visible at the
  edges, so the player keeps some peripheral awareness. `Anchor_CameraView` is authored by hand in
  the prefab at approximately 0.65 m in front of the screen surface, angled slightly downward.

Cinemachine is chosen over a hand-written camera lerp because the blend curves, priority-driven
transitions, and live retargeting are exactly the mechanism needed, and because this rig keeps
yaw/pitch on the pivot rather than on the camera transform — a `CinemachineBrain` on `PlayerCamera`
does not conflict with `PlayerController`.

`CinemachineInputAxisController` is **not** used for the limited look. `PlayerController.OnDisable()`
calls `actions.Player.Disable()`, and an `InputActionReference` resolves against the shared asset's
own instance rather than that clone — a working-but-confusing lifetime coupling. Instead
`FuseTabletPresenter` drives the clamped look itself: it reads `Mouse.current.delta`, clamps to
±15° yaw and ±10° pitch from the anchor's rest orientation, and rotates the vcam locally. Reading
devices directly matches how the rest of the gameplay code already works. Pan/tilt resets to zero
on every `Activate()`.

### Screen — world-space canvas

The single shared controller and its single canvas are kept. Copying the canvas into every tablet
prefab would mean N canvases and N controllers for an invariant that already guarantees one active
fuse at a time.

- The canvas becomes `RenderMode.WorldSpace` with `worldCamera = PlayerCamera`.
- `FuseMiniGameController.Begin()` takes a new `Transform screenAnchor` parameter. On open it
  reparents the canvas with `SetParent(screenAnchor, false)`, zeroes local position and rotation,
  and applies a fixed local scale (400×300 UI units ≈ 0.48×0.36 m).
- `ClosePanel()` returns the canvas to its own scene-root parent. This is not cosmetic: `MapFuseGate.ClearSpawned()`
  destroys fuse instances when a new map is published, and a canvas still parented under one would
  be destroyed with it.
- `darkenOverlay` is removed. The camera framing already isolates the screen, and dimming a
  world-space surface has no meaning.
- `progressText` and `statusText` migrate from legacy `UnityEngine.UI.Text` to `TMP_Text`. Legacy
  Text is unreadably soft at 0.65 m in world space, and TextMesh Pro is already in the project.
  This touches two fields and two setters in the controller plus the canvas construction in
  `Assets/_Project/Code/Editor/MainSceneFuseSetup.cs`.
- Button colours, pulse and flash animations, timing fields, sequence generation, and the minigame
  state machine are unchanged.

### Input — crosshair and click

- The cursor stays locked for the whole interaction. `PlayerController.OnDisable()` currently
  unlocks and shows the cursor whenever `manageCursor` is true, which fires the moment
  `FuseInteractionState` disables control. That unlock is removed; the lock in `OnEnable()` stays.
  The cursor code in `FuseMiniGameController.OpenPanel()`/`ClosePanel()` is removed too, leaving
  `PlayerController` as the single cursor owner (its own tooltip already anticipates a future pause
  menu taking that role).
- A new `FuseScreenPointer` component raycasts each frame from the viewport centre through the
  canvas's `GraphicRaycaster` (a `PointerEventData` positioned at screen centre), finds the `Button`
  under the crosshair, applies a hover visual, and on `Mouse.current.leftButton.wasPressedThisFrame`
  calls `button.onClick.Invoke()`. Existing `onClick` listeners wired in `Awake()` keep working
  untouched, so button-index handling and duplicate-listener safety are unaffected.
- The hover visual is a colour-only tint: the hovered button is set to its base colour times a new
  serialized `hoverBrightness`, sitting between `dimBrightness` and `highlightBrightness`. No scale
  change, so hover can never be confused with a sequence pulse, and hover never overrides an
  in-progress pulse/flash tween on the same button.
- The crosshair is a separate small `ScreenSpaceOverlay` canvas owned by the minigame controller —
  the world-space canvas cannot host it. Shown on open, hidden on close.
- ESC keeps its current behaviour in `FuseMiniGameController.Update()`. No monster-proximity warning
  and no auto-cancel: the player is deliberately blind during the minigame.

### Flow and integration

`FuseBox` must not gain a hard reference to Cinemachine. A new `IFuseViewPresenter` interface
exposes `Transform ScreenAnchor`, `Activate()`, `Deactivate()`, and `PlayCompletionAsync()`. The
concrete `FuseTabletPresenter` lives on the tablet prefab and owns the vcam priority, the clamped
look, and the lever. `MapFuseGate` reads the presenter off the instantiated prefab and passes it
through `fuseBox.Bind(...)` alongside the existing dependencies.

```
E → FuseBox.BeginInteract()
     ├ GameFlow → FuseInteractionState (control off; POV vcam freezes with the pivot)
     ├ presenter.Activate()            → CM_FuseView priority 20, 0.5s blend in
     └ miniGame.Begin(..., presenter.ScreenAnchor)

complete → presenter.PlayCompletionAsync()  → DOTween lever rotation, ~0.6s
        → presenter.Deactivate()            → 0.25s blend out
        → FinishInteraction()               → ExplorationState

ESC / death → HandleCancelled() → presenter.Deactivate() (no lever) → same blend out
```

Control is restored at the same moment the return blend starts, not after it. The POV vcam follows
`CameraPivot`, so the camera settles smoothly while the player is already free to move — better than
holding them still through a blend they cannot influence.

The death path is unchanged in shape: `MainSceneWiring` calls `fuseMiniGame.ForceCancel()` when the
flow enters `DeathState`, which reaches `FuseBox.HandleCancelled()` and therefore
`presenter.Deactivate()`. `FinishInteraction()` keeps its existing `DeathState` guard.

`MapFuseGate.ClearSpawned()` calls `ForceCancel()` before destroying instances, so a regenerated map
cannot destroy the tablet out from under an open minigame.

### Prefab authoring

`Assets/prefabs/fusetabler-game.prefab` gains two empty children:

- `Anchor_Screen` — aligned to the `Square` surface with +Z facing out of the screen.
- `Anchor_CameraView` — the fuse view camera pose described above.

`FuseTabletPresenter` serializes the lever's closed and open local rotations and the completion tween
duration. The screen material is already emissive and needs no change; no idle/standby screen state
is built.

## Data flow

1. `PlayerInteraction` raycasts a `FuseBox` and E is pressed.
2. `FuseBox.BeginInteract()` sets state Active, pushes `FuseInteractionState`, raises
   `OnInteractionStarted`, activates the presenter, and starts the minigame with the screen anchor.
3. `CinemachineBrain` blends `PlayerCamera` from `CM_PlayerPOV` to `CM_FuseView`.
4. `FuseMiniGameController` reparents the world-space canvas to `Anchor_Screen` and runs its existing
   round loop; `FuseScreenPointer` translates crosshair-plus-click into button presses.
5. On full completion the presenter plays the lever tween, then deactivates; on ESC or death it
   deactivates immediately.
6. `FuseBox.FinishInteraction()` returns the flow to `ExplorationState` unless the flow is in
   `DeathState`, and raises `OnInteractionStopped`.

## Scope

In scope:

- Cinemachine installation, brain/vcam setup, and blend configuration.
- `FuseTabletPresenter`, `IFuseViewPresenter`, `FuseScreenPointer`.
- World-space canvas migration and the TMP text swap inside the minigame UI.
- Cursor ownership fix in `PlayerController` and `FuseMiniGameController`.
- Lever completion tween via DOTween.
- Prefab anchors and `MapFuseGate` presenter wiring.
- PlayMode smoke tests and live Unity verification.

Out of scope:

- Changing the minigame's rules, sequence length, round counts, timings, or colours.
- Monster-proximity warnings, screen static, auto-cancel, or any idle/standby screen state.
- 3D mesh buttons or a RenderTexture screen pipeline.
- Migrating `TestPlayerController`, the AI test scene, or any other camera in the project to
  Cinemachine.
- Gamepad bindings, HUD changes, and audio.

## Testing and acceptance

PlayMode tests under `Assets/_Project/Code/Tests/PlayMode`:

- `Activate()` raises the fuse view above the POV vcam; `Deactivate()` restores POV priority.
- The canvas is parented to the supplied screen anchor on `Begin()` and returned to its scene-root
  parent on close, for both the completion and the cancel path.
- `FuseScreenPointer` resolves the button under the viewport centre and a left-click invokes that
  button's `onClick`.
- A cancelled fuse does not play the lever tween; a completed one does.
- The cursor remains locked across a full begin-complete cycle and across a begin-cancel cycle.

Unity verification: scripts compile clean, the module catalog still validates, and a Play Mode pass
on a generated main-scene map confirms the camera blend framing, readable TMP text on the tablet
screen, crosshair button presses, the lever animation, ESC bail-out, and a monster kill landing
mid-minigame without leaving the camera stuck at the tablet.
