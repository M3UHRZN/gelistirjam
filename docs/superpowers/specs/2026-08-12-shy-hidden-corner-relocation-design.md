# Shy Hidden-Corner Relocation Design

**Date:** 2026-08-12

**Status:** Implemented; corrected after live-play regression feedback

## Context

`ShyState` currently asks `MonsterNavigator.SetFleeDestinationAwayFrom` for a random reachable NavMesh point. The navigator validates NavMesh reachability and SafeZone exclusion, but it does not consider the player's camera frustum or environmental occlusion. After reaching the destination, Shy stops there. A later confirmed gaze can reset the Shy timer and run the existing yelling/staredown flow, but the next random flee point may still be visible to the player.

The generated map is a narrow collection of corridors and rooms with registered `StalkPoint` corner anchors. Shy should use those authored/generated anchors instead of inventing arbitrary NavMesh destinations.

## Goal

When the player confirms gaze on a monster that is already in Shy, immediately invalidate its exposed corner and send it to a different reachable `StalkPoint` corner whose entire representative body volume is outside the player's actual unobstructed view.

## Non-Goals

- Do not change the behavior, transitions, scoring, or tuning of Patrol, Interested, Reposition, Flank, Ambush, CutOff, Observed, FakeRetreat, Aggressive, Commit, Kill, or SafeZoneCamp.
- Do not replace the existing eye-gaze confirmation, yelling, staredown, anger, backward-facing retreat, FakeRetreat, or Shy lifetime behavior.
- Do not make flashlight state influence Shy visibility.
- Do not add random NavMesh sampling as a fallback.
- Do not require scene-authored corner objects beyond the existing `StalkPoint` registry.
- Do not alter map-generation ownership; generated neutral anchors continue to be converted and registered by `MapMonsterGate`.

## Behavior Contract

1. On Shy entry, the monster selects a hidden, reachable `StalkPoint` marked as a corner and retreats toward it while retaining the existing backward-facing movement and yelling reaction.
2. On arrival, the monster stops at that point and retains the existing Shy timer behavior.
3. A false-to-true confirmed-gaze edge while in Shy marks the current target corner as exposed and immediately selects a different hidden corner. The state does not recompute a destination every frame after a successful selection.
4. While gaze remains confirmed, the current timer reset, yelling/staredown, anger accumulation, and possible aggression behavior remain unchanged.
5. If a staredown activates, it may still stop the retreat and run the existing aggression roll when gaze breaks. If Shy continues, it resumes the already selected hidden destination rather than choosing again.
6. If the immediate selection fails, Shy stops and retries while gaze is held and after it breaks, using the bounded retry cadence.
7. If the monster is confirmed-gazed while travelling, its current destination is likewise marked exposed and replaced immediately.
8. Only the most recently exposed/current target is excluded. An older corner may become eligible again after the player and camera have moved, but Shy may never immediately reselect the corner that triggered the relocation.
9. If no eligible corner exists at a selection attempt, the monster stops and retries at the existing `RepositionInterval` cadence. It never accepts a visible, unreachable, non-corner, or SafeZone destination as a fallback.

## Visibility Contract

Relocation continues to be triggered by the existing confirmed eye gaze. Broad body visibility is used only to validate candidate destinations; a peripheral glimpse does not independently trigger yelling or relocation.

Candidate visibility uses the real player `Camera`, including horizontal FOV, vertical FOV, camera pitch, range, and near/behind-camera rejection. It must not rely only on the flattened player-root forward vector used by the existing stalk scoring API.

For each hypothetical monster destination, visibility is sampled at five representative points derived from the monster NavMesh capsule:

- lower body on the capsule centerline,
- torso on the capsule centerline,
- head on the capsule centerline,
- left torso edge,
- right torso edge.

Each sample is evaluated as follows:

1. Reject samples behind the camera, outside the camera viewport, or beyond the configured view range.
2. For an in-frustum sample, cast from the camera position to the sample with the existing environmental occluder mask and `QueryTriggerInteraction.Ignore`.
3. A wall, ceiling, floor, closed door, or other non-trigger opaque collider before the sample blocks that sample.
4. If at least one body sample is both inside the camera view and unobstructed, the candidate is visible and therefore ineligible.
5. A candidate is hidden only when every representative sample is outside the active camera view or blocked by geometry.

This allows a point inside the geometric FOV but fully behind a wall to remain valid, while rejecting a doorway/corner where even part of the body would be visible.

## Architecture

### `ShyState`

`ShyState` remains the sole owner of Shy lifecycle decisions. It gains only the state needed to track the selected corner, the most recently exposed corner, whether relocation is pending, and the retry countdown. Existing staredown and aggression ordering stays intact.

Destination selection is called on entry, on the confirmed-gaze rising edge, and on a bounded retry cadence after a failed selection. It is not called every frame after success.

### `ShyEscapePointSelector`

Add a Shy-specific selector under `Gameplay/Monster`. It consumes the registered `StalkPointRuntimeSet`, player position, monster position, `PlayerVisionModel`, `MonsterNavigator`, and the excluded current target.

Hard eligibility filters run before ranking:

- point exists and differs from the excluded target,
- `IsNearCorner` is true,
- the point is not currently body-visible,
- the point is reachable through a complete NavMesh path,
- the point is outside every SafeZone through the navigator's existing validation.

Among eligible points, prefer greater separation from the player, then higher darkness, then lower travel distance from the monster. Use stable registry order as the final tie-break so selection does not oscillate randomly.

This selector is intentionally separate from `StalkPointScorer`. The general scorer allows anger-driven visibility risk and prefers different tactical distances; changing it would alter Reposition and other states, violating the scope boundary.

### `PlayerVisionModel`

Preserve the signatures and semantics of `IsPotentiallyVisible` and `IsActuallyVisible`, because `StalkPointScorer` and debug visualization already depend on them.

Add an additive Shy-facing API that evaluates a hypothetical monster capsule against the real camera and multiple body samples. Extend binding so `MonsterRuntimeRig`, which already resolves the camera from `PlayerGazeDetector`, supplies that camera without changing other state consumers.

### `MonsterPerception`

Preserve `IsPlayerVisibleFrom` for existing stalk scoring. Add a narrowly named clear-line-of-sight query that reuses the serialized occluder mask and ignores triggers. `PlayerVisionModel` uses it for camera-to-body-sample checks, keeping physics-mask ownership in one component.

### `MonsterNavigator`

Preserve all current navigation behavior. Expose read-only NavMesh capsule height/radius for visibility sampling, resolve each candidate once through `TryResolveReachableDestination`, evaluate visibility at that exact resolved point, and commit the same point through `TrySetResolvedDestination` for complete-path and SafeZone enforcement.

### Runtime Wiring

`MonsterRuntimeRig.Initialize` already requires the gaze camera, vision model, perception, navigator, and StalkPoint set. Its existing atomic initialization boundary supplies the camera to the additive visibility API. `MapMonsterGate` and the generated-map-to-gameplay ownership direction remain unchanged.

## Data Flow

1. `PlayerGazeDetector` confirms eye gaze through the existing hold duration.
2. `MonsterBrain` exposes the confirmed gaze to `ShyState` and continues to own anger mutation.
3. `ShyState` marks its current destination exposed on the confirmed-gaze edge.
4. On that same confirmed-gaze edge, `ShyState` requests a replacement from `ShyEscapePointSelector`.
5. The selector filters registered corner points through camera/body visibility and navigator reachability.
6. `ShyState` commits the selected point through validated navigation and immediately resumes the existing player-facing retreat.
7. On failure, Shy stops and retries later without changing state or accepting an unsafe fallback; gaze release never causes a redundant second relocation after success.

## Failure Handling

- Missing camera, vision model, navigator, or StalkPoint registry is a runtime-wiring failure. Selection returns no target, Shy stops safely, and a single diagnostic decision reason is recorded; it does not continue toward a stale destination.
- A point that becomes unreachable between scoring and destination assignment is treated as a failed attempt. The state stops and retries rather than retaining the prior exposed destination.
- An empty or temporarily invalid registry produces the same safe stopped/retry behavior.
- Physics triggers never hide the monster. Only colliders included by the existing occluder mask may block visibility.
- No failure path mutates other monster states or the global anger/gaze contracts.

## Test Strategy

Add focused PlayMode coverage for the Unity camera, physics, NavMesh, and state-transition behavior:

1. An unobstructed in-frustum corner is rejected.
2. A corner fully occluded by a wall is eligible.
3. Floor/ceiling geometry hides a same-XZ corner on another storey.
4. A doorway that exposes only one body sample still causes rejection.
5. A point outside horizontal or vertical camera view remains eligible.
6. A confirmed gaze at a stopped hiding corner immediately selects a different hidden corner and resumes movement while gaze remains held.
7. A confirmed gaze during travel immediately invalidates and replaces the in-flight target without per-frame destination churn after success.
8. A SafeZone corner and a corner without a complete NavMesh path are rejected.
9. With no valid hidden corner, the navigator stops and retries without selecting a visible fallback or stale destination.
10. Existing yelling, backward facing, Shy timer reset, staredown anger, aggression roll, FakeRetreat, and Shy exit behavior remain intact.

Run the new focused fixture first, then the existing `Game.Gameplay.PlayModeTests` suite. Finally, perform a manual generated-map Play Mode pass in `MainGame`: expose Shy at one corner, hold gaze, confirm a different fully hidden corner is chosen immediately and movement resumes, then repeat with a doorway, a wall, and a different-floor candidate. Runtime success is claimed only after those checks actually run.

## Acceptance Criteria

- Shy chooses only registered `IsNearCorner` StalkPoints.
- The immediately exposed/current corner is never reselected for the next retreat.
- Any candidate with an unobstructed visible body sample is rejected.
- Walls, ceilings, floors, and other configured opaque colliders correctly occlude samples.
- Every committed target has a complete NavMesh path and lies outside SafeZones.
- Yelling, backward retreat, gaze confirmation, staredown, anger, aggression, FakeRetreat, and Shy duration retain their existing behavior.
- No other monster state or the shared tactical `StalkPointScorer` changes behavior.
- No random or visible fallback destination is introduced.
