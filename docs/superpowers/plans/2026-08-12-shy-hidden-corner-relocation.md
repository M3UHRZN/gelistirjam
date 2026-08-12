# Shy Hidden-Corner Relocation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Shy abandon an exposed corner and retreat to a different registered corner that is fully hidden from the real player camera by FOV or world geometry.

**Architecture:** Add a Shy-only body-visibility query and selector while preserving the existing tactical `StalkPointScorer` APIs. `ShyState` owns relocation timing; `PlayerVisionModel` evaluates camera/body samples; `MonsterPerception` owns occlusion; `MonsterNavigator` continues to own reachability and SafeZone validation.

**Tech Stack:** Unity 6000.3.13f1, C#, Unity Camera/Physics, Unity NavMesh, NUnit Unity Test Framework, existing `Game.Gameplay` assemblies.

## Global Constraints

- Preserve Patrol, Interested, Reposition, Flank, Ambush, CutOff, Observed, FakeRetreat, Aggressive, Commit, Kill, and SafeZoneCamp behavior.
- Preserve yelling, confirmed eye gaze, backward-facing retreat, staredown, anger, FakeRetreat, and Shy lifetime behavior.
- Shy destinations must be registered `StalkPoint` instances with `IsNearCorner == true`.
- Reject the immediately exposed/current corner, visible corners, SafeZone corners, and corners without a complete NavMesh path.
- Use the real player camera's horizontal and vertical view plus wall, ceiling, floor, door, and other configured non-trigger occluders.
- Do not use flashlight state, random NavMesh fallback, or shared `StalkPointScorer` changes.
- Keep generated map ownership unchanged: `MapMonsterGate` remains the neutral-anchor conversion and runtime wiring boundary.

---

## File Structure

- Create `Assets/_Project/Code/Gameplay/Monster/ShyEscapePointSelector.cs`: Shy-only hard filtering and deterministic corner ranking.
- Create `Assets/_Project/Code/Tests/PlayMode/MonsterShyVisibilityTests.cs`: real camera/body-sample and physics-occlusion coverage.
- Create `Assets/_Project/Code/Tests/PlayMode/ShyEscapePointSelectorTests.cs`: corner eligibility/ranking coverage on a runtime NavMesh fixture.
- Create `Assets/_Project/Code/Tests/PlayMode/MonsterShyRelocationTests.cs`: confirmed-gaze edge, immediate target invalidation/relocation, and no-fallback behavior.
- Modify `Assets/_Project/Code/Gameplay/Player/PlayerVisionModel.cs`: additive camera-bound hypothetical-body visibility API; preserve existing methods.
- Modify `Assets/_Project/Code/Gameplay/Monster/MonsterPerception.cs`: additive camera-to-sample line-of-sight query.
- Modify `Assets/_Project/Code/Gameplay/Monster/MonsterNavigator.cs`: read-only agent capsule dimensions; no navigation semantic changes.
- Modify `Assets/_Project/Code/Gameplay/Monster/MonsterRuntimeRig.cs`: pass the already resolved gaze camera into `PlayerVisionModel`.
- Modify `Assets/_Project/Code/Editor/MonsterAITestSceneBuilder.cs`: pass its existing player camera into the same additive binding overload.
- Modify `Assets/_Project/Code/Gameplay/Monster/States/ShyState.cs`: track current/exposed corner and schedule bounded relocation.

---

### Task 1: Camera/body visibility without changing shared stalk visibility

**Files:**
- Create: `Assets/_Project/Code/Tests/PlayMode/MonsterShyVisibilityTests.cs`
- Modify: `Assets/_Project/Code/Gameplay/Player/PlayerVisionModel.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/MonsterPerception.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/MonsterNavigator.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/MonsterRuntimeRig.cs`
- Modify: `Assets/_Project/Code/Editor/MonsterAITestSceneBuilder.cs`

**Interfaces:**
- Consumes: existing `PlayerVisionModel.Bind(Transform, MonsterPerception)`, `MonsterPerception` occluder mask, and `NavMeshAgent.height/radius`.
- Produces: `PlayerVisionModel.Bind(Transform, Camera, MonsterPerception)`, `bool PlayerVisionModel.IsMonsterBodyVisibleAt(Vector3 feetPosition, float height, float radius)`, `bool MonsterPerception.HasClearLineOfSight(Vector3 origin, Vector3 target)`, `float MonsterNavigator.AgentHeight`, and `float MonsterNavigator.AgentRadius`.

- [ ] **Step 1: Write failing camera/occlusion tests**

Create a disabled real `Camera` at eye height, bind it to a `PlayerVisionModel`, and use primitive colliders plus `Physics.SyncTransforms()`. Cover these exact assertions:

```csharp
Assert.That(vision.IsMonsterBodyVisibleAt(new Vector3(0f, 0f, 6f), 2.2f, 0.35f), Is.True,
    "An unobstructed body in the real camera viewport must be visible.");

wall.transform.position = new Vector3(0f, 1.1f, 3f);
wall.transform.localScale = new Vector3(4f, 3f, 0.2f);
Physics.SyncTransforms();
Assert.That(vision.IsMonsterBodyVisibleAt(new Vector3(0f, 0f, 6f), 2.2f, 0.35f), Is.False,
    "A full wall between camera and every body sample must hide the candidate.");

wall.transform.localScale = new Vector3(0.2f, 3f, 0.2f);
Physics.SyncTransforms();
Assert.That(vision.IsMonsterBodyVisibleAt(new Vector3(0f, 0f, 6f), 2.2f, 0.35f), Is.True,
    "One visible side-torso sample is enough to reject a partially exposed candidate.");
```

Add a second test that tilts the camera toward an upper-floor candidate, places a floor slab between them, and expects `false`; remove the slab and expect `true`. Add a third test for candidates outside horizontal and vertical viewport bounds expecting `false`.

- [ ] **Step 2: Run the focused test and verify RED**

Run through Unity Test Runner/MCP:

```text
Game.Gameplay.Tests.MonsterShyVisibilityTests
```

Expected: compile failure because the new `Bind` overload and `IsMonsterBodyVisibleAt` do not exist.

- [ ] **Step 3: Add the narrow line-of-sight API**

In `MonsterPerception`, preserve `IsPlayerVisibleFrom` and add:

```csharp
public bool HasClearLineOfSight(Vector3 origin, Vector3 target) =>
    !Physics.Linecast(origin, target, occluderMask, QueryTriggerInteraction.Ignore);
```

- [ ] **Step 4: Add real-camera body sampling while preserving existing methods**

Store an optional camera in `PlayerVisionModel`, preserve the current two-argument bind and both existing visibility methods, then add:

```csharp
public void Bind(Transform playerTransform, Camera playerCamera, MonsterPerception monsterPerception)
{
    player = playerTransform;
    viewCamera = playerCamera;
    perception = monsterPerception;
}

public bool IsMonsterBodyVisibleAt(Vector3 feetPosition, float height, float radius)
{
    if (viewCamera == null || perception == null) return true;

    float safeHeight = Mathf.Max(0.1f, height);
    float safeRadius = Mathf.Max(0f, radius);
    Vector3 cameraRight = viewCamera.transform.right * safeRadius;
    Vector3 torso = feetPosition + Vector3.up * (safeHeight * 0.55f);
    Vector3[] samples =
    {
        feetPosition + Vector3.up * (safeHeight * 0.2f),
        torso,
        feetPosition + Vector3.up * (safeHeight * 0.9f),
        torso - cameraRight,
        torso + cameraRight,
    };

    for (int i = 0; i < samples.Length; i++)
    {
        Vector3 sample = samples[i];
        if (Vector3.Distance(viewCamera.transform.position, sample) > viewRange) continue;
        Vector3 viewport = viewCamera.WorldToViewportPoint(sample);
        if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f
            || viewport.y < 0f || viewport.y > 1f) continue;
        if (perception.HasClearLineOfSight(viewCamera.transform.position, sample)) return true;
    }

    return false;
}
```

Use a reusable fixed-size field buffer instead of allocating `Vector3[]` per call in the final implementation.

- [ ] **Step 5: Expose capsule dimensions and wire the camera**

Add read-only `AgentHeight` and `AgentRadius` properties to `MonsterNavigator`. Change `MonsterRuntimeRig.Initialize` to call `visionModel.Bind(player, playerCamera, perception)`. In `MonsterAITestSceneBuilder.BuildMonster`, resolve `Camera playerCamera = gazeDetector.GetComponent<Camera>()` and change the existing bind call to `visionModel.Bind(player, playerCamera, perception)`.

- [ ] **Step 6: Run focused and existing rig tests and verify GREEN**

Run:

```text
Game.Gameplay.Tests.MonsterShyVisibilityTests
Game.Gameplay.Tests.MonsterRuntimeRigTests
Game.Gameplay.Tests.MonsterConfirmedGazeTests
```

Expected: all pass; the rig still initializes inactive and confirmed gaze still enters Yell exactly once.

- [ ] **Step 7: Commit Task 1**

```bash
git add Assets/_Project/Code/Gameplay/Player/PlayerVisionModel.cs Assets/_Project/Code/Gameplay/Monster/MonsterPerception.cs Assets/_Project/Code/Gameplay/Monster/MonsterNavigator.cs Assets/_Project/Code/Gameplay/Monster/MonsterRuntimeRig.cs Assets/_Project/Code/Editor/MonsterAITestSceneBuilder.cs Assets/_Project/Code/Tests/PlayMode/MonsterShyVisibilityTests.cs
git commit -m "feat: evaluate shy corners against player view"
```

---

### Task 2: Shy-only hidden-corner selector

**Files:**
- Create: `Assets/_Project/Code/Gameplay/Monster/ShyEscapePointSelector.cs`
- Create: `Assets/_Project/Code/Tests/PlayMode/ShyEscapePointSelectorTests.cs`

**Interfaces:**
- Consumes: `StalkPointRuntimeSet.Items`, `StalkPoint.IsNearCorner`, `StalkPoint.DarknessLevel`, `PlayerVisionModel.IsMonsterBodyVisibleAt`, `MonsterNavigator.IsReachable`, `MonsterNavigator.AgentHeight`, and `MonsterNavigator.AgentRadius`.
- Produces: `StalkPoint ShyEscapePointSelector.PickBest(StalkPointRuntimeSet points, Vector3 playerPosition, Vector3 monsterPosition, PlayerVisionModel visionModel, MonsterNavigator navigator, StalkPoint excluded)`.

- [ ] **Step 1: Write failing selector tests on a small runtime NavMesh**

Build a flat `NavMeshSurface`, place a navigator on it, bind a camera/vision fixture, and register named corner points. Assert:

```csharp
StalkPoint selected = ShyEscapePointSelector.PickBest(
    points, player.position, monster.position, vision, navigator, exposedCorner);

Assert.That(selected, Is.SameAs(hiddenFarCorner));
Assert.That(selected, Is.Not.SameAs(exposedCorner));
```

Add cases proving that a non-corner, an unobstructed visible corner, a SafeZone corner, and an unreachable corner are rejected. Add a deterministic ranking case: farther-from-player wins; equal distance prefers greater darkness; an exact tie retains registry order.

- [ ] **Step 2: Run the selector fixture and verify RED**

Run:

```text
Game.Gameplay.Tests.ShyEscapePointSelectorTests
```

Expected: compile failure because `ShyEscapePointSelector` does not exist.

- [ ] **Step 3: Implement hard filters and lexicographic ranking**

Create a static selector that returns `null` when required dependencies are missing. For each registry item, skip null, excluded, non-corner, body-visible, or unreachable points. Compare valid candidates in this exact order:

```csharp
float playerDistance = Vector3.Distance(candidate.Position, playerPosition);
float monsterDistance = Vector3.Distance(candidate.Position, monsterPosition);

bool better = best == null
    || playerDistance > bestPlayerDistance + 0.001f
    || (Mathf.Abs(playerDistance - bestPlayerDistance) <= 0.001f
        && candidate.DarknessLevel > bestDarkness + 0.001f)
    || (Mathf.Abs(playerDistance - bestPlayerDistance) <= 0.001f
        && Mathf.Abs(candidate.DarknessLevel - bestDarkness) <= 0.001f
        && monsterDistance < bestMonsterDistance - 0.001f);
```

Do not modify `StalkPointScorer` and do not add randomness.

- [ ] **Step 4: Run selector and visibility fixtures and verify GREEN**

Run:

```text
Game.Gameplay.Tests.ShyEscapePointSelectorTests
Game.Gameplay.Tests.MonsterShyVisibilityTests
```

Expected: all tests pass with no unexpected logs.

- [ ] **Step 5: Commit Task 2**

```bash
git add Assets/_Project/Code/Gameplay/Monster/ShyEscapePointSelector.cs Assets/_Project/Code/Tests/PlayMode/ShyEscapePointSelectorTests.cs
git commit -m "feat: select hidden stalk corners for shy"
```

---

### Task 3: Integrate relocation into Shy without changing other states

**Files:**
- Modify: `Assets/_Project/Code/Gameplay/Monster/States/ShyState.cs`
- Create: `Assets/_Project/Code/Tests/PlayMode/MonsterShyRelocationTests.cs`

**Interfaces:**
- Consumes: `ShyEscapePointSelector.PickBest`, `MonsterNavigator.TryResolveReachableDestination`, `MonsterNavigator.TrySetResolvedDestination`, existing `MonsterBrain.IsGazeConfirmed`, existing staredown controller, and `MonsterConfig.RepositionInterval`.
- Produces: read-only `StalkPoint ShyState.CurrentTarget` for debug/test observation; no cross-state transition API changes.

- [ ] **Step 1: Write the failing Shy relocation test**

Build an inactive production-shaped rig on a small runtime NavMesh with two registered corner points separated by a vision-only wall. Activate it, force `brain.ChangeState(brain.ShyState)`, wait at the first corner, and record `((ShyState)brain.ShyState).CurrentTarget`. Turn the camera to the real `MonsterGazeReceiver` until gaze confirms, then assert before releasing gaze:

```csharp
Assert.That(firstTarget, Is.Not.Null);
Assert.That(shy.CurrentTarget, Is.Not.Null);
Assert.That(shy.CurrentTarget, Is.Not.SameAs(firstTarget),
    "A confirmed gaze must burn the exposed Shy target for the next retreat.");
Assert.That(vision.IsMonsterBodyVisibleAt(
    shy.CurrentTarget.Position, navigator.AgentHeight, navigator.AgentRadius), Is.False);
```

Add a mid-travel gaze case and a no-valid-corner case. In the no-valid case, assert `NavMeshAgent.isStopped == true`, current target is null, and no visible corner becomes the destination after more than one retry interval.

- [ ] **Step 2: Run the relocation fixture and verify RED**

Run:

```text
Game.Gameplay.Tests.MonsterShyRelocationTests
```

Expected: compile failure because `ShyState.CurrentTarget` and selector integration do not exist.

- [ ] **Step 3: Replace Shy's random destination calls with one helper**

Add state fields:

```csharp
private StalkPoint currentTarget;
private StalkPoint exposedTarget;
private bool relocationPending;
private float relocationRetryTimer;

public StalkPoint CurrentTarget => currentTarget;
```

Add `TrySelectHiddenCorner()` that calls the selector, evaluates and returns one resolved NavMesh point, commits that same point through `TrySetResolvedDestination`, resumes on success, and stops/records a decision reason on failure. On entry, reset state and call it with no exclusion. Never call `SetFleeDestinationAwayFrom` from Shy.

- [ ] **Step 4: Preserve gaze/anger ordering and relocate on confirmed gaze**

On the confirmed-gaze false-to-true edge:

```csharp
if (gazedNow && !wasGazedLastFrame)
{
    exposedTarget = currentTarget;
    relocationPending = true;
    TrySelectHiddenCorner(exposedTarget);
}
```

The rising edge immediately attempts the new destination excluding `exposedTarget`. Clear `relocationPending` only after a successful selection; otherwise stop and arm `relocationRetryTimer = brain.Config.RepositionInterval`. While gaze remains held, retry only on that bounded cadence. If a staredown activates, keep the current aggression roll on gaze release; if Shy continues, resume the already selected destination instead of selecting a second time.

When not gazed and relocation remains pending, decrement the retry timer and retry only when it reaches zero. Keep the current Shy timer/FakeRetreat/exit logic unchanged.

- [ ] **Step 5: Run relocation and gaze regression fixtures and verify GREEN**

Run:

```text
Game.Gameplay.Tests.MonsterShyRelocationTests
Game.Gameplay.Tests.MonsterConfirmedGazeTests
Game.Gameplay.Tests.MainSceneWiringMonsterTests
```

Expected: all pass; confirmed gaze still raises one event/anger and enters Yell, while Shy immediately selects a different hidden target and resumes movement.

- [ ] **Step 6: Commit Task 3**

```bash
git add Assets/_Project/Code/Gameplay/Monster/States/ShyState.cs Assets/_Project/Code/Tests/PlayMode/MonsterShyRelocationTests.cs
git commit -m "feat: relocate shy after an exposed corner"
```

---

### Task 4: Full regression and generated-map acceptance

**Files:**
- Verify only; modify production/tests only if a concrete failing assertion proves a scoped defect.

**Interfaces:**
- Consumes: all APIs produced by Tasks 1-3.
- Produces: verified Shy relocation with no other state behavior regressions.

- [ ] **Step 1: Run static checks**

Run:

```bash
git diff --check
rg -n "SetFleeDestinationAwayFrom" Assets/_Project/Code/Gameplay/Monster/States/ShyState.cs
rg -n "StalkPointScorer" Assets/_Project/Code/Gameplay/Monster/ShyEscapePointSelector.cs Assets/_Project/Code/Gameplay/Monster/States/ShyState.cs
```

Expected: `git diff --check` clean; no Shy random-flee call; no shared scorer reference in the new selector/state.

- [ ] **Step 2: Run the full Gameplay PlayMode suite**

Run through Unity Test Runner/MCP:

```text
Game.Gameplay.PlayModeTests
```

Expected: every test passes and the Unity console contains no new errors/exceptions.

- [ ] **Step 3: Run generated-map focused regression**

Run:

```text
Game.Gameplay.Tests.MapMonsterGateTests
Game.Gameplay.Tests.MonsterRuntimeRigTests
```

Expected: generated anchors still register, exactly one monster spawns and initializes, and the same `PlayerVisionModel`/StalkPoint set remain bound.

- [ ] **Step 4: Perform manual `MainGame` Play Mode acceptance**

Use a generated narrow-corridor map and verify:

1. Shy initially retreats backward to a registered corner.
2. Looking into its eyes preserves Yell/staredown behavior.
3. Confirmed gaze immediately sends it to a different hidden corner while gaze is still held.
4. A wall-hidden corner may be selected even inside the camera's geometric cone.
5. A doorway exposing any body portion is rejected.
6. A different-floor point hidden by floor/ceiling geometry is accepted only when all samples are occluded.
7. Repeated reveals burn the current target once per confirmed-gaze edge without per-frame destination churn after success.
8. Patrol, Interested, Reposition, and Aggressive transitions still behave normally before and after Shy.

Expected: all eight observations match. If Unity cannot be run, report manual acceptance as outstanding rather than claiming runtime completion.

- [ ] **Step 5: Final repository check**

Run:

```bash
git status --short
git log -5 --oneline --decorate
```

Expected: only intentional scoped changes/commits are present; no generated assets, scenes, prefabs, `.meta` files, or unrelated user files are staged.
