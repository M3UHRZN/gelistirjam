# Monster Confirmed-Gaze Yell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a once-per-confirmed-gaze anger increment and wire the imported `Yelling` animation to that event.

**Architecture:** `MonsterBrain` detects the confirmed-gaze rising edge, owns the anger mutation, and publishes a C# event. `MonsterAnimationController` observes that event and queues a presentation-only Animator trigger; Mecanim returns to locomotion after the one-shot clip.

**Tech Stack:** Unity 6, C#, existing monster FSM, Mecanim Animator Controller, Unity Test Framework.

## Global Constraints

- Preserve the existing 0.2-second gaze confirmation and gaze-loss grace behavior.
- Apply the initial spotted anger once per continuous confirmed gaze.
- Preserve periodic Shy staredown anger.
- Keep root motion disabled and gameplay authoritative in `MonsterBrain`/`NavMeshAgent`.
- Preserve unrelated user scene, config, model-import, material, texture, and source-FBX changes.

---

### Task 1: Confirmed gaze event and centralized anger

**Files:**
- Modify: `Assets/_Project/Code/Gameplay/Monster/MonsterBrain.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/States/InterestedState.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/States/ObservedState.cs`
- Create: `Assets/_Project/Code/Tests/PlayMode/MonsterConfirmedGazeTests.cs`

**Interfaces:**
- Produces: `MonsterBrain.OnConfirmedGazeStarted : event Action`
- Consumes: `MonsterConfig.GazeConfirmDurationSeconds`, `MonsterConfig.AngerPerSpotted`

- [ ] **Step 1: Write the failing gaze integration test**

Create a complete inactive monster rig and real camera/gaze collider. Hold the camera on the receiver past the confirmation duration, then assert anger is exactly `1` and the gaze-start event fired exactly once. Continue holding for less than the periodic staredown interval and assert both values remain unchanged.

- [ ] **Step 2: Run the focused PlayMode test and verify RED**

Run `Game.Gameplay.Tests.MonsterConfirmedGazeTests.ContinuousConfirmedGaze_AddsOneAngerAndRaisesOneEvent`.

Expected: FAIL because `OnConfirmedGazeStarted` does not exist and the one-time anger write is still state-local.

- [ ] **Step 3: Implement the confirmed-gaze rising edge**

Track the prior confirmed value in `MonsterBrain.Update`. On `false -> true`, call `anger.AddAnger(config.AngerPerSpotted)` and invoke `OnConfirmedGazeStarted`. Clear the latch after gaze is no longer confirmed. Do not raise while already in `KillState`.

- [ ] **Step 4: Remove duplicate state-local anger writes**

Remove the initial `AddAnger` calls from `InterestedState` and `ObservedState`; keep their freeze, facing, gaze-break and aggro-roll behavior unchanged.

- [ ] **Step 5: Run the focused test and verify GREEN**

Expected: one continuous gaze produces anger `1`, event count `1`, and no duplicate while held.

### Task 2: Yell presentation contract

**Files:**
- Modify: `Assets/_Project/Code/Gameplay/Monster/MonsterAnimationController.cs`
- Modify: `Assets/_Project/Code/Tests/EditMode/MonsterAnimationAssetTests.cs`
- Modify: `Assets/_Project/Code/Tests/PlayMode/MonsterConfirmedGazeTests.cs`
- Modify: `Assets/_Project/Animations/MonsterAnimator.controller`
- Add: `Assets/animations/Yelling.anim`
- Add: `Assets/animations/Yelling.anim.meta`

**Interfaces:**
- Consumes: `MonsterBrain.OnConfirmedGazeStarted`
- Produces: Animator trigger `Yell`, state `Yell`

- [ ] **Step 1: Extend tests and verify RED**

Assert the production controller contains a `Yell` trigger, a non-looping `Yell` state using `Yelling`, and an Any State transition conditioned by `Yell`. Extend the PlayMode gaze test to assert the Animator enters current/next state `Yell` within ten frames.

- [ ] **Step 2: Implement event subscription and queued trigger**

Subscribe/unsubscribe symmetrically with the existing brain events. Queue `Yell` until the Animator is ready. On kill, clear pending/reset `Yell` before triggering `Attack`.

- [ ] **Step 3: Update the production Animator Controller**

Add the `Yell` parameter/state/transitions using `Assets/animations/Yelling.anim`, keep the clip non-looping, and preserve attack transition priority.

- [ ] **Step 4: Run focused EditMode and PlayMode tests**

Expected: asset contract passes and confirmed gaze enters `Yell`.

### Task 3: Regression verification and scoped commit

**Files:**
- Verify all files above.

- [ ] **Step 1: Run gameplay EditMode tests**

Run the three gameplay EditMode classes; expected `15/15` or the updated total with zero failures.

- [ ] **Step 2: Run monster PlayMode tests**

Run `MonsterRuntimeRigTests`, `MapMonsterGateTests`, `MainSceneWiringMonsterTests`, and `MonsterConfirmedGazeTests`; expected zero failures.

- [ ] **Step 3: Validate the live asset contract and console**

Confirm `Yell=Yelling`, non-looping, trigger transition present, prefab root inactive, controller assigned, and root motion disabled. Confirm no new console errors.

- [ ] **Step 4: Commit only feature files**

Stage the brain/state/controller/test/controller-asset/Yelling-clip/spec/plan files. Leave `MainGame.unity`, `MonsterConfig_Default.asset`, `monsterV2.fbx.meta`, `EditorSettings.asset`, source FBXs, material, and texture changes untouched.
