# Final Stalker Hunt Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the all-fuses permanent direct chase with omniscient stalking that still retreats from confirmed gaze.

**Architecture:** Keep the current class-per-state FSM. Add a final-hunt policy to `MonsterBrain`, enter through `RepositionState`, and make existing state exits respect the policy without changing critical-distance, contact-kill, or SafeZone authority.

**Tech Stack:** Unity 6000.3.13f1, C#, NUnit, Unity Test Framework PlayMode tests

## Global Constraints

- Do not change critical-distance commit, contact kill, or SafeZone immunity.
- Do not add a second AI controller or bypass the existing FSM.
- Final hunt must never intentionally select `AggressiveState`.
- No prefab, scene, `.meta`, map-generation, or tuning-asset changes.

---

### Task 1: Pin the final-hunt contract with failing tests

**Files:**
- Modify: `Assets/_Project/Code/Tests/PlayMode/MainSceneWiringMonsterTests.cs`
- Modify: `Assets/_Project/Code/Tests/PlayMode/MapMonsterGateTests.cs`

**Interfaces:**
- Consumes: `GameFlowController.FinalChaseState`, `MainSceneWiring.BindMonster(MonsterBrain)`
- Produces: behavioral expectations for `MonsterBrain.IsFinalHunt` and the non-aggressive final state

- [ ] Replace the two wiring assertions that require `IsRelentless`/`AggressiveState` with assertions that require `IsFinalHunt` and reject `AggressiveState`.
- [ ] Add `FinalHunt_DoesNotForgetDistantPlayer` by moving the player beyond normal awareness, ticking `InterestedState`, and asserting it does not fall back to `PatrolState`.
- [ ] Add `FinalHunt_ConfirmedGazeRetreatsAtMaximumAnger` by entering `ObservedState` at maximum anger and asserting `ShyState`.
- [ ] Update the generated-map late-spawn test to require final hunt and reject aggressive/patrol states.
- [ ] Run the focused PlayMode tests. Expected RED: current production exposes only `IsRelentless`, forces `AggressiveState`, forgets distant players, and can aggro after gaze.

### Task 2: Implement final-hunt policy through the existing FSM

**Files:**
- Modify: `Assets/_Project/Code/Gameplay/MainSceneWiring.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/MonsterBrain.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/States/PatrolState.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/States/InterestedState.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/States/RepositionState.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/States/FlankState.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/States/AmbushState.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/States/CutOffState.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/States/ObservedState.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/States/ShyState.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/States/FakeRetreatState.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/States/SafeZoneCampState.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/States/CommitState.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/States/AggressiveState.cs`

**Interfaces:**
- Produces: `bool MonsterBrain.IsFinalHunt { get; }`, `void MonsterBrain.BeginFinalHunt()`
- Consumes: existing `RepositionState`, `ObservedState`, `ShyState`, `CommitState`

- [ ] Add `IsFinalHunt` and `BeginFinalHunt()`, with `BeginFinalHunt()` entering `RepositionState`.
- [ ] Make `MainSceneWiring` call `BeginFinalHunt()` instead of setting `IsRelentless` and forcing `AggressiveState`.
- [ ] Suppress distance-abandon transitions during final hunt and redirect completed retreat/camp states to `RepositionState`.
- [ ] Make all final-hunt gaze-break paths choose `ShyState`; guard `AggressiveState.Enter()` from final-hunt entry.
- [ ] Make escaped `CommitState` resume `RepositionState` during final hunt.
- [ ] Run focused tests. Expected GREEN.

### Task 3: Verify the behaviour slice

**Files:**
- Verify only; no new production files

**Interfaces:**
- Consumes: focused Unity PlayMode test fixtures
- Produces: fresh compile/test evidence

- [ ] Run `MainSceneWiringMonsterTests` and `MapMonsterGateTests` through Unity Test Runner/MCP.
- [ ] Read Unity console for compile errors and test failures.
- [ ] Run `git diff --check` and inspect the scoped diff.
- [ ] Report static/test evidence separately from unrun manual Play Mode behaviour.
