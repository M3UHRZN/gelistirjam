# Generated Map Monster Integration Design

**Date:** 2026-08-09  
**Status:** Approved for implementation  
**Target scene:** `Assets/Scenes/MainGame.unity`

## Goal

Add the existing full monster AI to the procedural main game. Each published map must create exactly one fully wired monster at a deterministic, distant, reachable `MonsterSpawnCandidate`; convert the generated `StalkPoint_*` anchors into the runtime metadata the AI consumes; and connect death, fuse awareness, gaze/anger, SafeZones, lighting, and Final Chase behavior.

The first production prefab will retain the current test-scene capsule/eye debug visual. Its visual child must be replaceable later without changing the runtime AI contract.

## Non-goals

- No final monster model, animation set, audio pass, or VFX pass.
- No changes to procedural topology, room placement, or authored anchor positions.
- No second monster, pooling, respawning, multiplayer, or save/load support.
- No migration of the already working fuse, light, SafeZone, or exit gates into a new global gameplay binder.
- No enemy dependency inside map generation.

## Existing contracts

- Map generation publishes neutral runtime data through `GeneratedMapRegistry.MapReady` only after map and NavMesh validation.
- `GeneratedMapRuntimeData` already exposes `SpawnCandidates`, `StalkPoints`, `StartAnchor`, generated modules, and the map seed.
- `MapLightGate`, `MapSafeZoneGate`, `MapFuseGate`, `MapExitController`, and `PlayerSpawnGate` already translate other neutral anchors into gameplay.
- `MonsterBrain` and its states already degrade to NavMesh sampling when no authored `StalkPoint` is available.
- `MainSceneWiring` is the procedural main scene's composition root. It currently omits monster wiring explicitly because the scene has no monster spawner.

The one-way dependency remains:

`map generation -> neutral registry data -> gameplay gates -> monster AI`

Map generation must not reference `MonsterBrain`, monster states, or gameplay runtime sets.

## Architecture

### `Monster.prefab`

Create a production prefab from the working `MonsterAITestSceneBuilder.BuildMonster` rig. The prefab root is inactive so `Awake`/`OnEnable` cannot run against missing runtime references.

Root components:

- `CapsuleCollider`
- `NavMeshAgent`
- `MonsterNavigator`
- `MonsterPerception`
- `MonsterAngerController`
- `MonsterBrain`
- `MonsterRuntimeRig`

Children:

- `Body`: current debug capsule renderer and `MonsterVisualDriver`; this is the later model-replacement boundary.
- `GazeVolume`: trigger collider and `MonsterGazeReceiver`.
- `EyeVisual`: current small debug eye marker.

### `MonsterRuntimeRig`

`MonsterRuntimeRig` is the prefab contract. It owns serialized references to the prefab's internal components and exposes the initialized `MonsterBrain`. Its initialization method receives external runtime dependencies and performs all component binding while the root is inactive:

- `MonsterConfig`
- player transform
- `IPlayerVelocitySource`
- `PlayerGazeDetector`
- `SafeZoneRuntimeSet`
- `StalkPointRuntimeSet`
- `PlayerVisionModel`

Initialization binds navigator, perception, anger, gaze receiver, visual driver, brain, and stalk/vision systems. It returns failure without activating the root if a required internal or external reference is missing.

`MapMonsterGate` must not know or reconstruct the prefab's internal hierarchy.

### `MapMonsterGate`

`MapMonsterGate` is a dedicated gameplay gate beside the existing map gates. It owns:

- readiness coordination for the current published map,
- conversion of neutral `StalkPoint_*` transforms into runtime `StalkPoint` components,
- deterministic spawn selection,
- creation and initialization of one monster,
- handoff to `MainSceneWiring`,
- cleanup when the map is cleared or replaced.

It does not own AI decisions, fuse progression, light shutdown rules, SafeZone policy, game-state transitions, or procedural generation.

### Gate readiness

Do not depend on Unity scene order, event subscription order, or a one-frame delay.

`MapLightGate` and `MapSafeZoneGate` each retain the `GeneratedMapRuntimeData` instance they most recently built and expose:

- a completion event carrying that runtime-data instance,
- an `IsBuiltFor(data)` query.

Completion is reported even when the relevant anchor collection is empty, after the gate has issued its warning. This prevents the monster gate from waiting forever on a valid-but-empty result.

`MapMonsterGate` stores pending `MapReady` data and calls `TryBuildPending`. It proceeds only when both gates report completion for that exact runtime-data instance. Completion events call the same idempotent method, covering every subscriber order without duplicate builds.

`MapLightGate` also retains a `GeneratedMapModule -> LightZone` lookup and exposes `TryGetLightZone`. Modules deliberately omitted from fixture creation return no zone and are treated as dark.

### Registry cleanup

Add a neutral `GeneratedMapRegistry.MapCleared` lifecycle event carrying the data being cleared. `Clear` removes the current publication before raising the event. The event contains no gameplay or enemy type and therefore preserves the package boundary.

`MapMonsterGate` listens for it, unbinds the current brain from `MainSceneWiring`, destroys the monster, clears pending state, and disables/removes only the runtime `StalkPoint` components that the gate added. Disabling invokes `OnDisable` immediately so the runtime set is clean even when a caller clears the registry directly instead of using `MapGenerationBootstrap.ClearGeneratedMap`.

## Runtime data flow

1. `MapGenerationBootstrap` finishes generation, NavMesh build, and validation.
2. `GeneratedMapRegistry` publishes one `GeneratedMapRuntimeData` instance.
3. Light and SafeZone gates build their runtime objects and report completion for that instance.
4. `MapMonsterGate` converts every non-null published stalk anchor.
5. `MapMonsterGate` selects a reachable initial spawn.
6. It instantiates the inactive monster prefab and initializes the runtime rig.
7. `MainSceneWiring.BindMonster` connects cross-system events.
8. The monster root is activated only after every bind succeeds.

## Runtime StalkPoint conversion

Add one runtime `StalkPoint` component to each published `StalkPoint_*` anchor. Do not edit the map prefab assets or create duplicate positional markers.

Metadata is derived deterministically from existing authored names and parent modules:

- name contains `Corner` -> `IsNearCorner = true`
- parent catalog entry contains `TJunction` or `CrossJunction` -> `IsJunction = true`
- parent catalog entry contains `DeadEnd` -> `IsDeadEnd = true`
- name contains `StairLanding` -> `AmbushAllowed = false`
- all other current authored points -> `AmbushAllowed = true`

Each point binds the shared `SafeZoneRuntimeSet`, the parent module's `LightZone` when present, and the shared `StalkPointRuntimeSet`.

When the parent module has no `LightZone` because its fixture was intentionally omitted, configure static darkness as `1`. A point whose module cannot be resolved uses the existing neutral fallback darkness and logs a warning.

## Initial spawn selection

Use `GeneratedMapRuntimeData.StartAnchor`, not the player's current transform, so selection is independent of `PlayerSpawnGate` subscriber order.

For each non-null `SpawnCandidate`:

1. Resolve its parent `GeneratedMapModule`.
2. Prefer candidates on modules with `RoomRole.None`; do not initially spawn in Start, Exit, Fuse, or SafeZone rooms.
3. Snap to NavMesh with a small bounded tolerance.
4. Calculate a complete NavMesh path from the Start anchor.
5. Compute path length from path corners; do not use straight-line or vertical distance in the multi-floor map.

Candidates are sorted by descending path length with a stable position/name tie-break. Candidates at or above the Inspector-configurable minimum initial path distance (default `24 m`) form the preferred pool. Take at most the three farthest and select one using a dedicated `System.Random` stream derived from the map seed and a fixed monster-spawn salt.

This produces variation without allowing a close random spawn. It also leaves map-generation and presentation random streams untouched.

Fallback order:

1. farthest reachable ordinary-role spawn candidate, with a warning if below the minimum;
2. farthest reachable non-Start/non-SafeZone spawn candidate, with a warning;
3. farthest reachable published StalkPoint, with a warning;
4. do not spawn and emit one actionable error when no reachable position exists.

The selected NavMesh hit position is the monster's feet-level position. The anchor's yaw is retained.

## Player and prefab wiring

Move `IPlayerVelocitySource` out of `TestPlayerController.cs` into its own runtime file. Both `TestPlayerController` and the real `PlayerController` implement it; `PlayerController` already exposes Rigidbody velocity, so movement behavior does not change.

Add `PlayerVisionModel` to the shared player prefab. At monster initialization it binds the real player transform and the spawned monster's perception component.

The existing `PlayerGazeDetector` on the player camera is rebound with that camera and the same `MonsterConfig` used by the monster.

## Main scene event wiring

Extend `MainSceneWiring` with `BindMonster` and `UnbindMonster`. Core main-scene wiring remains valid before a monster exists.

Use named handlers and a replaceable current-brain reference:

- current brain `OnKill` -> `GameFlowController.DeathState`
- `ObjectiveManager.OnFuseInteractionStarted` -> current brain fuse awareness on, with position
- `ObjectiveManager.OnFuseInteractionStopped` -> current brain fuse awareness off
- `FinalChaseState` -> shut down remaining noncritical lights, set current brain relentless, switch to Aggressive

Binding a replacement brain first unsubscribes the previous brain's `OnKill`. Objective and game-state handlers are registered once and act on the current brain, preventing duplicate callbacks across map regeneration.

If a brain is bound while the game is already in Final Chase, apply relentless Aggressive behavior immediately.

## Failure behavior

- Missing registry, prefab, rig, config, player, camera gaze detector, runtime sets, or readiness gates: log one actionable error and keep the monster inactive/uncreated.
- Missing stalk anchors: warn and spawn; the existing AI uses NavMesh sampling fallback.
- Missing LightZone for an intentionally unlit module: no warning; bind static darkness `1`.
- Missing parent module for an anchor: warn, keep a safe metadata fallback, and continue.
- Zero reachable spawn positions: log an error and do not leave a partial monster instance.
- Duplicate readiness callbacks: ignored by runtime-data identity and the idempotent build guard.
- Map clear/republication: unbind and destroy the old monster, disable/remove gate-added stalk components, and clear their runtime-set registrations before another map is accepted.

## Scene integration

Wire the feature into `Assets/Scenes/MainGame.unity`, not only a test fixture:

- add `MapMonsterGate` as a separate gameplay composition root,
- assign registry, monster prefab, config, player, player camera gaze detector, both runtime sets, light gate, SafeZone gate, and `MainSceneWiring`,
- add/bind `PlayerVisionModel` through the shared player prefab,
- keep exactly one monster prefab reference.

The test scene may continue using its existing builder. It can adopt the prefab later, but that migration is not required for this feature.

## Tests

Implementation follows test-first development.

### EditMode

- same seed and same candidates choose the same spawn;
- stable tie-break makes input enumeration order irrelevant;
- when enough distant candidates exist, selection stays within the three farthest eligible candidates;
- a below-minimum reachable set falls back to the farthest point;
- critical room roles are excluded from the preferred pool;
- zero reachable candidates reports failure;
- stalk metadata inference covers Corner, T/Cross Junction, Dead End, DoorSide, and StairLanding names/modules.

### PlayMode component integration

- `MapMonsterGate` does not build before both light and SafeZone gates complete for the same map;
- completion in every subscriber order still creates exactly one monster;
- one runtime `StalkPoint` is registered per published non-null stalk anchor;
- lit points bind their module's `LightZone`; deliberately unlit points report static darkness `1`;
- spawned monster has a complete Start-to-spawn path and an active agent on NavMesh;
- brain, player, gaze receiver, velocity source, vision model, SafeZone set, and stalk set are all bound;
- missing stalk anchors still produces a working fallback monster;
- map clear destroys the monster and unregisters stalk points;
- republishing does not duplicate the monster or event handlers.

### Gameplay wiring

- monster kill enters Death;
- fuse start/stop updates only the current brain's awareness;
- Final Chase makes the current brain relentless and Aggressive;
- replacing/unbinding a brain prevents the old brain from affecting game flow.

### Procedural coverage

Generate and verify configured 2-, 3-, and 4-floor maps. For each case assert one spawned monster, non-empty registered stalk set, a complete NavMesh path, and no duplicate runtime objects. Include the stable seed `1042` plus at least one additional seed.

### Live `MainGame` verification

Using the active Unity Editor:

- validate scene references before Play Mode;
- enter Play Mode and wait for `GeneratedMapRegistry.IsReady`;
- confirm exactly one active monster under the monster gate;
- confirm its `NavMeshAgent.isOnNavMesh` and initial AI state;
- confirm player spawn and monster spawn are separated by the selected NavMesh path distance;
- exercise gaze, one fuse interaction, and Final Chase/Death event paths where feasible;
- inspect the Unity Console for exceptions and binding warnings;
- run the focused EditMode and PlayMode tests, then the existing map-generation/gameplay suites affected by the changes.

Static inspection or a clean diff is not runtime proof. Any unexercised gameplay behavior must be reported separately from verified results.

## Acceptance criteria

- `MainGame` produces exactly one fully initialized monster from existing generated anchors.
- The same map seed deterministically selects the same initial monster position.
- Initial spawn is reachable, distant by NavMesh path length, and outside critical gameplay rooms whenever an ordinary candidate exists.
- Generated StalkPoint anchors drive the existing scoring/state systems with light and SafeZone context.
- Gaze/anger, player prediction, kill/death, fuse awareness, and Final Chase are connected in the real scene.
- Map clearing or regeneration leaves no old monster, stalk registration, or cross-system subscription behind.
- The map-generation assembly remains free of enemy/gameplay dependencies.
- The debug visual can later be replaced without changing `MapMonsterGate` or map generation.
