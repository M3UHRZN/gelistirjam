# Procedural Map Player Spawn Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a procedurally generated map is published, teleport the player to that map's designated start room (`Anchor_PlayerStart`) instead of leaving them wherever they were placed in the editor scene.

**Architecture:** The map generator already computes and exposes everything needed — `GameplayRoleAssigner` (runtime `Core` layer) deterministically assigns one room `RoomRole.Start` per seed, `ModularMapBuilder` resolves that room's `Anchor_PlayerStart` child into `GeneratedMapRuntimeData.StartAnchor`, and `RuntimeMapValidator` guarantees (via `MapGenerationBootstrap.Generate`, which throws and aborts `Publish` on failure) that a published map's `StartAnchor` is never null and is NavMesh-reachable. `PlayerController` already has a `Teleport(Vector3 feetPosition, float yawDegrees)` method built for exactly this. The only missing piece is a small Gameplay-layer component, `PlayerSpawnGate`, that listens to `GeneratedMapRegistry.MapReady` and calls `Teleport` with `StartAnchor`'s position/yaw. This keeps the existing one-directional dependency (map generation → gameplay, mediated by `GeneratedMapRegistry`) intact: `Game.Gameplay` gets a new assembly reference to `Gloomveil.MapGeneration`, never the other way around.

**Tech Stack:** Unity 6000.3.13f1 (URP), C#, Unity Test Framework (`UnityTest`/PlayMode), Unity AI Navigation (`NavMeshSurface`), Unity Input System (already used by `PlayerController`).

## Global Constraints

- Map generation → gameplay stays one-directional and mediated by `GeneratedMapRegistry`/anchors (CLAUDE.md "Key invariant"). `Gloomveil.MapGeneration` must never reference `Game.Gameplay` or `Game.Core`.
- `Anchor_PlayerStart` sits at each room's floor level with identity local rotation (`docs/art/MODULAR_MAP_KIT.md`: "Root pivot: footprint'in yatay merkezi, alt zemin seviyesi"; confirmed in `PF_ROOM_2x2_01.prefab`'s `Anchor_PlayerStart` transform: `m_LocalPosition: {x: 0, y: 0, z: 0}`), so its world position is already correct as a "feet" position — no vertical offset needed.
- `GeneratedMapRuntimeData.StartAnchor` is guaranteed non-null whenever `GeneratedMapRegistry.MapReady` fires: `RuntimeMapValidator.ValidateAnchorCoverage` adds an error if it's missing, and `MapGenerationBootstrap.Generate` throws `MapGenerationException` (aborting before `registry.Publish(...)`) on any validation failure. Do not add a null-check fallback path for this — it cannot happen for a published map.
- `GeneratedMapRuntimeData.SpawnCandidates` (anchor kind `SpawnCandidate` / prefab node `MonsterSpawnCandidate`, per `docs/art/MODULAR_MAP_KIT.md`) is for monster spawn placement, not the player. Do not use it here.
- `Assets/Scenes/Game.unity` currently has no `GameManager` or `SceneWiring` GameObject at all — the game-flow FSM is not wired into this scene yet. That's out of scope for this plan; do not add it. This plan only makes the player's starting position correct once map generation publishes.
- `Assets/Scenes/Game.unity`'s `Player` GameObject (fileID `1712365056`) already has `Rigidbody` + `CapsuleCollider` (radius 0.35, height 1.8, center y=0.9 — bottom of capsule sits exactly at the GameObject's local y=0, matching `Teleport`'s "feet position" contract) and a `FlashlightController`, but does **not** yet have a `PlayerController` component attached. Attaching it is in scope (Task 2) because `PlayerSpawnGate` needs a `PlayerController` to call `Teleport` on.

---

## File Structure

- **Create** `Assets/_Project/Code/Gameplay/Player/PlayerSpawnGate.cs` — new `Game.Gameplay.Player` component. Owns the `GeneratedMapRegistry.MapReady` subscription and calls `PlayerController.Teleport` with the published map's `StartAnchor`. Self-contained (no `SceneWiring` involvement needed — `SceneWiring` exists specifically to redo *cross-system* event wiring between independently-existing components like `MonsterBrain`↔`GameManager`; this component owns its own single subscription the same way `SafeZone`/`FuseBox` own their own registration, via a `Bind()` method + dual `OnEnable`/`Start` registration for reliability).
- **Modify** `Assets/_Project/Code/Gameplay/Game.Gameplay.asmdef` — add `"Gloomveil.MapGeneration"` to `references` so `Game.Gameplay` can see `GeneratedMapRegistry`/`GeneratedMapRuntimeData`.
- **Create** `Assets/_Project/Code/Tests/PlayMode/Game.Gameplay.PlayModeTests.asmdef` — new PlayMode test assembly for `Game.Gameplay` (none currently exists under `_Project/Code`; `Gloomveil.MapGeneration.Tests` is the sibling pattern to copy).
- **Create** `Assets/_Project/Code/Tests/PlayMode/PlayerSpawnGateTests.cs` — `UnityTest` that generates a real map through `MapGenerationBootstrap` and asserts the player ends up at `StartAnchor`.
- **Modify** `Assets/Scenes/Game.unity` — attach `PlayerController` to the existing `Player` GameObject (wiring `cameraPivot`/`flashlight` to the existing `CameraPivot`/`FlashlightController`), add a `PlayerSpawnGate` component (on `Player`) bound to the scene's `GeneratedMapRegistry` and the new `PlayerController`. Done through the Unity Editor (MCP tools), not hand-edited YAML — this file is fragile to edit by hand and the project's own tooling (`MapModuleCatalogValidator`, etc.) assumes it's only ever touched through the Editor.

---

## Task 1: `PlayerSpawnGate` component (TDD)

**Files:**
- Create: `Assets/_Project/Code/Gameplay/Player/PlayerSpawnGate.cs`
- Modify: `Assets/_Project/Code/Gameplay/Game.Gameplay.asmdef`
- Create: `Assets/_Project/Code/Tests/PlayMode/Game.Gameplay.PlayModeTests.asmdef`
- Test: `Assets/_Project/Code/Tests/PlayMode/PlayerSpawnGateTests.cs`

**Interfaces:**
- Consumes: `Gloomveil.MapGeneration.GeneratedMapRegistry` (`event Action<GeneratedMapRuntimeData> MapReady`, `bool TryGetCurrent(out GeneratedMapRuntimeData data)`), `Gloomveil.MapGeneration.GeneratedMapRuntimeData.StartAnchor` (`Transform`), `Game.Gameplay.Player.PlayerController.Teleport(Vector3 feetPosition, float yawDegrees)` and `.Yaw` (`float`) — all already implemented, no changes needed to any of them.
- Produces: `Game.Gameplay.Player.PlayerSpawnGate` with `public void Bind(GeneratedMapRegistry mapRegistry, PlayerController player)`. Task 2 (scene wiring) calls this — or sets the equivalent serialized Inspector fields directly — to connect the scene's registry/player instances.

- [ ] **Step 1: Add the assembly reference so `Game.Gameplay` can see `Gloomveil.MapGeneration`**

Read `Assets/_Project/Code/Gameplay/Game.Gameplay.asmdef`, then edit its `references` array from:

```json
    "references": [
        "Game.Core",
        "Unity.InputSystem"
    ],
```

to:

```json
    "references": [
        "Game.Core",
        "Unity.InputSystem",
        "Gloomveil.MapGeneration"
    ],
```

- [ ] **Step 2: Create the test assembly definition**

Create `Assets/_Project/Code/Tests/PlayMode/Game.Gameplay.PlayModeTests.asmdef`:

```json
{
  "name": "Game.Gameplay.PlayModeTests",
  "rootNamespace": "Game.Gameplay.Tests",
  "references": [
    "Game.Gameplay",
    "Game.Core",
    "Gloomveil.MapGeneration",
    "Unity.AI.Navigation"
  ],
  "defineConstraints": [
    "UNITY_EDITOR"
  ],
  "optionalUnityReferences": [
    "TestAssemblies"
  ]
}
```

(This mirrors `Assets/Gloomveil/MapGeneration/Tests/PlayMode/Gloomveil.MapGeneration.PlayModeTests.asmdef` exactly, just pointed at the Gameplay assemblies.)

- [ ] **Step 3: Write the failing test**

Create `Assets/_Project/Code/Tests/PlayMode/PlayerSpawnGateTests.cs`:

```csharp
using System;
using System.Collections;
using Game.Gameplay.Player;
using Gloomveil.MapGeneration;
using NUnit.Framework;
using Unity.AI.Navigation;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class PlayerSpawnGateTests
    {
        private const string CatalogPath =
            "Assets/Gloomveil/MapGeneration/Data/GrayboxMapModuleCatalog.asset";

        [UnityTest]
        public IEnumerator OnMapReady_TeleportsPlayerToStartAnchor()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Bootstrap.Generate(2024);
                yield return null;

                Assert.That(fixture.Registry.IsReady, Is.True);
                Transform startAnchor = fixture.Registry.Current.StartAnchor;
                Assert.That(startAnchor, Is.Not.Null);

                Vector3 playerPosition = fixture.PlayerRigidbody.position;
                Assert.That(
                    Vector3.Distance(playerPosition, startAnchor.position),
                    Is.LessThan(0.01f));
                Assert.That(
                    Mathf.Abs(Mathf.DeltaAngle(fixture.PlayerController.Yaw, startAnchor.eulerAngles.y)),
                    Is.LessThan(0.01f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static Fixture CreateFixture()
        {
#if UNITY_EDITOR
            MapModuleCatalog catalog =
                AssetDatabase.LoadAssetAtPath<MapModuleCatalog>(CatalogPath);
#else
            MapModuleCatalog catalog = null;
#endif
            Assert.That(catalog, Is.Not.Null, CatalogPath);

            MapGenerationConfig config = ScriptableObject.CreateInstance<MapGenerationConfig>();
            config.ConfigureGeneratedContent(catalog, 2);

            var mapRoot = new GameObject("PlayerSpawnGateFixture_Map");
            GeneratedMapRegistry registry = mapRoot.AddComponent<GeneratedMapRegistry>();
            var surfaceObject = new GameObject("GeneratedGeometrySurface");
            surfaceObject.transform.SetParent(mapRoot.transform, false);
            NavMeshSurface surface = surfaceObject.AddComponent<NavMeshSurface>();
            GeneratedNavMeshController navMeshController = mapRoot.AddComponent<GeneratedNavMeshController>();
            navMeshController.Configure(surface);
            MapGenerationBootstrap bootstrap = mapRoot.AddComponent<MapGenerationBootstrap>();
            bootstrap.Configure(config, navMeshController, registry);

            var playerObject = new GameObject(
                "PlayerSpawnGateFixture_Player",
                typeof(Rigidbody),
                typeof(CapsuleCollider));
            PlayerController playerController = playerObject.AddComponent<PlayerController>();
            Rigidbody playerRigidbody = playerObject.GetComponent<Rigidbody>();

            var gateObject = new GameObject("PlayerSpawnGateFixture_Gate");
            PlayerSpawnGate gate = gateObject.AddComponent<PlayerSpawnGate>();
            gate.Bind(registry, playerController);

            return new Fixture(
                mapRoot,
                config,
                playerObject,
                gateObject,
                bootstrap,
                registry,
                playerController,
                playerRigidbody);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject mapRoot;
            private readonly MapGenerationConfig config;
            private readonly GameObject playerObject;
            private readonly GameObject gateObject;

            public MapGenerationBootstrap Bootstrap { get; }
            public GeneratedMapRegistry Registry { get; }
            public PlayerController PlayerController { get; }
            public Rigidbody PlayerRigidbody { get; }

            public Fixture(
                GameObject mapRoot,
                MapGenerationConfig config,
                GameObject playerObject,
                GameObject gateObject,
                MapGenerationBootstrap bootstrap,
                GeneratedMapRegistry registry,
                PlayerController playerController,
                Rigidbody playerRigidbody)
            {
                this.mapRoot = mapRoot;
                this.config = config;
                this.playerObject = playerObject;
                this.gateObject = gateObject;
                Bootstrap = bootstrap;
                Registry = registry;
                PlayerController = playerController;
                PlayerRigidbody = playerRigidbody;
            }

            public void Dispose()
            {
                Bootstrap.ClearGeneratedMap();
                UnityEngine.Object.DestroyImmediate(gateObject);
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(mapRoot);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Use the `mcp__UnityMCP__run_tests` tool with mode `PlayMode` and a filter/assembly targeting `Game.Gameplay.PlayModeTests` (or open Unity's Test Runner window → PlayMode tab → run `PlayerSpawnGateTests`).

Expected: compile error — `PlayerSpawnGate` does not exist yet (`Assets/_Project/Code/Gameplay/Player/PlayerSpawnGate.cs` has not been created).

- [ ] **Step 5: Implement `PlayerSpawnGate`**

Create `Assets/_Project/Code/Gameplay/Player/PlayerSpawnGate.cs`:

```csharp
using Gloomveil.MapGeneration;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// Places the player at the generated map's Anchor_PlayerStart the moment a map is published.
    /// Map generation -> gameplay stays one-directional, mediated by GeneratedMapRegistry (see
    /// CLAUDE.md's "Key invariant") -- this is the one place Gameplay reaches into it.
    ///
    /// Bind() plus a dual OnEnable/Start subscribe is the same reliability pattern SafeZone/FuseBox
    /// use for their own registrations (see SceneWiring.cs): whichever composition root wires this
    /// scene may call Bind() after OnEnable already ran once with null references, so Start()
    /// re-attempts the (idempotent, guarded) subscription as a fallback.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSpawnGate : MonoBehaviour
    {
        [SerializeField] private GeneratedMapRegistry registry;
        [SerializeField] private PlayerController playerController;

        private bool subscribed;

        public void Bind(GeneratedMapRegistry mapRegistry, PlayerController player)
        {
            registry = mapRegistry;
            playerController = player;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();

        private void Start() => Subscribe();

        private void OnDisable()
        {
            if (!subscribed) return;
            registry.MapReady -= OnMapReady;
            subscribed = false;
        }

        private void Subscribe()
        {
            if (subscribed || registry == null || playerController == null) return;
            subscribed = true;
            registry.MapReady += OnMapReady;
            if (registry.TryGetCurrent(out GeneratedMapRuntimeData data))
            {
                SpawnAt(data);
            }
        }

        private void OnMapReady(GeneratedMapRuntimeData data) => SpawnAt(data);

        private void SpawnAt(GeneratedMapRuntimeData data) =>
            playerController.Teleport(data.StartAnchor.position, data.StartAnchor.eulerAngles.y);
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Use `mcp__UnityMCP__run_tests` (mode `PlayMode`, filter `Game.Gameplay.PlayModeTests`) again, or the Test Runner window.

Expected: `PlayerSpawnGateTests.OnMapReady_TeleportsPlayerToStartAnchor` PASS.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Code/Gameplay/Player/PlayerSpawnGate.cs \
        Assets/_Project/Code/Gameplay/Game.Gameplay.asmdef \
        Assets/_Project/Code/Tests/PlayMode/Game.Gameplay.PlayModeTests.asmdef \
        Assets/_Project/Code/Tests/PlayMode/PlayerSpawnGateTests.cs
git commit -m "feat: spawn player at generated map's start anchor"
```

---

## Task 2: Wire `PlayerSpawnGate` into `Assets/Scenes/Game.unity`

**Files:**
- Modify: `Assets/Scenes/Game.unity`

**Interfaces:**
- Consumes: `Game.Gameplay.Player.PlayerSpawnGate.Bind(GeneratedMapRegistry, PlayerController)` and `Game.Gameplay.Player.PlayerController.Bind(Transform lookPivot, FlashlightController flashlightController)` (both from Task 1 / already-existing code) plus the scene's existing `GeneratedMapRegistry` on the `MapGenerationRoot` GameObject.
- Produces: a scene where pressing Play spawns the player at whatever room the current seed assigns as `RoomRole.Start`, instead of at the `Player` GameObject's authored `(0, 0, 0)`.

This task edits a `.unity` scene, which is binary-adjacent YAML that's easy to corrupt by hand — do it entirely through the Unity Editor (via the `mcp__UnityMCP__*` tools), not by editing the file's text.

- [ ] **Step 1: Inspect current scene state**

Use `mcp__UnityMCP__manage_scene` (or `find_gameobjects`) to confirm the scene layout matches what this plan assumes:
- A `Player` GameObject with `Rigidbody`, `CapsuleCollider`, `FlashlightController` children `CameraPivot` → `PlayerCamera`, but **no** `PlayerController` component yet.
- A `MapGenerationRoot`-style GameObject holding `MapGenerationBootstrap` and `GeneratedMapRegistry`.

If either has since changed (e.g. `PlayerController` was already added by other work), adjust the remaining steps accordingly rather than duplicating components.

- [ ] **Step 2: Attach and wire `PlayerController` on `Player`**

Using `mcp__UnityMCP__manage_components` (or `manage_gameobject`), add a `Game.Gameplay.Player.PlayerController` component to the `Player` GameObject. Set its serialized fields:
- `cameraPivot` → the `Player/CameraPivot` Transform.
- `flashlight` → the existing `FlashlightController` component already on `Player`.

Leave movement/look/stamina tuning fields at their `[SerializeField]` defaults (they match TGD sections 6-7 already).

- [ ] **Step 3: Add and bind `PlayerSpawnGate`**

Add a `Game.Gameplay.Player.PlayerSpawnGate` component to the `Player` GameObject (co-locating it with `PlayerController` keeps the player's own spawn behavior on the player itself). Set its serialized fields:
- `registry` → the scene's `GeneratedMapRegistry` component (on the map generation root GameObject).
- `playerController` → the `PlayerController` component just added in Step 2.

(Setting the `[SerializeField]` fields directly via the Inspector/`manage_components` is equivalent to calling `Bind(...)` at runtime — either works since both fields are serialized.)

- [ ] **Step 4: Save the scene**

Use `mcp__UnityMCP__manage_scene` to save `Assets/Scenes/Game.unity`.

- [ ] **Step 5: Verify in Play Mode**

Use `mcp__UnityMCP__manage_editor` to enter Play Mode on `Game.unity`. Confirm via `mcp__UnityMCP__read_console` that no errors were logged (in particular, no `PlayerSpawnGate is missing registry or playerController` — that would mean Step 3's references weren't set), and confirm (via `find_gameobjects`/`manage_gameobject` reading the `Player` Transform, or visually) that the `Player` GameObject's position after map generation matches the generated map's `StartAnchor` position for that GameObject's `MapGenerationBootstrap` seed — not `(0, 0, 0)`. Exit Play Mode.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scenes/Game.unity
git commit -m "chore: wire PlayerController + PlayerSpawnGate into Game.unity"
```

---

## Self-Review Notes

- **Spec coverage:** "start node seçilip playerın oraya spawn olması" (select a start node, spawn the player there) is fully covered — node selection already exists (`GameplayRoleAssigner` → `RoomRole.Start`) and is unmodified; Task 1 adds the spawn mechanism; Task 2 wires it into the actual gameplay scene so it's observable, not just unit-tested.
- **Placeholder scan:** no TBDs; all code blocks are complete and compile against the actual existing APIs read from the repo (`GeneratedMapRegistry`, `GeneratedMapRuntimeData`, `PlayerController.Teleport`/`.Yaw`, `MapGenerationBootstrap.Configure`/`.Generate`, `MapGenerationConfig.ConfigureGeneratedContent(catalog, floors)`).
- **Type consistency:** `PlayerSpawnGate.Bind(GeneratedMapRegistry, PlayerController)` in Task 1's implementation matches its usage in both the Task 1 test and Task 2's scene-wiring description.
