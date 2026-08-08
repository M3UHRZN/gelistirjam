# Light System TDD Compliance Audit + Correction — Design

## Context

Follow-up to `2026-08-08-main-scene-light-system-design.md`. That spec built the
Fixture/LightZone/SafeZone/LightDirector separation and wired it end-to-end
into the main scene. A full audit against TDD §2.3, §29–32, §38, §40–41,
§48, §51 confirms the architecture built there is sound and does not need
rework. One concrete gap remains, found during the prior session's final
review as "Important 3" and left as an open decision — this spec resolves it.

## Audit Summary

Walked the full lighting-relevant surface: `LightZone`, `SafeZone`,
`LightDirector`, `MapLightGate`, `MapSafeZoneGate`, `FuseBox`,
`ObjectiveManager`, `MainSceneWiring`, `GrayboxMapKitFactory`,
`MonsterNavigator`/`StalkPointScorer`/`SafeZoneCampState`,
`MonsterAITestSceneBuilder`.

| Area | Finding | Verdict |
|---|---|---|
| Fixture vs LightZone vs SafeZone separation | Three distinct types, no equivalence anywhere in code | PASS |
| Fuse → Light coupling | `FuseBox`/`ObjectiveManager` carry zero `Light`/`LightZone` references; only `OnAnyFuseCompleted` event | PASS |
| Monster ↔ Light boundary | Monster code (`MonsterNavigator`, `StalkPointScorer`, `SafeZoneCampState`) only ever reads `SafeZoneRuntimeSet`/`IsPointInSafeZone`; zero `Light` component access | PASS |
| Procedural → gameplay direction | Generator publishes neutral `LightAnchor`/`Anchor_SafeZone` anchors only; `MapLightGate`/`MapSafeZoneGate` do the interpreting | PASS |
| SafeZone lighting exemption | Every `RoomRole.SafeZone` module's zone is excluded from `LightDirector`'s shutdown list (fixed in prior session's final review) | PASS |
| Test scene vs main scene | `MonsterAITestSceneBuilder` hand-builds 5 fixed zones for its own sandbox; main scene sources everything from the procedural registry independently — no leakage | PASS |
| **Fuse → shutdown granularity** | `LightDirector.OnFuseCompleted()` shuts down exactly **one** zone per fuse regardless of pool size. Default map publishes **61** zones (11 module types × ~2 floors), `fuseCount = 3` → only ~5% of the map darkens across all three fuses, then `ShutdownAll()` kills the remaining ~95% in one instant cut at Final Chase. TGD §32 expects "Fuse 3: haritanın büyük bölümü karanlık" — a gradual majority-dark state, not a cliff. | **FAIL — this spec fixes it** |

Everything else the original 40-section brief asked to verify (density of
active fixtures, magic numbers, singleton hygiene, debug visualization
existing pattern, flicker not destabilizing AI) was already correct or is a
pre-existing, already-tracked minor (e.g. no in-scene debug view for
LightZone/SafeZone state — P1, not a regression, not in scope here).

## Fix Design

**Goal:** `LightDirector` shuts down a *percentage* of its managed zone pool
per completed fuse, not a fixed count — so the "most of the map goes dark by
Fuse 3" feel holds regardless of how many zones the procedural map generates.

### Target curve

Configurable, default `[F1: 75%, F2: 45%, F3: 15%]` (percentage of the
non-SafeZone pool still **active** after that fuse completes; SafeZone-role
zones are outside this pool entirely, as today). Start remains 100% active —
unchanged, and within TGD §32's stated "100% veya 80%" range.

### Mechanism

- `LightDirector` counts fuse-completion calls (1st, 2nd, 3rd, ...).
- On each call, look up that stage's target active percentage, convert to a
  target *disabled* count against `zonesToShutdown.Count`, and flicker
  zones from `nextIndexToShutdown` onward until that count is reached (may
  flicker more than one zone per fuse — still a flicker warning per zone,
  per TGD §31, just more of them at once as the pool grows).
- If `fuseCount` (map config) exceeds the configured curve's length, the
  curve's last percentage is reused for any extra fuse — no crash, no need
  to keep the two in lockstep.
- `ShutdownAll()` (Final Chase) is unchanged: it already kills every
  remaining non-SafeZone zone in one pass, which is now a much smaller
  remainder (~15%) instead of ~95% — this is what actually delivers TGD
  §38's "only a few critical lights remain."

### Where it lives

- `LightDirector.cs` — add the percentage-target shutdown logic (replaces
  the fixed "shift one zone" behavior).
- `MainSceneWiring.cs` — add `[SerializeField] private float[]
  shutdownTargetPercents = {75f, 45f, 15f};`, pass it to `LightDirector` at
  `Wire()` time. Inspector-configurable per project convention (no bare
  magic numbers in logic).
- No other production file changes. `MapLightGate`, `MapSafeZoneGate`,
  `FuseBox`, `ObjectiveManager`, `MonsterAITestSceneBuilder`, Monster AI —
  untouched; their boundaries are already correct.

### Testing

- New test(s) on `LightDirector` directly: given pools of different sizes
  (small, e.g. 5, and large, e.g. 61), assert the active-zone count after
  each `OnFuseCompleted()` call lands at/near the configured target
  percentage, and `ShutdownAll()` leaves zero non-exempt zones active.
- Regression: existing `MapLightGateTests`, `MainSceneWiringLightTests`
  still pass unmodified in behavior (they don't assert exact shutdown
  counts per fuse, only that shutdown eventually happens and SafeZone zones
  survive).

### Explicitly out of scope

- Debug visualization for LightZone/SafeZone/LightDirector state (P1,
  pre-existing gap, not a regression from this change).
- Any change to fixture density, light range tuning, or per-module
  authored variety (module prefabs currently all light up uniformly at
  Start — TDD's 100%-at-Start option already covers this; per-module
  "some fixtures start dark" flavor is a P2 polish item, not required to
  satisfy any P0 acceptance criterion).
- Monster AI, SafeZone geometry, Fuse system — no changes needed, audit
  confirmed correct.
