# Monster Death Sequence Design

## Goal

Make a monster kill unmistakable without delaying implementation: hide gameplay HUD, lock player input, attach the live player camera to a monster-owned kill-camera anchor, play a death sound, reveal a death panel, then reload the active scene.

## Chosen Approach

Use a small `DeathSequenceController` driven by `GameFlowController` entering `DeathState`. It receives the currently spawned monster from `MainSceneWiring`, resolves a child transform named `KillCameraAnchor`, and moves the existing gameplay camera there. This avoids adding a second Cinemachine rig or a Timeline dependency.

## Runtime Flow

1. `MonsterBrain.OnKill` makes `MainSceneWiring` enter `DeathState`.
2. `DeathState` disables player control; existing `HudVisibility` fades the HUD out because Death is not a visible state.
3. `DeathSequenceController` parents the player camera to `KillCameraAnchor` and resets its local position and rotation.
4. The controller plays its optional death audio source/clip.
5. After a short configurable delay, it reveals an optional death UI root.
6. After another configurable delay, it reloads the active scene.

The sequence uses unscaled time so it remains reliable if gameplay time is paused elsewhere.

## Scene Contract

- Monster prefab: optional child named `KillCameraAnchor`. The designer can reposition it inside the monster later.
- Main scene: one `DeathSequenceController`, player camera, optional death UI root, and optional audio source/clip.
- Missing presentation references degrade gracefully: input/HUD/reset still work, and missing anchor leaves the camera where it is.
- Repeated kill signals are ignored while a sequence is active.

## Wiring

`MainSceneWiring` owns the composition because it already receives replacement procedural monsters. It binds the death sequence to the current `GameFlowController`, forwards every newly spawned `MonsterBrain`, and unbinds the old one.

## Verification

- PlayMode test: Death state starts exactly one sequence, hides UI at the configured beat, and exposes the current monster anchor.
- PlayMode test: missing optional anchor/audio/UI does not prevent reset scheduling.
- Existing monster-kill wiring and HUD visibility tests remain green.
- Unity compilation and console are clean before handoff.
