# Final Stalker Hunt — Design

## Goal

Completing every fuse must not force the monster into a permanent direct chase. The final phase keeps exact player awareness, intensifies stalk-point selection, and preserves the existing gaze retreat, critical-distance commit, contact kill, and SafeZone rules.

## Behaviour Contract

- `FinalChaseState` starts a final-hunt policy on the current or subsequently spawned monster.
- Final hunt enters the existing stalking FSM through `RepositionState`; it never intentionally enters `AggressiveState`.
- Distance-based "lost player" transitions are disabled during final hunt, so the monster continues Reposition, Flank, Ambush, CutOff, Interested, and SafeZoneCamp behaviour using the live player transform.
- A confirmed gaze still routes tactical states through `ObservedState`, then always into `ShyState` during final hunt. Gaze duration and anger cannot turn that retreat into `AggressiveState`.
- After retreat/camping completes, final hunt resumes through `RepositionState` instead of `PatrolState`.
- `CriticalDistance`, `CommitState`, contact kill, and SafeZone immunity remain unchanged. Escaping a committed rush resumes stalking rather than a direct chase.

## Architecture

`MonsterBrain` owns one runtime policy flag, `IsFinalHunt`, and exposes `BeginFinalHunt()` as the composition-root boundary. Existing class-per-state FSM classes remain responsible for movement and transitions. `MainSceneWiring` only forwards `FinalChaseState` to `BeginFinalHunt()`; no new manager or parallel AI system is introduced.

## Testing

PlayMode wiring tests cover current and late-bound monsters, final-hunt refusal of `AggressiveState`, persistent awareness outside normal detection radii, and gaze-to-retreat at maximum anger. The generated-map integration test confirms a monster spawned after final chase starts in a stalking state rather than a direct chase.
