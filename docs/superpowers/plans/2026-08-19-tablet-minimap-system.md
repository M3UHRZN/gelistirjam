# Tablet Minimap System Implementation Plan

> **For agentic workers:** Execute inline in the current `minimap` branch. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the UI-independent procedural minimap topology and runtime navigation model.

**Architecture:** Pure map-generation code produces wall/door/stair topology and weighted graph routes. A small Observer-style `MinimapRuntimeModel` adapts current player, fuse, and exit state into immutable snapshots for the later uGUI view.

**Tech Stack:** Unity C#, NUnit EditMode tests, existing `Gloomveil.MapGeneration` and `Game.Gameplay` assemblies.

**Spec:** `docs/superpowers/specs/2026-08-19-tablet-minimap-system-design.md`

## Global Constraints

- Do not run or add PlayMode tests.
- Do not modify scenes, prefabs, Canvas objects, tablet assets, or UI code.
- Keep tests focused on pure topology and path behavior.
- Do not commit, merge, or push.

---

### Task 1: Pure minimap topology

**Files:**
- Create: `Assets/Gloomveil/MapGeneration/Tests/EditMode/MinimapTopologyTests.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/MinimapTopology.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/MinimapTopologyBuilder.cs`

**Interfaces:**
- Consumes: `MapLayout`, `GridEdgeKey`, room/stair ports, corridor connection masks.
- Produces: `MinimapTopologyBuilder.Build(MapLayout)` and immutable `MinimapTopology.Walls` / `Stairs`.

- [x] Write an EditMode test proving an open room port is absent from `Walls`, while closed perimeter edges remain.
- [x] Run the focused EditMode test and confirm it fails because minimap topology types do not exist.
- [x] Implement immutable topology types and perimeter/open-edge conversion.
- [x] Re-run the focused EditMode test and confirm it passes.

### Task 2: Weighted route reconstruction

**Files:**
- Create: `Assets/Gloomveil/MapGeneration/Tests/EditMode/MapGraphPathTests.cs`
- Modify: `Assets/Gloomveil/MapGeneration/Runtime/Core/MapGraphSearch.cs`

**Interfaces:**
- Consumes: `MapTraversalGraph`, start node ID, target node ID.
- Produces: `MapGraphSearch.WeightedShortestPath(...)` returning ordered `MapTraversalNode` values.

- [x] Write a test graph where the cheapest path has more edges than the expensive direct edge.
- [x] Run the focused test and confirm failure because `WeightedShortestPath` is missing.
- [x] Implement deterministic Dijkstra predecessor reconstruction; return one node for start equals target and an empty list for unreachable targets.
- [x] Re-run the focused test and confirm it passes.

### Task 3: Publish minimap generation data

**Files:**
- Modify: `Assets/Gloomveil/MapGeneration/Runtime/Unity/GeneratedMapRuntimeData.cs`
- Modify: `Assets/Gloomveil/MapGeneration/Runtime/Unity/ModularMapBuilder.cs`

**Interfaces:**
- Produces: `TraversalGraph`, `MinimapTopology`, `UnitSize`, and `FloorHeight` on the runtime snapshot.

- [x] Add backward-compatible optional constructor parameters to `GeneratedMapRuntimeData`.
- [x] Build topology once in `ModularMapBuilder` and publish it with the existing neutral runtime data.
- [x] Compile the map-generation assembly and EditMode test assembly.

### Task 4: UI-independent runtime model

**Files:**
- Create: `Assets/_Project/Code/Gameplay/Navigation/MinimapRuntimeSnapshot.cs`
- Create: `Assets/_Project/Code/Gameplay/Navigation/MinimapRuntimeModel.cs`

**Interfaces:**
- Consumes: `GeneratedMapRegistry`, player `Transform`, `MapFuseGate`, and `MapExitController`.
- Produces: `Current`, `SnapshotChanged`, `Bind(...)`, and `RefreshNow()` for a later presenter.

- [x] Implement immutable snapshot fields for topology, floor, player map position/yaw, fuse direction, and exit route.
- [x] Implement Observer subscriptions with symmetric unsubscribe and a low-frequency refresh interval.
- [x] Select the nearest incomplete fuse by weighted graph distance and point at its next route node.
- [x] When the exit opens, suppress fuse direction and publish a weighted route to the exit module.
- [x] Compile `Game.Gameplay` without creating any UI or scene wiring.

### Task 5: Focused verification

- [x] Run only the new/affected EditMode tests.
- [x] Run C# assembly compilation.
- [x] Inspect `git diff --check`, `git status --short`, and the scoped diff.
- [x] Confirm no PlayMode, scene, prefab, Canvas, tablet, commit, merge, or push action occurred.
