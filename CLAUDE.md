# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Gloomveil** — first-person psychological horror / maze-escape, built in Unity 6000.3.13f1 (URP). Solo/small-team 48-hour game jam project (see `TECHNICAL GAME DESIGN DOCUMENT.md` for full design spec — read it before touching monster AI, map generation, or the fuse/light/safe-zone systems, it is the source of truth for gameplay rules).

Core loop: player explores a seeded procedural modular maze, avoids a monster that hunts by *stalking outside the player's vision* rather than chasing directly, activates fuse panels to open the exit, and escapes during an escalating final chase. Looking the monster in the eyes freezes/repels it but permanently raises its Anger (speed/aggression).

There is also `MASTER_PROMPT.md` at the repo root — a generic, third-party Unity engineering style guide (naming, architecture layering, performance budgets, pooling, DI, etc.), not authored by the project owner. **It is not binding.** Where useful it happens to overlap with this codebase's actual conventions (see below), but don't cite it as authority or enforce rules from it that the existing code doesn't already follow. Prefer inferring conventions from the actual code.

## Working with Unity from Claude Code

This is a Unity Editor project, not a CLI-buildable app — there is no `npm`/`gradle`/`make` step. Two ways to interact with it:

- **Unity MCP tools** (`mcp__UnityMCP__*`) are connected — use `manage_scene`, `manage_gameobject`, `manage_script`, `read_console`, `run_tests`, `manage_editor` (play mode control) etc. to inspect/modify the live Editor state instead of guessing. Check `mcpforunity://custom-tools` for project-specific tools before assuming something isn't available.
- Tests can be run through the MCP `run_tests` tool (EditMode/PlayMode) or via the Unity Editor's Test Runner window. There is no separate test CLI in this repo.
- Source-only changes (new `.cs` files, edits) can be made directly with normal file tools; Unity will recompile on focus/refresh. Use `mcp__UnityMCP__refresh_unity` if a change needs to be picked up without switching to the Editor.

## Architecture

### Assembly layout (all under `Assets/`)

Two independent assembly groups exist:

1. **`_Project/Code`** — the game itself, split by MASTER_PROMPT's layering (Presentation → Application → Domain → Infrastructure):
   - `Core/` (`Game.Core.asmdef`) — `GameManager` + `GameStateMachine`, the game-flow FSM (Initialization → Exploration → FuseInteraction/FinalChase → Death/Victory, TGD §4). Core never references concrete Gameplay types — it only depends on `IPlayerControlGate`/`IGameState`; the composition root (`SceneWiring`) wires concrete player types and calls `GameManager.ChangeState(...)` for events it observes.
   - `Gameplay/` (`Game.Gameplay.asmdef`) — `Monster/` (brain, navigator, perception, anger, gaze — each monster behaviour is its own `IMonsterState` under `Monster/States/`), `Player/` (movement, gaze detection, flashlight), `World/` (`SafeZone`, `LightZone`, `LightDirector`, `ExitController`), `Objectives/` (`FuseBox`, `ObjectiveManager`), `Interaction/` (`IInteractable`, `PlayerInteraction`), and `SceneWiring.cs` (the composition root).
   - `Gameplay/Debug/` (`Game.DebugTools.asmdef`) — gizmo/HUD-only debug visualization, separate assembly so it can be stripped.
   - `Editor/` (`Game.Editor.asmdef`) — editor-only tooling (test scene builders, NavMesh diagnostics).
   - `Domain/`, `Infrastructure/`, `UI/` — reserved per MASTER_PROMPT's layering but currently empty; not yet built out.

2. **`Gloomveil/MapGeneration`** — the procedural map generator, a self-contained package with its own assemblies and tests:
   - `Runtime/Core/` (`Gloomveil.MapGeneration.asmdef`) — pure C#, no `UnityEngine` dependency where practical. Deterministic pipeline: seed/config validation → variable-footprint room+stair packing (`RoomAndStairPacker`) → corridor routing on a hidden 4×4 m lattice (`OrthogonalCorridorRouter`, `CorridorModulePlanner`) → gameplay role assignment (start/exit/fuse/safe-zone, `GameplayRoleAssigner`) → traversal graph + NavMesh validation (`MapTraversalGraphBuilder`, `MapLayoutValidator`). A map that fails validation is never published.
   - `Runtime/Unity/` — the Unity-facing layer: `MapGenerationBootstrap` drives the pipeline at runtime/editor time, `ModularMapBuilder` resolves prefabs from `MapModuleCatalog` and instantiates the visible map, `GeneratedMapRegistry`/`GeneratedMapRuntimeData` publish the finished map for gameplay/AI to consume, `GeneratedNavMeshController` builds/validates the NavMesh.
   - `Editor/` — `GrayboxMapKitFactory` (procedural graybox prefab generation), `MapModuleCatalogValidator` (**`Tools/Gloomveil/Map/Validate Module Catalog`** menu item — run this after any prefab/socket change).
   - `Tests/EditMode` + `Tests/PlayMode` — smoke tests for the resolver and layout generator; run these after touching anything in `Runtime/Core`.
   - `Data/` — `DefaultMapGenerationConfig.asset`, `GrayboxMapModuleCatalog.asset` (module-to-prefab catalog, keyed by `CatalogEntryId`).
   - `Prefabs/Graybox/` and `Prefabs/TexturedBoxs/` — two parallel prefab sets (graybox placeholders being replaced by textured art in batches per `docs/art/MODULAR_MAP_KIT.md`). The map generation layer never calls gameplay/enemy code directly — it only publishes neutral metadata (`StalkPoint_*`, `Anchor_*`, `LightAnchor`, `Landing_*`) that gameplay/AI interprets.

**Key invariant**: map generation → gameplay is one-directional and mediated by `GeneratedMapRegistry`/anchors. Monster AI and gameplay systems read published map data; they never reach back into the generator.

### Prefab/module contract

`docs/art/MODULAR_MAP_KIT.md` defines the full geometric contract for map modules (socket naming `Socket_{N,E,S,W}_{index}`, anchor naming `Anchor_PlayerStart`/`Anchor_Exit`/`Anchor_Fuse_00`/`Anchor_SafeZone`, stair collider naming, the hidden 4×4 m placement lattice vs. variable visible footprints `1×2`/`2×2`/`2×3`). Read it before adding or modifying any prefab in `Assets/Gloomveil/MapGeneration/Prefabs/`. `docs/superpowers/plans/` holds dated implementation plans for map-generation features — check for an existing plan before starting new map-gen work.

### Monster AI

Two-layer design (TGD §11, §50): `MonsterBrain` makes high-level decisions (stalk/flank/ambush/retreat/attack) via `MonsterStateMachine` + `IMonsterState` implementations in `Monster/States/`; `MonsterNavigator` handles NavMesh movement toward whatever target the brain picks. `MonsterPerception` evaluates the player's FOV/visibility; `MonsterAngerController` tracks Anger (raised by successful player gaze, TGD §19-21, driving both speed and AI aggression tier). `MonsterGazeReceiver` is the collider target for the player's gaze raycasts. Candidate stalk positions come from `StalkPoint_*` anchors baked into map modules, evaluated on a fixed interval (~0.4–0.8s), not per-frame.

## Conventions

Infer conventions from the existing code, not from `MASTER_PROMPT.md` (see note above — it's a generic third-party guide, not project policy). Notable points already established in the codebase:
- Namespaces mirror assembly/folder (`Game.Core`, presumably `Game.Gameplay.*` etc.).
- XML doc comments on non-obvious public types explain architectural intent (see `GameManager.cs`), not just restate the API.
- `SceneWiring.cs` is the single composition root for the gameplay scene — new cross-system wiring goes there, not scattered `FindObjectOfType` calls.
