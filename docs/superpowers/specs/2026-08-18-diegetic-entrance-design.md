# Diegetic Entrance Sequence Design

## Goal

Build the code-only foundation for a short `Enterance` scene that never presents itself as a tutorial. Every New Game will eventually follow `MainMenu -> Enterance -> MainGame`. The mandatory path is to solve one real fuse memory minigame, walk through the powered exit, and enter `MainGame`. A side-room `DON'T` button starts an optional, controlled monster lesson.

The user is authoring the map manually. This implementation must not edit `Assets/Scenes/Enterance.unity`, any prefab, Build Settings, or scene references until the user says the map is ready.

## Player Experience

### Mandatory route

1. The player explores the authored entrance space without tutorial overlays.
2. The player uses the existing `FuseBox` and real `FuseMiniGameController` flow.
3. Completing the fuse opens the authored exit door and enables its portal trigger.
4. Crossing the portal assigns a fresh generated-map seed and loads `MainGame`.

### Optional monster lesson

1. The player may ignore the side-room button and finish the mandatory route.
2. Interacting with the button labelled `DON'T` opens the corridor door and starts one deterministic trial.
3. A lightweight monster actor follows the corridor NavMesh toward the player. It does not run `MonsterBrain`, procedural-map gates, stalking, anger, SafeZone, or full death presentation.
4. The existing `PlayerGazeDetector` identifies the actor's `MonsterGazeReceiver`.
5. If the player looks into its eyes before contact, the actor retreats to its configured point, disappears, the corridor door closes, and the button remains disabled for the rest of that scene instance.
6. If the actor reaches the player, player control is disabled, an optional caught presentation runs, and the active `Enterance` scene reloads. Reloading resets the fuse, doors, button, monster, and player together.

## Architecture

All runtime code lives under `Assets/_Project/Code/Gameplay/Entrance/` in namespace `Game.Gameplay.Entrance`.

### `EntranceMonsterTrialStateMachine`

A pure C# state model with `Ready`, `Approaching`, `Retreating`, `PlayerCaught`, and `Completed` states. It accepts only legal transitions and makes the one-shot rule explicit. Setup failure may abort `Approaching -> Ready`; successful completion has no transition back to `Ready`.

### `EntranceDoorActuator`

A reusable scene-authored door driver. It records a configured moving transform's closed local position and rotation, then animates to Inspector-defined local position and Euler offsets. It toggles configured blocker colliders only after closing or at the start of opening. Public immediate mode supports deterministic initialization and EditMode tests.

### `EntranceFuseExitController`

Subscribes to one real `FuseBox.OnCompleted`. At initialization it keeps the exit door closed and portal collider disabled unless that fuse is already complete. Completion idempotently opens the door and enables the portal. It owns no minigame logic.

### `EntranceScenePortal`

A trigger relay that accepts only colliders belonging to `PlayerController`, assigns `MapGenerationBootstrap.NextRunSeed`, and loads the serialized destination scene exactly once. Its default destination is `MainGame`.

### `EntranceMonsterTrialButton`

Implements the existing `IInteractable` contract. Its prompt is serialized with default `DON'T`. It is interactable only while the trial state is `Ready`, and forwards the interaction to the trial controller.

### `EntranceMonsterTrialController`

Owns runtime side effects for the optional encounter. Serialized references provide the player, gaze detector, gaze receiver, NavMeshAgent, monster root, start/retreat points, corridor door, and scene resetter. Inspector fields control approach speed, retreat speed, path refresh interval, contact distance, NavMesh sample radius, and optional presentation events.

The controller activates and warps the lightweight actor onto the corridor NavMesh only after the button is pressed. During `Approaching`, it refreshes the destination toward the player, gives gaze priority over contact in the same frame, and transitions to retreat when `PlayerGazeDetector.CurrentTarget` equals the configured receiver. During `Retreating`, arrival at the configured retreat point completes the one-shot trial.

The controller must fail safely. Missing references or failure to sample/warp onto NavMesh logs one clear error, restores the door/actor to idle, and returns the state to `Ready` so the player is not permanently locked out by authoring error.

### `EntranceSceneResetter`

Disables player control, optionally fades a serialized `CanvasGroup` with unscaled time, waits a serialized delay, and reloads the active scene. Re-entry is ignored while a reset is already running.

## Monster Asset Contract

The production `Assets/_Project/Prefabs/Monster.prefab` is a full inactive `MonsterRuntimeRig` and must not be activated uninitialized. When the map is ready, create or author a lightweight entrance variant using the visual model/Animator plus `NavMeshAgent`, `MonsterGazeReceiver`, colliders, and the new entrance controller. Full `MonsterBrain`, `MonsterRuntimeRig`, `MapMonsterGate`, SafeZone, StalkPoint, anger, and procedural-map bindings are intentionally excluded from this trial.

## Scene-Wiring Contract (Deferred)

When the user says the map is ready:

- Place/bind the existing Player prefab and its camera gaze detector.
- Place/bind the real fuse prefab, minigame UI, `GameFlowController`, and `ObjectiveManager` required by `FuseBox`.
- Bind the authored main exit door and its trigger portal.
- Bind the side button, corridor door, lightweight monster actor, start point, retreat point, and reset fade if present.
- Change the Main Menu New Game destination to `Enterance`.
- Add `Enterance` to Build Settings before `MainGame` or otherwise ensure name-based loading succeeds.
- Keep all placement, offsets, speeds, distances, prompt copy, scene names, and animation/audio hooks serialized for Inspector tuning.

## Failure and Reset Rules

- Fuse cancellation uses the existing minigame behavior and does not unlock the exit.
- Repeated fuse completion notifications do not replay or duplicate exit opening.
- Repeated button input after trial start is ignored.
- Successful gaze is permanent for the current scene instance: the button stays disabled.
- Player contact before gaze causes a full scene reload rather than a partial object-by-object reset.
- Missing NavMesh or invalid trial references never kill or trap the player; the optional trial returns to `Ready` and the mandatory fuse route remains usable.
- Portal entry is guarded so overlapping player colliders cannot request multiple scene loads.

## Verification

Code-only acceptance before map wiring:

- EditMode tests prove every legal and illegal monster-trial state transition, including the one-shot completion rule and setup-abort retry.
- EditMode tests prove door immediate open/close poses and blocker state.
- EditMode tests prove fuse exit unlock is idempotent and toggles the portal.
- EditMode tests prove the button delegates only while the trial is ready.
- Unity compiles with zero new errors.
- Focused entrance tests pass independently of the repository's pre-existing `MonsterAnimationAssetTests` animation-name mismatch.
- `Assets/Scenes/Enterance.unity`, prefabs, and Build Settings remain untouched in the code-only phase.

Runtime acceptance is deferred until the map is ready: real fuse completion, door traversal into `MainGame`, NavMesh approach/retreat, gaze success, caught reset, and one-shot button behavior must then be exercised in Play Mode.
