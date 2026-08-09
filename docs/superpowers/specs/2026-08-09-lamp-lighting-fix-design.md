# Lamp Lighting Fix — Design

## Problem

`Assets/Gloomveil/mapprobs/lamp.prefab` (the hanging corridor lamp fixture) renders wrong in-game:

- The glass shade panels appear as a solid black silhouette instead of the warm/cream glow they show in the asset's normal (unlit-preview) appearance.
- The fixture casts no visible light on the floor or walls around it — it looks like the light is trapped inside the shade.

## Root Cause

Both problems trace back to the same prefab, and are independent bugs that compound:

1. **Light aims the wrong way.** The prefab is a single GameObject that carries the mesh, the `MeshRenderer`, and the `Light` component all on one Transform. That Transform's local rotation is `(-90°, 0, 0)`, applied so the mesh (authored/exported with a different default orientation) hangs correctly. A Unity `Light` always shines along its local `+Z`. Because the light shares the mesh's Transform, that same `-90°` rotation redirects the light's `+Z` into world `+Y` — straight up into the ceiling — instead of down/outward into the corridor. Nothing under or beside the fixture receives any illumination from it.
2. **The glass material has no emission.** The shade's material (embedded in `lamp.fbx`, referenced by the prefab's `MeshRenderer`) relies entirely on external light hitting it to show any color — it has no self-illumination. Since the fixture's own light doesn't reach the shade's exterior (see #1) and no other scene light reaches this corridor spot, the panels render as flat black. The reference "normal" appearance (warm cream glow reference the user provided) is achieved by an emissive glass material lit from within by its own bulb, independent of external light — this behavior is currently entirely absent.

## Fix

### 1. Decouple the light's aim from the mesh's rotation

Add a child GameObject (e.g. `LightSource`) under the existing lamp root, positioned at local `(0,0,0)` (co-located with the parent pivot — do not offset position, since the parent's local scale is `100` and would amplify any child-local position offset unpredictably). Move the existing `Light` and `UniversalAdditionalLightData` components from the root onto this child.

Rotate the child (independently of the parent) until the Spot Light's cone gizmo in the Scene view visibly aims down through the shade's open bottom, illuminating the floor and nearby walls. Because Unity's rotation composition here is not hand-derivable safely from the YAML alone (left-handed axis conventions, parent's existing `-90°` X rotation), this is tuned visually in the Editor and confirmed by eye — not asserted as a specific pre-computed value in this spec.

Keep the light's existing type (Spot), color, and roughly its current intensity/range (`Intensity 2`, `Range 10`, `SpotAngle 30`) as a starting point; nudge these only if the resulting pool of light on the floor looks too dim or too tight once aimed correctly. This keeps the fixture compatible with `LightZone` (which toggles/flickers grouped `Light[]` references, TGD §31-32) — the `Light` component reference just needs to be re-pointed at the new child if any `LightZone` in a scene already references this fixture.

### 2. Give the shade material a warm emissive glow

Extract the lamp's material out of the embedded FBX asset into a standalone `.mat` file (via the Model Importer's "Extract Materials" action, saved into a `Materials/` folder alongside the prefab) so it survives FBX re-imports and is independently editable. On the extracted material:

- Enable Emission.
- Set an emission color in the warm cream/amber range matching the reference screenshot (approx. `#F5DDA6`-ish warm white, not pure white).
- Reuse the material's existing base/albedo map as the emission map if it already carries the panel pattern, so the glow follows the glass paneling rather than lighting the whole shade uniformly.

This makes the shade read as "lit from within" regardless of whether external scene light reaches it — matching the reference image and removing the dependency on fix #1 being perfectly aimed for the shade itself to look correct. Fix #1 is still required so the fixture actually contributes light to the corridor around it (its actual gameplay-visible purpose, not just its own glow).

## Testing / Verification

- Open the lamp prefab in isolation and confirm in the Scene view that the Spot Light cone points down/outward through the shade opening, not into the mesh or upward.
- Enter Play mode in a scene containing (or with the fixture temporarily dragged into) a corridor module; confirm the floor/walls below and beside the fixture are visibly lit, and the shade panels show a warm glow instead of solid black, matching the reference screenshot.
- Check any existing `LightZone` component in project scenes that may already reference this prefab's `Light` — if the component moved to a new child GameObject, that reference needs to be re-pointed instead of breaking silently (a stale `null` entry in `LightZone.lights`).
- No EditMode/PlayMode automated tests cover prop lighting; verification here is manual/visual, in-Editor.

## Out of Scope

- Redesigning the lamp mesh geometry or its authored orientation/export pivot.
- Wiring this prefab into `GrayboxMapKitFactory`'s `LightAnchor` spawn points or the `MapModuleCatalog`. Note: this prefab IS already nested (via `PrefabInstance`) inside 10 map module prefabs under `Assets/Gloomveil/MapGeneration/Prefabs/TexturedBoxs/` (e.g. `PF_TCOR_Straight_1x3_01.prefab`, `PF_TROOM_2x2_01.prefab`), and those module prefabs are registered in `Assets/Gloomveil/MapGeneration/Data/GrayboxMapModuleCatalog.asset`. So this lamp is the actual light fixture used under every `LightAnchor` in every generated map, and a base-prefab fix here has game-wide blast radius (every lit corridor/room), not just one orphaned asset — this section is only about not additionally touching the catalog/anchor wiring itself.
- Retuning other lights/LightZones in the project.
