# Monster Death Sequence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current frozen-on-kill result with a readable kill-camera beat and a persistent death panel containing Retry and Main Menu buttons.

**Architecture:** A focused `DeathSequenceController` observes `GameFlowController` and owns only death presentation. `MainSceneWiring`, which already owns the current procedural monster binding, passes the replacement monster to it; existing `DeathState` and `HudVisibility` continue to own input locking and HUD hiding.

**Tech Stack:** Unity 6 C#, uGUI, TextMeshPro, Unity Input System UI, Cinemachine, Unity Test Framework PlayMode tests.

## Global Constraints

- Do not modify the dirty binary scenes, map configuration, animator controller, or project build settings.
- There is no automatic reset timer; the death panel waits indefinitely.
- Retry reloads the active scene and Main Menu loads `MainMenu`.
- `KillCameraAnchor`, audio, and prebuilt death UI references are optional and must fail gracefully.
- Use unscaled time for the short attack-to-panel delay.

---

### Task 1: Death presentation controller

**Files:**
- Create: `Assets/_Project/Code/Gameplay/Death/DeathSequenceController.cs`
- Create: `Assets/_Project/Code/Tests/PlayMode/DeathSequenceControllerTests.cs`

**Interfaces:**
- Consumes: `GameFlowController.OnStateChanged`, `MonsterBrain`, `Camera`, optional `AudioClip`.
- Produces: `Configure(GameFlowController, Camera, AudioClip, float, string)`, `BindMonster(MonsterBrain)`, `BindSceneActions(Action, Action)`, `Retry()`, `MainMenu()`, `IsPlaying`, `DeathUiRoot`, and `KillCameraAnchor`.

- [ ] **Step 1: Write the failing PlayMode tests**

```csharp
[UnityTest]
public IEnumerator DeathState_LocksCameraAndShowsPersistentPanel()
{
    using var fixture = new DeathSequenceFixture(panelDelay: 0f);
    fixture.Flow.ChangeState(fixture.Flow.DeathState);
    yield return null;
    Assert.That(fixture.Controller.IsPlaying, Is.True);
    Assert.That(fixture.Camera.transform.parent, Is.SameAs(fixture.Anchor));
    Assert.That(fixture.Controller.DeathUiRoot.activeSelf, Is.True);
    Assert.That(fixture.SceneLoadCount, Is.Zero);
}

[Test]
public void Buttons_InvokeOnlyTheirConfiguredSceneAction()
{
    using var fixture = new DeathSequenceFixture(panelDelay: 0f);
    fixture.Controller.Retry();
    Assert.That(fixture.RetryCount, Is.EqualTo(1));
    fixture.Controller.MainMenu();
    Assert.That(fixture.MainMenuCount, Is.EqualTo(1));
}
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run `Game.Gameplay.Tests.DeathSequenceControllerTests` in PlayMode. Expected failure: `DeathSequenceController` does not exist.

- [ ] **Step 3: Implement the minimal controller**

Implement a `MonoBehaviour` that subscribes to `GameFlowController.OnStateChanged`, begins once on `DeathState`, disables any `CinemachineBrain` on the live camera, parents the camera to a recursive child named `KillCameraAnchor`, plays optional audio, waits with `WaitForSecondsRealtime`, and displays either an assigned root or a generated overlay. Generate a Screen Space Overlay canvas with a dark background, `ÖLDÜN` label, and two uGUI buttons wired to `Retry` and `MainMenu`. Default scene actions use `SceneManager.LoadScene(SceneManager.GetActiveScene().name)` and `SceneManager.LoadScene(mainMenuSceneName)`; injected `Action` delegates provide a no-load test seam.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run `Game.Gameplay.Tests.DeathSequenceControllerTests` in PlayMode. Expected: all pass with no console errors.

---

### Task 2: Procedural monster wiring

**Files:**
- Modify: `Assets/_Project/Code/Gameplay/MainSceneWiring.cs`
- Modify: `Assets/_Project/Code/Tests/PlayMode/MainSceneWiringMonsterTests.cs`

**Interfaces:**
- Consumes: `DeathSequenceController.Configure(...)` and `BindMonster(...)` from Task 1.
- Produces: automatic death-sequence creation on the existing wiring object and forwarding of the currently bound monster.

- [ ] **Step 1: Add the failing wiring test**

```csharp
[Test]
public void BindMonster_ForwardsCurrentMonsterToDeathSequence()
{
    using var fixture = new WiringFixture();
    MonsterBrain brain = fixture.CreateInactiveBrain("Monster");
    fixture.Wiring.BindMonster(brain);
    Assert.That(fixture.Wiring.DeathSequence.BoundMonster, Is.SameAs(brain));
}
```

- [ ] **Step 2: Run the focused wiring test and verify RED**

Run `Game.Gameplay.Tests.MainSceneWiringMonsterTests.BindMonster_ForwardsCurrentMonsterToDeathSequence` in PlayMode. Expected failure: `DeathSequence` API is missing.

- [ ] **Step 3: Add minimal composition wiring**

Add serialized presentation settings to `MainSceneWiring`: optional camera and death sound, `deathPanelDelaySeconds = 0.65f`, and `mainMenuSceneName = "MainMenu"`. During `Wire()`, reuse or add one `DeathSequenceController`, configure it, and forward the current monster from `BindMonster`/`UnbindMonster`. Expose a read-only `DeathSequence` property for validation.

- [ ] **Step 4: Run wiring and death tests and verify GREEN**

Run both `Game.Gameplay.Tests.MainSceneWiringMonsterTests` and `Game.Gameplay.Tests.DeathSequenceControllerTests` in PlayMode. Expected: all pass.

---

### Task 3: Unity integration verification

**Files:**
- No scene or prefab edits required.

**Interfaces:**
- Consumes: compiled Task 1-2 code and current `yedeksahne`/`MainGame` wiring.
- Produces: verified runtime fallback UI and inspector-ready `KillCameraAnchor`/audio contract.

- [ ] **Step 1: Validate scripts and compile**

Validate both changed production scripts, wait until Unity reports `ready_for_tools`, and read the console for errors.

- [ ] **Step 2: Run regression tests**

Run `Game.Gameplay.Tests.DeathSequenceControllerTests`, `Game.Gameplay.Tests.MainSceneWiringMonsterTests`, and the existing monster animation/kill tests. Expected: all pass.

- [ ] **Step 3: Verify live scene without saving it**

Enter Play Mode in the currently open gameplay scene, confirm one runtime `DeathSequenceController` exists on `MainSceneWiring`, then stop Play Mode. Do not save or alter the user's dirty scene.

- [ ] **Step 4: Commit only scoped source/test changes**

Stage only the new controller, its test, `MainSceneWiring.cs`, and `MainSceneWiringMonsterTests.cs`; leave all pre-existing dirty assets untouched.
