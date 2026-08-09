# Main Menu Terminal — Design

## Problem

Gloomveil has no main menu. `GameFlowController`'s `InitializationState` transitions straight to `ExplorationState` on the first tick (`Assets/_Project/Code/Core/States/InitializationState.cs:12`) — there is no title screen, no way to start a run deliberately, no options screen, no quit button. The game currently only "starts" because whatever scene happens to be open in the Editor is playing.

We want a diegetic main menu: a new scene where the camera looks at a physical tablet prop showing a retro green CRT terminal, and the player navigates a menu (NEW GAME / CONTINUE / OPTIONS / QUIT) rendered on that tablet's screen instead of a full-screen UI overlay.

## Reference

Two reference images (user-provided): a "Pong Terminal" boxed menu UI (green monochrome CRT, boxed menu rows, a greyed-out disabled row for a feature not yet available) and an in-engine screenshot of a tablet prop with an in-world terminal rendered on its screen, viewed through the Unity Game view camera. Both establish the visual target: monospace green-on-black text, boxed/highlighted menu rows, scanline texture, an in-world (not screen-space) screen.

## Existing conventions this reuses

- **`FuseTabletPresenter`** (`Assets/_Project/Code/Gameplay/Objectives/FuseTabletPresenter.cs`) already establishes the "camera looks at a tablet's screen" pattern for the fuse minigame: a `screenAnchor` transform aligned to the tablet's screen surface with `+Z` pointing out of the screen, a `CinemachineCamera` blended in via priority, and `IsActive` gating input consumption. The main menu presenter follows the same shape (own `screenAnchor`, own camera, own `Activate`/`Deactivate`-style lifecycle) but is a **new, independent component** — it must not depend on the fuse system, and the fuse system must not depend on it.
- **`Assets/prefabs/tablet.prefab`** is an existing, currently-unused tablet mesh (distinct from `Assets/model/fusetablet.prefab`, which belongs to the fuse minigame). It gets duplicated into a new `MainMenuTablet.prefab` rather than reused in place, so the fuse tablet and the main-menu tablet remain independently editable props.
- **`LightZone`** (`Assets/_Project/Code/Gameplay/World/LightZone.cs`) already implements a flicker routine (TGD §31-32). The main menu's ambient light reuses that same flicker *language* (not the same component instance — this is a different scene) so the menu doesn't visually clash with the game it's the front door to.

## Scope

**New scene:** `Assets/Scenes/MainMenu.unity`. This is the scene that plays when the game boots (or when the Editor's Play button is hit from this scene) — it is not a pause menu and does not coexist in the gameplay scene. Out of scope for this design: hooking `MainMenu.unity` up as the actual first scene in `ProjectSettings` build order (the project's build settings currently only list `SampleScene`, and reworking the whole scene-load list is a separate, pre-existing mess this design doesn't take on) — the deliverable is a working, playable `MainMenu.unity` scene the user can open directly, or wire into build order themselves.

**In scope:**
1. `MainMenuTablet.prefab` — duplicate of `tablet.prefab` plus a World Space `Canvas` child aligned to its screen, a live clock readout, and the menu itself.
2. A `MainMenuPresenter` (or similarly named) component driving menu navigation, selection animation, confirm animation, and scene-load on NEW GAME.
3. A minimal OPTIONS screen: a static control-scheme readout (styled like the Pong Terminal reference's CONTROLS panel) plus one Master Volume slider wired to `AudioListener.volume`.
4. Scene dressing: a static (non-Cinemachine-blended — there's no gameplay camera to blend from, the menu scene has only this one camera) camera framing the tablet, a subtle idle sway, ambient lighting with `LightZone`-style flicker.
5. QUIT wired to `Application.Quit()` (no-op in the Editor, as usual).

**Out of scope:** save/load system (CONTINUE stays disabled), audio mixer / per-channel volume (Master only), rebindable controls (OPTIONS is read-only display), scene transition VFX beyond a plain `SceneManager.LoadScene`.

## Menu structure

```
NEW GAME
CONTINUE   (disabled — "(no save yet)", styled exactly like the reference's greyed-out SCORES row)
OPTIONS
QUIT
```

- **NEW GAME** → `SceneManager.LoadScene("MainGame")` (`Assets/Scenes/MainGame.unity`).
- **CONTINUE** → visually present but non-interactive (no hover/click response), matching the reference's disabled-row treatment. Wired for real once a save system exists — not this design.
- **OPTIONS** → swaps the terminal's content area to the controls/volume screen (same tablet, same Canvas — a second "page" within it, not a new scene).
- **QUIT** → `Application.Quit()`.

Navigation: arrow keys (Up/Down) move the selection, Enter confirms; mouse hover also moves the selection and click also confirms (both input paths drive the same selection state, matching how `FuseTabletPresenter` already reads `Mouse.current` directly rather than through an abstracted input service).

## Visual design

- **Palette:** monochrome green-on-black CRT — `#4fff8a`-range green text/borders on near-black (`#05130a`-range) background, matching both reference images and the brainstorming mockup the user approved.
- **Selected row:** border brightens, subtle background tint, glowing box-shadow, and a `>` cursor glyph slides in from the left (opacity + translateX transition in the mockup; in Unity this is a `RectTransform`/`CanvasGroup` tween, not CSS — see Architecture).
- **Confirm:** the selected row flashes brighter green briefly (a short, punchy pulse — the mockup used ~0.35s) before the scene-level action fires (load scene / open options / quit).
- **Boot-in:** on scene start, a short (~0.3-0.5s) CRT power-on flicker on the tablet's screen, then the menu rows fade/pop in. No scrolling boot-log text — that was explicitly rejected in favor of the simpler boxed-menu style ("A" in the brainstorming comparison).
- **Idle camera sway:** the menu camera is not perfectly static — a very small (1-2°) continuous sway so the shot doesn't feel like a frozen screenshot, without being distracting.
- **Top-right corner readout:** replaces the mockup's placeholder `rev.03` with the device's live clock (system `DateTime.Now`, updating periodically — not every frame, this is flavor text not a stopwatch).
- **Ambient light flicker:** the scene's key light(s) flicker using the same on/off-with-warning-flicker language as `LightZone.Flicker()` (a new, independent flicker behavior local to this scene — not a shared component with gameplay's `LightZone`, since the menu scene has no `LightZone`-relevant gameplay state to gate).

## Architecture

**No hardcoded layout.** Every menu row, the clock readout, the options screen's controls list and volume slider are real `GameObject`s with `RectTransform`s placed in the scene / prefab hierarchy in the Editor — not instantiated from code with computed positions, and not string-built at runtime beyond filling in live values (the clock text, the volume slider's current value). This is a hard requirement from the user: after this is built, they need to be able to open the prefab/scene, select any element in the Hierarchy, and drag it to a new position/size to match the tablet's actual screen dimensions, without touching code.

Consequently, `MainMenuPresenter` **references** its rows/labels via serialized fields (`[SerializeField]`), it does not build them. Its job is behavior, not layout:
- Track selected index (`int`), move it on Up/Down or hover.
- Apply the "selected" visual state to the current row and clear it from the previous one (a component-level style toggle, e.g. enabling/disabling a child "highlight" `Image`/outline object and playing its transition — not per-frame procedural drawing).
- Play the confirm pulse, then invoke the row's action after the pulse completes.
- Own the clock readout's periodic refresh (e.g. every 1s via `InvokeRepeating` or a coroutine, not `Update()`).
- Own OPTIONS page swap (a second content root under the same Canvas, toggled active/inactive — not a scene load) and the volume slider's `onValueChanged` → `AudioListener.volume`.

**Initial sizing is still the implementer's job.** "No hardcoded layout" means the *shipped* result must be scene-authored and freely repositionable afterward — it does not mean the implementer skips fitting the Canvas and its rows to `MainMenuTablet.prefab`'s actual screen mesh dimensions in the first place. The plan must include actually opening/measuring the tablet's screen quad (or its existing `screenAnchor`-equivalent, if `tablet.prefab` already carries one — check before assuming it needs adding) and sizing the World Space Canvas' `RectTransform` (width/height/scale) to match, the same care `FuseTabletPresenter`'s `screenAnchor` already gets. A canvas floating at some arbitrary default size, technically "in the scene" but visibly mismatched to the tablet mesh, does not satisfy this section.

Selection/confirm animation: implemented with Unity's animation primitives already used elsewhere in this codebase (`DOTween` — already a project dependency per `FuseTabletPresenter`'s `using DG.Tweening;` — is the natural fit for the highlight glow/slide and confirm pulse, consistent with how `FuseTabletPresenter.PlayCompletion()` already tweens the fuse lever). Reuse it rather than introducing a second animation approach.

## Testing / Verification

No Unity Editor/MCP session is available in this execution environment (same constraint as the recent lamp-lighting-fix work) — implementation is source/scene-file edits, verified structurally, with a manual in-Editor pass left for the user afterward. Where reasonable, add EditMode tests for pure logic that doesn't need a live Editor scene (e.g. `MainMenuPresenter`'s selection-index wrapping/clamping logic, if it's extracted into a testable method — follow the existing pattern in `Assets/_Project/Code/Tests/PlayMode/FuseTabletPresenterTests.cs` for what is and isn't practically testable for this kind of component).

Manual checklist for the user to run once in the Editor (listed here so the plan can hand it off explicitly, the same way the lamp-fix plan did):
- Open `MainMenu.unity`, enter Play mode: confirm the boot flicker plays, the four rows render legibly against the tablet's screen mesh (not misaligned/floating), Up/Down changes selection with the glow+cursor animation, Enter/click on NEW GAME loads `MainGame.unity`, CONTINUE does not respond to input, OPTIONS swaps to the controls/volume page, the volume slider audibly changes output, QUIT exits Play mode... (in a build, quits the application).
- Confirm the clock readout updates and is legible.
- Confirm the ambient light flicker doesn't strobe uncomfortably fast/bright (this is themed flavor, not a jump-scare beat).

## Out of Scope (explicit)

- Wiring `MainMenu.unity` into `ProjectSettings/EditorBuildSettings.asset`'s scene list.
- A save/load system (blocks CONTINUE from ever being enabled here).
- An audio mixer or per-channel (SFX/music/voice) volume controls — Master only, via `AudioListener.volume`.
- Rebindable controls — OPTIONS is a read-only reference display.
- Any changes to `FuseTabletPresenter`, `Assets/model/fusetablet.prefab`, or the fuse minigame.
