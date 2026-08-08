# Main Scene Light System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the already-implemented `LightZone`/`LightDirector`/`SafeZone` gameplay systems into the procedurally generated main scene, so a generated map produces real light zones and safe zones instead of publishing unused anchors.

**Architecture:** Two new `MonoBehaviour` "gates" (`MapLightGate`, `MapSafeZoneGate`) mirror the existing `MapFuseGate`/`MapExitController` pattern: self-subscribe to `GeneratedMapRegistry.MapReady`, get `Bind()`-injected once by an editor setup script, and turn published anchors into real gameplay objects. `MainSceneWiring` gets a small addition (expose its `LightDirector`, add the missing Final Chase shutdown hookup) so `MapLightGate` can push a seeded shutdown order into it.

**Tech Stack:** Unity 6000.3.13f1, C#, NUnit `[UnityTest]` PlayMode tests (Unity Test Runner / `mcp__UnityMCP__run_tests`), no new packages or dependencies.

## Global Constraints

- Spec of record: `docs/superpowers/specs/2026-08-08-main-scene-light-system-design.md` — every task below implements one row of its Requirement Matrix.
- **Do not commit anything.** The user reviews and commits all changes themselves. Skip every "Commit" step that would otherwise appear in this plan.
- No Monster/`MonsterBrain` work — out of scope, confirmed with the user.
- Do not touch `LightZone.cs`, `LightDirector.cs`, `SafeZone.cs`, `SafeZoneRuntimeSet.cs`, `MapFuseGate.cs`, `MapExitController.cs`, `ObjectiveManager.cs`, or any Monster AI file — they already work correctly.
- New gameplay code lives in `Game.Gameplay.World` namespace (`Assets/_Project/Code/Gameplay/World/`), same assembly (`Game.Gameplay.asmdef`) as `MapFuseGate`/`MapExitController` — it already references `Gloomveil.MapGeneration`.
- New editor code lives in `Game.Editor` namespace (`Assets/_Project/Code/Editor/`), mirroring `MainSceneFuseSetup.cs`.
- New tests live in `Assets/_Project/Code/Tests/PlayMode/` (`Game.Gameplay.PlayModeTests.asmdef`, namespace `Game.Gameplay.Tests`) — same assembly `PlayerSpawnGateTests.cs` already uses, so no asmdef changes are needed.
- Test fixtures generate a real map via `MapGenerationBootstrap` + `MapGenerationConfig.ConfigureGeneratedContent(catalog, floors)` against the real catalog asset at `Assets/Gloomveil/MapGeneration/Data/GrayboxMapModuleCatalog.asset`, exactly like `PlayerSpawnGateTests.CreateFixture()` — do not hand-roll fake `GeneratedMapRuntimeData`.
- Default `MapGenerationConfig` values (relevant to these tests): `fuseCount = 3`, `safeZoneCount = 2`. Every generated room module (regardless of `RoomRole`) carries exactly one `LightAnchor` — confirmed in `GrayboxMapKitFactory.cs:741`, the universal per-room anchor block also used for `StalkPoint_Corner`/`MonsterSpawnCandidate`/`PropSocket`. This means every generated map's `SafeZone`-role room is guaranteed to have a `LightAnchor`, so tests can assert on it directly without a conditional skip.

---

### Task 1: `MapLightGate` — spawn LightZones from generated Light anchors

**Files:**
- Create: `Assets/_Project/Code/Gameplay/World/MapLightGate.cs`
- Test: `Assets/_Project/Code/Tests/PlayMode/MapLightGateTests.cs`

**Interfaces:**
- Consumes: `Gloomveil.MapGeneration.GeneratedMapRegistry` (`event Action<GeneratedMapRuntimeData> MapReady`, `bool TryGetCurrent(out GeneratedMapRuntimeData)`), `GeneratedMapRuntimeData.LightAnchors` (`IReadOnlyList<Transform>`), `Transform.GetComponentInParent<GeneratedMapModule>()`, `GeneratedMapModule.RoomRole`/`.CatalogEntryId`/`.PlacementId`/`.RotatedFootprintUnits`, `Game.Gameplay.World.LightZone.Bind(Light[])`/`.SetActive(bool)`/`.State`, `Game.Gameplay.World.LightDirector.SetShutdownOrder(IReadOnlyList<LightZone>)`.
- Produces: `public class MapLightGate : MonoBehaviour` with `void Bind(GeneratedMapRegistry, LightDirector)`, `IReadOnlyList<LightZone> SpawnedLightZones`, `LightZone SafeZoneLightZone` (the zone excluded from the shutdown order; `null` if the map has no SafeZone-role module).

- [ ] **Step 1: Write the failing test**

```csharp
// Assets/_Project/Code/Tests/PlayMode/MapLightGateTests.cs
using System;
using System.Collections;
using System.Linq;
using Game.Gameplay.World;
using Gloomveil.MapGeneration;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class MapLightGateTests
    {
        private const string CatalogPath =
            "Assets/Gloomveil/MapGeneration/Data/GrayboxMapModuleCatalog.asset";

        [UnityTest]
        public IEnumerator OnMapReady_BuildsOneZonePerLitModule_AndExemptsSafeZoneFromShutdown()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Bootstrap.Generate(2024);

                Assert.That(fixture.Gate.SpawnedLightZones.Count, Is.GreaterThan(0));
                Assert.That(fixture.Gate.SafeZoneLightZone, Is.Not.Null,
                    "every generated room carries a LightAnchor, so the SafeZone-role room must too");
                Assert.That(fixture.Gate.SpawnedLightZones, Does.Contain(fixture.Gate.SafeZoneLightZone));

                fixture.LightDirector.ShutdownAll();

                foreach (LightZone zone in fixture.Gate.SpawnedLightZones)
                {
                    if (zone == fixture.Gate.SafeZoneLightZone)
                    {
                        Assert.That(zone.State, Is.EqualTo(LightZoneState.Active),
                            "the SafeZone's own LightZone must survive Final Chase shutdown (TGD section 38)");
                    }
                    else
                    {
                        Assert.That(zone.State, Is.EqualTo(LightZoneState.Disabled));
                    }
                }
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator OnMapReady_SameSeedProducesSameShutdownOrder_DifferentSeedProducesDifferentOrder()
        {
            Fixture fixtureA = CreateFixture();
            Fixture fixtureB = CreateFixture();
            Fixture fixtureC = CreateFixture();

            try
            {
                fixtureA.Bootstrap.Generate(2024);
                fixtureB.Bootstrap.Generate(2024);
                fixtureC.Bootstrap.Generate(4242);

                string[] orderA = ZoneNamesExcludingSafeZone(fixtureA.Gate);
                string[] orderB = ZoneNamesExcludingSafeZone(fixtureB.Gate);
                string[] orderC = ZoneNamesExcludingSafeZone(fixtureC.Gate);

                Assert.That(orderA, Is.EqualTo(orderB), "same seed must produce the same shutdown order");
                Assert.That(orderA, Is.Not.EqualTo(orderC), "different seeds should produce different shutdown orders");
            }
            finally
            {
                fixtureA.Dispose();
                fixtureB.Dispose();
                fixtureC.Dispose();
            }

            yield break;
        }

        private static string[] ZoneNamesExcludingSafeZone(MapLightGate gate) =>
            gate.SpawnedLightZones
                .Where(zone => zone != gate.SafeZoneLightZone)
                .Select(zone => zone.name)
                .ToArray();

        private static Fixture CreateFixture()
        {
#if UNITY_EDITOR
            MapModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<MapModuleCatalog>(CatalogPath);
#else
            MapModuleCatalog catalog = null;
#endif
            Assert.That(catalog, Is.Not.Null, CatalogPath);

            MapGenerationConfig config = ScriptableObject.CreateInstance<MapGenerationConfig>();
            config.ConfigureGeneratedContent(catalog, 2);

            var mapRoot = new GameObject("MapLightGateFixture_Map");
            GeneratedMapRegistry registry = mapRoot.AddComponent<GeneratedMapRegistry>();
            var surfaceObject = new GameObject("GeneratedGeometrySurface");
            surfaceObject.transform.SetParent(mapRoot.transform, false);
            var surface = surfaceObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            var navMeshController = mapRoot.AddComponent<GeneratedNavMeshController>();
            navMeshController.Configure(surface);
            var bootstrap = mapRoot.AddComponent<MapGenerationBootstrap>();
            bootstrap.Configure(config, navMeshController, registry);

            var gateObject = new GameObject("MapLightGateFixture_Gate");
            MapLightGate gate = gateObject.AddComponent<MapLightGate>();
            var lightDirector = new LightDirector();
            gate.Bind(registry, lightDirector);

            return new Fixture(mapRoot, config, gateObject, bootstrap, registry, gate, lightDirector);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject mapRoot;
            private readonly MapGenerationConfig config;
            private readonly GameObject gateObject;

            public MapGenerationBootstrap Bootstrap { get; }
            public GeneratedMapRegistry Registry { get; }
            public MapLightGate Gate { get; }
            public LightDirector LightDirector { get; }

            public Fixture(GameObject mapRoot, MapGenerationConfig config, GameObject gateObject,
                MapGenerationBootstrap bootstrap, GeneratedMapRegistry registry, MapLightGate gate,
                LightDirector lightDirector)
            {
                this.mapRoot = mapRoot;
                this.config = config;
                this.gateObject = gateObject;
                Bootstrap = bootstrap;
                Registry = registry;
                Gate = gate;
                LightDirector = lightDirector;
            }

            public void Dispose()
            {
                Bootstrap.ClearGeneratedMap();
                UnityEngine.Object.DestroyImmediate(gateObject);
                UnityEngine.Object.DestroyImmediate(mapRoot);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run via `mcp__UnityMCP__run_tests` (mode `PlayMode`, filter `Game.Gameplay.Tests.MapLightGateTests`), or Unity's Test Runner window.
Expected: FAIL — compile error, `MapLightGate` does not exist yet.

- [ ] **Step 3: Write `MapLightGate`**

```csharp
// Assets/_Project/Code/Gameplay/World/MapLightGate.cs
using System;
using System.Collections.Generic;
using Gloomveil.MapGeneration;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Spawns a LightZone for every generated map module that carries LightAnchors, when a map is
    /// published (same subscribe-to-MapReady pattern as MapFuseGate/MapExitController — map
    /// generation reaches gameplay only through GeneratedMapRegistry and neutral anchors). Anchors
    /// are grouped by their parent GeneratedMapModule since GeneratedMapRuntimeData.LightAnchors is a
    /// flat, ungrouped list (one room/corridor module = one LightZone). The module tagged
    /// RoomRole.SafeZone is deliberately excluded from the shutdown order it hands to LightDirector —
    /// TGD section 29/38: SafeZone lighting is the "critical light" that must survive Final Chase.
    /// The remaining zones are shuffled with a seed-derived RNG so the shutdown order is part of the
    /// map's deterministic, per-seed randomization (TGD section 43).
    /// </summary>
    [DisallowMultipleComponent]
    public class MapLightGate : MonoBehaviour
    {
        [SerializeField] private GeneratedMapRegistry registry;

        [SerializeField, Tooltip("Point light spawned at a LightAnchor that has no Light of its own.")]
        private float fallbackLightRange = 8f;
        [SerializeField] private float fallbackLightIntensity = 1.2f;

        private LightDirector lightDirector;
        private readonly List<LightZone> spawnedZones = new List<LightZone>();
        private bool subscribed;

        public IReadOnlyList<LightZone> SpawnedLightZones => spawnedZones;
        public LightZone SafeZoneLightZone { get; private set; }

        public void Bind(GeneratedMapRegistry mapRegistry, LightDirector director)
        {
            registry = mapRegistry;
            lightDirector = director;
            if (isActiveAndEnabled) Subscribe();
        }

        // Same OnEnable+Start double-attempt as MapFuseGate/MapExitController: whichever runs first
        // with both references present wins, the guard makes the second a no-op.
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
            if (subscribed || registry == null || lightDirector == null) return;

            subscribed = true;
            registry.MapReady += OnMapReady;
            if (registry.TryGetCurrent(out GeneratedMapRuntimeData data))
            {
                SpawnAll(data);
            }
        }

        private void OnMapReady(GeneratedMapRuntimeData data) => SpawnAll(data);

        private void SpawnAll(GeneratedMapRuntimeData data)
        {
            // A freshly published map is a fresh run: old zones (and their shutdown state) must not
            // carry over, same "fresh run" reasoning as MapFuseGate.SpawnAll/MapExitController.BuildExit.
            ClearSpawned();

            if (data.LightAnchors.Count == 0)
            {
                Debug.LogWarning(
                    $"Generated map (seed {data.Seed}) published no Light anchors — no LightZones were built.",
                    this);
                lightDirector.SetShutdownOrder(Array.Empty<LightZone>());
                return;
            }

            var moduleOrder = new List<GeneratedMapModule>();
            var lightsByModule = new Dictionary<GeneratedMapModule, List<Light>>();
            GeneratedMapModule safeZoneModule = null;

            foreach (Transform anchor in data.LightAnchors)
            {
                if (anchor == null) continue;

                GeneratedMapModule module = anchor.GetComponentInParent<GeneratedMapModule>();
                if (module == null)
                {
                    Debug.LogWarning(
                        $"Light anchor '{anchor.name}' has no parent GeneratedMapModule — skipped.", anchor);
                    continue;
                }

                Light light = anchor.GetComponentInChildren<Light>();
                if (light == null) light = BuildDefaultLight(anchor);

                if (!lightsByModule.TryGetValue(module, out List<Light> lights))
                {
                    lights = new List<Light>();
                    lightsByModule[module] = lights;
                    moduleOrder.Add(module);
                }
                lights.Add(light);

                if (module.RoomRole == RoomRole.SafeZone) safeZoneModule = module;
            }

            var shutdownCandidates = new List<LightZone>();
            SafeZoneLightZone = null;

            foreach (GeneratedMapModule module in moduleOrder)
            {
                LightZone zone = BuildLightZone(module, lightsByModule[module]);
                spawnedZones.Add(zone);

                if (module == safeZoneModule)
                {
                    SafeZoneLightZone = zone;
                }
                else
                {
                    shutdownCandidates.Add(zone);
                }
            }

            if (safeZoneModule == null)
            {
                Debug.LogWarning(
                    $"Generated map (seed {data.Seed}) has no SafeZone-role module among its lit " +
                    "modules — no LightZone is exempt from shutdown.",
                    this);
            }

            Shuffle(shutdownCandidates, data.Seed);
            lightDirector.SetShutdownOrder(shutdownCandidates);
        }

        private LightZone BuildLightZone(GeneratedMapModule module, List<Light> lights)
        {
            var go = new GameObject($"LightZone_{module.CatalogEntryId}_{module.PlacementId}");
            go.transform.SetParent(transform, false);
            LightZone zone = go.AddComponent<LightZone>();
            zone.Bind(lights.ToArray());
            zone.SetActive(true);
            return zone;
        }

        private Light BuildDefaultLight(Transform anchor)
        {
            var go = new GameObject("Light_Fallback");
            go.transform.SetParent(anchor, false);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = fallbackLightRange;
            light.intensity = fallbackLightIntensity;
            return light;
        }

        // Fisher-Yates with a seed-derived RNG: same seed -> same shutdown order (TGD section 43).
        private static void Shuffle(List<LightZone> zones, int seed)
        {
            var random = new System.Random(seed);
            for (int i = zones.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (zones[i], zones[j]) = (zones[j], zones[i]);
            }
        }

        private void ClearSpawned()
        {
            foreach (LightZone zone in spawnedZones)
            {
                if (zone != null) Destroy(zone.gameObject);
            }
            spawnedZones.Clear();
            SafeZoneLightZone = null;
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run via `mcp__UnityMCP__run_tests` (mode `PlayMode`, filter `Game.Gameplay.Tests.MapLightGateTests`).
Expected: PASS — both tests green. If `OnMapReady_SameSeedProducesSameShutdownOrder_DifferentSeedProducesDifferentOrder` fails on the "different order" assertion, check the console for the actual shutdown-order arrays before changing the shuffle: a same-length, same-multiset order collision between two different seeds is possible in principle but should not happen with the default room count — do not weaken the assertion without first confirming a real collision.

---

### Task 2: `MapSafeZoneGate` — spawn SafeZones from generated SafeZone anchors

**Files:**
- Create: `Assets/_Project/Code/Gameplay/World/MapSafeZoneGate.cs`
- Test: `Assets/_Project/Code/Tests/PlayMode/MapSafeZoneGateTests.cs`

**Interfaces:**
- Consumes: `GeneratedMapRuntimeData.SafeZoneAnchors` (`IReadOnlyList<Transform>`), `GeneratedMapModule.RotatedFootprintUnits` (`GridSize2` with `.Width`/`.Depth`, in 4m grid units), `Game.Gameplay.World.SafeZone.Bind(SafeZoneRuntimeSet)`, `Game.Gameplay.World.SafeZoneRuntimeSet.Register`/`.Items`.
- Produces: `public class MapSafeZoneGate : MonoBehaviour` with `void Bind(GeneratedMapRegistry, SafeZoneRuntimeSet)`, `IReadOnlyList<SafeZone> SpawnedSafeZones`.

- [ ] **Step 1: Write the failing test**

```csharp
// Assets/_Project/Code/Tests/PlayMode/MapSafeZoneGateTests.cs
using System;
using System.Collections;
using Game.Gameplay.World;
using Gloomveil.MapGeneration;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class MapSafeZoneGateTests
    {
        private const string CatalogPath =
            "Assets/Gloomveil/MapGeneration/Data/GrayboxMapModuleCatalog.asset";

        [UnityTest]
        public IEnumerator OnMapReady_SpawnsOneSafeZonePerAnchor_RegisteredInRuntimeSet()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Bootstrap.Generate(2024);

                Assert.That(fixture.Registry.IsReady, Is.True);
                int expectedCount = fixture.Registry.Current.SafeZoneAnchors.Count;
                Assert.That(expectedCount, Is.GreaterThan(0), "default config has safeZoneCount = 2");

                Assert.That(fixture.Gate.SpawnedSafeZones.Count, Is.EqualTo(expectedCount));
                Assert.That(fixture.SafeZoneSet.Items.Count, Is.EqualTo(expectedCount));

                foreach (SafeZone zone in fixture.Gate.SpawnedSafeZones)
                {
                    BoxCollider box = zone.GetComponent<BoxCollider>();
                    Assert.That(box, Is.Not.Null);
                    Assert.That(box.isTrigger, Is.True);
                    Assert.That(box.size.x, Is.GreaterThan(0f));
                    Assert.That(box.size.z, Is.GreaterThan(0f));
                    Assert.That(fixture.SafeZoneSet.Items, Does.Contain(zone));
                }
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        private static Fixture CreateFixture()
        {
#if UNITY_EDITOR
            MapModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<MapModuleCatalog>(CatalogPath);
#else
            MapModuleCatalog catalog = null;
#endif
            Assert.That(catalog, Is.Not.Null, CatalogPath);

            MapGenerationConfig config = ScriptableObject.CreateInstance<MapGenerationConfig>();
            config.ConfigureGeneratedContent(catalog, 2);

            var mapRoot = new GameObject("MapSafeZoneGateFixture_Map");
            GeneratedMapRegistry registry = mapRoot.AddComponent<GeneratedMapRegistry>();
            var surfaceObject = new GameObject("GeneratedGeometrySurface");
            surfaceObject.transform.SetParent(mapRoot.transform, false);
            var surface = surfaceObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            var navMeshController = mapRoot.AddComponent<GeneratedNavMeshController>();
            navMeshController.Configure(surface);
            var bootstrap = mapRoot.AddComponent<MapGenerationBootstrap>();
            bootstrap.Configure(config, navMeshController, registry);

            SafeZoneRuntimeSet safeZoneSet = ScriptableObject.CreateInstance<SafeZoneRuntimeSet>();

            var gateObject = new GameObject("MapSafeZoneGateFixture_Gate");
            MapSafeZoneGate gate = gateObject.AddComponent<MapSafeZoneGate>();
            gate.Bind(registry, safeZoneSet);

            return new Fixture(mapRoot, config, gateObject, safeZoneSet, bootstrap, registry, gate);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject mapRoot;
            private readonly MapGenerationConfig config;
            private readonly GameObject gateObject;
            private readonly SafeZoneRuntimeSet safeZoneSet;

            public MapGenerationBootstrap Bootstrap { get; }
            public GeneratedMapRegistry Registry { get; }
            public MapSafeZoneGate Gate { get; }
            public SafeZoneRuntimeSet SafeZoneSet => safeZoneSet;

            public Fixture(GameObject mapRoot, MapGenerationConfig config, GameObject gateObject,
                SafeZoneRuntimeSet safeZoneSet, MapGenerationBootstrap bootstrap,
                GeneratedMapRegistry registry, MapSafeZoneGate gate)
            {
                this.mapRoot = mapRoot;
                this.config = config;
                this.gateObject = gateObject;
                this.safeZoneSet = safeZoneSet;
                Bootstrap = bootstrap;
                Registry = registry;
                Gate = gate;
            }

            public void Dispose()
            {
                Bootstrap.ClearGeneratedMap();
                UnityEngine.Object.DestroyImmediate(gateObject);
                UnityEngine.Object.DestroyImmediate(mapRoot);
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(safeZoneSet);
            }
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run via `mcp__UnityMCP__run_tests` (mode `PlayMode`, filter `Game.Gameplay.Tests.MapSafeZoneGateTests`).
Expected: FAIL — compile error, `MapSafeZoneGate` does not exist yet.

- [ ] **Step 3: Write `MapSafeZoneGate`**

```csharp
// Assets/_Project/Code/Gameplay/World/MapSafeZoneGate.cs
using System.Collections.Generic;
using Gloomveil.MapGeneration;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Spawns a SafeZone at each of the generated map's Anchor_SafeZone markers when a map is
    /// published (same subscribe-to-MapReady pattern as MapFuseGate/MapLightGate). SafeZone.cs
    /// requires an attached Collider but nothing upstream in the generator publishes a room-bounds
    /// Collider (GeneratedMapModule only stores grid-unit footprint sizes) — this builds a BoxCollider
    /// from RotatedFootprintUnits * 4m, world-axis-aligned, since the map's hidden lattice only uses
    /// cardinal rotations. Anchor_SafeZone sits at the room's centre (same placement convention as
    /// Anchor_Exit — see MapExitController's doc comment), so the box is centred there.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapSafeZoneGate : MonoBehaviour
    {
        [SerializeField] private GeneratedMapRegistry registry;
        [SerializeField] private SafeZoneRuntimeSet safeZoneSet;

        [SerializeField, Tooltip("Trigger volume height. Module footprints only carry floor-plan width/depth, not height (TGD kit: 3m walls).")]
        private float roomHeight = 3f;

        [SerializeField, Tooltip("Fallback footprint (metres) used if a SafeZone anchor has no parent GeneratedMapModule.")]
        private Vector2 fallbackFootprint = new Vector2(4f, 4f);

        private readonly List<SafeZone> spawned = new List<SafeZone>();
        private bool subscribed;

        public IReadOnlyList<SafeZone> SpawnedSafeZones => spawned;

        public void Bind(GeneratedMapRegistry mapRegistry, SafeZoneRuntimeSet set)
        {
            registry = mapRegistry;
            safeZoneSet = set;
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
            if (subscribed || registry == null || safeZoneSet == null) return;

            subscribed = true;
            registry.MapReady += OnMapReady;
            if (registry.TryGetCurrent(out GeneratedMapRuntimeData data))
            {
                SpawnAll(data);
            }
        }

        private void OnMapReady(GeneratedMapRuntimeData data) => SpawnAll(data);

        private void SpawnAll(GeneratedMapRuntimeData data)
        {
            // A freshly published map is a fresh run: old SafeZone instances must not carry over,
            // same "fresh run" reasoning as MapFuseGate.SpawnAll/MapExitController.BuildExit.
            ClearSpawned();

            if (data.SafeZoneAnchors.Count == 0)
            {
                Debug.LogWarning(
                    $"Generated map (seed {data.Seed}) published no SafeZone anchors — no SafeZones were built.",
                    this);
                return;
            }

            foreach (Transform anchor in data.SafeZoneAnchors)
            {
                if (anchor == null) continue;
                spawned.Add(BuildSafeZone(anchor));
            }
        }

        private SafeZone BuildSafeZone(Transform anchor)
        {
            GeneratedMapModule module = anchor.GetComponentInParent<GeneratedMapModule>();
            float width = module != null ? module.RotatedFootprintUnits.Width * 4f : fallbackFootprint.x;
            float depth = module != null ? module.RotatedFootprintUnits.Depth * 4f : fallbackFootprint.y;

            string suffix = module != null ? module.PlacementId.ToString() : anchor.GetInstanceID().ToString();
            var go = new GameObject($"SafeZone_{suffix}");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(anchor.position, Quaternion.identity);

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, roomHeight * 0.5f, 0f);
            box.size = new Vector3(width, roomHeight, depth);
            box.isTrigger = true;

            SafeZone safeZone = go.AddComponent<SafeZone>();
            safeZone.Bind(safeZoneSet);
            return safeZone;
        }

        private void ClearSpawned()
        {
            foreach (SafeZone zone in spawned)
            {
                if (zone != null) Destroy(zone.gameObject);
            }
            spawned.Clear();
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run via `mcp__UnityMCP__run_tests` (mode `PlayMode`, filter `Game.Gameplay.Tests.MapSafeZoneGateTests`).
Expected: PASS.

---

### Task 3: `MainSceneWiring` — expose `LightDirector`, add Final Chase shutdown hookup

**Files:**
- Modify: `Assets/_Project/Code/Gameplay/MainSceneWiring.cs`
- Test: `Assets/_Project/Code/Tests/PlayMode/MainSceneWiringLightTests.cs`

**Interfaces:**
- Consumes: `Game.Gameplay.World.LightDirector.ShutdownAll()`, `Game.Core.GameFlowController.OnStateChanged` (`event Action<string>`), `.FinalChaseState` (`IGameState`), `.ChangeState(IGameState)`.
- Produces: `MainSceneWiring.LightDirector` (`public LightDirector LightDirector { get; }`) — `MapLightGate`/the editor setup script in Task 4 read this to hand the gate a shutdown-order target.
- The obsolete `lightShutdownOrder` inspector field is removed — `MapLightGate` now pushes the order into `LightDirector` directly at runtime (procedural zones don't exist at edit/inspector time).

- [ ] **Step 1: Write the failing test**

```csharp
// Assets/_Project/Code/Tests/PlayMode/MainSceneWiringLightTests.cs
using System.Collections;
using Game.Core;
using Game.Gameplay.Objectives;
using Game.Gameplay.World;
using Gloomveil.MapGeneration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class MainSceneWiringLightTests
    {
        [UnityTest]
        public IEnumerator FinalChaseState_ShutsDownEveryRegisteredLightZone()
        {
            var gameFlowObject = new GameObject("GameFlow");
            GameFlowController gameFlow = gameFlowObject.AddComponent<GameFlowController>();

            var objectiveManagerObject = new GameObject("Objectives");
            ObjectiveManager objectiveManager = objectiveManagerObject.AddComponent<ObjectiveManager>();

            var registryObject = new GameObject("Registry");
            GeneratedMapRegistry registry = registryObject.AddComponent<GeneratedMapRegistry>();
            var mapExitObject = new GameObject("MapExit");
            MapExitController mapExit = mapExitObject.AddComponent<MapExitController>();
            mapExit.Bind(registry, gameFlow);

            var fuseMiniGameObject = new GameObject("FuseMiniGame");
            FuseMiniGameController fuseMiniGame = fuseMiniGameObject.AddComponent<FuseMiniGameController>();

            var wiringObject = new GameObject("Wiring");
            MainSceneWiring wiring = wiringObject.AddComponent<MainSceneWiring>();
            wiring.Bind(gameFlow, objectiveManager, mapExit, fuseMiniGame);

            var zoneObject = new GameObject("Zone");
            LightZone zone = zoneObject.AddComponent<LightZone>();
            zone.SetActive(true);
            wiring.LightDirector.SetShutdownOrder(new[] { zone });

            try
            {
                gameFlow.ChangeState(gameFlow.FinalChaseState);

                Assert.That(zone.State, Is.EqualTo(LightZoneState.Disabled));
            }
            finally
            {
                Object.DestroyImmediate(zoneObject);
                Object.DestroyImmediate(wiringObject);
                Object.DestroyImmediate(fuseMiniGameObject);
                Object.DestroyImmediate(mapExitObject);
                Object.DestroyImmediate(registryObject);
                Object.DestroyImmediate(objectiveManagerObject);
                Object.DestroyImmediate(gameFlowObject);
            }

            yield break;
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run via `mcp__UnityMCP__run_tests` (mode `PlayMode`, filter `Game.Gameplay.Tests.MainSceneWiringLightTests`).
Expected: FAIL — compile error, `MainSceneWiring.LightDirector` does not exist yet (private field only).

- [ ] **Step 3: Modify `MainSceneWiring.cs`**

Replace the whole file with:

```csharp
using Game.Core;
using Game.Gameplay.Objectives;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Composition-root wiring for the real procedural main scene (see chat: Fuse Memory Minigame
    /// main-scene integration). Mirrors SceneWiring — the MonsterAI_TestScene's composition root —
    /// but adapted to what the main scene actually has: GameFlowController (not a second GameManager),
    /// MapExitController (not the hand-built-scene ExitController), and no Monster yet. Anger/
    /// fuse-awareness wiring for MonsterBrain is deliberately left for whenever a monster spawner
    /// exists — adding it here now would mean guessing an integration that does not exist yet (out of
    /// scope by explicit instruction). LightDirector is constructed here and exposed publicly so
    /// MapLightGate (a MapReady-driven spawner, same pattern as MapFuseGate/MapExitController) can
    /// hand it a real, seeded shutdown order once the generated map's LightAnchors exist — see
    /// MapLightGate.cs.
    /// </summary>
    public class MainSceneWiring : MonoBehaviour
    {
        [SerializeField] private GameFlowController gameFlow;
        [SerializeField] private ObjectiveManager objectiveManager;
        [SerializeField] private MapExitController mapExit;
        [SerializeField] private FuseMiniGameController fuseMiniGame;

        private readonly LightDirector lightDirector = new LightDirector();
        private bool wired;

        public LightDirector LightDirector => lightDirector;

        public void Bind(GameFlowController flow, ObjectiveManager objectives, MapExitController exit,
            FuseMiniGameController miniGame)
        {
            gameFlow = flow;
            objectiveManager = objectives;
            mapExit = exit;
            fuseMiniGame = miniGame;
            Wire();
        }

        // Same Awake+Start double-attempt as SceneWiring — idempotent-guarded, safe either order.
        private void Awake() => Wire();
        private void Start() => Wire();

        private void Wire()
        {
            if (wired) return;
            if (gameFlow == null || objectiveManager == null || mapExit == null || fuseMiniGame == null) return;
            wired = true;

            objectiveManager.OnAnyFuseCompleted += lightDirector.OnFuseCompleted;

            // All fuses complete -> open the exit -> MapExitController.Open() itself enters
            // FinalChaseState (TGD section 37/38) -- not duplicated here.
            objectiveManager.OnAllComplete += mapExit.Open;

            // Final Chase (TGD section 38): kill the remaining lights. Mirrors SceneWiring's
            // FinalChase handler minus the Monster-specific lines (IsRelentless/AggressiveState) —
            // there is no Monster in the main scene yet.
            gameFlow.OnStateChanged += stateName =>
            {
                if (stateName != nameof(FinalChaseState)) return;
                lightDirector.ShutdownAll();
            };

            // Death mid-minigame (prompt section 27, same fix already made for the test scene's
            // SceneWiring — see chat): the Fuse popup has no way to know the player died on its own.
            gameFlow.OnStateChanged += stateName =>
            {
                if (stateName != nameof(DeathState)) return;
                fuseMiniGame.ForceCancel();
            };
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run via `mcp__UnityMCP__run_tests` (mode `PlayMode`, filter `Game.Gameplay.Tests.MainSceneWiringLightTests`).
Expected: PASS.

- [ ] **Step 5: Re-run Task 1 and Task 2 tests to confirm no regression**

Run via `mcp__UnityMCP__run_tests` (mode `PlayMode`, filter `Game.Gameplay.Tests.MapLightGateTests|Game.Gameplay.Tests.MapSafeZoneGateTests`).
Expected: PASS (unaffected — neither test touches `MainSceneWiring`).

---

### Task 4: Editor setup — wire the new gates into the main scene

**Files:**
- Create: `Assets/_Project/Code/Editor/MainSceneLightSetup.cs`

**Interfaces:**
- Consumes: `MainSceneWiring.LightDirector` (from Task 3), `MapLightGate.Bind(GeneratedMapRegistry, LightDirector)` (from Task 1), `MapSafeZoneGate.Bind(GeneratedMapRegistry, SafeZoneRuntimeSet)` (from Task 2), the existing shared asset `Assets/_Project/Data/SafeZoneRuntimeSet.asset` (same asset already read by Monster AI code and the hand-built test scene — no new registry).
- Produces: a `Tools/Gloomveil/Setup Main Scene Light System` menu item. This is an editor-only, scene-mutating tool — it is not unit tested (no existing precedent for testing `MainSceneFuseSetup.cs` either); verification is manual, in Step 2 below.

- [ ] **Step 1: Write `MainSceneLightSetup.cs`**

```csharp
// Assets/_Project/Code/Editor/MainSceneLightSetup.cs
using Game.Gameplay;
using Game.Gameplay.World;
using Gloomveil.MapGeneration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// One-click setup that wires MapLightGate and MapSafeZoneGate into the currently-open main
    /// procedural scene, mirroring MainSceneFuseSetup.cs — purely ADDITIVE, safe to re-run (every
    /// step checks for an existing object/component first). Requires MainSceneWiring to already exist
    /// (run "Setup Main Scene Fuse System" first if it doesn't) since MapLightGate needs its
    /// LightDirector instance.
    /// </summary>
    public static class MainSceneLightSetup
    {
        private const string SafeZoneRuntimeSetPath = "Assets/_Project/Data/SafeZoneRuntimeSet.asset";

        [MenuItem("Tools/Gloomveil/Setup Main Scene Light System")]
        public static void Run()
        {
            GeneratedMapRegistry registry = Object.FindFirstObjectByType<GeneratedMapRegistry>();
            MainSceneWiring wiring = Object.FindFirstObjectByType<MainSceneWiring>();

            if (registry == null || wiring == null)
            {
                Debug.LogError("Setup Main Scene Light System: missing GeneratedMapRegistry or " +
                                "MainSceneWiring in the active scene. Run 'Setup Main Scene Fuse " +
                                "System' first, and open the main scene.");
                return;
            }

            SafeZoneRuntimeSet safeZoneSet =
                AssetDatabase.LoadAssetAtPath<SafeZoneRuntimeSet>(SafeZoneRuntimeSetPath);
            if (safeZoneSet == null)
            {
                Debug.LogError(
                    $"Setup Main Scene Light System: no SafeZoneRuntimeSet asset at {SafeZoneRuntimeSetPath}.");
                return;
            }

            MapLightGate lightGate = EnsureLightGate();
            MapSafeZoneGate safeZoneGate = EnsureSafeZoneGate();

            lightGate.Bind(registry, wiring.LightDirector);
            safeZoneGate.Bind(registry, safeZoneSet);

            EditorUtility.SetDirty(lightGate);
            EditorUtility.SetDirty(safeZoneGate);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("Main scene Light system wired: MapLightGate, MapSafeZoneGate.");
        }

        private static MapLightGate EnsureLightGate()
        {
            MapLightGate existing = Object.FindFirstObjectByType<MapLightGate>();
            if (existing != null) return existing;

            GameObject go = new GameObject("LightGate");
            return go.AddComponent<MapLightGate>();
        }

        private static MapSafeZoneGate EnsureSafeZoneGate()
        {
            MapSafeZoneGate existing = Object.FindFirstObjectByType<MapSafeZoneGate>();
            if (existing != null) return existing;

            GameObject go = new GameObject("SafeZoneGate");
            return go.AddComponent<MapSafeZoneGate>();
        }
    }
}
```

- [ ] **Step 2: Manually verify in the Unity Editor**

1. Open `Assets/Scenes/MainGame.unity`.
2. Confirm `Tools/Gloomveil/Setup Main Scene Fuse System` has already been run in this scene (required — `MainSceneWiring` must exist). Run it first if not.
3. Run `Tools/Gloomveil/Setup Main Scene Light System`.
4. Confirm the console prints `Main scene Light system wired: MapLightGate, MapSafeZoneGate.` with no errors.
5. Confirm two new GameObjects, `LightGate` and `SafeZoneGate`, exist in the scene hierarchy.
6. Enter Play Mode. Confirm the map generates and the console shows no `NullReferenceException`/`MissingReferenceException`.
7. In the Hierarchy during Play Mode, confirm `LightGate` has child `LightZone_*` GameObjects (one per lit module) and `SafeZoneGate` has child `SafeZone_*` GameObjects (matching the generated map's safe-zone room count).
8. Complete a fuse (or call `ObjectiveManager.HandleFuseCompleted` indirectly by finishing the mini-game) and confirm one non-SafeZone `LightZone` flickers then goes dark.
9. Drive `GameFlowController` to `FinalChaseState` (complete all fuses, reach the exit) and confirm every `LightZone` except the one under the SafeZone-role room goes dark.
10. Exit Play Mode. Save the scene if the user wants these `Bind()` references to persist (note: `Bind()`-injected references are runtime state and will not itself dirty the scene beyond what `Run()` already saved in Step 1).

---

## Self-Review Notes

- **Spec coverage:** every Requirement Matrix row with status "Pending" in the spec maps to a task above — `MapLightGate` (Task 1), `MapSafeZoneGate` (Task 2), Final Chase hookup + `LightDirector` exposure (Task 3), editor wiring (Task 4). Audio (lowest priority, explicitly optional/null-safe in the spec) is intentionally not implemented — no audio system exists anywhere in the repo to hook into yet (confirmed during research), and adding one would be a new system, which the spec's "no new framework" constraint and the brainstorming scope answer (light system only) both rule out. If audio is wanted later, `LightZone.Flicker()`/`SetActive()` are the two call sites a future task would hook.
- **Placeholder scan:** no TBD/TODO markers; every step has runnable code.
- **Type consistency:** `MapLightGate.Bind(GeneratedMapRegistry, LightDirector)` (Task 1) matches its call site in Task 4 (`lightGate.Bind(registry, wiring.LightDirector)`). `MapSafeZoneGate.Bind(GeneratedMapRegistry, SafeZoneRuntimeSet)` (Task 2) matches its Task 4 call site. `MainSceneWiring.LightDirector` (Task 3) is the exact property Task 4 reads. `LightZone.State`/`LightZoneState` enum names match `LightZone.cs` exactly (Active/Flickering/Disabled) — verified against source, not assumed.
