# Generated Map Monster Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Spawn and fully wire exactly one existing monster AI into every valid generated `MainGame` map using deterministic distant spawn candidates and generated stalk metadata.

**Architecture:** A dedicated `MapMonsterGate` waits for the published map plus explicit light/SafeZone readiness, builds runtime `StalkPoint` components, selects a complete-path spawn, initializes an inactive `Monster.prefab` through `MonsterRuntimeRig`, and hands the brain to `MainSceneWiring`. Map generation remains enemy-neutral; lifecycle cleanup is exposed through a neutral registry event.

**Tech Stack:** Unity 6000.3.13f1, C#, Unity Test Framework/NUnit, Unity AI Navigation/NavMesh, Unity MCP for prefab and binary-scene work.

## Global Constraints

- Source of truth: `docs/superpowers/specs/2026-08-09-generated-map-monster-integration-design.md` and `TECHNICAL GAME DESIGN DOCUMENT.md`.
- Target the real `Assets/Scenes/MainGame.unity`; fixtures alone do not complete the feature.
- Keep dependency direction `map generation -> neutral registry data -> gameplay gates -> monster AI`.
- Do not add `MonsterBrain` or other gameplay references to the map-generation assembly.
- Produce exactly one monster; no pooling, respawn system, second monster, final model, animation, audio, VFX, multiplayer, or save work.
- Preserve current procedural topology and all authored anchor positions.
- Use existing `MonsterSpawnCandidate` and `StalkPoint_*` anchors.
- Spawn selection uses complete NavMesh path length, default minimum `24 m`, top-three distant seeded choice, and the documented fallback order.
- Do not rely on scene order, `MapReady` subscription order, or frame delays.
- Wire all runtime references before activating the monster root.
- Cover configured 2-, 3-, and 4-floor maps, including seed `1042` and at least one additional seed.
- Report static, automated, and live Play Mode evidence separately.

---

## File Map

**Create**

- `Assets/_Project/Code/Gameplay/Player/IPlayerVelocitySource.cs` — production velocity-source contract shared by real and test players.
- `Assets/_Project/Code/Gameplay/Monster/MonsterRuntimeRig.cs` — inactive-prefab initialization contract.
- `Assets/_Project/Code/Gameplay/Monster/MonsterSpawnSelector.cs` — deterministic selection over already validated spawn options.
- `Assets/_Project/Code/Gameplay/Monster/GeneratedStalkPointMetadata.cs` — name/module-to-metadata inference.
- `Assets/_Project/Code/Gameplay/Monster/MapMonsterGate.cs` — readiness, runtime stalk conversion, NavMesh option collection, spawn, and cleanup.
- `Assets/_Project/Code/Tests/EditMode/Game.Gameplay.EditModeTests.asmdef` — fast pure gameplay test assembly.
- `Assets/_Project/Code/Tests/EditMode/MonsterSpawnSelectorTests.cs` — selector policy tests.
- `Assets/_Project/Code/Tests/EditMode/GeneratedStalkPointMetadataTests.cs` — metadata inference tests.
- `Assets/_Project/Code/Tests/PlayMode/MonsterRuntimeRigTests.cs` — prefab-contract initialization tests.
- `Assets/_Project/Code/Tests/PlayMode/MainSceneWiringMonsterTests.cs` — dynamic event wiring tests.
- `Assets/_Project/Code/Tests/PlayMode/MapMonsterGateTests.cs` — generated-map integration and lifecycle tests.
- `Assets/_Project/Prefabs/Monster.prefab` — inactive debug monster rig, created through the Unity Editor.
- `Assets/_Project/Materials/MonsterDebugBody.mat` — dark debug body material.
- `Assets/_Project/Materials/MonsterDebugEye.mat` — emissive debug eye material.

**Modify**

- `Assets/Gloomveil/MapGeneration/Runtime/Unity/GeneratedMapRegistry.cs` — neutral `MapCleared` event.
- `Assets/Gloomveil/MapGeneration/Tests/EditMode/MapModuleResolverSmokeTests.cs` — add registry lifecycle coverage, or create `GeneratedMapRegistryTests.cs` in the same folder if keeping the resolver test focused.
- `Assets/_Project/Code/Gameplay/World/MapLightGate.cs` — build identity/event and module-to-zone lookup.
- `Assets/_Project/Code/Gameplay/World/MapSafeZoneGate.cs` — build identity/event.
- `Assets/_Project/Code/Tests/PlayMode/MapLightGateTests.cs` — light readiness/lookup coverage.
- `Assets/_Project/Code/Tests/PlayMode/MapSafeZoneGateTests.cs` — SafeZone readiness coverage.
- `Assets/_Project/Code/Gameplay/Player/TestPlayerController.cs` — consume the extracted interface.
- `Assets/_Project/Code/Gameplay/Player/PlayerController.cs` — implement `IPlayerVelocitySource` without changing movement.
- `Assets/_Project/Code/Gameplay/Monster/StalkPoint.cs` — configure static darkness for intentionally unlit modules.
- `Assets/_Project/Code/Gameplay/MainSceneWiring.cs` — dynamic monster bind/unbind and named handlers.
- `Assets/_Project/Prefabs/Player.prefab` — add `PlayerVisionModel` through Unity.
- `Assets/Scenes/MainGame.unity` — add and serialize `MapMonsterGate` through Unity.

---

### Task 1: Neutral map lifecycle and gate readiness

**Files:**
- Modify: `Assets/Gloomveil/MapGeneration/Runtime/Unity/GeneratedMapRegistry.cs`
- Create: `Assets/Gloomveil/MapGeneration/Tests/EditMode/GeneratedMapRegistryTests.cs`
- Modify: `Assets/_Project/Code/Gameplay/World/MapLightGate.cs`
- Modify: `Assets/_Project/Code/Gameplay/World/MapSafeZoneGate.cs`
- Modify: `Assets/_Project/Code/Tests/PlayMode/MapLightGateTests.cs`
- Modify: `Assets/_Project/Code/Tests/PlayMode/MapSafeZoneGateTests.cs`

**Interfaces:**
- Produces: `GeneratedMapRegistry.MapCleared`, `MapLightGate.BuildCompleted`, `MapLightGate.IsBuiltFor`, `MapLightGate.TryGetLightZone`, `MapSafeZoneGate.BuildCompleted`, `MapSafeZoneGate.IsBuiltFor`.
- Consumes: existing `GeneratedMapRegistry.MapReady` and `GeneratedMapRuntimeData` identity.

- [ ] **Step 1: Write the failing registry lifecycle test**

Add a focused EditMode test that publishes a minimal `GeneratedMapRuntimeData`, calls `Clear`, and pins event order:

```csharp
[Test]
public void Clear_RemovesCurrentBeforeRaisingMapCleared()
{
    var go = new GameObject("Registry");
    var registry = go.AddComponent<GeneratedMapRegistry>();
    var root = new GameObject("GeneratedMap");
    GeneratedMapRuntimeData data = new GeneratedMapRuntimeData(
        root,
        Array.Empty<GeneratedMapModule>(),
        Array.Empty<Transform>(),
        Array.Empty<Transform>(),
        Array.Empty<Transform>(),
        Array.Empty<Transform>(),
        Array.Empty<Transform>(),
        null,
        null,
        null,
        null,
        1042,
        1,
        1,
        2,
        "registry-test");
    GeneratedMapRuntimeData observed = null;
    bool wasReadyInsideCallback = true;

    registry.MapCleared += cleared =>
    {
        observed = cleared;
        wasReadyInsideCallback = registry.IsReady;
    };

    registry.Publish(data);
    registry.Clear();

    Assert.That(observed, Is.SameAs(data));
    Assert.That(wasReadyInsideCallback, Is.False);
    Object.DestroyImmediate(go);
    Object.DestroyImmediate(root);
}
```

- [ ] **Step 2: Run the focused EditMode test and confirm the intended failure**

Use Unity MCP `run_tests` in EditMode filtered to `GeneratedMapRegistryTests.Clear_RemovesCurrentBeforeRaisingMapCleared`.

Expected: compile/test failure because `MapCleared` does not exist.

- [ ] **Step 3: Add the neutral lifecycle event**

Implement exactly this behavior:

```csharp
public event Action<GeneratedMapRuntimeData> MapCleared;

public void Clear()
{
    if (current == null) return;
    GeneratedMapRuntimeData cleared = current;
    current = null;
    MapCleared?.Invoke(cleared);
}
```

- [ ] **Step 4: Write failing readiness tests for both gameplay gates**

In the existing gate fixtures, subscribe before generation and assert completion is raised after spawned collections are populated:

```csharp
GeneratedMapRuntimeData completedFor = null;
fixture.Gate.BuildCompleted += data => completedFor = data;
fixture.Bootstrap.Generate(2024);

Assert.That(completedFor, Is.SameAs(fixture.Registry.Current));
Assert.That(fixture.Gate.IsBuiltFor(fixture.Registry.Current), Is.True);
```

For `MapLightGate`, also assert every spawned zone is retrievable by the parent module of one of its source anchors. Add an empty-anchor fixture assertion that completion still fires.

- [ ] **Step 5: Run the two focused PlayMode test classes and verify failure**

Use Unity MCP `run_tests` in PlayMode filtered to `MapLightGateTests` and `MapSafeZoneGateTests`.

Expected: compile failure because readiness members do not exist.

- [ ] **Step 6: Implement idempotent readiness and light lookup**

Add to both gates:

```csharp
public event Action<GeneratedMapRuntimeData> BuildCompleted;
private GeneratedMapRuntimeData builtFor;
public bool IsBuiltFor(GeneratedMapRuntimeData data) => ReferenceEquals(builtFor, data);
```

At every successful or empty-anchor exit from `SpawnAll`, set `builtFor = data` and invoke `BuildCompleted(data)` after collections are finalized.

In `MapLightGate`, maintain:

```csharp
private readonly Dictionary<GeneratedMapModule, LightZone> zonesByModule = new();
public bool TryGetLightZone(GeneratedMapModule module, out LightZone zone) =>
    module != null && zonesByModule.TryGetValue(module, out zone);
```

Populate it only for built zones and clear it in `ClearSpawned`. Deliberately omitted corridor modules must return false.

- [ ] **Step 7: Run focused and regression tests**

Run the registry EditMode test and both entire gate PlayMode test classes.

Expected: all pass, with no new Console errors.

- [ ] **Step 8: Commit the lifecycle slice**

```powershell
git add -- Assets/Gloomveil/MapGeneration/Runtime/Unity/GeneratedMapRegistry.cs Assets/Gloomveil/MapGeneration/Tests/EditMode/GeneratedMapRegistryTests.cs Assets/Gloomveil/MapGeneration/Tests/EditMode/GeneratedMapRegistryTests.cs.meta Assets/_Project/Code/Gameplay/World/MapLightGate.cs Assets/_Project/Code/Gameplay/World/MapSafeZoneGate.cs Assets/_Project/Code/Tests/PlayMode/MapLightGateTests.cs Assets/_Project/Code/Tests/PlayMode/MapSafeZoneGateTests.cs
git commit -m "feat: expose generated gameplay readiness"
```

### Task 2: Production player velocity contract and monster runtime rig

**Files:**
- Create: `Assets/_Project/Code/Gameplay/Player/IPlayerVelocitySource.cs`
- Modify: `Assets/_Project/Code/Gameplay/Player/TestPlayerController.cs`
- Modify: `Assets/_Project/Code/Gameplay/Player/PlayerController.cs`
- Create: `Assets/_Project/Code/Gameplay/Monster/MonsterRuntimeRig.cs`
- Create: `Assets/_Project/Code/Tests/PlayMode/MonsterRuntimeRigTests.cs`

**Interfaces:**
- Produces: `IPlayerVelocitySource.Velocity`, `MonsterRuntimeRig.Initialize(...)`, `MonsterRuntimeRig.Brain`, `MonsterRuntimeRig.Perception`.
- Consumes: existing bind APIs on monster components and `PlayerVisionModel`.

- [ ] **Step 1: Write the failing real-player contract test**

```csharp
[Test]
public void PlayerController_ImplementsVelocitySource()
{
    Assert.That(typeof(IPlayerVelocitySource).IsAssignableFrom(typeof(PlayerController)), Is.True);
}
```

- [ ] **Step 2: Extract and implement the interface**

Create the dedicated file:

```csharp
using UnityEngine;

namespace Game.Gameplay.Player
{
    public interface IPlayerVelocitySource
    {
        Vector3 Velocity { get; }
    }
}
```

Remove the duplicate declaration from `TestPlayerController.cs`. Change only the real player's declaration:

```csharp
public class PlayerController : MonoBehaviour, IPlayerControlGate, IPlayerVelocitySource
```

Keep its existing Rigidbody-backed `Velocity` property unchanged.

- [ ] **Step 3: Write the failing runtime-rig initialization test**

Build an inactive test hierarchy containing the same component types as the planned prefab, then call:

```csharp
var velocitySource = player.AddComponent<VelocitySourceStub>();
bool initialized = rig.Initialize(
    config,
    player.transform,
    velocitySource,
    gazeDetector,
    safeZoneSet,
    stalkPointSet,
    visionModel);

Assert.That(initialized, Is.True);
Assert.That(rig.Brain.Player, Is.SameAs(player.transform));
Assert.That(rig.Brain.StalkPoints, Is.SameAs(stalkPointSet));
Assert.That(rig.Brain.VisionModel, Is.SameAs(visionModel));
Assert.That(rig.Perception.HasVelocitySource, Is.True);
Assert.That(gazeReceiver.Owner, Is.SameAs(rig.Brain));
```

Define the test-only velocity source in the test class:

```csharp
private sealed class VelocitySourceStub : MonoBehaviour, IPlayerVelocitySource
{
    public Vector3 Velocity => new Vector3(1f, 0f, 0f);
}
```

Add a second test with a missing `MonsterGazeReceiver`; assert `Initialize` returns false and the root stays inactive.

- [ ] **Step 4: Run the focused PlayMode tests and confirm failure**

Use Unity MCP PlayMode tests filtered to `MonsterRuntimeRigTests`.

Expected: compile failure because `MonsterRuntimeRig` does not exist.

- [ ] **Step 5: Implement `MonsterRuntimeRig`**

Use serialized internal references with null-safe self-resolution and one initialization entry point:

```csharp
public bool Initialize(
    MonsterConfig config,
    Transform player,
    IPlayerVelocitySource velocitySource,
    PlayerGazeDetector gazeDetector,
    SafeZoneRuntimeSet safeZones,
    StalkPointRuntimeSet stalkPoints,
    PlayerVisionModel visionModel)
```

Validation requires config, player, velocity source, detector with Camera, both runtime sets, vision model, brain, navigator, perception, anger, gaze receiver, visual driver, and body renderer. On success, call existing bind methods in dependency order and expose:

```csharp
public MonsterBrain Brain => brain;
public MonsterPerception Perception => perception;
public bool IsInitialized { get; private set; }
```

Do not activate the root in this class; activation remains the gate's final step.

- [ ] **Step 6: Run focused tests and compile validation**

Run `MonsterRuntimeRigTests`, then validate `MonsterRuntimeRig.cs` through Unity MCP script validation and inspect Console errors.

Expected: tests pass and no compile errors.

- [ ] **Step 7: Commit the runtime contract slice**

```powershell
git add -- Assets/_Project/Code/Gameplay/Player/IPlayerVelocitySource.cs Assets/_Project/Code/Gameplay/Player/IPlayerVelocitySource.cs.meta Assets/_Project/Code/Gameplay/Player/TestPlayerController.cs Assets/_Project/Code/Gameplay/Player/PlayerController.cs Assets/_Project/Code/Gameplay/Monster/MonsterRuntimeRig.cs Assets/_Project/Code/Gameplay/Monster/MonsterRuntimeRig.cs.meta Assets/_Project/Code/Tests/PlayMode/MonsterRuntimeRigTests.cs Assets/_Project/Code/Tests/PlayMode/MonsterRuntimeRigTests.cs.meta
git commit -m "feat: add monster runtime rig contract"
```

### Task 3: Deterministic spawn policy and generated stalk metadata

**Files:**
- Create: `Assets/_Project/Code/Tests/EditMode/Game.Gameplay.EditModeTests.asmdef`
- Create: `Assets/_Project/Code/Tests/EditMode/MonsterSpawnSelectorTests.cs`
- Create: `Assets/_Project/Code/Tests/EditMode/GeneratedStalkPointMetadataTests.cs`
- Create: `Assets/_Project/Code/Gameplay/Monster/MonsterSpawnSelector.cs`
- Create: `Assets/_Project/Code/Gameplay/Monster/GeneratedStalkPointMetadata.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/StalkPoint.cs`

**Interfaces:**
- Produces: `MonsterSpawnOption`, `MonsterSpawnTier`, `MonsterSpawnSelectionReason`, `MonsterSpawnSelector.TrySelect`, `GeneratedStalkPointMetadata.Infer`, `StalkPoint.ConfigureStaticDarkness`.
- Consumes: no scene state and no NavMesh API; options already contain validated positions/path lengths.

- [ ] **Step 1: Create the EditMode test assembly**

```json
{
  "name": "Game.Gameplay.EditModeTests",
  "rootNamespace": "Game.Gameplay.Tests",
  "references": ["Game.Gameplay", "Gloomveil.MapGeneration"],
  "optionalUnityReferences": ["TestAssemblies"],
  "includePlatforms": ["Editor"]
}
```

- [ ] **Step 2: Write failing selector policy tests**

Use helper options with unique stable keys and assert:

```csharp
MonsterSpawnOption[] options =
{
    Option("A", 30f, MonsterSpawnTier.OrdinaryCandidate),
    Option("B", 40f, MonsterSpawnTier.OrdinaryCandidate),
    Option("C", 50f, MonsterSpawnTier.OrdinaryCandidate),
    Option("D", 60f, MonsterSpawnTier.OrdinaryCandidate),
    Option("Fuse", 80f, MonsterSpawnTier.RelaxedCandidate),
};

Assert.That(MonsterSpawnSelector.TrySelect(options, 1042, 24f, out var first, out var reason), Is.True);
Assert.That(MonsterSpawnSelector.TrySelect(options.Reverse().ToArray(), 1042, 24f, out var second, out _), Is.True);
Assert.That(second.StableKey, Is.EqualTo(first.StableKey));
Assert.That(new[] { "B", "C", "D" }, Does.Contain(first.StableKey));
Assert.That(reason, Is.EqualTo(MonsterSpawnSelectionReason.SeededDistant));
```

Define the helper in the test class so no scene object is required:

```csharp
private static MonsterSpawnOption Option(string key, float pathLength, MonsterSpawnTier tier) =>
    new MonsterSpawnOption(
        key,
        new Vector3(pathLength, 0f, 0f),
        0f,
        pathLength,
        tier);
```

Add tests for below-minimum farthest fallback, relaxed-role fallback, stalk fallback, and empty input failure.

- [ ] **Step 3: Write failing metadata inference tests**

Pin the authored conventions:

```csharp
[TestCase("StalkPoint_Corner", "PF_TCOR_Straight_1x1_01", true, false, false, true)]
[TestCase("StalkPoint_DoorSide", "PF_TCOR_TJunction_1x1_01", false, true, false, true)]
[TestCase("StalkPoint_DoorSide", "PF_TCOR_DeadEnd_1x1_01", false, false, true, true)]
[TestCase("StalkPoint_StairLanding", "PF_TSTA_Straight_1x2_01", false, false, false, false)]
public void Infer_UsesAnchorAndModuleNames(...)
```

- [ ] **Step 4: Run the two EditMode classes and confirm failure**

Use Unity MCP EditMode tests filtered to `MonsterSpawnSelectorTests` and `GeneratedStalkPointMetadataTests`.

Expected: compile failure because the production types do not exist.

- [ ] **Step 5: Implement the pure selector**

Define immutable options with `StableKey`, `Position`, `Yaw`, `PathLength`, and `Tier`. The algorithm must:

```csharp
public static bool TrySelect(
    IReadOnlyList<MonsterSpawnOption> options,
    int seed,
    float minimumPathDistance,
    out MonsterSpawnOption selected,
    out MonsterSpawnSelectionReason reason)
```

- stable-sort ordinary candidates by path length descending then `StableKey` ordinal;
- choose with `new System.Random(seed ^ 0x4D4F4E53)` from at most the first three ordinary candidates meeting the minimum;
- otherwise choose farthest ordinary, then farthest relaxed, then farthest stalk fallback;
- return false for no options.

- [ ] **Step 6: Implement metadata inference and static darkness configuration**

`GeneratedStalkPointMetadata.Infer(anchorName, catalogEntryId)` uses ordinal-ignore-case checks for `Corner`, `TJunction`, `CrossJunction`, `DeadEnd`, and `StairLanding`.

Add to `StalkPoint`:

```csharp
public void ConfigureStaticDarkness(float darkness) =>
    staticDarknessLevel = Mathf.Clamp01(darkness);
```

- [ ] **Step 7: Run EditMode tests and regression compile**

Run both new test classes and inspect Console errors.

Expected: all selector and metadata tests pass.

- [ ] **Step 8: Commit the policy slice**

```powershell
git add -- Assets/_Project/Code/Tests/EditMode Assets/_Project/Code/Tests/EditMode.meta Assets/_Project/Code/Gameplay/Monster/MonsterSpawnSelector.cs Assets/_Project/Code/Gameplay/Monster/MonsterSpawnSelector.cs.meta Assets/_Project/Code/Gameplay/Monster/GeneratedStalkPointMetadata.cs Assets/_Project/Code/Gameplay/Monster/GeneratedStalkPointMetadata.cs.meta Assets/_Project/Code/Gameplay/Monster/StalkPoint.cs
git commit -m "feat: define generated monster spawn policy"
```

### Task 4: Dynamic monster wiring in the main composition root

**Files:**
- Modify: `Assets/_Project/Code/Gameplay/MainSceneWiring.cs`
- Create: `Assets/_Project/Code/Tests/PlayMode/MainSceneWiringMonsterTests.cs`

**Interfaces:**
- Produces: `MainSceneWiring.BindMonster(MonsterBrain)`, `MainSceneWiring.UnbindMonster(MonsterBrain)`, `MainSceneWiring.BoundMonster`.
- Consumes: initialized `MonsterBrain`, existing game flow/objective events, existing light director.

- [ ] **Step 1: Write failing death and replacement tests**

Create a fixture with `MainSceneWiring`, `GameFlowController`, `ObjectiveManager`, `MapExitController`, and `FuseMiniGameController`. Bind a brain and raise kill:

```csharp
wiring.BindMonster(firstBrain);
firstBrain.RaiseKill();
Assert.That(flow.CurrentStateName, Is.EqualTo(nameof(DeathState)));
```

Reset the flow, bind a second brain, raise kill on the old brain, and assert the flow does not change. Raising kill on the second brain must enter Death.

- [ ] **Step 2: Write failing fuse and Final Chase tests**

Invoke `ObjectiveManager`'s private forwarding methods through a test helper using `BindingFlags.Instance | BindingFlags.NonPublic`, then assert only `wiring.BoundMonster` receives position/active changes.

Build a fully initialized inactive/active brain fixture through `MonsterRuntimeRig`, enter `FinalChaseState`, and assert:

```csharp
Assert.That(brain.IsRelentless, Is.True);
Assert.That(brain.CurrentStateName, Is.EqualTo(nameof(AggressiveState)));
```

Also bind a brain after the flow already entered Final Chase and assert immediate application.

- [ ] **Step 3: Run the focused PlayMode class and confirm failure**

Expected: compile failure because dynamic bind methods do not exist.

- [ ] **Step 4: Refactor `MainSceneWiring` to named handlers**

Retain the existing core `Wire` guard. Subscribe once to objective/game-flow events using named methods. Implement replaceable brain ownership:

```csharp
public MonsterBrain BoundMonster => brain;

public void BindMonster(MonsterBrain monsterBrain)
{
    if (ReferenceEquals(brain, monsterBrain)) return;
    if (brain != null) brain.OnKill -= HandleMonsterKill;
    brain = monsterBrain;
    if (brain == null) return;
    brain.OnKill += HandleMonsterKill;
    ApplyCurrentStateToMonster();
}

public void UnbindMonster(MonsterBrain monsterBrain)
{
    if (!ReferenceEquals(brain, monsterBrain)) return;
    brain.OnKill -= HandleMonsterKill;
    brain = null;
}
```

`HandleGameStateChanged` preserves existing light/minigame behavior and applies Final Chase to the current brain. `HandleFuseStarted/Stopped` no-op when no brain exists. `OnDestroy` unsubscribes core handlers and the current brain.

- [ ] **Step 5: Run focused and existing wiring tests**

Run `MainSceneWiringMonsterTests` and `MainSceneWiringLightTests`.

Expected: all pass; existing light shutdown curve remains unchanged.

- [ ] **Step 6: Commit the composition-root slice**

```powershell
git add -- Assets/_Project/Code/Gameplay/MainSceneWiring.cs Assets/_Project/Code/Tests/PlayMode/MainSceneWiringMonsterTests.cs Assets/_Project/Code/Tests/PlayMode/MainSceneWiringMonsterTests.cs.meta
git commit -m "feat: wire runtime monster into game flow"
```

### Task 5: Generated-map monster gate

**Files:**
- Create: `Assets/_Project/Code/Gameplay/Monster/MapMonsterGate.cs`
- Create: `Assets/_Project/Code/Tests/PlayMode/MapMonsterGateTests.cs`

**Interfaces:**
- Consumes: all Tasks 1-4 interfaces plus Unity NavMesh.
- Produces: `MapMonsterGate.Bind(...)`, `SpawnedBrain`, `SpawnedStalkPoints`, `SelectedSpawnPathDistance`, `SpawnCount`, and complete runtime behavior.

- [ ] **Step 1: Write the failing readiness-order test**

Create a generated-map fixture with real registry/bootstrap/light/SafeZone gates and an inactive test monster rig. Bind `MapMonsterGate`, keep the light-gate GameObject inactive, then publish a map. The active SafeZone gate completes but no monster may spawn. Activate the light gate; its `OnEnable` observes the current publication and completes, after which exactly one monster must exist. Repeat with light active and SafeZone inactive.

```csharp
Assert.That(fixture.MonsterGate.SpawnedBrain, Is.Null);
fixture.LightGate.gameObject.SetActive(true);
Assert.That(fixture.MonsterGate.SpawnedBrain, Is.Not.Null);
Assert.That(fixture.MonsterGate.SpawnCount, Is.EqualTo(1));
```

- [ ] **Step 2: Write failing stalk conversion and binding tests**

After generation, assert:

```csharp
Assert.That(gate.SpawnedStalkPoints.Count, Is.EqualTo(registry.Current.StalkPoints.Count));
Assert.That(stalkSet.Items.Count, Is.EqualTo(registry.Current.StalkPoints.Count));
Assert.That(gate.SpawnedBrain.StalkPoints, Is.SameAs(stalkSet));
Assert.That(gate.SpawnedBrain.Perception.HasVelocitySource, Is.True);
Assert.That(gate.SpawnedBrain.VisionModel, Is.SameAs(visionModel));
```

For a point in a lit module assert bound darkness follows the zone; for a deliberately omitted corridor assert `DarknessLevel == 1f`.

- [ ] **Step 3: Write failing spawn-path and cleanup tests**

Assert the active agent is on NavMesh and calculate a complete path from `StartAnchor` to the spawned root. Pin same-seed same-position behavior across fixture rebuilds. Call `MapGenerationBootstrap.ClearGeneratedMap` and assert brain null, no spawned stalk registrations, and `MainSceneWiring.BoundMonster` null. Add a separate direct `registry.Clear()` case to pin the gate's explicit component cleanup. Regenerate and assert a single new brain.

- [ ] **Step 4: Run `MapMonsterGateTests` and confirm failure**

Expected: compile failure because `MapMonsterGate` does not exist.

- [ ] **Step 5: Implement serialized dependencies and lifecycle subscriptions**

Provide a scene/test binding API matching serialized fields:

```csharp
public void Bind(
    GeneratedMapRegistry registry,
    MonsterRuntimeRig monsterPrefab,
    MonsterConfig config,
    PlayerController player,
    PlayerGazeDetector gazeDetector,
    PlayerVisionModel visionModel,
    SafeZoneRuntimeSet safeZoneSet,
    StalkPointRuntimeSet stalkPointSet,
    MapLightGate lightGate,
    MapSafeZoneGate safeZoneGate,
    MainSceneWiring wiring)
```

Subscribe idempotently to registry ready/cleared and both completion events. Store pending data and require `lightGate.IsBuiltFor(data) && safeZoneGate.IsBuiltFor(data)` before building.

Expose current-state diagnostics without maintaining a second counter:

```csharp
public MonsterBrain SpawnedBrain => spawnedRig != null ? spawnedRig.Brain : null;
public int SpawnCount => spawnedRig != null ? 1 : 0;
public IReadOnlyList<StalkPoint> SpawnedStalkPoints => spawnedStalkPoints;
public float SelectedSpawnPathDistance { get; private set; }
```

- [ ] **Step 6: Implement runtime stalk conversion**

For each non-null published anchor:

```csharp
GeneratedMapModule module = anchor.GetComponentInParent<GeneratedMapModule>();
GeneratedStalkPointMetadata metadata = GeneratedStalkPointMetadata.Infer(
    anchor.name,
    module != null ? module.CatalogEntryId : string.Empty);
StalkPoint point = anchor.gameObject.GetComponent<StalkPoint>() ?? anchor.gameObject.AddComponent<StalkPoint>();
point.ConfigureTopology(metadata.IsNearCorner, metadata.IsJunction, metadata.IsDeadEnd, metadata.AmbushAllowed);
LightZone zone = module != null && lightGate.TryGetLightZone(module, out var found) ? found : null;
if (module != null && zone == null) point.ConfigureStaticDarkness(1f);
point.Bind(safeZoneSet, zone, stalkPointSet);
```

Track every configured point in `spawnedStalkPoints`. Track components newly added by this gate in a separate `addedStalkPoints` list so cleanup never destroys a pre-existing authored component.

- [ ] **Step 7: Implement NavMesh option collection and selection**

For each candidate and fallback stalk transform, use bounded `NavMesh.SamplePosition` and `NavMesh.CalculatePath(StartAnchor.position, hit.position, NavMesh.AllAreas, path)`. Reject non-complete paths. Sum adjacent path corners for `PathLength` and create stable keys from source hierarchy/name plus invariant position coordinates.

Assign tiers:

- `RoomRole.None` spawn candidate -> `OrdinaryCandidate`
- Fuse/Exit spawn candidate -> `RelaxedCandidate`
- Start/SafeZone spawn candidate -> excluded
- published stalk position -> `StalkFallback`

Call `MonsterSpawnSelector.TrySelect` with the map seed and serialized `minimumInitialPathDistance = 24f`.

- [ ] **Step 8: Instantiate, initialize, wire, then activate**

Validate `monsterPrefab.gameObject.activeSelf == false` before instantiation; log an actionable error and do not instantiate an active prefab. Instantiate the inactive `MonsterRuntimeRig` at the selected position/yaw. Destroy the clone if `Initialize` returns false. On success:

```csharp
wiring.BindMonster(spawnedRig.Brain);
spawnedRig.gameObject.SetActive(true);
```

Record the selected path distance and build identity. Do not create another instance for duplicate callbacks.

- [ ] **Step 9: Implement cleanup**

On matching `MapCleared`, unsubscribe the current brain through `MainSceneWiring`, destroy the rig, disable and destroy only the tracked `StalkPoint` components added by this gate, then clear pending data/build guards. Disabling makes `StalkPoint.OnDisable` unregister immediately even if the generated root remains active. `MapMonsterGate.OnDisable` unsubscribes all external events.

- [ ] **Step 10: Run focused integration tests**

Run `MapMonsterGateTests`, `MonsterRuntimeRigTests`, and both readiness gate test classes.

Expected: all pass with exactly one active monster per publication and no Console exceptions.

- [ ] **Step 11: Commit the monster-gate slice**

```powershell
git add -- Assets/_Project/Code/Gameplay/Monster/MapMonsterGate.cs Assets/_Project/Code/Gameplay/Monster/MapMonsterGate.cs.meta Assets/_Project/Code/Tests/PlayMode/MapMonsterGateTests.cs Assets/_Project/Code/Tests/PlayMode/MapMonsterGateTests.cs.meta
git commit -m "feat: spawn monster from generated map"
```

### Task 6: Production prefab, player prefab, and real `MainGame` wiring

**Files:**
- Create through Unity: `Assets/_Project/Materials/MonsterDebugBody.mat`
- Create through Unity: `Assets/_Project/Materials/MonsterDebugEye.mat`
- Create through Unity: `Assets/_Project/Prefabs/Monster.prefab`
- Modify through Unity: `Assets/_Project/Prefabs/Player.prefab`
- Modify through Unity: `Assets/Scenes/MainGame.unity`

**Interfaces:**
- Consumes: compiled `MonsterRuntimeRig`, `MapMonsterGate`, existing config/runtime-set assets, and live scene objects.
- Produces: serialized production assets and scene references.

- [ ] **Step 1: Pin the correct Unity instance and validate compile state**

Read `mcpforunity://instances`, select `gelistirjam@<hash>`, confirm project root `C:/Users/metin/Documents/Unity/gelistirjam`, active scene `Assets/Scenes/MainGame.unity`, Edit Mode, idle compilation, and empty error Console. If a different project is active, do not mutate it; reopen/pin Gloomveil first.

- [ ] **Step 2: Create debug materials through Unity**

Create URP/Lit materials:

- `MonsterDebugBody.mat`: base color approximately `(0.12, 0.12, 0.16, 1)`.
- `MonsterDebugEye.mat`: dark yellow base with emission enabled around `(1, 0.9, 0.1)`.

- [ ] **Step 3: Build and save the inactive monster prefab**

Create this exact hierarchy in an empty staging root:

```text
Monster (inactive)
├── Body
├── GazeVolume
└── EyeVisual
```

Root values match the tested builder:

- `CapsuleCollider`: center `(0,1,0)`, height `2.2`, radius `0.35`.
- `NavMeshAgent`: radius `0.35`, height `2.2`, baseOffset `0`, acceleration `12`, angularSpeed `720`, stoppingDistance `0.3`.
- add navigator, perception, anger, brain, runtime rig.

`Body` is a capsule primitive with local position `(0,1,0)`, scale `(0.6,1.1,0.6)`, no collider, body material, and `MonsterVisualDriver`.

`GazeVolume` is at local `(0,1.2,0)` with a trigger sphere radius `0.7` and `MonsterGazeReceiver`.

`EyeVisual` is a sphere at local `(0,1.9,0.5)`, scale `0.16`, no collider, and eye material.

Assign all `MonsterRuntimeRig` internal references, save as `Assets/_Project/Prefabs/Monster.prefab`, and confirm the saved prefab root remains inactive.

- [ ] **Step 4: Add `PlayerVisionModel` to the shared player prefab**

Open `Assets/_Project/Prefabs/Player.prefab`, add exactly one `PlayerVisionModel` on the player root, save, exit Prefab Stage, and verify the `MainGame` player instance inherits it.

- [ ] **Step 5: Add and serialize `MapMonsterGate` in `MainGame`**

Create root `MonsterGate` with `MapMonsterGate`. Assign:

- registry: `MapGenerationRoot/GeneratedMapRegistry`
- monster prefab: `Assets/_Project/Prefabs/Monster.prefab`
- config: `Assets/_Project/Data/MonsterConfig_Default.asset`
- player: scene `Player/PlayerController`
- gaze detector: `Player/CameraPivot/PlayerCamera/PlayerGazeDetector`
- vision model: scene `Player/PlayerVisionModel`
- SafeZone set: `Assets/_Project/Data/SafeZoneRuntimeSet.asset`
- stalk set: `Assets/_Project/Data/StalkPointRuntimeSet.asset`
- light gate: scene `LightGate/MapLightGate`
- SafeZone gate: scene `SafeZoneGate/MapSafeZoneGate`
- wiring: scene `MainSceneWiring/MainSceneWiring`
- minimum path distance: `24`

Save `Assets/Scenes/MainGame.unity`.

- [ ] **Step 6: Validate serialized assets before Play Mode**

Reopen the prefab and scene; inspect component properties with Unity resources. Confirm one gate, one vision model, inactive prefab root, every required reference non-null, and no unexpected scene dirtiness after save.

- [ ] **Step 7: Commit production asset wiring**

After checking `git status` for Unity-generated `.meta` files belonging only to these new assets:

```powershell
git add -- Assets/_Project/Materials/MonsterDebugBody.mat Assets/_Project/Materials/MonsterDebugBody.mat.meta Assets/_Project/Materials/MonsterDebugEye.mat Assets/_Project/Materials/MonsterDebugEye.mat.meta Assets/_Project/Prefabs/Monster.prefab Assets/_Project/Prefabs/Monster.prefab.meta Assets/_Project/Prefabs/Player.prefab Assets/Scenes/MainGame.unity
git commit -m "feat: wire monster into main game scene"
```

### Task 7: Procedural coverage and live completion verification

**Files:**
- Modify: `Assets/_Project/Code/Tests/PlayMode/MapMonsterGateTests.cs`
- Modify only if required by verified defects: files from Tasks 1-6.

**Interfaces:**
- Consumes: completed production feature.
- Produces: automated 2/3/4-floor evidence and live `MainGame` evidence.

- [ ] **Step 1: Add parameterized 2/3/4-floor integration coverage**

Use a real catalog/config fixture and cases `(2,1042)`, `(3,1042)`, `(4,1042)`, plus `(2,2024)`. For every case:

```csharp
Assert.That(registry.IsReady, Is.True);
Assert.That(gate.SpawnedBrain, Is.Not.Null);
Assert.That(gate.SpawnCount, Is.EqualTo(1));
Assert.That(gate.SpawnedStalkPoints, Is.Not.Empty);
Assert.That(gate.SpawnedBrain.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True);
Assert.That(gate.SelectedSpawnPathDistance, Is.GreaterThan(0f));
```

Calculate and assert a complete Start-to-monster path. Generate the same case twice and assert the same selected position within `0.01 m`.

- [ ] **Step 2: Run focused EditMode tests**

Run:

- `GeneratedMapRegistryTests`
- `MonsterSpawnSelectorTests`
- `GeneratedStalkPointMetadataTests`

Expected: all pass.

- [ ] **Step 3: Run focused PlayMode tests**

Run:

- `MonsterRuntimeRigTests`
- `MainSceneWiringMonsterTests`
- `MapMonsterGateTests`
- `MapLightGateTests`
- `MapSafeZoneGateTests`
- `PlayerSpawnGateTests`

Expected: all pass with no test-run Console exceptions.

- [ ] **Step 4: Run affected existing suites**

Run the entire `Gloomveil.MapGeneration.EditModeTests`, `Gloomveil.MapGeneration.PlayModeTests`, `Game.Gameplay.EditModeTests`, and `Game.Gameplay.PlayModeTests` assemblies.

Expected: all pass. Record counts from actual Unity results.

- [ ] **Step 5: Verify the real scene in Play Mode**

Load `Assets/Scenes/MainGame.unity`, clear Console, enter Play Mode, and wait for registry readiness. Inspect:

- exactly one active `Monster` under the runtime gate;
- active `NavMeshAgent.isOnNavMesh`;
- non-null brain player/stalk/vision references;
- current AI state is a valid initialized state, not `-`;
- player and monster are separated by the recorded complete NavMesh path;
- no `NullReferenceException`, missing-reference error, partial-path error, or duplicate-spawn warning.

Exercise gaze by looking at the debug eye, begin/cancel one fuse interaction, and trigger/observe Final Chase or invoke the existing public state transition in a controlled Editor test. Confirm gaze target, fuse awareness, and relentless Aggressive behavior respectively. Exercise `RaiseKill` only in a controlled verification path and confirm Death state.

- [ ] **Step 6: Exit Play Mode and inspect artifacts**

Confirm no unintended scene/prefab modifications were produced by Play Mode. Read Unity Console, latest test results, and `git status`. Run:

```powershell
git diff --check
git status --short
```

- [ ] **Step 7: Commit final test additions or verified corrections**

If Task 7 changed only coverage:

```powershell
git add -- Assets/_Project/Code/Tests/PlayMode/MapMonsterGateTests.cs
git commit -m "test: cover monster across generated floors"
```

If verification required a production correction, stage only the exact corrected source/test files and use a commit message naming the corrected behavior.

- [ ] **Step 8: Final evidence report**

Report:

- commit SHAs created by Tasks 1-7;
- exact EditMode and PlayMode pass counts;
- seeds/floor counts exercised;
- real-scene monster count, initial state, and NavMesh status;
- gaze/fuse/Final Chase/Death behaviors actually exercised;
- any remaining behavior not verified live.

Do not describe the implementation as complete if the real `MainGame` scene was not run successfully.
