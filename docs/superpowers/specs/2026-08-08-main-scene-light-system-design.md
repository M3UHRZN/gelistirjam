# Main Scene Light System — Design

Date: 2026-08-08
Status: Approved for implementation planning

## Context

The procedural main scene (`Assets/Scenes/MainGame.unity`, wired by `MainSceneWiring.cs`) generates its map via `Gloomveil.MapGeneration` and already has working `FuseBox`/`ObjectiveManager` progression and an `Exit` (`MapFuseGate`, `MapExitController`). The gameplay Light system described in the TGD (`LightZone`, `LightDirector`, `SafeZone`) is **already fully implemented and working** in `Assets/_Project/Code/Gameplay/World/`, and is even fully wired end-to-end (including to Monster AI) in the hand-authored `MonsterAI_TestScene`. It is simply never instantiated for the procedurally generated main scene: `GeneratedMapRuntimeData` already publishes `LightAnchors` and `SafeZoneAnchors`, but nothing consumes them to spawn real `LightZone`/`SafeZone` objects there, and `MainSceneWiring` never calls `LightDirector.ShutdownAll()` on Final Chase.

This is a wiring/integration task, not a new-system build.

## TDD Requirements (TGD §8, §29–§38, §40, §42–§49, §51, §60)

- **LightZone** (§31): grouped lights with states `Active` / `Flickering` / `Disabled`.
- **LightDirector** (§32): progressive shutdown — Start (100%/80% lit) → Fuse 1 kills one zone → Fuse 2 kills another → Fuse 3 leaves most dark → Final Chase leaves only critical lights.
- **SafeZone** (§29–§30): a lit area; monster cannot select a destination inside it, player inside cannot be attacked, monster may camp just outside.
- **Fuse→Light coupling** (§33, §36): fuse completion drives zone shutdown; monster fuse-awareness is a monster-only concern.
- **Final Chase lighting** (§38): most zones close; a few critical lights remain lit. Per product decision below, the critical lights are the SafeZone room's own lights.
- **Seeded determinism** (§43): light shutdown order is part of the seeded randomization list — same seed ⇒ same order.
- **Audio** (§60): "electric hum"/"light buzz" ambient, "lights off" cue — optional and null-safe, not blocking gameplay.
- **No flashlight/monster-AI coupling** (§8): light *state* never touches Monster AI directly; only `SafeZone` geometry does, and that's already implemented.

## Requirement Matrix

| TDD Requirement | Existing System | Implementation Point | Status |
|---|---|---|---|
| LightZone states/flicker/disable | `LightZone.cs` | none needed | Already implemented |
| Progressive shutdown per fuse | `LightDirector.cs` + `ObjectiveManager.OnAnyFuseCompleted` | none needed | Already implemented |
| SafeZone monster restriction | `SafeZone.cs`, `MonsterNavigator`, `SafeZoneCampState` | none needed | Already implemented (unused in main scene) |
| Procedural Light anchors → real LightZones | `GeneratedMapRuntimeData.LightAnchors` (flat `Transform` list, no grouping) | New: `MapLightGate` | Pending |
| Procedural SafeZone anchor → real SafeZone | `GeneratedMapRuntimeData.SafeZoneAnchors` (1 per SafeZone-role module) | New: `MapSafeZoneGate` | Pending |
| Seeded deterministic shutdown order | `GeneratedMapRuntimeData.Seed` | `MapLightGate` (seeded shuffle) | Pending |
| SafeZone lights exempt from shutdown/Final Chase | n/a | `MapLightGate` (excludes SafeZone-role module's zone from the order) | Pending |
| Final Chase → `LightDirector.ShutdownAll()` | Present in `SceneWiring`, missing in `MainSceneWiring` | Modify `MainSceneWiring.Wire()` | Pending |
| Editor-time wiring of new gates | `MainSceneFuseSetup.cs` pattern | New sibling editor setup step | Pending |
| Audio (hum/buzz/lights-off) | Not present anywhere | Optional `AudioSource` hook on `LightZone`, null-safe | Pending, low priority |

## Out of scope

No Monster/`MonsterBrain` exists in the main scene, and none is added by this task. `SetFuseAwareness` and the Final-Chase `IsRelentless`/`AggressiveState` monster hookups are left for a future monster-spawner task; `LightDirector`/`ObjectiveManager` need no changes to support that later, since they already expose the same events `SceneWiring` uses.

## Architecture

Two new standalone `MonoBehaviour` gates, mirroring the existing `MapFuseGate` / `MapExitController` pattern exactly: each self-subscribes to `GeneratedMapRegistry.MapReady`, is `Bind()`-injected once from an editor setup script, and reports results via public properties (no events required, consistent with `MapFuseGate`).

### `MapLightGate`

1. On `MapReady`, group `data.LightAnchors` (flat `IReadOnlyList<Transform>`) by parent `GeneratedMapModule` via `GetComponentInParent<GeneratedMapModule>()`.
2. For each group, build one `LightZone`: collect `Light` components under that module (spawn a default point light per anchor as a fallback if none exist, mirroring `MapFuseGate`'s prefab-or-fallback pattern), call `LightZone.Bind(lights)`, default state `Active`.
3. Identify the group belonging to the module with `RoomRole.SafeZone` and exclude it from the shutdown order — its lights are the "critical lights" that remain through Final Chase.
4. Build the shutdown order for all remaining zones via `new System.Random(data.Seed)` for a deterministic shuffle.
5. Call `lightDirector.SetShutdownOrder(order)` directly — the gate holds a `LightDirector` reference via `Bind()`, the same way `MapFuseGate` holds `ObjectiveManager`.
6. Expose `IReadOnlyList<LightZone> SpawnedLightZones` for debug/inspection.

### `MapSafeZoneGate`

1. On `MapReady`, for each entry in `SafeZoneAnchors`, find its parent `GeneratedMapModule` and compute world bounds from `RotatedFootprintUnits * 4f` (fallback: aggregated renderer bounds, mirroring `MapExitController.TryGetWorldBounds`).
2. Instantiate a `BoxCollider` sized to those bounds at the anchor's position/rotation, add a `SafeZone` component, and `Bind()` it to the existing shared `SafeZoneRuntimeSet` asset (the same asset already used by the test scene and read by Monster AI — no new registry).
3. Expose `IReadOnlyList<SafeZone> SpawnedSafeZones`.

### `MainSceneWiring` changes

- Remove the now-superseded empty `lightShutdownOrder` inspector field and its "left for a later spawner" comment — `MapLightGate` now pushes the order into `LightDirector` directly at runtime, since procedural zones don't exist at edit/inspector time.
- Add the missing Final Chase hookup: on `GameFlowController.OnStateChanged` → `FinalChaseState` → `lightDirector.ShutdownAll()`, mirroring `SceneWiring` minus its monster-specific lines.

### Editor wiring

Extend the existing `MainSceneFuseSetup.cs`-style editor script (or add a small sibling, e.g. `MainSceneLightSetup.cs`) to `Bind()` the two new gates onto the scene's `GeneratedMapRegistry`, the `MainSceneWiring`-owned `LightDirector`, and the shared `SafeZoneRuntimeSet` asset — same idempotent pattern as the existing fuse setup script.

## Data flow

```
GeneratedMapRegistry.MapReady
        ├─→ MapFuseGate        (existing, unchanged)
        ├─→ MapExitController  (existing, unchanged)
        ├─→ MapLightGate     (new) → group LightAnchors by module → LightZone[] → seeded shutdown order → LightDirector.SetShutdownOrder()
        └─→ MapSafeZoneGate  (new) → SafeZoneAnchors → SafeZone + BoxCollider → SafeZoneRuntimeSet

ObjectiveManager.OnAnyFuseCompleted → LightDirector.OnFuseCompleted   (existing, unchanged)
GameFlowController → FinalChaseState → LightDirector.ShutdownAll()   (new hookup in MainSceneWiring)
```

## Edge cases

- **Regeneration**: `GeneratedMapRegistry` enforces single-publish per run; a fresh scene load rebuilds everything from scratch, so no stale-reference cleanup logic is needed beyond what already exists.
- **No LightAnchors in a module**: simply no `LightZone` is created for it — not an error.
- **No SafeZone-role module found** (e.g. `parameters.SafeRoomCount == 0`): shutdown order includes every zone; log a warning, do not throw.
- **Determinism**: different seeds produce different shutdown orders (different map topology and different `System.Random(seed)` shuffle); the same seed reproduces the same order, since grouping and shuffling are pure functions of published map data plus `data.Seed`.

## Testing

Covers TESTs 1–4, 6, 8, 11, 12, 13, 15 from the original task list directly (generator produces anchors, gate spawns zones/safezones, initial state, fuse progression, SafeZone behavior, seed variance/determinism, regeneration, full gameplay loop). TESTs 5/7/9/10/14 (per-fuse-tier detail, Monster interaction, Final Chase monster behavior, restart/objective reset) are exercised only insofar as they don't depend on the out-of-scope Monster spawner; Monster-specific assertions are deferred to the future monster-spawner task.
