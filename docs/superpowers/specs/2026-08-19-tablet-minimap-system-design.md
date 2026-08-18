# Tablet Minimap System Design

## Goal

Provide UI-independent runtime data for a player-centred tablet minimap. The later uGUI view will draw the current floor, visible door gaps, a fuse direction arrow orbiting the player icon, stair transitions, and an exit route after every fuse is complete.

## Scope

- Generate wall segments from the procedural layout and omit open port edges so doors appear as gaps.
- Publish traversal graph, minimap topology, unit size, and floor height with `GeneratedMapRuntimeData`.
- Track the player's map-space position, facing, and current floor.
- Point toward the next graph step of the nearest incomplete fuse.
- Replace fuse guidance with a graph route to the exit after the exit opens.
- Expose immutable snapshot data and a C# event for a later uGUI presenter.

## Explicit exclusions

- No PlayMode tests.
- No Canvas, scene, prefab, tablet asset, input, animation, or presentation code.
- No information/status row.
- No monster marker.
- No commit, merge, or push.

## Architecture

`MinimapTopologyBuilder` is a pure map-generation service. It converts rooms, corridors, stairs, and their open ports into immutable line and stair-marker data. `MapGraphSearch` owns deterministic weighted path reconstruction. `MinimapRuntimeModel` is a Unity component that observes the generated map and reads existing fuse/exit state, then publishes a `MinimapRuntimeSnapshot`; the future view does not query gameplay systems directly.

## Runtime behavior

- The map remains world-aligned; the player icon rotates.
- Only the current floor is intended to be drawn by the future view.
- The fuse direction vector follows the first graph step toward the nearest incomplete fuse and therefore moves around the player icon as the player turns or changes route.
- When the exit opens, fuse direction is hidden and the full graph route to the exit is published. The future view filters the route to the current floor and uses stair markers for vertical transitions.

