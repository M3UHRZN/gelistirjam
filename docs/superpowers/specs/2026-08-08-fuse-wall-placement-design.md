# Fuse Wall Placement — Design

Date: 2026-08-08
Status: Approved for implementation planning

## Context

The procedural map assigns Fuse roles to generated room modules and publishes each room's `Anchor_Fuse_00` through `GeneratedMapRuntimeData.FuseAnchors`. All six current room prefabs place that anchor at the same near-centre floor position (`-0.5, 0, 0.5`), so `MapFuseGate` currently spawns fuse visuals in open floor space. The newly authored `Assets/prefabs/tablet.prefab` should be the fuse visual and should mount on a usable wall without requiring manual anchor edits in every graybox, textured, or future room prefab.

The map module contract already exposes the information needed for deterministic wall placement: `Socket_*` transforms face outward, an active matching `WallCap_*` identifies a closed wall, and active `Portal_*` objects identify open doorways. `MapExitController` already uses this contract to put the exit into a closed wall.

## Decision

`MapFuseGate` will resolve a wall mount at runtime. For each Fuse anchor it will find the parent `GeneratedMapModule`, enumerate its sockets, active wall caps, and active portals, then choose the closed wall socket farthest from the room's open doorways. Ties use ordinal socket-name order, making the result deterministic for a given generated map.

This is preferred over editing every room prefab because closed and open sockets are determined by the generated layout, not by the prefab alone. It is preferred over physics raycasts because raycasts can select props or other incidental colliders instead of the module's intended wall.

## Placement and authoring controls

`MapFuseGate` will expose these serialized controls:

- `placeOnClosedWall`: enables automatic wall mounting; enabled by default.
- `mountHeight`: height of the visual's bottom above the socket/floor level.
- `wallClearance`: gap between the visual's back face and the wall's inner face.
- `wallOffset`: final position adjustment in wall-local coordinates: X along the wall, Y upward, Z into the room.
- `rotationOffsetEuler`: X/Y/Z rotation applied after the visual is aligned to face into the room. This lets the tablet model's orientation be corrected from the Inspector without changing the FBX or prefab.

The spawned object will be parented under the identity-scaled `MapFuseGate`, not under a potentially mirrored generated module or anchor. Its world pose will still be derived from the chosen module socket. This prevents negative inherited scale from corrupting the tablet collider.

The tablet prefab will be assigned to the existing `MapFuseGate.fuseBoxPrefab` field in `MainGame`. `MapFuseGate` remains responsible for ensuring a collider and `FuseBox` component exist and for binding objective/minigame dependencies.

## Data flow

1. `GeneratedMapRegistry.MapReady` invokes `MapFuseGate.SpawnAll`.
2. For each `FuseAnchor`, the gate locates the containing `GeneratedMapModule`.
3. The gate selects a socket whose matching `WallCap` is active, maximizing its minimum distance from active portals.
4. The tablet prefab is instantiated under `MapFuseGate` and aligned so its front faces into the room.
5. Renderer bounds place the tablet's bottom at `mountHeight` and its back face at `wallClearance` from the wall; `wallOffset` and `rotationOffsetEuler` are then applied.
6. The existing collider/`FuseBox` setup, rounds pattern, dependency binding, and objective count continue unchanged.

## Fallbacks and diagnostics

- If `placeOnClosedWall` is disabled, spawn at the original Fuse anchor and apply `rotationOffsetEuler`.
- If the anchor has no parent `GeneratedMapModule`, no valid closed socket exists, or the chosen socket has no usable horizontal forward direction, spawn at the original anchor and log one contextual warning.
- If no matching wall renderer is available, use the socket pose, `mountHeight`, `wallClearance`, and offsets without renderer-bounds flush fitting.
- If the prefab has no renderer, placement still succeeds using the socket pose and configured offsets.
- Existing behavior for zero Fuse anchors, missing colliders, missing `FuseBox`, round assignment, and map regeneration remains unchanged.

## Scope

In scope:

- Runtime closed-wall selection for generated Fuse rooms.
- Tablet prefab assignment in the main scene.
- Inspector-controlled position and rotation corrections.
- Focused automated tests and live Unity verification.

Out of scope:

- Moving `Anchor_Fuse_00` in individual room prefabs.
- Changing Fuse minigame, objective progression, monster awareness, or map role assignment.
- Modifying the tablet FBX import or source mesh.
- Refactoring the already-working `MapExitController` wall-placement implementation.

## Testing and acceptance

Automated tests will verify that wall resolution:

- ignores sockets without an active matching wall cap;
- chooses the closed wall farthest from active portals;
- breaks equal-score ties deterministically by socket name;
- falls back to the Fuse anchor when no valid wall is available;
- composes the configured rotation offset with the wall-facing rotation;
- applies wall-frame position offsets consistently for different compass directions.

Unity verification will confirm that scripts compile without errors, the map module catalog still validates, a generated main-scene map spawns every Fuse tablet on a closed wall, colliders remain valid under rotated or mirrored modules, and Inspector changes to `rotationOffsetEuler` visibly change tablet orientation. Visual placement is only considered runtime-verified after entering Play Mode and inspecting the generated Fuse rooms.
