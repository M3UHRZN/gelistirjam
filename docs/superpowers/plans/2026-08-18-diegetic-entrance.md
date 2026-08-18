# Diegetic Entrance Sequence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the code-only foundation for the mandatory fuse exit and optional one-shot monster corridor trial in `Enterance`, without modifying the scene or prefabs.

**Architecture:** Focused `Game.Gameplay.Entrance` components compose existing `FuseBox`, `PlayerGazeDetector`, `MonsterGazeReceiver`, `PlayerController`, and `NavMeshAgent` APIs. A pure trial state machine owns legal transitions; MonoBehaviours own door, navigation, scene transition, and presentation side effects. Full `MonsterBrain` and procedural-map gates are deliberately excluded.

**Tech Stack:** Unity 6000.3.13f1, C#, Unity Test Framework/NUnit, built-in NavMesh, existing `Game.Gameplay` assembly.

**Spec:** `docs/superpowers/specs/2026-08-18-diegetic-entrance-design.md`

## Global Constraints

- Do not edit `Assets/Scenes/Enterance.unity`, any prefab, `ProjectSettings/EditorBuildSettings.asset`, or existing scene references until the user says the map is ready.
- Work in `Assets/_Project/Code/Gameplay/Entrance/` and namespace `Game.Gameplay.Entrance`.
- Use the real `FuseBox` completion event and the real player gaze marker contract; do not duplicate the fuse minigame or raycast logic.
- Do not activate the production `MonsterRuntimeRig` without its full initialization. The corridor controller targets a lightweight actor containing `NavMeshAgent`, `MonsterGazeReceiver`, visual/Animator, and colliders.
- Keep placement, door offsets, speeds, distances, prompt text, animation hooks, and destination scene names serialized for later Inspector tuning.
- Use test-first RED/GREEN cycles. Run only focused EditMode tests while the user is actively authoring the scene; full PlayMode acceptance waits for map-ready wiring.
- Preserve the unrelated baseline failure in `MonsterAnimationAssetTests` (`Yelling` expected, `Look Away Gesture` present).

---

## File Map

- Create `Assets/_Project/Code/Gameplay/Entrance/EntranceMonsterTrialStateMachine.cs`: pure one-shot state transitions.
- Create `Assets/_Project/Code/Gameplay/Entrance/EntranceDoorActuator.cs`: local-pose door animation and blocker control.
- Create `Assets/_Project/Code/Gameplay/Entrance/EntranceFuseExitController.cs`: real fuse completion to exit unlock.
- Create `Assets/_Project/Code/Gameplay/Entrance/EntranceScenePortal.cs`: guarded Player-only transition to `MainGame` with fresh seed.
- Create `Assets/_Project/Code/Gameplay/Entrance/EntranceSceneResetter.cs`: caught fade/control lock/current-scene reload.
- Create `Assets/_Project/Code/Gameplay/Entrance/EntranceMonsterTrialButton.cs`: existing `IInteractable` adapter.
- Create `Assets/_Project/Code/Gameplay/Entrance/EntranceMonsterTrialController.cs`: deterministic NavMesh approach/retreat/caught orchestration.
- Create `Assets/_Project/Code/Tests/EditMode/EntranceMonsterTrialStateMachineTests.cs`: state transition tests.
- Create `Assets/_Project/Code/Tests/EditMode/EntranceDoorAndExitTests.cs`: immediate door pose, blocker, and exit unlock tests.
- Create `Assets/_Project/Code/Tests/EditMode/EntranceInteractionTests.cs`: button gating and portal duplicate-load tests.

### Task 1: Pure Monster Trial State Machine

**Files:**
- Create: `Assets/_Project/Code/Tests/EditMode/EntranceMonsterTrialStateMachineTests.cs`
- Create: `Assets/_Project/Code/Gameplay/Entrance/EntranceMonsterTrialStateMachine.cs`

**Interfaces:**
- Produces: `EntranceMonsterTrialState State { get; }`
- Produces: `bool TryBegin()`, `bool TryRepel()`, `bool TryCatchPlayer()`, `bool TryCompleteRetreat()`, `bool TryAbortSetup()`

- [ ] **Step 1: Write the failing state-transition tests**

```csharp
using Game.Gameplay.Entrance;
using NUnit.Framework;

namespace Game.Gameplay.Tests
{
    public sealed class EntranceMonsterTrialStateMachineTests
    {
        [Test]
        public void BeginThenRepelThenComplete_IsOneShot()
        {
            var trial = new EntranceMonsterTrialStateMachine();

            Assert.That(trial.TryBegin(), Is.True);
            Assert.That(trial.TryRepel(), Is.True);
            Assert.That(trial.TryCompleteRetreat(), Is.True);
            Assert.That(trial.State, Is.EqualTo(EntranceMonsterTrialState.Completed));
            Assert.That(trial.TryBegin(), Is.False);
        }

        [Test]
        public void CatchPlayer_IsLegalOnlyWhileApproaching()
        {
            var trial = new EntranceMonsterTrialStateMachine();

            Assert.That(trial.TryCatchPlayer(), Is.False);
            Assert.That(trial.TryBegin(), Is.True);
            Assert.That(trial.TryCatchPlayer(), Is.True);
            Assert.That(trial.State, Is.EqualTo(EntranceMonsterTrialState.PlayerCaught));
            Assert.That(trial.TryRepel(), Is.False);
        }

        [Test]
        public void SetupAbort_ReturnsApproachingTrialToReady()
        {
            var trial = new EntranceMonsterTrialStateMachine();

            Assert.That(trial.TryBegin(), Is.True);
            Assert.That(trial.TryAbortSetup(), Is.True);
            Assert.That(trial.State, Is.EqualTo(EntranceMonsterTrialState.Ready));
            Assert.That(trial.TryBegin(), Is.True);
        }
    }
}
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run through Unity MCP EditMode with test name `Game.Gameplay.Tests.EntranceMonsterTrialStateMachineTests`.

Expected: compilation/test failure because `Game.Gameplay.Entrance.EntranceMonsterTrialStateMachine` does not exist.

- [ ] **Step 3: Implement the minimal pure state machine**

```csharp
namespace Game.Gameplay.Entrance
{
    public enum EntranceMonsterTrialState
    {
        Ready,
        Approaching,
        Retreating,
        PlayerCaught,
        Completed,
    }

    public sealed class EntranceMonsterTrialStateMachine
    {
        public EntranceMonsterTrialState State { get; private set; } = EntranceMonsterTrialState.Ready;

        public bool TryBegin() => TryTransition(EntranceMonsterTrialState.Ready, EntranceMonsterTrialState.Approaching);
        public bool TryRepel() => TryTransition(EntranceMonsterTrialState.Approaching, EntranceMonsterTrialState.Retreating);
        public bool TryCatchPlayer() => TryTransition(EntranceMonsterTrialState.Approaching, EntranceMonsterTrialState.PlayerCaught);
        public bool TryCompleteRetreat() => TryTransition(EntranceMonsterTrialState.Retreating, EntranceMonsterTrialState.Completed);
        public bool TryAbortSetup() => TryTransition(EntranceMonsterTrialState.Approaching, EntranceMonsterTrialState.Ready);

        private bool TryTransition(EntranceMonsterTrialState expected, EntranceMonsterTrialState next)
        {
            if (State != expected) return false;
            State = next;
            return true;
        }
    }
}
```

- [ ] **Step 4: Run focused tests and verify GREEN**

Expected: 3 passed, 0 failed, and no new console errors.

- [ ] **Step 5: Commit the state-machine slice**

```powershell
git add -- Assets/_Project/Code/Gameplay/Entrance/EntranceMonsterTrialStateMachine.cs Assets/_Project/Code/Tests/EditMode/EntranceMonsterTrialStateMachineTests.cs
git commit -m "feat: add entrance monster trial state machine"
```

### Task 2: Reusable Door and Fuse-Gated Exit

**Files:**
- Create: `Assets/_Project/Code/Tests/EditMode/EntranceDoorAndExitTests.cs`
- Create: `Assets/_Project/Code/Gameplay/Entrance/EntranceDoorActuator.cs`
- Create: `Assets/_Project/Code/Gameplay/Entrance/EntranceFuseExitController.cs`

**Interfaces:**
- Produces: `EntranceDoorActuator.Bind(Transform, Vector3, Vector3, float, Collider[])`
- Produces: `EntranceDoorActuator.SetOpen(bool open, bool immediate = false)` and `bool IsOpen`
- Produces: `EntranceFuseExitController.Bind(FuseBox, EntranceDoorActuator, Collider)` and `UnlockExit()`

- [ ] **Step 1: Write failing immediate-pose and idempotent-unlock tests**

Tests create a parent door object, a moving child, blocker collider, portal collider, and an incomplete `FuseBox`. Configure zero travel duration. Assert that immediate open applies the exact local position and rotation offsets and disables blockers; immediate close restores the captured pose and enables blockers. Bind the exit controller, call `UnlockExit()` twice, and assert `IsUnlocked`, door-open state, and enabled portal without duplicated side effects.

```csharp
Assert.That(moving.localPosition, Is.EqualTo(closedPosition + new Vector3(0f, 3f, 0f)));
Assert.That(Quaternion.Angle(moving.localRotation, closedRotation * Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f));
Assert.That(blocker.enabled, Is.False);
Assert.That(portal.enabled, Is.True);
```

- [ ] **Step 2: Run `EntranceDoorAndExitTests` and verify RED**

Expected: compile failure because the door and exit types do not exist.

- [ ] **Step 3: Implement `EntranceDoorActuator`**

Capture the closed local pose once. `SetOpen` cancels any existing motion; opening disables blockers before movement, closing enables them only after the closed pose is reached. The open rotation is composed as `closedLocalRotation * Quaternion.Euler(openLocalEulerOffset)` so untouched axes are preserved. Zero duration or `immediate=true` applies deterministically without a coroutine.

- [ ] **Step 4: Implement `EntranceFuseExitController`**

Subscribe/unsubscribe idempotently in `OnEnable`, `OnDisable`, and `Bind`. Initial binding closes/disables the exit unless `fuse.State == FuseBoxState.Complete`. `UnlockExit()` returns immediately after the first call, then opens the door and enables the portal collider.

- [ ] **Step 5: Run focused tests and verify GREEN**

Expected: all door/exit tests pass with zero new console errors.

- [ ] **Step 6: Commit the door/exit slice**

```powershell
git add -- Assets/_Project/Code/Gameplay/Entrance/EntranceDoorActuator.cs Assets/_Project/Code/Gameplay/Entrance/EntranceFuseExitController.cs Assets/_Project/Code/Tests/EditMode/EntranceDoorAndExitTests.cs
git commit -m "feat: add fuse-gated entrance exit"
```

### Task 3: Guarded Scene Loading and Full-Scene Reset

**Files:**
- Create: `Assets/_Project/Code/Tests/EditMode/EntranceInteractionTests.cs`
- Create: `Assets/_Project/Code/Gameplay/Entrance/EntranceScenePortal.cs`
- Create: `Assets/_Project/Code/Gameplay/Entrance/EntranceSceneResetter.cs`

**Interfaces:**
- Produces: public `IEntranceSceneLoader.Load(string sceneName)` for a small fake in EditMode tests.
- Produces: `EntranceScenePortal.Bind(string, IEntranceSceneLoader, Func<int>)` and `bool TryLoad()`.
- Produces: `EntranceSceneResetter.Bind(PlayerController, CanvasGroup, IEntranceSceneLoader)` and `bool BeginReset()`.

- [ ] **Step 1: Write the failing portal duplicate-load test**

Create a fake loader that records calls and inject a fixed seed source. Assert that the first `TryLoad()` returns true, stores the fixed seed in `MapGenerationBootstrap.NextRunSeed`, and requests `MainGame`; a second call returns false and does not load again.

- [ ] **Step 2: Run the interaction tests and verify RED**

Expected: compile failure because the transition APIs do not exist.

- [ ] **Step 3: Implement the portal and loader seam**

`OnTriggerEnter` accepts only `other.GetComponentInParent<PlayerController>() != null`, then calls `TryLoad()`. The default loader delegates to the reflected Unity API `SceneManager.LoadScene(string)`. Default seed generation uses `UnityEngine.Random.Range(int.MinValue, int.MaxValue)`.

- [ ] **Step 4: Implement the resetter**

`BeginReset()` rejects re-entry, disables player control via `SetControlEnabled(false)`, and starts an unscaled-time fade/delay coroutine. The coroutine asks the same scene-loader seam to load `SceneManager.GetActiveScene().name`. A missing CanvasGroup skips fading rather than blocking the reset.

- [ ] **Step 5: Run interaction tests and verify GREEN**

Expected: portal duplicate-load test passes. Reset coroutine receives compile/console verification now and PlayMode coverage after scene wiring.

- [ ] **Step 6: Commit the transition/reset slice**

```powershell
git add -- Assets/_Project/Code/Gameplay/Entrance/EntranceScenePortal.cs Assets/_Project/Code/Gameplay/Entrance/EntranceSceneResetter.cs Assets/_Project/Code/Tests/EditMode/EntranceInteractionTests.cs
git commit -m "feat: add entrance transitions and reset"
```

### Task 4: Optional `DON'T` Monster Trial

**Files:**
- Modify: `Assets/_Project/Code/Tests/EditMode/EntranceInteractionTests.cs`
- Create: `Assets/_Project/Code/Gameplay/Entrance/EntranceMonsterTrialButton.cs`
- Create: `Assets/_Project/Code/Gameplay/Entrance/EntranceMonsterTrialController.cs`

**Interfaces:**
- Produces: `IEntranceMonsterTrialStarter` with `EntranceMonsterTrialState State` and `bool TryStart()`.
- Produces: `EntranceMonsterTrialButton.Bind(IEntranceMonsterTrialStarter)` and the existing `IInteractable` members.
- Produces: `EntranceMonsterTrialController.TryStart()` plus serialized scene-authoring fields.

- [ ] **Step 1: Add failing button-gating tests**

Inject a fake trial starter. Assert the default prompt is `DON'T`, `CanInteract` is true only in `Ready`, `BeginInteract()` forwards once, and further interactions do nothing once the fake reports `Approaching` or `Completed`.

- [ ] **Step 2: Run the button tests and verify RED**

Expected: compile failure because the button/starter contract does not exist.

- [ ] **Step 3: Implement the button adapter**

Require a collider, implement `IInteractable`, keep `EndInteract()` empty, and resolve the injected test starter before the serialized concrete controller.

- [ ] **Step 4: Implement deterministic NavMesh trial orchestration**

The controller lives on a separate coordinator object, not on the inactive monster actor. It owns an `EntranceMonsterTrialStateMachine` and serialized references to player, gaze detector/receiver, agent, monster root, start/retreat points, door, resetter, and optional Animator/UnityEvents.

`TryStart()` performs this order:

1. Enter `Approaching` through the state machine.
2. Validate all required references.
3. `NavMesh.SamplePosition` around the start point.
4. Activate the lightweight actor, call reflected `NavMeshAgent.Warp(Vector3)`, and verify `isOnNavMesh`.
5. Calculate a cached `NavMeshPath` to the sampled player target and require `PathComplete` before assigning it.
6. Open the corridor door and set the approach speed/destination.
6. On any setup failure, stop/deactivate the actor, close the door immediately, `TryAbortSetup()`, and log one actionable error.

During `Approaching`, compare gaze before distance contact. If `gazeDetector.CurrentTarget == gazeReceiver`, enter `Retreating`, set retreat presentation, sample the retreat destination, require a complete path, and complete immediately with a warning if no retreat path can be established. Otherwise refresh the sampled player destination at the serialized interval only when `NavMeshAgent.CalculatePath` returns `PathComplete`, and catch when planar distance is within `contactDistance`.

During `Retreating`, complete when the agent is on NavMesh, has no pending path, and `remainingDistance <= stoppingDistance`. Completion stops/deactivates the actor, closes the door, raises completion presentation, and leaves state permanently `Completed`. Catch stops the agent, triggers optional attack presentation, disables player control, and calls `EntranceSceneResetter.BeginReset()`.

- [ ] **Step 5: Run all focused entrance EditMode tests and verify GREEN**

Run these three fixtures together:

```text
Game.Gameplay.Tests.EntranceMonsterTrialStateMachineTests
Game.Gameplay.Tests.EntranceDoorAndExitTests
Game.Gameplay.Tests.EntranceInteractionTests
```

Expected: all pass with zero new console errors. Do not run or modify the unrelated failing animation asset test.

- [ ] **Step 6: Validate every new script and inspect the Unity console**

Use Unity MCP `validate_script` for all seven runtime scripts, wait until `mcpforunity://editor/state` reports `ready_for_tools`, then read error/warning console entries. Expected: no compile errors attributable to entrance code.

- [ ] **Step 7: Confirm scope fence and commit**

Run `git status --short` and `git diff --name-only HEAD`. Confirm no scene, prefab, Build Settings, or user-authored map file is included. Stage only entrance runtime/test files and the plan.

```powershell
git add -- docs/superpowers/plans/2026-08-18-diegetic-entrance.md Assets/_Project/Code/Gameplay/Entrance Assets/_Project/Code/Tests/EditMode/EntranceMonsterTrialStateMachineTests.cs Assets/_Project/Code/Tests/EditMode/EntranceDoorAndExitTests.cs Assets/_Project/Code/Tests/EditMode/EntranceInteractionTests.cs
git commit -m "feat: add diegetic entrance runtime"
```

### Task 5: Code-Only Acceptance Report

**Files:**
- No production changes.

**Interfaces:**
- Consumes: focused test results, console output, and Git diff.
- Produces: explicit handoff contract for later map-ready scene wiring.

- [ ] **Step 1: Re-run focused entrance EditMode tests**

Expected: all entrance tests pass, independent of the known repository-wide animation mismatch.

- [ ] **Step 2: Recheck repository scope**

Confirm `Assets/Scenes/Enterance.unity`, every prefab, and `ProjectSettings/EditorBuildSettings.asset` have no Codex-authored changes.

- [ ] **Step 3: Report deferred runtime verification accurately**

Report code compilation and focused EditMode evidence as verified. State that real fuse completion, authored door movement, `MainGame` transition, NavMesh approach/retreat, gaze success, caught reset, animation/audio, and one-shot runtime behavior remain unverified until the user says the map is ready and scene wiring is performed.

### Task 6: Map-Ready Direct Scene Wiring

**Authorization:** The user said `hazır` on 2026-08-18. The deferred scene/prefab/Build Settings fence is now open for this task only. Preserve the authored geometry and placement; add runtime bindings around it. During execution the user explicitly replaced the setup-script/TDD route with direct live Unity MCP wiring and said they will perform the Play Mode test themselves. Do not add a permanent editor builder or setup fixture, do not run automated tests, and do not enter Play Mode in this task.

**Files:**
- Create: `Assets/_Project/Code/Gameplay/Entrance/EntranceFuseRuntimeBinder.cs`
- Modify: `Assets/Scenes/Enterance.unity`
- Modify: `Assets/prefabs/MainMenuTablet.prefab`
- Modify: `Assets/_Project/Prefabs/Exit.prefab`
- Modify: `ProjectSettings/EditorBuildSettings.asset`
- Modify: this plan and `.superpowers/sdd/2026-08-18-diegetic-entrance/progress.md`
- Create: the baked `Enterance` NavMesh data asset and Unity-generated `.meta` files required by these scene references

**Interfaces and scene contract:**
- Consumes: the pinned live `gelistirjam@fcf7d29f` Editor with active scene exactly `Assets/Scenes/Enterance.unity`; mutations are performed directly through Unity Editor APIs, never by patching scene YAML.
- Consumes: existing `Player`, `Player/CameraPivot/PlayerCamera`, `fusetabler-game`, `exitdoor`, `exitdoor/exitdoorv3`, `exitdoor/blocker`, `exitdoor/trigger`, `mechanicdoor`, and corridor anchor objects.
- Produces: one real `FuseBox` plus fully bound `FuseMiniGameController`, `GameFlowController`, and `ObjectiveManager`; `EntranceFuseRuntimeBinder` restores the non-serialized `IFuseViewPresenter` composition in `Awake`; one fuse completion opens the authored exit and enables the guarded `MainGame` portal.
- Produces: one lightweight corridor monster actor containing only the visual/Animator, `NavMeshAgent`, `MonsterGazeReceiver`, and colliders; never add or activate `MonsterBrain`, `MonsterRuntimeRig`, `MapMonsterGate`, or procedural-map dependencies.
- Produces: one `DON'T` interactable, one corridor-door actuator, one reset fade/resetter, and one trial controller bound to authored start/retreat points.
- Produces: `MainMenu -> Enterance -> MainGame` routing and an enabled Build Settings entry for `Enterance` before `MainGame`.

- [x] **Step 1: Preserve and inspect the authored scene**

Preserve the current user-authored `Enterance.unity` in the ignored SDD workspace before mutation. Inspect the live authored hierarchy and refuse mutation if the exact player/tablet/door/corridor anchors are missing. The preserved pre-wiring copy has SHA256 `7331135C1E8D0BDE5C73C93687348B7005A246DC5FB6A1E1DA0C2CC91696B02D`.

- [x] **Step 2: Wire the authored scene directly through Unity MCP**

With the exact Editor instance pinned, disable only the duplicate root camera. Add the one-round fuse/UI/core binding, persistent runtime binder, safe authored exit actuator and locked `MainGame` portal, corridor door, `DON'T` button, lightweight raw-model actor, dedicated eye-height gaze collider, far authored trial anchors, exact-player resetter, and screen fade. Save through Unity Editor APIs.

- [x] **Step 3: Bake navigation and update routing**

Build and save a scene-owned NavMesh asset without moving authored geometry. Require complete static paths from the far start anchor to both the player and retreat anchor. Retarget only the two serialized New Game routes that directly loaded `MainGame` to `Enterance`; keep the guarded portal destination `MainGame`. Order enabled Build Settings entries `MainMenu`, `Enterance`, `MainGame`, preserving all other entries afterward.

- [x] **Step 4: Validate serialized result without tests or Play Mode**

Save and reopen `Enterance`, inspect every required serialized reference and forbidden-component fence, recheck the scene-owned NavMesh and complete authored paths, validate all eight Entrance runtime scripts, and read a fresh Unity console. Do not run Unity Test Framework fixtures.

- [x] **Step 5: Record user-deferred runtime acceptance**

The user explicitly reserved gameplay testing for themselves. Do not simulate the fuse, trial, gaze, catch/reset, door animation, or portal. Report these runtime outcomes as not observed in this task.

- [x] **Step 6: Validate assets, scope, and commit**

Validate the scene and all eight Entrance runtime scripts, read a fresh Unity console, run `git diff --check`, and inspect every changed path. Remove the exact Codex-created screenshot artifact and all abandoned fixture/builder files from the superseded route. Commit only the runtime binder, required Unity `.meta` files, authored scene wiring, NavMesh data, the two New Game routing prefabs, Build Settings, and this plan/ledger update. Do not push or merge.
