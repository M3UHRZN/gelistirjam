# Sci-Fi Map Module Catalog Design

## Goal

Add a second `MapModuleCatalog` that selects the Sci-Fi visual module family without changing the procedural layout contract or the catalog currently assigned in a scene.

## Scope

- Create `Assets/Gloomveil/MapGeneration/Data/SciFiMapModuleCatalog.asset`.
- Preserve the existing 13 catalog entries' IDs, families, categories, dimensions, corridor shapes, room roles, weights, rotation flags, and dynamic-socket flags.
- Point each entry to its identically named prefab under `Assets/Gloomveil/MapGeneration/Prefabs/newcatlog-scfi/prefabs/`.
- Leave all scenes, generation bootstrap references, scripts, and existing catalog assets unchanged.

## Verification

Run `Tools/Gloomveil/Map/Validate Module Catalog` and confirm the new asset passes with no errors.
