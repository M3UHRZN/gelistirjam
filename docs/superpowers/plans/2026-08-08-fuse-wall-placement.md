# Fuse Wall Placement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Spawn every generated Fuse tablet on a deterministic closed room wall and expose Inspector controls for tablet position and orientation.

**Architecture:** Keep map generation neutral and make `MapFuseGate` consume the existing socket, wall-cap, and portal contract at runtime. The gate chooses the capped socket farthest from active portals, instantiates under its own identity-scaled transform, aligns and bounds-fits the visual, then performs its existing `FuseBox` wiring. Behavior is verified through `MapFuseGate`'s public spawned-fuse output rather than exposing placement internals.

**Tech Stack:** Unity 6000.3.13f1, C#, Unity Test Framework PlayMode tests, Unity MCP, NUnit.

## Global Constraints

- Preserve all pre-existing dirty scene, lighting, model, texture, and prefab changes.
- Do not edit any `Anchor_Fuse_00` transform in graybox or textured room prefabs.
- Do not change Fuse minigame, objective progression, monster awareness, or map role assignment.
- Keep map generation to gameplay communication one-directional through `GeneratedMapRegistry` and published anchors.
- Treat Play Mode visual success as unverified until the generated main scene is actually run and inspected.

---

### Task 1: Observable automatic closed-wall placement

**Files:**
- Create: `Assets/_Project/Code/Tests/PlayMode/MapFuseGateTests.cs`
- Modify: `Assets/_Project/Code/Gameplay/World/MapFuseGate.cs`

**Interfaces:**
- Consumes: `GeneratedMapRuntimeData.FuseAnchors`, `GeneratedMapModule`, `ModuleSocketController.TryParse(string, string, out SocketKey)`.
- Produces: existing `MapFuseGate.SpawnedFuseBoxes`; new serialized fields `placeOnClosedWall`, `mountHeight`, `wallClearance`, `wallOffset`, and `rotationOffsetEuler`.
- Test fixture API: `CreateFixture()`, `Renderer AddClosedSocket(string suffix, Vector3 position, Vector3 outward, bool addRenderer = false)`, `AddPortal(string suffix, Vector3 position)`, `UseRenderedVisual(Vector3 size)`, `SetMountSettings(float height, float clearance, Vector3 offset, Vector3 euler)`, `Publish()`, `Anchor`, `Gate`, and `Dispose()`.
- The fixture assigns a renderer-free `GameObject` carrying only `BoxCollider` as `fuseBoxPrefab`, so pose assertions are not altered by bounds fitting; a separate renderer-bounds test covers that behavior.

- [ ] **Step 1: Write a failing PlayMode test for closed-wall selection**

Create a synthetic Fuse room with `Socket_N_00`, `Socket_S_00`, active matching wall caps, an active portal near the north socket, and an `Anchor_Fuse_00` at room centre. Bind a real `MapFuseGate`, publish `GeneratedMapRuntimeData`, and assert the spawned box uses the south socket:

```csharp
[UnityTest]
public IEnumerator OnMapReady_SelectsClosedWallFarthestFromOpenPortal()
{
    Fixture fixture = CreateFixture();
    fixture.AddClosedSocket("N_00", new Vector3(0f, 0f, 4f), Vector3.forward);
    fixture.AddClosedSocket("S_00", new Vector3(0f, 0f, -4f), Vector3.back);
    fixture.AddPortal("N_00", new Vector3(0f, 0f, 3.5f));

    fixture.Publish();

    Transform spawned = fixture.Gate.SpawnedFuseBoxes.Single().transform;
    Assert.That(spawned.position.z, Is.LessThan(0f));
    fixture.Dispose();
    yield break;
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run `MapFuseGateTests.OnMapReady_SelectsClosedWallFarthestFromOpenPortal` through Unity Test Runner/MCP.

Expected: FAIL because current `MapFuseGate` spawns at `Anchor_Fuse_00` instead of resolving a capped socket.

- [ ] **Step 3: Add serialized wall-mount controls and deterministic resolver**

Add these fields next to `fuseBoxPrefab`:

```csharp
[SerializeField] private bool placeOnClosedWall = true;
[SerializeField] private float mountHeight = 1.25f;
[SerializeField] private float wallClearance = 0.02f;
[SerializeField] private Vector3 wallOffset;
[SerializeField] private Vector3 rotationOffsetEuler;
```

Add a private resolver that finds the anchor's parent `GeneratedMapModule`, collects `Socket_*`, active `WallCap_*`, and active `Portal_*` children, scores each capped socket by its minimum portal distance, and breaks ties with `string.CompareOrdinal`. Project the chosen socket's outward `forward` onto the horizontal plane and return `intoRoom = -outward`.

- [ ] **Step 4: Instantiate under the gate and apply the resolved world pose**

Replace anchor-parent instantiation with identity-scale parenting:

```csharp
GameObject instance = fuseBoxPrefab != null
    ? Instantiate(fuseBoxPrefab, transform, false)
    : BuildDefaultFuseVisual();

Quaternion baseRotation = mounted
    ? Quaternion.LookRotation(intoRoom, Vector3.up)
    : anchor.rotation;
instance.transform.SetPositionAndRotation(
    mounted ? socket.position : anchor.position,
    baseRotation * Quaternion.Euler(rotationOffsetEuler));
```

Keep collider creation, `FuseBox` creation, round assignment, dependency binding, and objective counting unchanged.

- [ ] **Step 5: Run the focused test and verify GREEN**

Run `MapFuseGateTests.OnMapReady_SelectsClosedWallFarthestFromOpenPortal`.

Expected: PASS with exactly one Fuse box positioned at the south-side mount.

- [ ] **Step 6: Commit the independently working wall resolver**

```powershell
git add -- 'Assets/_Project/Code/Gameplay/World/MapFuseGate.cs' 'Assets/_Project/Code/Tests/PlayMode/MapFuseGateTests.cs'
git commit -m "feat: mount generated fuses on closed walls"
```

---

### Task 2: Bounds fitting, authoring offsets, and fallback behavior

**Files:**
- Modify: `Assets/_Project/Code/Tests/PlayMode/MapFuseGateTests.cs`
- Modify: `Assets/_Project/Code/Gameplay/World/MapFuseGate.cs`

**Interfaces:**
- Consumes: the Task 1 resolver result: chosen socket, horizontal `intoRoom`, and matching wall-cap renderer.
- Produces: renderer-bounds flush fitting, wall-frame offset calculation, deterministic fallback to the original Fuse anchor.

- [ ] **Step 1: Add failing tests for tie-break, fallback, rotation, and wall-frame offsets**

Add five focused tests:

```csharp
[UnityTest]
public IEnumerator OnMapReady_EqualScoresChooseOrdinalSocketName()
{
    Fixture fixture = CreateFixture();
    fixture.AddClosedSocket("W_00", new Vector3(-4f, 0f, 0f), Vector3.left);
    fixture.AddClosedSocket("E_00", new Vector3(4f, 0f, 0f), Vector3.right);
    fixture.Publish();
    Assert.That(fixture.Gate.SpawnedFuseBoxes.Single().transform.position.x, Is.GreaterThan(0f));
    fixture.Dispose();
    yield break;
}

[UnityTest]
public IEnumerator OnMapReady_NoClosedWallFallsBackToFuseAnchor()
{
    Fixture fixture = CreateFixture();
    LogAssert.Expect(LogType.Warning, new Regex("could not resolve a closed wall.*Anchor_Fuse_00"));
    fixture.Publish();
    Assert.That(fixture.Gate.SpawnedFuseBoxes.Single().transform.position,
        Is.EqualTo(fixture.Anchor.position).Using(Vector3ComparerWithEqualsOperator.Instance));
    fixture.Dispose();
    yield break;
}

[UnityTest]
public IEnumerator OnMapReady_ComposesRotationOffsetAfterFacingIntoRoom()
{
    Fixture fixture = CreateFixture();
    fixture.AddClosedSocket("N_00", new Vector3(0f, 0f, 4f), Vector3.forward);
    fixture.SetMountSettings(0f, 0f, Vector3.zero, new Vector3(0f, 90f, 0f));
    fixture.Publish();
    Transform spawned = fixture.Gate.SpawnedFuseBoxes.Single().transform;
    Quaternion expected = Quaternion.LookRotation(Vector3.back, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);
    Assert.That(Quaternion.Angle(spawned.rotation, expected), Is.LessThan(0.01f));
    fixture.Dispose();
    yield break;
}

[UnityTest]
public IEnumerator OnMapReady_AppliesOffsetInChosenWallFrame()
{
    Fixture fixture = CreateFixture();
    fixture.AddClosedSocket("N_00", new Vector3(0f, 0f, 4f), Vector3.forward);
    fixture.SetMountSettings(0f, 0f, new Vector3(1f, 2f, 3f), Vector3.zero);
    fixture.Publish();
    Vector3 expected = new Vector3(-1f, 2f, 1f);
    Assert.That(Vector3.Distance(fixture.Gate.SpawnedFuseBoxes.Single().transform.position, expected),
        Is.LessThan(0.01f));
    fixture.Dispose();
    yield break;
}

[UnityTest]
public IEnumerator OnMapReady_RenderedVisualSitsAtHeightAndFlushWithWall()
{
    Fixture fixture = CreateFixture();
    Renderer wall = fixture.AddClosedSocket(
        "N_00", new Vector3(0f, 0f, 4f), Vector3.forward, addRenderer: true);
    fixture.UseRenderedVisual(new Vector3(1f, 0.5f, 0.2f));
    fixture.SetMountSettings(1.25f, 0.02f, Vector3.zero, Vector3.zero);
    fixture.Publish();
    Renderer visual = fixture.Gate.SpawnedFuseBoxes.Single().GetComponentInChildren<Renderer>();
    Assert.That(Mathf.Abs(visual.bounds.min.y - 1.25f), Is.LessThan(0.01f));
    Assert.That(Mathf.Abs(visual.bounds.max.z - (wall.bounds.min.z - 0.02f)), Is.LessThan(0.01f));
    fixture.Dispose();
    yield break;
}
```

Use `UnityEditor.SerializedObject` inside the fixture to set `mountHeight`, `wallClearance`, `wallOffset`, and `rotationOffsetEuler`. Assert world position/rotation from `SpawnedFuseBoxes.Single().transform`; use `Mathf.DeltaAngle` and a `0.01f` positional tolerance.

- [ ] **Step 2: Run the four tests and verify RED**

Run the five named `MapFuseGateTests` through Unity Test Runner/MCP.

Expected: all five tests FAIL for missing deterministic wall selection, missing fallback warning, missing rotation composition, missing wall-frame adjustment, or missing renderer-bounds fitting. Confirm every failure is behavioral rather than fixture construction.

- [ ] **Step 3: Add renderer-bounds helpers and mount adjustment**

Implement:

```csharp
private static bool TryGetWorldBounds(GameObject instance, out Bounds bounds);
private static float ProjectedExtent(Bounds bounds, Vector3 direction);
private Vector3 WallFrameOffset(Vector3 intoRoom);
private void MountAgainstWall(
    GameObject instance,
    float floorY,
    Vector3 intoRoom,
    Renderer wallSurface);
```

`MountAgainstWall` sets the visual bottom to `floorY + mountHeight`. With a wall renderer, it shifts the visual so its back projected face sits at the wall's inner projected face plus `wallClearance`; without one, it shifts by `intoRoom * wallClearance`. Apply `wallOffset.x` along `Vector3.Cross(Vector3.up, intoRoom)`, `.y` upward, and `.z` into the room.

- [ ] **Step 4: Add contextual fallback warning**

When automatic mounting is enabled but resolution fails, log one warning containing the anchor name and generated module name when available, then spawn at `anchor.position`/`anchor.rotation` with `rotationOffsetEuler`. Do not warn when `placeOnClosedWall` is deliberately disabled.

- [ ] **Step 5: Run all MapFuseGate tests and the complete PlayMode suite**

Run `MapFuseGateTests`, then all tests in `Game.Gameplay.PlayModeTests`.

Expected: every test passes with no compile errors and no unexpected warnings.

- [ ] **Step 6: Commit bounds fitting and authoring controls**

```powershell
git add -- 'Assets/_Project/Code/Gameplay/World/MapFuseGate.cs' 'Assets/_Project/Code/Tests/PlayMode/MapFuseGateTests.cs'
git commit -m "feat: add configurable fuse wall offsets"
```

---

### Task 3: Main scene tablet binding and runtime verification

**Files:**
- Modify in Unity Editor: `Assets/Scenes/MainGame.unity`
- Reuse without editing mesh/import data: `Assets/prefabs/tablet.prefab`

**Interfaces:**
- Consumes: `MapFuseGate.fuseBoxPrefab`, `rotationOffsetEuler`, `mountHeight`, `wallClearance`, `wallOffset`.
- Produces: the main scene's FuseGate spawning `tablet.prefab` at generated wall mounts.

- [ ] **Step 1: Inspect the live Unity instance and preserve scene state**

Read `mcpforunity://custom-tools`, `mcpforunity://instances`, editor state, active scene, console errors, and the `FuseGate` component. Confirm the active project is `gelistirjam`, the active scene is `MainGame`, and the editor is not in Play Mode before mutation.

- [ ] **Step 2: Assign the tablet prefab and initial mount values**

Using Unity MCP/serialized component editing, assign `Assets/prefabs/tablet.prefab` to `FuseGate.MapFuseGate.fuseBoxPrefab`. Start with `mountHeight = 1.25`, `wallClearance = 0.02`, `wallOffset = (0, 0, 0)`, and `rotationOffsetEuler = (0, 0, 0)`. Save the scene without rebuilding it or replacing unrelated serialized data.

- [ ] **Step 3: Refresh and verify compilation**

Refresh Unity, wait for compilation/domain reload to finish, then read the console.

Expected: no C# compiler errors. Existing unrelated warnings must be recorded separately rather than attributed to this task.

- [ ] **Step 4: Run focused and regression tests**

Run all `MapFuseGateTests`, then `Game.Gameplay.PlayModeTests`, then execute `Tools/Gloomveil/Map/Validate Module Catalog`.

Expected: focused and regression tests pass; module catalog reports no errors because no module prefab anchors or sockets were modified.

- [ ] **Step 5: Enter Play Mode and inspect generated Fuse tablets**

Generate/run the main map, inspect every entry in `MapFuseGate.SpawnedFuseBoxes`, and verify each tablet belongs to a Fuse-role room, sits on an active closed wall cap, faces into the room after `rotationOffsetEuler`, has a valid non-negative-scale collider, and remains interactable. Read console errors/warnings before exiting Play Mode.

- [ ] **Step 6: Tune only the serialized mount controls if the model axes require correction**

Change only `rotationOffsetEuler`, `mountHeight`, `wallClearance`, or `wallOffset`; repeat Play Mode inspection until the tablet is upright, visible, flush with the wall, and reachable by the interaction ray. Do not edit the FBX, room prefabs, or Fuse anchors.

- [ ] **Step 7: Final verification and handoff**

Run `git diff --check`, inspect `git status --short`, and confirm only scoped code/test changes plus the already-dirty main scene/tablet assets are present. Report test counts, catalog validation, Play Mode evidence, final serialized mount values, and any remaining user-owned dirty files.
