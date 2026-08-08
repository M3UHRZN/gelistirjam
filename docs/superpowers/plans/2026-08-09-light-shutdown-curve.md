# Light Shutdown Curve Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `LightDirector`'s fixed "shut down exactly one zone per completed fuse" behavior with a percentage-of-pool target curve, so Final Chase's "most of the map is dark" (TGD §32) holds regardless of how many `LightZone`s the procedural map generates.

**Architecture:** `LightDirector` (plain C# class, no `UnityEngine` dependency today beyond the `LightZone` type it already references) tracks how many fuse completions it has seen and, on each `OnFuseCompleted()` call, computes a target *active* zone count from a percentage curve (`[75%, 45%, 15%]` by default), then flickers zones from its ordered list until that target is reached. `MainSceneWiring` exposes the curve as an Inspector-tunable `float[]` and hands it to the `LightDirector` instance it owns at `Wire()` time.

**Tech Stack:** Unity 6000.3.13f1, C#, NUnit + Unity Test Framework (PlayMode tests — this assembly has no EditMode test folder, and `LightZone` requires a live `GameObject`, so new tests join the existing `PlayMode` folder alongside `MainSceneWiringLightTests.cs`).

## Global Constraints

- Do NOT run `git commit`, `git add`, or any other git command — the user reviews and commits all changes themselves this session. Every task's last step is "leave the change uncommitted," not a commit.
- No other production files besides `LightDirector.cs` and `MainSceneWiring.cs` change. `MapLightGate.cs`, `MapSafeZoneGate.cs`, `FuseBox.cs`, `ObjectiveManager.cs`, `MonsterAITestSceneBuilder.cs`, and all Monster AI files are explicitly out of scope — the audit (see `docs/superpowers/specs/2026-08-09-light-tdd-compliance-audit-design.md`) confirmed their boundaries are already correct.
- Default shutdown curve: `{75f, 45f, 15f}` (percent of the non-SafeZone pool still active after Fuse 1/2/3 respectively). SafeZone-role zones never enter `LightDirector`'s pool at all (unchanged, handled entirely by `MapLightGate` before `SetShutdownOrder` is ever called) — this plan does not touch that exemption.
- `ShutdownAll()` (Final Chase) behavior is unchanged: it still unconditionally disables every zone in the pool.

---

### Task 1: `LightDirector` percentage-target shutdown + direct tests

**Files:**
- Modify: `Assets/_Project/Code/Gameplay/World/LightDirector.cs` (full file, ~40 lines today)
- Test: `Assets/_Project/Code/Tests/PlayMode/LightDirectorShutdownCurveTests.cs` (new)

**Interfaces:**
- Consumes: `Game.Gameplay.World.LightZone` — existing public members used: `SetActive(bool)`, `Flicker()`, `State` (returns `LightZoneState`, enum values `Active`/`Flickering`/`Disabled`).
- Produces (for Task 2 and for `MapLightGate`/`MainSceneWiring`, both already calling the untouched members):
  - `LightDirector.SetShutdownOrder(IReadOnlyList<LightZone> orderedZones)` — signature unchanged.
  - `LightDirector.OnFuseCompleted()` — signature unchanged, behavior changes internally.
  - `LightDirector.ShutdownAll()` — signature and behavior unchanged.
  - **New:** `LightDirector.SetShutdownTargetPercents(float[] targetActivePercents)` — sets the per-stage curve; no-ops (keeps prior curve) if passed `null` or an empty array. Task 2 calls this from `MainSceneWiring.Wire()`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/_Project/Code/Tests/PlayMode/LightDirectorShutdownCurveTests.cs`:

```csharp
using System.Collections.Generic;
using Game.Gameplay.World;
using NUnit.Framework;
using UnityEngine;

namespace Game.Gameplay.Tests
{
    public sealed class LightDirectorShutdownCurveTests
    {
        [Test]
        public void OnFuseCompleted_DefaultCurve_SmallPool_MatchesExpectedActiveCountsPerStage()
        {
            var fixture = new Fixture(zoneCount: 5);
            try
            {
                var director = new LightDirector();
                director.SetShutdownOrder(fixture.Zones);

                director.OnFuseCompleted();
                Assert.That(fixture.ActiveCount, Is.EqualTo(4), "after Fuse 1 (target 75% of 5)");

                director.OnFuseCompleted();
                Assert.That(fixture.ActiveCount, Is.EqualTo(2), "after Fuse 2 (target 45% of 5)");

                director.OnFuseCompleted();
                Assert.That(fixture.ActiveCount, Is.EqualTo(1), "after Fuse 3 (target 15% of 5)");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void OnFuseCompleted_DefaultCurve_LargePool_MatchesExpectedActiveCountsPerStage()
        {
            var fixture = new Fixture(zoneCount: 61);
            try
            {
                var director = new LightDirector();
                director.SetShutdownOrder(fixture.Zones);

                director.OnFuseCompleted();
                Assert.That(fixture.ActiveCount, Is.EqualTo(46), "after Fuse 1 (target 75% of 61)");

                director.OnFuseCompleted();
                Assert.That(fixture.ActiveCount, Is.EqualTo(27), "after Fuse 2 (target 45% of 61)");

                director.OnFuseCompleted();
                Assert.That(fixture.ActiveCount, Is.EqualTo(9), "after Fuse 3 (target 15% of 61)");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void OnFuseCompleted_BeyondCurveLength_ReusesLastPercent()
        {
            var fixture = new Fixture(zoneCount: 10);
            try
            {
                var director = new LightDirector();
                director.SetShutdownOrder(fixture.Zones);
                director.SetShutdownTargetPercents(new[] { 50f });

                director.OnFuseCompleted(); // stage 1: target 50% of 10 -> 5 active
                Assert.That(fixture.ActiveCount, Is.EqualTo(5), "after Fuse 1 (only curve entry)");

                director.OnFuseCompleted(); // stage 2: curve exhausted, reuse 50% -> already at 5 active, no further shutdown
                Assert.That(fixture.ActiveCount, Is.EqualTo(5), "after Fuse 2 (curve length 1, reuses last entry)");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void SetShutdownTargetPercents_NullOrEmpty_KeepsPriorCurve()
        {
            var fixture = new Fixture(zoneCount: 5);
            try
            {
                var director = new LightDirector();
                director.SetShutdownOrder(fixture.Zones);
                director.SetShutdownTargetPercents(null);
                director.SetShutdownTargetPercents(System.Array.Empty<float>());

                director.OnFuseCompleted();
                Assert.That(fixture.ActiveCount, Is.EqualTo(4), "still using the built-in default curve (75% of 5)");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ShutdownAll_DisablesEveryZoneRegardlessOfCurveProgress()
        {
            var fixture = new Fixture(zoneCount: 6);
            try
            {
                var director = new LightDirector();
                director.SetShutdownOrder(fixture.Zones);

                director.OnFuseCompleted(); // partial shutdown only
                director.ShutdownAll();

                Assert.That(fixture.ActiveCount, Is.EqualTo(0));
                foreach (LightZone zone in fixture.Zones)
                {
                    Assert.That(zone.State, Is.EqualTo(LightZoneState.Disabled));
                }
            }
            finally
            {
                fixture.Dispose();
            }
        }

        /// <summary>Builds N live, Active LightZones on their own GameObjects and tears them down on Dispose.</summary>
        private sealed class Fixture
        {
            public readonly List<LightZone> Zones = new List<LightZone>();
            private readonly List<GameObject> gameObjects = new List<GameObject>();

            public Fixture(int zoneCount)
            {
                for (int i = 0; i < zoneCount; i++)
                {
                    var go = new GameObject($"Zone_{i}");
                    gameObjects.Add(go);
                    LightZone zone = go.AddComponent<LightZone>();
                    zone.SetActive(true);
                    Zones.Add(zone);
                }
            }

            public int ActiveCount
            {
                get
                {
                    int count = 0;
                    foreach (LightZone zone in Zones)
                    {
                        if (zone.State != LightZoneState.Disabled) count++;
                    }
                    return count;
                }
            }

            public void Dispose()
            {
                foreach (GameObject go in gameObjects)
                {
                    if (go != null) Object.DestroyImmediate(go);
                }
            }
        }
    }
}
```

Note: `ActiveCount` counts anything not `Disabled` (i.e. `Active` or `Flickering`) as "still on" — `OnFuseCompleted` calls `Flicker()`, which sets `State` to `Flickering` synchronously and only reaches `Disabled` after its coroutine runs (`flickerDurationSeconds`, default 2.5s of simulated time). Since these are plain `[Test]` (not `[UnityTest]`) methods, no frames advance and the coroutine never runs — so freshly-flickered zones read `Flickering`, not yet `Disabled`. Counting them as "still on" for `ActiveCount` is deliberately correct: they are still lit (mid-warning) at the moment `OnFuseCompleted()` returns, matching TGD §31's "flicker is a warning, not an instant cut." `ShutdownAll_DisablesEveryZoneRegardlessOfCurveProgress` calls `SetActive(false)` on every zone directly (no coroutine involved), so it does resolve to `Disabled` synchronously — this is why that test can assert `Disabled` directly while the curve tests assert on `ActiveCount` instead.

- [ ] **Step 2: Run the tests to verify they fail**

Run via Unity's Test Runner (Window > General > Test Runner > PlayMode tab > run `Game.Gameplay.Tests.LightDirectorShutdownCurveTests`), or `mcp__UnityMCP__run_tests` with `mode=PlayMode`, `test_names=Game.Gameplay.Tests.LightDirectorShutdownCurveTests` if the Unity MCP connection is available.

Expected: compile error (`SetShutdownTargetPercents` doesn't exist yet) or, if compilation is somehow clean, `OnFuseCompleted_DefaultCurve_SmallPool_MatchesExpectedActiveCountsPerStage` and its sibling FAIL — current behavior shuts down exactly one zone per call (`ActiveCount` would be 4, 3, 2 instead of 4, 2, 1).

- [ ] **Step 3: Replace `LightDirector.cs` with the percentage-target implementation**

Replace the full contents of `Assets/_Project/Code/Gameplay/World/LightDirector.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Progressively shuts down LightZones as fuses complete (TGD section 31-32). Zones are shut
    /// down in the order they're registered — design that order deliberately (less-critical zones
    /// first, anything near the exit last) when building the real level. SafeZone-linked zones
    /// should simply never be added to this list.
    ///
    /// Shutdown pace is driven by a percentage-of-pool curve rather than a fixed zone count: each
    /// completed fuse targets "this many percent of the pool should still be active," which is what
    /// makes "Fuse 3: most of the map is dark" (TGD section 32) hold true whether the pool has 5
    /// zones (hand-built test scenes) or 60+ (the procedural main scene) — a fixed "one zone per
    /// fuse" pace only reads as intended at the small end of that range.
    /// </summary>
    public class LightDirector
    {
        private static readonly float[] DefaultShutdownTargetPercents = { 75f, 45f, 15f };

        private readonly List<LightZone> zonesToShutdown = new List<LightZone>();
        private float[] shutdownTargetPercents = DefaultShutdownTargetPercents;
        private int nextIndexToShutdown;
        private int fuseCompletionsSeen;

        public void SetShutdownOrder(IReadOnlyList<LightZone> orderedZones)
        {
            zonesToShutdown.Clear();
            zonesToShutdown.AddRange(orderedZones);
            nextIndexToShutdown = 0;
            fuseCompletionsSeen = 0;
        }

        /// <summary>
        /// Overrides the default per-fuse "percent of pool still active" curve. Each entry is the
        /// target active percentage after that fuse completes (entry 0 -> after fuse 1, entry 1 ->
        /// after fuse 2, ...); if more fuses complete than the curve has entries, the last entry is
        /// reused. No-ops on a null or empty array so callers can't accidentally clear the curve.
        /// </summary>
        public void SetShutdownTargetPercents(float[] targetActivePercents)
        {
            if (targetActivePercents == null || targetActivePercents.Length == 0) return;
            shutdownTargetPercents = targetActivePercents;
        }

        /// <summary>
        /// Called once per completed fuse — flickers (TGD section 31: warning, not an instant cut)
        /// however many zones are needed to bring the pool down to this stage's target active
        /// percentage, then leaves the rest untouched until the next fuse.
        /// </summary>
        public void OnFuseCompleted()
        {
            fuseCompletionsSeen++;
            int stageIndex = Math.Min(fuseCompletionsSeen, shutdownTargetPercents.Length) - 1;
            float targetActivePercent = shutdownTargetPercents[stageIndex];

            int targetActiveCount = (int)Math.Round(
                zonesToShutdown.Count * (targetActivePercent / 100f), MidpointRounding.AwayFromZero);
            int targetDisabledCount = zonesToShutdown.Count - targetActiveCount;

            while (nextIndexToShutdown < targetDisabledCount && nextIndexToShutdown < zonesToShutdown.Count)
            {
                zonesToShutdown[nextIndexToShutdown].Flicker();
                nextIndexToShutdown++;
            }
        }

        /// <summary>Final chase (TGD section 38): kill every zone this director controls, in one pass.</summary>
        public void ShutdownAll()
        {
            for (int i = 0; i < zonesToShutdown.Count; i++)
            {
                zonesToShutdown[i].SetActive(false);
            }
            nextIndexToShutdown = zonesToShutdown.Count;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run via Unity's Test Runner (PlayMode > `Game.Gameplay.Tests.LightDirectorShutdownCurveTests`) or `mcp__UnityMCP__run_tests` (`mode=PlayMode`, `test_names=Game.Gameplay.Tests.LightDirectorShutdownCurveTests`).

Expected: 5/5 pass.

Also re-run `Game.Gameplay.Tests.MainSceneWiringLightTests` (regression — its only assertion is that `ShutdownAll()` via Final Chase still disables a zone; unaffected by this change) and confirm it still passes 1/1.

- [ ] **Step 5: Leave uncommitted**

Do not run `git add`/`git commit`. The user commits all changes themselves this session — confirm the working tree still shows `LightDirector.cs` modified and `LightDirectorShutdownCurveTests.cs` untracked, and move on to Task 2.

---

### Task 2: Wire the curve into `MainSceneWiring`

**Files:**
- Modify: `Assets/_Project/Code/Gameplay/MainSceneWiring.cs:20-40` (the field block and `Wire()` method)
- Test: `Assets/_Project/Code/Tests/PlayMode/MainSceneWiringLightTests.cs` (add one test to the existing file)

**Interfaces:**
- Consumes: `LightDirector.SetShutdownTargetPercents(float[])` (produced by Task 1).
- Produces: `MainSceneWiring` gains a `[SerializeField] private float[] shutdownTargetPercents` field, default `{75f, 45f, 15f}`, applied to the `LightDirector` it owns during `Wire()`. No new public members — `MainSceneWiring.LightDirector` (existing property) is how tests/other code observe the result.

- [ ] **Step 1: Write the failing test**

Add this test method inside the existing `MainSceneWiringLightTests` class in `Assets/_Project/Code/Tests/PlayMode/MainSceneWiringLightTests.cs` (add it after `FinalChaseState_ShutsDownEveryRegisteredLightZone`, before the closing brace of the class):

```csharp
        [UnityTest]
        public IEnumerator Wire_AppliesConfiguredShutdownTargetPercentsToLightDirector()
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

            var zoneObjects = new GameObject[10];
            var zones = new LightZone[10];
            for (int i = 0; i < zones.Length; i++)
            {
                zoneObjects[i] = new GameObject($"Zone_{i}");
                zones[i] = zoneObjects[i].AddComponent<LightZone>();
                zones[i].SetActive(true);
            }
            wiring.LightDirector.SetShutdownOrder(zones);

            try
            {
                // Drive LightDirector directly rather than through a real FuseBox/ObjectiveManager
                // completion flow (ObjectiveManager only exposes OnAnyFuseCompleted via a registered
                // FuseBox's own interaction flow — irrelevant to what this test checks). This test's
                // job is only to confirm Wire() applied MainSceneWiring's configured curve to the
                // LightDirector instance it owns, not to re-test fuse completion plumbing (already
                // covered by MapFuseGate/FuseBox's own tests).
                wiring.LightDirector.OnFuseCompleted(); // one fuse -> Wire()'s default curve targets 75% active

                int activeCount = 0;
                foreach (LightZone zone in zones)
                {
                    if (zone.State != LightZoneState.Disabled) activeCount++;
                }

                Assert.That(activeCount, Is.EqualTo(8), "default curve targets 75% of 10 active after Fuse 1");
            }
            finally
            {
                foreach (GameObject go in zoneObjects) Object.DestroyImmediate(go);
                Object.DestroyImmediate(wiringObject);
                Object.DestroyImmediate(fuseMiniGameObject);
                Object.DestroyImmediate(mapExitObject);
                Object.DestroyImmediate(registryObject);
                Object.DestroyImmediate(objectiveManagerObject);
                Object.DestroyImmediate(gameFlowObject);
            }

            yield break;
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run `Game.Gameplay.Tests.MainSceneWiringLightTests.Wire_AppliesConfiguredShutdownTargetPercentsToLightDirector`.

Expected: FAIL — `activeCount` is 9 (today's fixed "one zone per fuse" behavior), not 8.

- [ ] **Step 3: Modify `MainSceneWiring.cs`**

In `Assets/_Project/Code/Gameplay/MainSceneWiring.cs`, add the new field next to the existing `[SerializeField]` fields (after `private FuseMiniGameController fuseMiniGame;`, before the `lightDirector` field):

```csharp
        [SerializeField, Tooltip("Target percentage of the non-SafeZone LightZone pool still active after fuse 1, 2, 3, ... (TGD section 32). The last entry is reused for any fuse beyond the array's length.")]
        private float[] shutdownTargetPercents = { 75f, 45f, 15f };
```

This needs `using UnityEngine;`'s `Tooltip` attribute, already available since the file already has `using UnityEngine;` (it derives `MonoBehaviour`).

Then, inside `Wire()`, immediately after the existing line `objectiveManager.OnAnyFuseCompleted += lightDirector.OnFuseCompleted;`, add:

```csharp
            lightDirector.SetShutdownTargetPercents(shutdownTargetPercents);
```

- [ ] **Step 4: Run the test to verify it passes**

Run `Game.Gameplay.Tests.MainSceneWiringLightTests` (the full fixture — both the pre-existing `FinalChaseState_ShutsDownEveryRegisteredLightZone` and the new test).

Expected: 2/2 pass.

Also re-run `Game.Gameplay.Tests.LightDirectorShutdownCurveTests` (5/5), `Game.Gameplay.Tests.MapLightGateTests` (2/2), and `Game.Gameplay.Tests.MapSafeZoneGateTests` (1/1) as a full regression pass — none of these should change behavior, but Task 1 touched the class they all depend on.

- [ ] **Step 5: Leave uncommitted**

Do not run `git add`/`git commit`. Confirm the working tree shows `MainSceneWiring.cs` and `MainSceneWiringLightTests.cs` modified, alongside Task 1's changes, and report completion.

---

## Self-Review

**Spec coverage:** The spec's two concrete deliverables — (1) `LightDirector` percentage-target shutdown replacing the fixed one-zone-per-fuse pace, (2) `MainSceneWiring`-exposed, Inspector-tunable curve wired at `Wire()` time — are Task 1 and Task 2 respectively. The spec's "no other production files change" constraint is stated in Global Constraints and no other task touches any other file. The spec's testing requirement (small pool + large pool percentage validation, regression on existing tests) is covered by Task 1's five tests plus the regression re-runs called out in both tasks' Step 4.

**Placeholder scan:** No TBD/TODO; every step has literal code, not a description of code. Task 2's test drives `wiring.LightDirector.OnFuseCompleted()` directly rather than through `ObjectiveManager`/`FuseBox` (verified against `ObjectiveManager.cs`'s real API — `OnAnyFuseCompleted` only fires through a registered `FuseBox`'s own completion flow, which is irrelevant to what this test needs to prove).

**Type consistency:** `LightZone`/`LightZoneState` used identically to their real definitions (`Assets/_Project/Code/Gameplay/World/LightZone.cs`) in both tasks' tests. `LightDirector.SetShutdownTargetPercents(float[])` is defined in Task 1 and consumed with the identical signature in Task 2. `MainSceneWiring.LightDirector` (existing `public LightDirector LightDirector => lightDirector;` property) is relied on unchanged by both tasks' tests.
