# Sci-Fi Map Module Catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an independent `SciFiMapModuleCatalog` that resolves every procedural module to the supplied Sci-Fi prefab family.

**Architecture:** The catalog is a new `MapModuleCatalog` ScriptableObject serialized with the same 13 semantic entries as `GrayboxMapModuleCatalog`. Its only functional difference is its prefab GUIDs, which target `Prefabs/newcatlog-scfi/prefabs`; the active `DefaultMapGenerationConfig` remains unchanged.

**Tech Stack:** Unity 6000.3.13f1, Unity YAML assets, Gloomveil MapGeneration editor validator.

## Global Constraints

- Do not change scenes, `DefaultMapGenerationConfig.asset`, scripts, existing catalog assets, or user-owned dirty files.
- Preserve entry IDs, families, categories, canonical dimensions, corridor shapes, room roles, weights, rotation flags, and dynamic-socket flags from `GrayboxMapModuleCatalog.asset`.
- Use the identically named Sci-Fi prefab for every one of the 13 entries.
- Validate the catalog through `Tools/Gloomveil/Map/Validate Module Catalog` after Unity imports it.

---

### Task 1: Create and validate the independent Sci-Fi catalog

**Files:**
- Create: `Assets/Gloomveil/MapGeneration/Data/SciFiMapModuleCatalog.asset`
- Create: `Assets/Gloomveil/MapGeneration/Data/SciFiMapModuleCatalog.asset.meta`
- Verify: `Assets/Gloomveil/MapGeneration/Data/GrayboxMapModuleCatalog.asset`
- Verify: `Assets/Gloomveil/MapGeneration/Prefabs/newcatlog-scfi/prefabs/*.prefab`

**Interfaces:**
- Consumes: `MapModuleCatalog.entries`, serialized as `MapModuleEntry` records.
- Produces: a Unity `MapModuleCatalog` asset selectable from `MapGenerationConfig.moduleCatalog` without replacing the currently referenced catalog.

- [ ] **Step 1: Verify the input family is complete**

Run:

```powershell
(Get-ChildItem 'Assets/Gloomveil/MapGeneration/Prefabs/newcatlog-scfi/prefabs' -Filter '*.prefab').Count
```

Expected: `13`, with one prefab named identically to each source catalog entry.

- [ ] **Step 2: Create the catalog with the source schema and Sci-Fi prefab GUIDs**

Create `SciFiMapModuleCatalog.asset` by copying all serialized entry fields from `GrayboxMapModuleCatalog.asset`, changing only `m_Name` to `SciFiMapModuleCatalog` and each `prefab.guid` to the GUID in the matching Sci-Fi prefab's `.meta` file. Create the matching `.meta` file with a fresh unique GUID and the existing `MapModuleCatalog` script GUID `70653b8feaf51f344a18a44fe4e73c95` in the asset's `m_Script` reference.

- [ ] **Step 3: Check serialized parity before Unity validation**

Compare the two catalog assets while ignoring `m_Name` and `prefab` lines. Expected: the remaining serialized entry fields are identical and there are exactly 13 Sci-Fi prefab GUID references.

- [ ] **Step 4: Import and run the Unity catalog validator**

In Unity, run `Tools/Gloomveil/Map/Validate Module Catalog`.

Expected: `Validated 2 map module catalog(s): no errors.` The new catalog must resolve all module families and pass prefab/socket/anchor checks.

- [ ] **Step 5: Review the focused diff and commit**

Run:

```powershell
git diff --check -- Assets/Gloomveil/MapGeneration/Data/SciFiMapModuleCatalog.asset Assets/Gloomveil/MapGeneration/Data/SciFiMapModuleCatalog.asset.meta
git status --short
```

Expected: no whitespace errors; only the new Sci-Fi catalog asset and its `.meta` are staged for this task.
