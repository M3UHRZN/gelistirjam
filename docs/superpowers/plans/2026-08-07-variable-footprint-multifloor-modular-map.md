# Configurable Multi-Floor Variable-Footprint Modular Map Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a deterministic, seed-driven procedural maze with a configurable minimum of two floors, varied rectangular rooms, unequal corridor runs, multiple stairs between adjacent floors, and a neutral runtime contract for enemy AI.

**Architecture:** Keep a 4-metre integer placement lattice only for deterministic packing, collision checks, and socket alignment; never render one prefab for every lattice square. Pack variable-footprint rooms and stairs, route orthogonal corridors through unused lattice cells, compress straight corridor runs into 1-, 2-, and 3-unit modules, build a weighted traversal graph, assign gameplay roles, instantiate rotated prefabs, bake one runtime NavMesh, then publish a read-only map snapshot.

**Tech Stack:** Unity 6000.3.13f1, C#, Unity Test Framework 1.6.0, AI Navigation 2.0.12 (`NavMeshSurface`), ScriptableObject configuration, editor Gizmos.

## Global Constraints

- `floorCount` is Inspector-configurable with a hard minimum of 2 and no hard-coded maximum; acceptance coverage includes 2, 3, and 4 floors.
- Every adjacent floor pair contains at least `verticalLinksPerFloorPair` independent stairs; its hard minimum is 2.
- The hidden placement unit is 4 × 4 metres and floor-to-floor height is 4 metres.
- The placement lattice is an implementation detail. The builder must not fill every coordinate with a square prefab or expose a repeated 4 × 4 floor border.
- Required canonical room footprints are `1x2`, `2x2`, and `2x3` units: 4 × 8 m, 8 × 8 m, and 8 × 12 m. A 90° turn of `2x3` is the required `3x2` form.
- Room and stair footprint lists are data-driven; adding a later `1x3`, `3x3`, or another rectangular size must require catalogue/config data, not a generator code branch.
- Every default floor contains all three canonical room footprints and at least one non-square room at an odd quarter turn.
- Every accepted default layout contains `2x3` rooms in both even and odd quarter-turn classes, so both 8 × 12 m and 12 × 8 m (`3x2`) forms are present.
- Supported room rotations are 0°, 90°, 180°, and 270°. Cardinal duplicates are runtime rotations, not separate meshes.
- Straight corridor modules use lengths `1x1`, `1x2`, and `1x3` units: 4 m, 8 m, and 12 m. Room spacing and routed corridor distance also vary by seed.
- Corridor clear width remains 2.8 m for reliable player and monster traversal; length and surrounding space vary, not navigation width.
- Default generated layouts contain at least two distinct straight-corridor lengths and never use one repeated length for every run.
- Rooms and stairs reserve rectangular occupancy; no two occupied rectangles overlap on the same floor.
- A stair prefab spans exactly one 4 m floor interval and reserves its footprint on both participating floors. Stairs for consecutive floor pairs do not share an occupied rectangle.
- Use `System.Random` through one seeded wrapper; never use `UnityEngine.Random` for packing, routing, role assignment, or module selection.
- The same configuration and seed reproduce room rectangles, rotations, stairs, corridor tiles, module segmentation, roles, generated `PlacementId` values, and selected `CatalogEntryId` values.
- Start, exit, fuse, and safe-zone roles are assigned only to rooms. Corridors and stairs never receive those roles.
- Exit selection uses greatest weighted shortest-path distance from start, measured in placement units; unweighted cell-count BFS is not used for variable-length geometry.
- Fuse rooms cover `min(fuseCount, floorCount)` distinct floors before a floor receives another fuse.
- Each floor contains a connected route graph plus configured loop links; a floor may not be only a perfect-tree route.
- Map code does not reference `MonsterBrain`, enemy states, or an enemy namespace. Enemy AI consumes the published snapshot.
- Stair art uses visible steps and a simple continuous ramp collider so both player and `NavMeshAgent` can traverse it.
- Do not modify `Assets/Scenes/SampleScene.unity`; create `Assets/Scenes/MapGenerationTest.unity`.
- Do not add multiplayer, save/load, addressables, pooling, async streaming, terrain generation, ladders, elevators, destructible rooms, diagonal corridors, or arbitrary-angle modules.
- Commits stage targeted paths only; do not use `git add -A`.

## Deliberately Small Test Budget

Keep automated coverage to three focused files:

1. `VariableFootprintLayoutSmokeTests.cs`: rotation math plus one parameterized 2/3/4-floor determinism, overlap, connectivity, room-variety, corridor-variety, and stair-invariant check.
2. `MapModuleResolverSmokeTests.cs`: one rectangular-room rotation/socket case and one variable corridor-length resolution case.
3. `GeneratedMapPlayModeSmokeTests.cs`: one coroutine that sequentially performs the 2/3/4-floor build, NavMesh, and publication smoke path.

Do not add a 100-seed sweep, per-class micro-tests, performance benchmarks, or exhaustive prefab permutations. Manually inspect only seeds `1042` at 2 floors, `2718` at 3 floors, and `9001` at 4 floors.

## Spatial Model

The generator uses two different representations:

- **Hidden occupancy:** integer coordinates used to place rectangular rooms/stairs and route one-unit-wide corridor paths without overlap.
- **Visible placements:** one prefab per room, stair, corner/junction, or compressed straight corridor run. Empty occupancy coordinates produce no GameObject.

Room and stair roots use a floor-centred pivot. For a placement with southwest origin `(x,z)` and rotated size `(width,depth)`, the root position is:

~~~csharp
new Vector3(
    (x + width * 0.5f) * unitSize,
    floor * floorHeight,
    (z + depth * 0.5f) * unitSize);
~~~

All rectangles are half-open: `[x, x + width) × [z, z + depth)`. A 90° or 270° rotation swaps width and depth.

## Artist Parallel Track

The 3D artist can begin after Task 1 locks `docs/art/MODULAR_MAP_KIT.md`. P0 delivery is:

~~~text
Corridors
PF_COR_Straight_1x1_01   4 x 4 m
PF_COR_Straight_1x2_01   4 x 8 m
PF_COR_Straight_1x3_01   4 x 12 m
PF_COR_Corner_1x1_01
PF_COR_TJunction_1x1_01
PF_COR_CrossJunction_1x1_01
PF_COR_DeadEnd_1x1_01
PF_COR_DoorTransition_1x1_01

Rooms
PF_ROOM_1x2_01           4 x 8 m
PF_ROOM_2x2_01           8 x 8 m
PF_ROOM_2x3_01           8 x 12 m; runtime rotation supplies 12 x 8 m

Stairs
PF_STA_Straight_1x2_01   spans exactly 4 m vertically
PF_STA_Switchback_2x2_01 spans exactly 4 m vertically
~~~

Each base prefab must be verified at 0°, 90°, 180°, and 270° in Unity. Separate `North/East/South/West` files are not requested. P1 adds visual variants with suffixes `_02` and `_03` without changing footprints, pivots, sockets, or colliders. Every variant gets a unique `CatalogEntryId` and shares a `FamilyId` with its compatible `_01` module.

Large rooms expose multiple candidate boundary sockets:

~~~text
Socket_N_00, Socket_N_01, ...
Socket_E_00, Socket_E_01, ...
Socket_S_00, Socket_S_01, ...
Socket_W_00, Socket_W_01, ...
~~~

For a canonical `width × depth` footprint, north/south have `width` candidates and east/west have `depth` candidates. Each socket sits at the centre of its 4 m boundary interval, faces outward, and has matching `Portal_<side>_<index>` and `WallCap_<side>_<index>` children.

At floor level, exact local positions are:

~~~text
North/South index i:
x = (i + 0.5 - width / 2.0) * 4 m
z = +(depth * 2 m) for North, -(depth * 2 m) for South

East/West index i:
x = +(width * 2 m) for East, -(width * 2 m) for West
z = (i + 0.5 - depth / 2.0) * 4 m
~~~

Index increases along local +X for north/south and local +Z for east/west. Stair ports are `Socket_Bottom_<side>_<index>` at floor offset 0 and `Socket_Top_<side>_<index>` at floor offset 1; their landing anchors are `Landing_Bottom` and `Landing_Top`.

Canonical fixed openings and stair positions are:

| Prefab family | Canonical open ports |
|---|---|
| `Straight_1x1/1x2/1x3` | `N_00`, `S_00` |
| `Corner_1x1` | `N_00`, `E_00` |
| `TJunction_1x1` | `N_00`, `E_00`, `W_00` |
| `CrossJunction_1x1` | `N_00`, `E_00`, `S_00`, `W_00` |
| `DeadEnd_1x1` | `N_00` |
| `DoorTransition_1x1` | `N_00`, `S_00` |
| `STA_Straight_1x2` | bottom `S_00` at `(0,0,-4)`; top `N_00` at `(0,4,+4)` |
| `STA_Switchback_2x2` | bottom `S_00` at `(-2,0,-4)`; top `S_01` at `(+2,4,-4)` |

Door openings are 1.6 m wide and at least 3.0 m high; usable ceiling height is at least 3.2 m. Stair clear width and landing clear width are at least 2.8 m. A straight stair uses one ramp collider; a switchback uses one ramp collider per flight plus a simple flat landing collider. Rendered steps never replace those navigation colliders.

Art must avoid visible “notebook grid” cues: no painted 4 × 4 border around every unit, no identical prop centred in each unit, and no mandatory wall seam every 4 m. Tileable materials, long beams/pipes, asymmetric damage, and dressing variants should cross or disguise hidden module boundaries.

## Planned File Structure

~~~text
Assets/Gloomveil/MapGeneration/
├── Runtime/
│   ├── Core/
│   │   ├── GridCoord3.cs
│   │   ├── GridSize2.cs
│   │   ├── GridRect.cs
│   │   ├── CardinalDirection.cs
│   │   ├── SocketKey.cs
│   │   ├── GridEdgeKey.cs
│   │   ├── MapPort.cs
│   │   ├── FootprintSpec.cs
│   │   ├── RoomRole.cs
│   │   ├── PlacedRoom.cs
│   │   ├── PlacedStair.cs
│   │   ├── PackedAreaLayout.cs
│   │   ├── CorridorTile.cs
│   │   ├── CorridorShape.cs
│   │   ├── CorridorModulePlacement.cs
│   │   ├── MapTraversalGraph.cs
│   │   ├── MapLayout.cs
│   │   ├── MapGenerationParameters.cs
│   │   ├── MapGenerationException.cs
│   │   ├── SeededRandom.cs
│   │   ├── RoomAndStairPacker.cs
│   │   ├── OrthogonalCorridorRouter.cs
│   │   ├── CorridorModulePlanner.cs
│   │   ├── MapTraversalGraphBuilder.cs
│   │   ├── VariableFootprintLayoutGenerator.cs
│   │   ├── MapGraphSearch.cs
│   │   ├── GameplayRoleAssigner.cs
│   │   ├── MapValidationReport.cs
│   │   └── MapLayoutValidator.cs
│   ├── Unity/
│   │   ├── MapGenerationConfig.cs
│   │   ├── MapModuleCategory.cs
│   │   ├── RoomRoleMask.cs
│   │   ├── MapModuleEntry.cs
│   │   ├── MapModuleCatalog.cs
│   │   ├── MapModuleSelection.cs
│   │   ├── MapModuleResolver.cs
│   │   ├── ModuleSocketController.cs
│   │   ├── GeneratedMapModule.cs
│   │   ├── MapAnchor.cs
│   │   ├── MapAnchorKind.cs
│   │   ├── GeneratedMapRuntimeData.cs
│   │   ├── GeneratedMapRegistry.cs
│   │   ├── ModularMapBuilder.cs
│   │   ├── GeneratedNavMeshController.cs
│   │   ├── RuntimeValidationReport.cs
│   │   ├── RuntimeMapValidator.cs
│   │   ├── MapGenerationBootstrap.cs
│   │   └── MapGenerationDebugView.cs
│   └── Gloomveil.MapGeneration.asmdef
├── Editor/
│   ├── MapModuleCatalogValidator.cs
│   ├── GrayboxMapKitFactory.cs
│   └── Gloomveil.MapGeneration.Editor.asmdef
├── Tests/
│   ├── EditMode/
│   │   ├── VariableFootprintLayoutSmokeTests.cs
│   │   ├── MapModuleResolverSmokeTests.cs
│   │   └── Gloomveil.MapGeneration.EditModeTests.asmdef
│   └── PlayMode/
│       ├── GeneratedMapPlayModeSmokeTests.cs
│       └── Gloomveil.MapGeneration.PlayModeTests.asmdef
├── Data/
│   ├── DefaultMapGenerationConfig.asset
│   └── GrayboxMapModuleCatalog.asset
└── Prefabs/Graybox/
    ├── PF_COR_Straight_1x1_01.prefab
    ├── PF_COR_Straight_1x2_01.prefab
    ├── PF_COR_Straight_1x3_01.prefab
    ├── PF_COR_Corner_1x1_01.prefab
    ├── PF_COR_TJunction_1x1_01.prefab
    ├── PF_COR_CrossJunction_1x1_01.prefab
    ├── PF_COR_DeadEnd_1x1_01.prefab
    ├── PF_COR_DoorTransition_1x1_01.prefab
    ├── PF_ROOM_1x2_01.prefab
    ├── PF_ROOM_2x2_01.prefab
    ├── PF_ROOM_2x3_01.prefab
    ├── PF_STA_Straight_1x2_01.prefab
    └── PF_STA_Switchback_2x2_01.prefab

Assets/Scenes/MapGenerationTest.unity
docs/art/MODULAR_MAP_KIT.md
~~~

---

### Task 1: Lock the variable-footprint spatial and artist contract

**Files:**
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Gloomveil.MapGeneration.asmdef`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/GridCoord3.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/GridSize2.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/GridRect.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/CardinalDirection.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/SocketKey.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/GridEdgeKey.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/MapPort.cs`
- Create: `Assets/Gloomveil/MapGeneration/Tests/EditMode/Gloomveil.MapGeneration.EditModeTests.asmdef`
- Create: `Assets/Gloomveil/MapGeneration/Tests/EditMode/VariableFootprintLayoutSmokeTests.cs`
- Create: `docs/art/MODULAR_MAP_KIT.md`

**Interfaces:**
- Produces: `GridSize2.Rotate(int quarterTurns)`, `GridRect.Overlaps(GridRect other)`, `SocketKey.Rotate(GridSize2 canonicalSize, int quarterTurns)`, `GridEdgeKey`, and `MapPort`.
- Consumes: only `System`; no scene or AI types.

- [ ] **Step 1: Create runtime and EditMode assembly definitions**

Use assembly names `Gloomveil.MapGeneration` and `Gloomveil.MapGeneration.EditModeTests`. The runtime assembly references `Unity.AI.Navigation`; the test assembly references the runtime and uses `optionalUnityReferences: ["TestAssemblies"]`.

- [ ] **Step 2: Write the two spatial smoke assertions**

~~~csharp
[TestCase(1, 2, 0, 1, 2)]
[TestCase(1, 2, 1, 2, 1)]
[TestCase(2, 3, 3, 3, 2)]
public void Rotate_RectangularFootprint_SwapsOnlyOnOddQuarterTurns(
    int width, int depth, int turns, int expectedWidth, int expectedDepth)
{
    GridSize2 rotated = new GridSize2(width, depth).Rotate(turns);
    Assert.That(rotated, Is.EqualTo(new GridSize2(expectedWidth, expectedDepth)));
}

[Test]
public void Overlaps_UsesHalfOpenBounds()
{
    var left = new GridRect(0, 0, 2, 2);
    var touching = new GridRect(2, 0, 1, 2);
    var overlapping = new GridRect(1, 0, 2, 2);
    Assert.That(left.Overlaps(touching), Is.False);
    Assert.That(left.Overlaps(overlapping), Is.True);
}
~~~

- [ ] **Step 3: Run the focused test and confirm missing types**

Run `VariableFootprintLayoutSmokeTests` in Unity Test Runner.

Expected: compilation fails because `GridSize2` and `GridRect` do not exist.

- [ ] **Step 4: Implement rotation-safe primitives**

`GridSize2.Rotate` normalizes negative or large turn counts and swaps dimensions only for odd turns:

~~~csharp
public GridSize2 Rotate(int quarterTurns)
{
    int turns = ((quarterTurns % 4) + 4) % 4;
    return (turns & 1) == 0 ? this : new GridSize2(Depth, Width);
}
~~~

`GridRect` stores `X`, `Z`, `Width`, and `Depth`, rejects non-positive size, exposes exclusive `MaxX`/`MaxZ`, and implements:

~~~csharp
public bool Overlaps(GridRect other) =>
    X < other.MaxX && MaxX > other.X &&
    Z < other.MaxZ && MaxZ > other.Z;
~~~

`SocketKey` contains canonical side, zero-based boundary index, and `FloorOffset` (`0` normally, `1` only for a stair top port). Its rotation remaps side and index using a lattice-coordinate transform against the rotated footprint; do not maintain a separate hand-written switch table.

`GridEdgeKey` identifies one normalized shared boundary segment with `Floor`, axis (`AlongX` or `AlongZ`), and the lower lattice endpoint `X,Z`. A room port and its neighbouring corridor port must produce the same `GridEdgeKey` and opposite world-facing directions.

`MapPort` contains:

~~~csharp
int OwnerPlacementId;
SocketKey LocalSocket;
SocketKey RotatedSocket;
GridEdgeKey WorldEdge;
GridCoord3 WorldOutsideCell;
CardinalDirection WorldFacing;
~~~

These are the single sources of truth shared by router, resolver, builder, and artist validator.

- [ ] **Step 5: Write the artist handoff document**

Copy the exact P0 asset list, dimensions, pivot formula, socket naming, rotation rule, collider rule, anchor names, and anti-grid art rules from the plan’s Artist Parallel Track. Required room anchors are `Anchor_PlayerStart`, `Anchor_Exit`, `Anchor_Fuse_00`, and `Anchor_SafeZone`. Common optional anchors are `StalkPoint_Corner`, `StalkPoint_DoorSide`, `StalkPoint_BehindCover`, `StalkPoint_StairLanding`, `MonsterSpawnCandidate`, `LightAnchor`, and `PropSocket`.

- [ ] **Step 6: Re-run the focused test and read back the handoff**

Expected: rotation and half-open overlap cases pass; `docs/art/MODULAR_MAP_KIT.md` contains all 13 P0 prefab names and the 0°/90°/180°/270° rule.

- [ ] **Step 7: Commit the spatial contract**

~~~bash
git add Assets/Gloomveil/MapGeneration/Runtime/Gloomveil.MapGeneration.asmdef Assets/Gloomveil/MapGeneration/Runtime/Core Assets/Gloomveil/MapGeneration/Tests/EditMode docs/art/MODULAR_MAP_KIT.md
git commit -m "feat: define variable footprint map contract"
~~~

---

### Task 2: Pack varied rooms and multi-floor stairs deterministically

**Files:**
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/FootprintSpec.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/RoomRole.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/PlacedRoom.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/PlacedStair.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/PackedAreaLayout.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/MapGenerationParameters.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/MapGenerationException.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/SeededRandom.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/RoomAndStairPacker.cs`
- Modify: `Assets/Gloomveil/MapGeneration/Tests/EditMode/VariableFootprintLayoutSmokeTests.cs`

**Interfaces:**
- Produces: `bool RoomAndStairPacker.TryPack(MapGenerationParameters parameters, SeededRandom attemptRandom, out PackedAreaLayout layout, out string failure)`.
- `PackedAreaLayout` exposes rooms, stairs, and occupied rectangles by floor.

- [ ] **Step 1: Add one parameterized packing smoke test**

~~~csharp
[TestCase(2)]
[TestCase(3)]
[TestCase(4)]
public void Pack_Defaults_CreateVariedNonOverlappingRoomsAndSeparatedStairs(int floors)
{
    MapGenerationParameters p = MapGenerationParameters.CreateProductionDefaults(floors);
    PackedAreaLayout packed = PackFirstValidAreas(p, 1042);

    Assert.That(packed.FloorCount, Is.EqualTo(floors));
    foreach (int floor in Enumerable.Range(0, floors))
    {
        PlacedRoom[] rooms = packed.Rooms.Where(r => r.Floor == floor).ToArray();
        CollectionAssert.IsSubsetOf(
            new[] { "1x2", "2x2", "2x3" },
            rooms.Select(r => r.FootprintId).Distinct().ToArray());
        Assert.That(rooms.Any(r => r.CanonicalSize.Width != r.CanonicalSize.Depth &&
                                   (r.QuarterTurns & 1) == 1), Is.True);
        AssertNoOverlaps(packed.OccupiedRectsForFloor(floor));
    }

    for (int floor = 0; floor < floors - 1; floor++)
    {
        PlacedStair[] stairs = packed.Stairs
            .Where(s => s.LowerFloor == floor).ToArray();
        Assert.That(stairs, Has.Length.EqualTo(2));
        Assert.That(CentreManhattanUnits(stairs[0], stairs[1]),
            Is.GreaterThanOrEqualTo(p.MinimumStairCenterSeparationUnits));
    }
}
~~~

The subset assertion must be implemented as “all three required IDs are present”; do not reverse expected and actual when writing the final test. Define `PackFirstValidAreas` in this test file to call `TryPack` with streams `areas-0` through `areas-(maxLayoutAttempts-1)` and fail with the final diagnostic when none succeed. Define `AssertNoOverlaps(IEnumerable<GridRect>)` and `CentreManhattanUnits(PlacedStair, PlacedStair)` as private helpers in the same file; the distance helper uses the doubled-centre formula from Step 4 and returns units as `double`.

- [ ] **Step 2: Run the packing test and confirm missing packing types**

Expected: compilation fails on `MapGenerationParameters` and `RoomAndStairPacker`.

- [ ] **Step 3: Implement exact production defaults**

`CreateProductionDefaults(floors)` returns:

~~~text
widthUnits = 16
depthUnits = 16
floorCount = floors
minRoomsPerFloor = 7
maxRoomsPerFloor = 9
roomClearanceUnits = 1
extraRoomLinksPerFloor = 2
verticalLinksPerFloorPair = 2
minimumStairCenterSeparationUnits = 5
verticalTraversalCostUnits = 4
fuseCount = 3
safeZoneCount = 2
maxLayoutAttempts = 12
maxPlacementAttemptsPerArea = 64
required room footprints = 1x2(weight 4), 2x2(weight 3), 2x3(weight 2)
stair footprints = 1x2(weight 3), 2x2(weight 1)
corridor run lengths = 1, 2, 3
~~~

Use this constructor order consistently:

~~~csharp
public MapGenerationParameters(
    int widthUnits,
    int depthUnits,
    int floorCount,
    int minRoomsPerFloor,
    int maxRoomsPerFloor,
    int roomClearanceUnits,
    int extraRoomLinksPerFloor,
    int verticalLinksPerFloorPair,
    double minimumStairCenterSeparationUnits,
    int verticalTraversalCostUnits,
    int fuseCount,
    int safeZoneCount,
    int maxLayoutAttempts,
    int maxPlacementAttemptsPerArea,
    IReadOnlyList<FootprintSpec> roomFootprints,
    IReadOnlyList<FootprintSpec> stairFootprints,
    IReadOnlyList<int> corridorRunLengths)
~~~

`Validate()` rejects a floor count below 2, dimensions below 8 units, fewer than two stairs per floor pair, room minimum below the three required footprints, max below min, non-positive footprint dimensions/weights, missing `1x2`/`2x2`/`2x3`, negative clearance, a vertical traversal cost below 1, and configurations that cannot fit two separated stairs.

`FootprintSpec` is data, not an enum: `Id`, `CanonicalSize`, `Weight`, and `RequiredPerFloor`. Room specs use `RequiredPerFloor`; stair specs leave it false.

`PlacedRoom` stores `PlacementId`, `FootprintId`, `Floor`, `SouthwestOrigin`, `CanonicalSize`, `QuarterTurns`, computed `RotatedRect`, `RoomRole`, `GraphNodeId`, and `OpenLocalSockets` plus selected `MapPort` instances.

`PlacedStair` stores `PlacementId`, `FootprintId`, `LowerFloor`, computed `UpperFloor = LowerFloor + 1`, `SouthwestOrigin`, `CanonicalSize`, `QuarterTurns`, computed `RotatedRect`, bottom/top `MapPort`, and bottom/top graph node IDs. It occupies two floor layers while spanning exactly one floor interval.

`PackedAreaLayout` exposes `FloorCount`, `Rooms`, `Stairs`, and `OccupiedRectsForFloor(int floor)`. `RoomRole` values are `None`, `Start`, `Exit`, `Fuse`, and `SafeZone`; structural room/corridor/stair category remains separate.

`SeededRandom(int rootSeed, string streamName)` derives a stable 32-bit stream seed with an explicitly coded UTF-8 FNV-1a hash plus integer mixing. Never use `string.GetHashCode()`, because its result is not the persistence contract. Expose `Next(int maxExclusive)`, `Next(int minInclusive, int maxExclusive)`, and in-place Fisher–Yates `Shuffle<T>(IList<T>)`.

- [ ] **Step 4: Implement one bounded deterministic packing attempt**

For one `TryPack` call:

1. Use only the supplied attempt stream; never read clock time or start a nested layout-attempt loop.
2. Place `verticalLinksPerFloorPair` stair rectangles for every adjacent floor pair first. Each stair reserves the same rectangle on lower and upper floors.
3. Reject stair candidates overlapping a previously reserved stair or whose rectangle-centre Manhattan distance is below `minimumStairCenterSeparationUnits`. Compute doubled centres as `(2 * X + Width, 2 * Z + Depth)` so the comparison remains deterministic without floating point.
4. Avoid reusing a stair rectangle for the next floor boundary.
5. Place required room sizes once on every floor, then place weighted filler rooms around the reserved stairs.
6. Randomize origin and quarter turns; reserve at least one odd non-square rotation per floor and schedule the required `2x3` rooms so the whole layout contains both even and odd quarter-turn classes.
7. Inflate candidate rectangles by `roomClearanceUnits` only for clearance checks; store the uninflated footprint.
8. Return `false` plus one exact failure reason when any floor misses room count, footprint diversity, rotated non-square room, or stair count.

`RoomAndStairPacker` never consumes `maxLayoutAttempts` itself. Task 3’s `VariableFootprintLayoutGenerator` owns the only whole-layout retry loop so packing, routing, cycle creation, and corridor segmentation retry together.

- [ ] **Step 5: Run the packing smoke test**

Expected: the single parameterized test passes for 2, 3, and 4 floors with no rectangle overlaps and exactly two separated stairs per adjacent floor pair.

- [ ] **Step 6: Commit deterministic area packing**

~~~bash
git add Assets/Gloomveil/MapGeneration/Runtime/Core Assets/Gloomveil/MapGeneration/Tests/EditMode/VariableFootprintLayoutSmokeTests.cs
git commit -m "feat: pack varied rooms and stairs"
~~~

---

### Task 3: Route unequal corridors and build the weighted traversal graph

**Files:**
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/CorridorTile.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/CorridorShape.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/CorridorModulePlacement.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/MapTraversalGraph.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/MapLayout.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/OrthogonalCorridorRouter.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/CorridorModulePlanner.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/MapTraversalGraphBuilder.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/VariableFootprintLayoutGenerator.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/MapGraphSearch.cs`
- Modify: `Assets/Gloomveil/MapGeneration/Tests/EditMode/VariableFootprintLayoutSmokeTests.cs`

**Interfaces:**
- Produces: `MapLayout VariableFootprintLayoutGenerator.Generate(MapGenerationParameters parameters, int seed)`.
- Produces: `IReadOnlyDictionary<int,int> MapGraphSearch.WeightedDistancesFrom(MapTraversalGraph graph, int startNodeId)`.

`MapLayout` exposes `WidthUnits`, `DepthUnits`, `FloorCount`, `Rooms`, `Stairs`, `CorridorTiles`, `CorridorModules`, `TraversalGraph`, and `CreateSignature()` as read-only data.

- [ ] **Step 1: Extend the smoke test with determinism, connectivity, and visible variety**

~~~csharp
[TestCase(2)]
[TestCase(3)]
[TestCase(4)]
public void Generate_Defaults_AreDeterministicConnectedAndVisiblyVaried(int floors)
{
    MapGenerationParameters p = MapGenerationParameters.CreateProductionDefaults(floors);
    var generator = new VariableFootprintLayoutGenerator();
    MapLayout first = generator.Generate(p, 1042);
    MapLayout second = generator.Generate(p, 1042);

    Assert.That(second.CreateSignature(), Is.EqualTo(first.CreateSignature()));
    Assert.That(first.TraversalGraph.IsFullyConnected, Is.True);
    Assert.That(first.CorridorModules.Where(m => m.Shape == CorridorShape.Straight)
        .Select(m => m.LengthUnits).Distinct().Count(), Is.GreaterThanOrEqualTo(2));
    Assert.That(first.Rooms.Select(r => r.FootprintId).Distinct(),
        Is.SupersetOf(new[] { "1x2", "2x2", "2x3" }));
}
~~~

- [ ] **Step 2: Run the expanded test and confirm router/generator types are missing**

Expected: compilation fails on `VariableFootprintLayoutGenerator`.

- [ ] **Step 3: Implement room/stair connection targets**

`VariableFootprintLayoutGenerator.Generate` owns the only `for (attempt < maxLayoutAttempts)` loop. For each attempt it derives stable `pack-<attempt>`, `route-<attempt>`, and `segment-<attempt>` streams from the root seed, calls `RoomAndStairPacker.TryPack`, routes required/tree and loop edges, plans corridor modules, builds the traversal graph, and applies structural/style checks. Any failure continues to the next whole-layout attempt. After the final attempt, throw `MapGenerationException` containing seed, attempt count, stage, and last failure.

For each floor, treat rooms plus the bottom/top landings of stairs touching that floor as connection targets. Build a candidate complete graph weighted by Manhattan distance between compatible boundary socket cells. Create a deterministic minimum spanning tree, then add exactly `extraRoomLinksPerFloor` non-tree links when a route is possible.

Every selected room door creates a `MapPort` containing its unrotated local `SocketKey` plus rotated world-facing data. Do not represent a large room with only one `North/East/South/West` bit; multiple ports may exist on the same side.

- [ ] **Step 4: Implement deterministic orthogonal A-star routing**

Route each selected target pair across unused cells:

- room and stair interiors are blocked except the chosen boundary transition;
- map bounds are exclusive;
- existing corridor cells are reusable and receive a small positive reuse preference so natural T/cross junctions can form;
- bend cost is 2 units, step cost is 10 units, and deterministic tie order is `fScore, hScore, z, x`;
- if one selected socket pair fails, try the next compatible pair in deterministic order;
- if a required spanning-tree edge still fails, reject the layout attempt instead of silently disconnecting it.

Corridor tiles store their four-way neighbour mask after all routes are carved.

- [ ] **Step 5: Compress corridor tiles into varied visible modules**

`CorridorModulePlanner` classifies corner, T, cross, and dead-end tiles as `1x1` modules. For maximal degree-two straight runs:

1. A tile directly outside a room port becomes `DoorTransition_1x1` when its mask is exactly straight and it is not also a junction, corner, or stair landing. Otherwise the room’s own portal trim handles the threshold and the tile keeps its actual shape.
2. Split remaining straight runs using allowed lengths `3,2,1` but rotate the starting preference with the seeded stream.
3. Never emit a segment across a corner, junction, room door, stair landing, door-transition tile, or floor boundary.
4. When a layout has at least six eligible straight cells, require at least two distinct emitted lengths; otherwise reject and regenerate.
5. Store placement ID, canonical footprint, origin, quarter turns, shape, and open local socket keys in each `CorridorModulePlacement`.

No filler prefab is created for unused occupancy coordinates.

- [ ] **Step 6: Build weighted traversal and stable signatures**

Create graph nodes for room anchors, corridor tiles, and each stair landing. A room anchor connects to every selected door’s adjacent corridor tile with a cost equal to the centre-to-door lattice distance, clamped to at least 1. Stair bottom/top nodes connect to their adjacent corridor tiles and to each other. Horizontal movement costs 1 per hidden placement unit; a stair edge costs `verticalTraversalCostUnits`, whose production default is 4 in the same abstract distance units. Dijkstra uses non-negative integer costs and stable node IDs.

Each accepted non-tree room link must increase that floor graph’s cycle rank by one; a route that only reuses an existing path does not satisfy `extraRoomLinksPerFloor` and must try another candidate.

`MapLayout.CreateSignature()` sorts and serializes:

~~~text
rooms: placementId,footprintId,floor,origin,canonicalSize,quarterTurns,localSocketKeys,worldEdgeKeys
stairs: placementId,footprintId,lowerFloor,upperFloor,rect,bottomLocalSocket,topLocalSocket,worldEdgeKeys
corridor tiles: floor,x,z,connectionMask
corridor modules: placementId,origin,size,quarterTurns,shape,length
roles: roomPlacementId,role
~~~

Use invariant formatting and no Unity object references.

- [ ] **Step 7: Run the expanded smoke test**

Expected: 2/3/4-floor layouts are identical for the same seed, fully connected, include all room footprints, and use at least two straight-corridor lengths.

- [ ] **Step 8: Commit routing and weighted topology**

~~~bash
git add Assets/Gloomveil/MapGeneration/Runtime/Core Assets/Gloomveil/MapGeneration/Tests/EditMode/VariableFootprintLayoutSmokeTests.cs
git commit -m "feat: route varied modular corridors"
~~~

---

### Task 4: Assign room roles and validate layout invariants

**Files:**
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/GameplayRoleAssigner.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/MapValidationReport.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Core/MapLayoutValidator.cs`
- Modify: `Assets/Gloomveil/MapGeneration/Tests/EditMode/VariableFootprintLayoutSmokeTests.cs`

**Interfaces:**
- Produces: `void GameplayRoleAssigner.Assign(MapLayout layout, MapGenerationParameters parameters, SeededRandom random)`.
- Produces: `MapValidationReport MapLayoutValidator.Validate(MapLayout layout, MapGenerationParameters parameters)`.

- [ ] **Step 1: Add role and validation assertions to the existing parameterized smoke test**

~~~csharp
new GameplayRoleAssigner().Assign(first, p, new SeededRandom(1042, "roles"));
new GameplayRoleAssigner().Assign(second, p, new SeededRandom(1042, "roles"));
Assert.That(second.CreateSignature(), Is.EqualTo(first.CreateSignature()));
MapValidationReport report = new MapLayoutValidator().Validate(first, p);
Assert.That(report.IsValid, Is.True, string.Join(System.Environment.NewLine, report.Errors));

PlacedRoom start = first.Rooms.Single(r => r.Role == RoomRole.Start);
PlacedRoom exit = first.Rooms.Single(r => r.Role == RoomRole.Exit);
var distances = MapGraphSearch.WeightedDistancesFrom(first.TraversalGraph, start.GraphNodeId);
int farthestEligible = first.Rooms
    .Where(r => r.Role != RoomRole.Start)
    .Max(r => distances[r.GraphNodeId]);
Assert.That(distances[exit.GraphNodeId], Is.EqualTo(farthestEligible));
~~~

- [ ] **Step 2: Run the test and confirm role/validator types are missing**

Expected: compilation fails on `GameplayRoleAssigner` and `MapLayoutValidator`.

- [ ] **Step 3: Implement weighted role assignment**

Assignment order is fixed:

1. Choose start from floor 0 rooms, preferring a room with one opened door and breaking ties with the role seed stream.
2. Run Dijkstra from start and choose the farthest room as exit; ties prefer the highest floor, then stable seeded order.
3. Select fuse rooms by uncovered-floor priority, then greater minimum weighted distance from start and previously selected fuses.
4. Select safe rooms from remaining rooms with at least two opened doors and weighted distance at least 3 units from start, exit, fuses, and another safe room.

Throw `MapGenerationException` when requested counts cannot be satisfied; never reduce counts silently.

- [ ] **Step 4: Implement one authoritative validator**

`MapValidationReport` exposes `IsValid` and ordered error strings. Validate:

- exact configured floor count;
- all required room sizes and odd-turn non-square room on every floor;
- both even and odd quarter-turn classes for `2x3` rooms across the layout;
- no room/stair overlap and all placements inside bounds;
- every corridor tile/module inside bounds and outside room/stair interiors;
- all connected ports share the same normalized `GridEdgeKey` and have opposite `WorldFacing` values;
- complete weighted traversal connectivity;
- exactly one start and exit plus exact fuse/safe counts;
- exit is farthest by weighted shortest path;
- fuse floor coverage equals `min(fuseCount,floorCount)`;
- exact stair count and separation for each adjacent floor pair;
- every stair has connected lower and upper landing routes;
- required loop count per floor;
- at least two visible straight lengths when six eligible straight cells exist;
- generated `PlacementId` values are unique across rooms, stairs, and corridor modules.

- [ ] **Step 5: Run the sole layout smoke file**

Expected: all cases in `VariableFootprintLayoutSmokeTests` pass for 2, 3, and 4 floors.

- [ ] **Step 6: Commit role assignment and validation**

~~~bash
git add Assets/Gloomveil/MapGeneration/Runtime/Core Assets/Gloomveil/MapGeneration/Tests/EditMode/VariableFootprintLayoutSmokeTests.cs
git commit -m "feat: assign and validate room gameplay roles"
~~~

---

### Task 5: Resolve variable modules and enforce the prefab contract

**Files:**
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/MapGenerationConfig.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/MapModuleCategory.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/RoomRoleMask.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/MapModuleEntry.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/MapModuleCatalog.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/MapModuleSelection.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/MapModuleResolver.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/ModuleSocketController.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/GeneratedMapModule.cs`
- Create: `Assets/Gloomveil/MapGeneration/Editor/Gloomveil.MapGeneration.Editor.asmdef`
- Create: `Assets/Gloomveil/MapGeneration/Editor/MapModuleCatalogValidator.cs`
- Create: `Assets/Gloomveil/MapGeneration/Tests/EditMode/MapModuleResolverSmokeTests.cs`
- Modify: `docs/art/MODULAR_MAP_KIT.md`

**Interfaces:**
- Produces: `MapModuleSelection ResolveRoom(PlacedRoom room, SeededRandom random)`.
- Produces: `MapModuleSelection ResolveCorridor(CorridorModulePlacement corridor, SeededRandom random)`.
- Produces: `MapModuleSelection ResolveStair(PlacedStair stair, SeededRandom random)`.
- Produces: `void ModuleSocketController.Apply(IReadOnlyCollection<SocketKey> openSockets)`.

- [ ] **Step 1: Create Inspector configuration**

`MapGenerationConfig` serializes every production-default field from Task 2, including `minimumStairCenterSeparationUnits` and `verticalTraversalCostUnits`, plus `unitSize = 4f`, `floorHeight = 4f`, room footprint array, stair footprint array, and corridor lengths `1,2,3`. `OnValidate` reports a clear error containing the asset name; `ToParameters()` returns a validated pure-C# copy.

- [ ] **Step 2: Write two resolver smoke cases**

~~~csharp
[Test]
public void ResolveRoom_TwoByThreeAtQuarterTurn_UsesThreeByTwoPlacementAndRotatedSockets()
{
    PlacedRoom room = TestLayouts.Room(
        footprintId: "2x3",
        canonicalSize: new GridSize2(2, 3),
        southwestOrigin: new GridCoord3(10, 20, 0),
        quarterTurns: 1,
        openLocalSocket: new SocketKey(CardinalDirection.North, 0, 0));
    MapModuleSelection result = resolver.ResolveRoom(room, new SeededRandom(7, "modules"));
    Assert.That(result.RotatedSize, Is.EqualTo(new GridSize2(3, 2)));
    Assert.That(result.QuarterTurns, Is.EqualTo(1));
    Assert.That(result.OpenLocalSockets, Is.EqualTo(room.OpenLocalSockets));

    MapPort port = result.Ports.Single();
    Assert.That(port.RotatedSocket,
        Is.EqualTo(new SocketKey(CardinalDirection.East, 1, 0)));
    Assert.That(port.WorldFacing, Is.EqualTo(CardinalDirection.East));
    Assert.That(port.WorldEdge,
        Is.EqualTo(new GridEdgeKey(0, GridEdgeAxis.AlongZ, 13, 21)));
}

[TestCase(1, "PF_COR_Straight_1x1_01")]
[TestCase(2, "PF_COR_Straight_1x2_01")]
[TestCase(3, "PF_COR_Straight_1x3_01")]
public void ResolveCorridor_StraightLength_UsesMatchingPrefab(int length, string expected)
{
    MapModuleSelection result = resolver.ResolveCorridor(
        TestLayouts.Straight(length), new SeededRandom(7, "modules"));
    Assert.That(result.Prefab.name, Is.EqualTo(expected));
}
~~~

Define `TestLayouts.Room(...)` and `TestLayouts.Straight(...)` inside this test file; they construct only the exact core placement objects used by these two cases. Build the resolver fixture from an in-memory catalogue containing the listed P0 entries.

- [ ] **Step 3: Implement catalogue entries and deterministic resolution**

`MapModuleEntry` fields are:

~~~csharp
string CatalogEntryId;
string FamilyId;
MapModuleCategory Category;
GridSize2 CanonicalFootprintUnits;
CorridorShape CorridorShape;
RoomRoleMask AllowedRoomRoles;
GameObject Prefab;
int Weight;
bool AllowQuarterTurns;
bool UsesDynamicSockets;
~~~

Categories are `Room`, `Corridor`, and `Stair`. Filter by category, exact canonical footprint, corridor shape when applicable, requested quarter turn, and allowed room role. `FamilyId` groups structurally interchangeable visual variants; select the final `CatalogEntryId` from compatible candidates by deterministic integer weight. The resolver may rotate a canonical `2x3` room into `3x2` but may not stretch a prefab or use non-uniform scale.

`ModuleSocketController` indexes children by exact `SocketKey` and toggles the corresponding `Portal_*` and `WallCap_*` pair. A room may open multiple sockets on one side.

`MapModuleSelection` contains `PlacementId`, `CatalogEntryId`, `FamilyId`, `Category`, `RoomRole`, `Prefab`, `Origin`, `MinFloor`, `MaxFloor`, `CanonicalSize`, `RotatedSize`, `QuarterTurns`, `UsesDynamicSockets`, `OpenLocalSockets`, and `Ports`. Rooms/corridors have `MinFloor == MaxFloor`; stairs have `MaxFloor == MinFloor + 1`. The selected local keys address prefab children before root rotation; `Ports` retain rotated socket, world edge, outside cell, and facing information.

- [ ] **Step 4: Implement catalogue validation**

The editor validator reports one actionable error per issue:

- blank/duplicate `CatalogEntryId`, blank `FamilyId`, missing prefab, or weight below 1;
- entries sharing one `FamilyId` disagree on category, footprint, shape, socket set, or dynamic-socket policy;
- missing required P0 category/footprint/shape;
- root scale not `(1,1,1)` or pivot outside the documented centre;
- socket missing, duplicated, off boundary-centre formula, or facing inward;
- dynamic socket missing its portal/wall-cap pair;
- room missing required role anchors;
- stair does not span exactly 4 m or lacks a simple ramp collider;
- collider protrudes into a neighbour’s footprint at a documented socket;
- a canonical P0 prefab fails any of four quarter-turn placement checks.

Add `Tools/Gloomveil/Map/Validate Module Catalog`.

- [ ] **Step 5: Run resolver tests and read back the art contract**

Expected: both resolver cases pass; the art document names variable room/corridor sizes, multi-socket rules, four rotations, stair spans, anchors, colliders, and anti-grid presentation constraints.

- [ ] **Step 6: Commit catalogue and handoff**

~~~bash
git add Assets/Gloomveil/MapGeneration/Runtime/Unity Assets/Gloomveil/MapGeneration/Editor Assets/Gloomveil/MapGeneration/Tests/EditMode/MapModuleResolverSmokeTests.cs docs/art/MODULAR_MAP_KIT.md
git commit -m "feat: resolve variable modular map prefabs"
~~~

---

### Task 6: Generate the graybox kit and build visible layouts

**Files:**
- Create: `Assets/Gloomveil/MapGeneration/Editor/GrayboxMapKitFactory.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/MapAnchorKind.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/MapAnchor.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/GeneratedMapRuntimeData.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/ModularMapBuilder.cs`
- Create: `Assets/Gloomveil/MapGeneration/Tests/PlayMode/Gloomveil.MapGeneration.PlayModeTests.asmdef`
- Create: `Assets/Gloomveil/MapGeneration/Tests/PlayMode/GeneratedMapPlayModeSmokeTests.cs`
- Generate: `Assets/Gloomveil/MapGeneration/Prefabs/Graybox/*.prefab`
- Generate: `Assets/Gloomveil/MapGeneration/Data/GrayboxMapModuleCatalog.asset`

**Interfaces:**
- Produces: `GeneratedMapRuntimeData ModularMapBuilder.Build(MapLayout layout, int seed)`.
- Consumes: validated layout, catalogue, `unitSize`, and `floorHeight`.

- [ ] **Step 1: Start the one PlayMode smoke test with builder assertions**

~~~csharp
[UnityTest]
public IEnumerator Build_TwoFloors_UsesOneObjectPerVisiblePlacement()
{
    MapLayout layout = GenerateAssignedLayout(floors: 2, seed: 1042);
    GeneratedMapRuntimeData data = builder.Build(layout, 1042);
    int expected = layout.Rooms.Count + layout.Stairs.Count + layout.CorridorModules.Count;

    Assert.That(data.Modules.Count, Is.EqualTo(expected));
    Assert.That(data.Modules.Count, Is.LessThan(layout.WidthUnits * layout.DepthUnits * 2));
    Assert.That(data.RoomModules.Select(m => m.CanonicalFootprintUnits),
        Does.Contain(new GridSize2(1, 2)));
    Assert.That(data.RoomModules.Select(m => m.CanonicalFootprintUnits),
        Does.Contain(new GridSize2(2, 2)));
    Assert.That(data.RoomModules.Select(m => m.CanonicalFootprintUnits),
        Does.Contain(new GridSize2(2, 3)));
    yield return null;
}
~~~

Define `GenerateAssignedLayout(int floors, int seed)` and the temporary builder/catalogue setup as private helpers in `GeneratedMapPlayModeSmokeTests.cs`. Task 7 expands this same fixture; do not create another PlayMode test file.

- [ ] **Step 2: Run the PlayMode smoke and confirm builder/assets are missing**

Expected: compilation or setup failure names `ModularMapBuilder` or missing graybox catalogue.

- [ ] **Step 3: Implement idempotent graybox kit generation**

Add `Tools/Gloomveil/Map/Create Graybox Kit`. Generate the exact 13 P0 prefabs listed above from primitives, including dynamic room socket children and both stair footprints. The straight stair uses one invisible-renderer ramp collider; the switchback uses two ramp colliders plus a landing collider. Re-running updates the same paths and catalogue entries without duplicates, saves assets, and invokes catalogue validation.

- [ ] **Step 4: Implement placement, rotation, and anchor collection**

For each selection:

~~~csharp
GridSize2 size = selection.CanonicalSize.Rotate(selection.QuarterTurns);
Vector3 position = new Vector3(
    (selection.Origin.X + size.Width * 0.5f) * unitSize,
    selection.MinFloor * floorHeight,
    (selection.Origin.Z + size.Depth * 0.5f) * unitSize);
Quaternion rotation = Quaternion.Euler(0f, selection.QuarterTurns * 90f, 0f);
~~~

Instantiate under `GeneratedMap_<seed>`. Call `ModuleSocketController.Apply(OpenLocalSockets)` only when `UsesDynamicSockets` is true, then rotate the root. Attach `GeneratedMapModule` with `PlacementId`, `CatalogEntryId`, `FamilyId`, category, origin, `CanonicalFootprintUnits`, `RotatedFootprintUnits`, `MinFloor`, `MaxFloor`, role, and seed. Regeneration destroys only the previous generated root owned by this builder.

`GeneratedMapRuntimeData` exposes read-only collections for modules, rooms, corridors, stairs, stalk points, fuse anchors, safe-zone anchors, light anchors, spawn candidates, start/exit modules, seed, width units, depth units, floor count, and topology signature.

- [ ] **Step 5: Generate assets and run the builder smoke**

Expected: the number of GameObjects equals visible placements rather than every hidden coordinate; all three room footprints appear; the catalogue validator prints no errors.

- [ ] **Step 6: Commit graybox construction**

~~~bash
git add Assets/Gloomveil/MapGeneration/Editor/GrayboxMapKitFactory.cs Assets/Gloomveil/MapGeneration/Runtime/Unity Assets/Gloomveil/MapGeneration/Tests/PlayMode Assets/Gloomveil/MapGeneration/Prefabs/Graybox Assets/Gloomveil/MapGeneration/Data
git commit -m "feat: build variable footprint graybox maps"
~~~

---

### Task 7: Bake NavMesh and publish the AI-neutral map snapshot

**Files:**
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/GeneratedNavMeshController.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/RuntimeValidationReport.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/RuntimeMapValidator.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/GeneratedMapRegistry.cs`
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/MapGenerationBootstrap.cs`
- Modify: `Assets/Gloomveil/MapGeneration/Tests/PlayMode/GeneratedMapPlayModeSmokeTests.cs`

**Interfaces:**
- Produces: `RuntimeValidationReport GeneratedNavMeshController.RebuildAndValidate(GeneratedMapRuntimeData data)`.
- Produces: `GeneratedMapRegistry.MapReady`, `Current`, and `TryGetCurrent(out GeneratedMapRuntimeData data)`.
- Produces: `MapGenerationBootstrap.Generate(int seed)`.

- [ ] **Step 1: Replace the Task 6 builder-only method with one final 2/3/4-floor smoke**

~~~csharp
[UnityTest]
public IEnumerator Generate_BuildsNavigableMapsThenPublishes()
{
    foreach (int floors in new[] { 2, 3, 4 })
    {
        MapGenerationFixture fixture = CreateFixture(floors);
        int publishCount = 0;
        System.Action<GeneratedMapRuntimeData> onReady = _ => publishCount++;
        fixture.Registry.MapReady += onReady;

        fixture.Bootstrap.Generate(1042);
        yield return null;

        Assert.That(fixture.Registry.IsReady, Is.True, $"floors={floors}");
        Assert.That(publishCount, Is.EqualTo(1), $"floors={floors}");
        GeneratedMapRuntimeData data = fixture.Registry.Current;
        int visiblePlacementCount =
            data.RoomModules.Count + data.CorridorModules.Count + data.StairModules.Count;
        Assert.That(data.Modules.Count, Is.EqualTo(visiblePlacementCount));
        Assert.That(data.Modules.Count,
            Is.LessThan(data.WidthUnits * data.DepthUnits * floors));
        Assert.That(data.RoomModules.Select(m => m.CanonicalFootprintUnits),
            Is.SupersetOf(new[]
            {
                new GridSize2(1, 2),
                new GridSize2(2, 2),
                new GridSize2(2, 3)
            }));

        AssertCompletePath(data.StartAnchor, data.ExitAnchor);
        foreach (Transform fuse in data.FuseAnchors)
            AssertCompletePath(data.StartAnchor, fuse);
        foreach (GeneratedMapModule stair in data.StairModules)
            AssertCompletePath(stair.BottomLanding, stair.TopLanding);

        fixture.Registry.MapReady -= onReady;
        fixture.Dispose();
    }
}
~~~

Define `MapGenerationFixture`, `CreateFixture(int floors)`, and `AssertCompletePath(Transform from, Transform to)` as private helpers in the same test file. The fixture creates a fresh runtime `MapGenerationConfig` for each loop, owns all temporary GameObjects/NavMesh data, and its `Dispose()` removes them before the next floor count.

Delete the temporary `Build_TwoFloors_UsesOneObjectPerVisiblePlacement` method from Task 6 when this final method is added; its builder assertions now live inside the loop. The finished PlayMode file contains this one `UnityTest` method only.

- [ ] **Step 2: Run the expanded smoke and confirm navigation/bootstrap types are missing**

Expected: compilation fails on `GeneratedNavMeshController`, `GeneratedMapRegistry`, or `MapGenerationBootstrap`.

- [ ] **Step 3: Implement runtime NavMesh lifecycle**

~~~csharp
public RuntimeValidationReport RebuildAndValidate(GeneratedMapRuntimeData data)
{
    surface.RemoveData();
    surface.BuildNavMesh();
    return runtimeValidator.Validate(data);
}
~~~

The `NavMeshSurface` collects collider geometry only from the generated root. Sample start, exit, fuse, and both landing anchors of every stair within 1.5 m. Reject any partial/incomplete required path and include seed, floor count, source anchor, target anchor, and path status in the error.

- [ ] **Step 4: Implement scene-owned publication**

`GeneratedMapRegistry` is a serialized scene component, not a static singleton. `Publish` rejects null and fires once; `Clear` removes the snapshot before regeneration. The bootstrap order is:

~~~text
GenerateSpatialLayout
-> AssignRoomRoles
-> ValidateLayout
-> ResolveAndBuildModules
-> BuildNavMesh
-> ValidateRuntimePaths
-> Publish
~~~

On failure, log one error with seed and stage, leave the registry empty, and keep a disabled failed root only in development/editor builds. No map class calls enemy code.

- [ ] **Step 5: Run the single PlayMode smoke file**

Expected: 2-, 3-, and 4-floor cases publish exactly once after complete start/exit/fuse/stair paths; zero Console errors.

- [ ] **Step 6: Commit navigation and publication**

~~~bash
git add Assets/Gloomveil/MapGeneration/Runtime/Unity Assets/Gloomveil/MapGeneration/Tests/PlayMode/GeneratedMapPlayModeSmokeTests.cs
git commit -m "feat: validate and publish generated maps"
~~~

---

### Task 8: Create the test scene, debug view, GDD update, and acceptance pass

**Files:**
- Create: `Assets/Gloomveil/MapGeneration/Runtime/Unity/MapGenerationDebugView.cs`
- Create: `Assets/Scenes/MapGenerationTest.unity`
- Create: `Assets/Gloomveil/MapGeneration/Data/DefaultMapGenerationConfig.asset`
- Modify: `TECHNICAL GAME DESIGN DOCUMENT.md`
- Modify: `docs/art/MODULAR_MAP_KIT.md`

**Interfaces:**
- Consumes: the completed generator, builder, NavMesh controller, registry, and validated artist contract.
- Produces: a dedicated visual acceptance scene and authoritative project documentation.

- [ ] **Step 1: Implement Gizmo-only diagnostics**

Draw room/stair rectangles rather than one cube per hidden cell:

- room footprint outlines coloured by role;
- corridor centre-lines and visible segment bounds;
- vertical stairs in cyan with lower/upper floor labels;
- selected `PlacementId`, `CatalogEntryId`, canonical size, rotated size, quarter turns, open sockets, seed, and floor count;
- invalid socket/path pairs in red.

Debug drawing reads data and never mutates generation state.

- [ ] **Step 2: Create the dedicated scene**

`Assets/Scenes/MapGenerationTest.unity` contains `MapGenerationRoot` with bootstrap, builder, registry, NavMesh controller, debug view, one configured `NavMeshSurface`, camera, directional light, default config, and graybox catalogue. It contains no monster or gameplay-system dependency.

- [ ] **Step 3: Run the deliberately small automated suite**

Run:

1. `VariableFootprintLayoutSmokeTests`
2. `MapModuleResolverSmokeTests`
3. `GeneratedMapPlayModeSmokeTests`

Expected: zero failures, no ignored map tests, and no Console compilation/runtime errors.

- [ ] **Step 4: Perform three manual visual checks**

Generate only:

- seed `1042` with 2 floors;
- seed `2718` with 3 floors;
- seed `9001` with 4 floors.

For each, confirm:

- the floor is not a filled rectangle of repeated 4 × 4 squares;
- 1×2, 2×2, and 2×3 rooms exist, including a visible 3×2 rotation;
- corridor distances between rooms/turns vary and at least two of 4/8/12 m straight assets appear;
- empty negative space remains between unrelated rooms;
- every adjacent floor pair has two separated stairs;
- player-sized and monster-sized NavMesh traversal works through every stair;
- start, exit, fuses, and safe zones have valid anchors;
- no module gaps, wall-cap leaks, overlaps, or z-fighting are visible.

- [ ] **Step 5: Update the GDD**

Replace handcrafted-single-map and optional-procedural wording with:

- seeded procedural modular maze in the first playable;
- Inspector-configurable `2+` floors;
- hidden placement lattice with variable-size visible rooms/corridors;
- required `1x2`, `2x2`, `2x3/3x2` room support;
- variable 4/8/12 m corridor runs and four quarter-turn orientations;
- two separated stairs per adjacent floor pair;
- weighted shortest-path start/exit selection;
- fuse distribution across floors;
- neutral StalkPoint, SafeZone, Light, Fuse, spawn, and stair metadata;
- navigation validation before publication to gameplay/AI.

- [ ] **Step 6: Accept artist replacements in small batches**

Replace corridor, room, then stair grayboxes as separate batches. After each batch run the catalogue validator and the matching one of the three manual seeds. Preserve prefab root, footprint, pivot, socket/anchor transforms, layer, collider coverage, and stable catalogue ID.

- [ ] **Step 7: Verify final repository state**

~~~powershell
git status --short
git diff --check
~~~

Expected: only intentional map-generation, scene, GDD, and art-contract changes remain; `git diff --check` prints no whitespace errors.

- [ ] **Step 8: Commit the accepted scene and documentation**

~~~bash
git add Assets/Gloomveil/MapGeneration/Runtime/Unity/MapGenerationDebugView.cs Assets/Gloomveil/MapGeneration/Data/DefaultMapGenerationConfig.asset Assets/Scenes/MapGenerationTest.unity "TECHNICAL GAME DESIGN DOCUMENT.md" docs/art/MODULAR_MAP_KIT.md
git commit -m "docs: finalize variable footprint procedural map"
~~~

---

## Completion Evidence

The feature is complete only when:

- the three focused test files pass for their stated 2/3/4-floor cases;
- same seed/config reproduces room placement, rotations, corridor segmentation, roles, `PlacementId` values, and selected `CatalogEntryId` values;
- no rooms/stairs overlap and no hidden empty coordinate becomes a filler prefab;
- each default floor visibly contains 1×2, 2×2, and 2×3 rooms plus an odd-quarter-turn non-square room;
- each accepted layout contains both 2×3 and rotated 3×2 forms;
- straight corridor assets use at least two of 4/8/12 m lengths in each accepted default layout;
- every adjacent floor pair has at least two separated traversable stairs;
- start, exit, fuse, and all stair landing NavMesh paths are complete;
- `MapReady` publishes only after layout and runtime navigation validation;
- the enemy branch can consume the snapshot without a map-to-enemy reference;
- all 13 P0 artist prefabs pass catalogue validation at 0°, 90°, 180°, and 270°;
- `MapGenerationTest.unity` runs the three acceptance seeds with zero Console errors;
- the GDD and artist handoff describe the implemented variable-footprint system.
