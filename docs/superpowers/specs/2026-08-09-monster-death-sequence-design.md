# Monster Death Sequence Design

## Goal

Make a monster kill unmistakable without delaying implementation: hide gameplay HUD, lock player input, attach the live player camera to a monster-owned kill-camera anchor, play a death sound, then reveal a persistent death panel with Retry and Main Menu actions.

## Chosen Approach

Use a small `DeathSequenceController` driven by `GameFlowController` entering `DeathState`. It receives the currently spawned monster from `MainSceneWiring`, resolves a child transform named `KillCameraAnchor`, and moves the existing gameplay camera there. This avoids adding a second Cinemachine rig or a Timeline dependency.

## Runtime Flow

1. `MonsterBrain.OnKill` makes `MainSceneWiring` enter `DeathState`.
2. `DeathState` disables player control; existing `HudVisibility` fades the HUD out because Death is not a visible state.
3. `DeathSequenceController` parents the player camera to `KillCameraAnchor` and resets its local position and rotation.
4. The controller plays its optional death audio source/clip.
5. After a short configurable presentation delay, it reveals the death UI root.
6. The sequence waits indefinitely for input. Retry reloads the active scene; Main Menu loads `MainMenu`.

The presentation delay uses unscaled time so it remains reliable if gameplay time is paused elsewhere. There is no automatic reset timer.

## Scene Contract

- Monster prefab: optional child named `KillCameraAnchor`. The designer can reposition it inside the monster later.
- Main scene: one `DeathSequenceController`, player camera, optional death UI root, optional audio source/clip, and menu scene name defaulting to `MainMenu`.
- If no death UI is assigned, the controller builds a minimal full-screen panel with `ÖLDÜN`, `RETRY`, and `MAIN MENU` at runtime.
- Missing optional anchor/audio references degrade gracefully: input/HUD/buttons still work, and a missing anchor leaves the camera where it is.
- Repeated kill signals are ignored while a sequence is active.

## Wiring

`MainSceneWiring` owns the composition because it already receives replacement procedural monsters. It binds the death sequence to the current `GameFlowController`, forwards every newly spawned `MonsterBrain`, and unbinds the old one.

## Verification

- PlayMode test: Death state starts exactly one sequence, reveals UI at the configured beat, and exposes the current monster anchor.
- PlayMode test: missing optional anchor/audio does not prevent the panel and buttons from becoming available.
- PlayMode test: Retry targets the active scene and Main Menu targets `MainMenu`; no automatic load occurs.
- Existing monster-kill wiring and HUD visibility tests remain green.
- Unity compilation and console are clean before handoff.
