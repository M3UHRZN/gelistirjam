# Lamp Lighting Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix `Assets/Gloomveil/mapprobs/lamp.prefab` so its light actually illuminates the corridor around it (instead of pointing into the ceiling) and its glass shade shows a warm emissive glow (instead of rendering solid black).

**Architecture:** Two independent, additive edits to the same prefab file, done as hand-authored Unity YAML (no Unity Editor/MCP session is attached in this execution environment, so every step is a plain-text file edit, verified with `grep`/structural checks rather than in-Editor visual inspection):
1. Move the existing `Light` + `UniversalAdditionalLightData` components off the lamp's root Transform onto a new child GameObject, with a local rotation that undoes the parent's `-90°` X rotation and aims the light straight down (world `(0,-1,0)`) — verified by hand quaternion/matrix composition, not left as a "tune it visually" step.
2. Create a new standalone `.mat` asset (Universal Render Pipeline/Lit shader) with a warm base color and warm HDR emission, and repoint the prefab's `MeshRenderer` at it instead of the material embedded in `lamp.fbx`.

**Tech Stack:** Unity 6000.3.13f1, URP. Hand-edited `.prefab` / `.mat` YAML (Unity's native serialized-text format).

## Global Constraints

- Do not modify `lamp.fbx` or its mesh/import settings (spec: "Out of scope — Redesigning the lamp mesh geometry or its authored orientation/export pivot").
- Do not wire this prefab into `GrayboxMapKitFactory`, `MapModuleCatalog`, or any `LightAnchor` spawn point (spec: out of scope — prefab is not currently referenced by any scene or catalog).
- Keep the existing `Light` type (Spot), color, and starting `Intensity`/`Range`/`SpotAngle` values (`2` / `10` / `30`) — spec calls for aiming the fixture correctly, not retuning it.
- No automated EditMode/PlayMode test covers prop lighting in this project; verification here is structural (grep/YAML-reference checks) plus a manual in-Editor visual check the user runs afterward.

---

### Task 1: Decouple the Light from the mesh's rotation

**Files:**
- Modify: `Assets/Gloomveil/mapprobs/lamp.prefab`

**Interfaces:**
- Consumes: existing prefab structure — root GameObject `fileID 4121241004174069912`, root Transform `fileID 7139456081244860460` (local rotation `(-90°, 0, 0)` about X, quaternion `{x: -0.7071068, y: 0, z: 0, w: 0.7071068}`), existing `Light` component `fileID 3182114943804274600`, existing `UniversalAdditionalLightData` MonoBehaviour `fileID 4862320602507775761`.
- Produces: a new child GameObject named `LightSource` (`fileID 8901234567890123456`) with its own Transform (`fileID 8901234567890123457`), parented under the root Transform, carrying the (unchanged, just re-parented) `Light` and `UniversalAdditionalLightData` components. Root GameObject's `m_Component` list no longer includes the Light/MonoBehaviour entries. Task 2 does not depend on this task's internals — it only touches the `MeshRenderer` block, which this task does not change.

- [ ] **Step 1: Confirm current (broken) light direction with a hand-derived check**

This isn't a runnable test — Unity isn't attached in this session — but write down the check so the "before" state is falsifiable, not assumed.

Root quaternion `{x: -0.7071068, y: 0, z: 0, w: 0.7071068}` is a -90° rotation about world X. Converting to a rotation matrix and applying it to the Light's local-forward `(0,0,1)`:

```
R_parent =
[ 1  0  0]
[ 0  0  1]
[ 0 -1  0]

R_parent * (0,0,1)^T = (0, 1, 0)   # world +Y = straight up, into the ceiling
```

This confirms the bug described in the spec: the Light's forward direction currently points straight up.

- [ ] **Step 2: Read the current prefab file**

Run: `cat -n /Users/lee/Desktop/gelistirjam/Assets/Gloomveil/mapprobs/lamp.prefab`

Confirm it matches the structure assumed above (5 components on the root GameObject: Transform, MeshFilter, MeshRenderer, Light, UniversalAdditionalLightData; root Transform has `m_Children: []`).

- [ ] **Step 3: Trim the root GameObject's component list**

In the `GameObject` block for `fileID 4121241004174069912`, change:

```yaml
  m_Component:
  - component: {fileID: 7139456081244860460}
  - component: {fileID: 660039278823533207}
  - component: {fileID: 6938868686698383163}
  - component: {fileID: 3182114943804274600}
  - component: {fileID: 4862320602507775761}
```

to:

```yaml
  m_Component:
  - component: {fileID: 7139456081244860460}
  - component: {fileID: 660039278823533207}
  - component: {fileID: 6938868686698383163}
```

(This drops the Light and UniversalAdditionalLightData entries — they move to the new child GameObject in Step 5.)

- [ ] **Step 4: Point the root Transform's `m_Children` at the new child**

In the `Transform` block for `fileID 7139456081244860460`, change:

```yaml
  m_Children: []
```

to:

```yaml
  m_Children:
  - {fileID: 8901234567890123457}
```

- [ ] **Step 5: Re-parent the existing Light and UniversalAdditionalLightData components**

In the `Light` block (`--- !u!108 &3182114943804274600`), change:

```yaml
  m_GameObject: {fileID: 4121241004174069912}
```

to:

```yaml
  m_GameObject: {fileID: 8901234567890123456}
```

In the `MonoBehaviour` block (`--- !u!114 &4862320602507775761`, the `UniversalAdditionalLightData`), change:

```yaml
  m_GameObject: {fileID: 4121241004174069912}
```

to:

```yaml
  m_GameObject: {fileID: 8901234567890123456}
```

Leave every other field in both blocks untouched — the Light's type, color, intensity, range, spot angle, etc. are not changing, only which GameObject owns them.

- [ ] **Step 6: Append the new child GameObject + Transform to the end of the file**

Add this as new YAML documents at the end of `lamp.prefab`:

```yaml
--- !u!1 &8901234567890123456
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 8901234567890123457}
  - component: {fileID: 3182114943804274600}
  - component: {fileID: 4862320602507775761}
  m_Layer: 0
  m_Name: LightSource
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &8901234567890123457
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 8901234567890123456}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 1, z: 0, w: 0}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 7139456081244860460}
  m_LocalEulerAnglesHint: {x: 0, y: 180, z: 0}
```

The child's local rotation `{x: 0, y: 1, z: 0, w: 0}` is a 180° rotation about Y (i.e. Euler `(0, 180, 0)`). Combined with the parent's existing -90° X rotation, the composed matrix is:

```
R_parent * R_child =
[-1  0  0]
[ 0  0 -1]
[ 0 -1  0]

(R_parent * R_child) * (0,0,1)^T = (0, -1, 0)   # world -Y = straight down
```

This is the corrected direction — confirms the Light's forward now points straight down at the floor instead of up into the ceiling.

- [ ] **Step 7: Verify file structure is well-formed**

Run:
```bash
python3 - <<'EOF'
import re
path = "/Users/lee/Desktop/gelistirjam/Assets/Gloomveil/mapprobs/lamp.prefab"
text = open(path).read()
ids = re.findall(r'^--- !u!\d+ &(\d+)', text, re.MULTILINE)
assert len(ids) == len(set(ids)), f"duplicate fileIDs: {ids}"
assert "8901234567890123456" in ids and "8901234567890123457" in ids
assert text.count("m_GameObject: {fileID: 8901234567890123456}") == 2, "Light + UniversalAdditionalLightData should both point at the new child"
assert "m_Father: {fileID: 7139456081244860460}" in text
print("OK:", len(ids), "unique blocks")
EOF
```
Expected: `OK: 8 unique blocks` (original 6 minus none removed, plus 2 new = 8) and no assertion errors.

- [ ] **Step 8: Commit**

```bash
cd /Users/lee/Desktop/gelistirjam
git add Assets/Gloomveil/mapprobs/lamp.prefab
git commit -m "Fix lamp light aiming into ceiling instead of down at the floor

Moves the Light + UniversalAdditionalLightData components onto a new
child GameObject so the light's aim is independent of the mesh's
-90 degree X rotation (needed for the mesh to hang correctly). The
child's 180 degree Y rotation, composed with the parent's rotation,
now points the light straight down instead of straight up.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 2: Give the glass shade a warm emissive material

**Files:**
- Create: `Assets/Gloomveil/mapprobs/Materials/LampGlass.mat`
- Create: `Assets/Gloomveil/mapprobs/Materials/LampGlass.mat.meta`
- Modify: `Assets/Gloomveil/mapprobs/lamp.prefab`

**Interfaces:**
- Consumes: nothing from Task 1 — this task only touches the `MeshRenderer` block (`fileID 6938868686698383163`), which Task 1 never edits.
- Produces: a standalone material asset with guid `a1b2c3d4e5f60718293a4b5c6d7e8f90`, referenced by the prefab's `MeshRenderer.m_Materials[0]`. No later task depends on this.

- [ ] **Step 1: Create the Materials folder and the material asset**

Run: `mkdir -p /Users/lee/Desktop/gelistirjam/Assets/Gloomveil/mapprobs/Materials`

Write `Assets/Gloomveil/mapprobs/Materials/LampGlass.mat`:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: LampGlass
  m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords:
  - _EMISSION
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_BuildTextureStacks: []
  m_AllowLocking: 1
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _BumpMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _EmissionMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _MetallicGlossMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _OcclusionMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Ints: []
    m_Floats:
    - _Smoothness: 0.2
    - _Metallic: 0
    - _Surface: 0
    m_Colors:
    - _BaseColor: {r: 0.85, g: 0.72, b: 0.5, a: 1}
    - _EmissionColor: {r: 3.4, g: 2.3, b: 1.1, a: 1}
```

Write `Assets/Gloomveil/mapprobs/Materials/LampGlass.mat.meta`:

```yaml
fileFormatVersion: 2
guid: a1b2c3d4e5f60718293a4b5c6d7e8f90
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 2100000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

`_BaseColor` is a warm cream (`0.85, 0.72, 0.5`) matching the reference screenshot's glass tint. `_EmissionColor` uses HDR values above 1.0 (`3.4, 2.3, 1.1`, same warm hue, boosted) so the shade visibly glows regardless of scene lighting, and reads bright enough for URP's default bloom-adjacent auto-exposure. `_Surface: 0` = Opaque (the reference photo shows solid lit panels, not see-through glass).

- [ ] **Step 2: Repoint the prefab's MeshRenderer at the new material**

In `Assets/Gloomveil/mapprobs/lamp.prefab`, in the `MeshRenderer` block (`fileID 6938868686698383163`), change:

```yaml
  m_Materials:
  - {fileID: -876546973899608171, guid: a9f567059c98ba9499881e0a71908712, type: 3}
```

to:

```yaml
  m_Materials:
  - {fileID: 2100000, guid: a1b2c3d4e5f60718293a4b5c6d7e8f90, type: 2}
```

(`type: 2` is a standalone `.mat` asset reference, versus `type: 3` for the FBX-embedded material it replaces.)

- [ ] **Step 3: Verify the material and reference are well-formed**

Run:
```bash
python3 - <<'EOF'
mat = open("/Users/lee/Desktop/gelistirjam/Assets/Gloomveil/mapprobs/Materials/LampGlass.mat").read()
assert "_EMISSION" in mat
assert "_EmissionColor: {r: 3.4, g: 2.3, b: 1.1, a: 1}" in mat
assert "guid: 933532a4fcc9baf4fa0491de14d08ed7" in mat  # URP Lit shader

meta = open("/Users/lee/Desktop/gelistirjam/Assets/Gloomveil/mapprobs/Materials/LampGlass.mat.meta").read()
assert "guid: a1b2c3d4e5f60718293a4b5c6d7e8f90" in meta

prefab = open("/Users/lee/Desktop/gelistirjam/Assets/Gloomveil/mapprobs/lamp.prefab").read()
assert "fileID: 2100000, guid: a1b2c3d4e5f60718293a4b5c6d7e8f90, type: 2" in prefab
assert "guid: a9f567059c98ba9499881e0a71908712, type: 3" not in prefab  # old FBX material reference is gone
print("OK")
EOF
```
Expected: `OK`, no assertion errors.

- [ ] **Step 4: Commit**

```bash
cd /Users/lee/Desktop/gelistirjam
git add Assets/Gloomveil/mapprobs/Materials/LampGlass.mat Assets/Gloomveil/mapprobs/Materials/LampGlass.mat.meta Assets/Gloomveil/mapprobs/lamp.prefab
git commit -m "Give the lamp's glass shade a warm emissive material

Replaces the FBX-embedded material (no emission, renders black
whenever no external light reaches it) with a standalone URP/Lit
material: warm cream base color plus warm HDR emission, so the
shade glows on its own like the reference screenshot instead of
depending on external light direction.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 3: Manual in-Editor verification

**Files:** none (verification only — no code changes)

**Interfaces:**
- Consumes: the finished prefab from Tasks 1-2.
- Produces: nothing consumed by later tasks; this is the last task.

- [ ] **Step 1: Open the prefab in Unity and check the Light's aim**

In the Unity Editor, open `Assets/Gloomveil/mapprobs/lamp.prefab` in Prefab Mode. Select the new `LightSource` child object. In the Scene view, confirm the Spot Light's cone gizmo points down out of the shade's open bottom, not up into the mesh or sideways.

- [ ] **Step 2: Check the shade's glow in the Editor's Scene/Game view**

Still in Prefab Mode (or with the prefab instanced in any scene), confirm the shade's glass panels show the warm cream/amber glow from `LampGlass.mat`'s emission, instead of appearing as a flat black silhouette.

- [ ] **Step 3: Check in Play mode with the fixture in a real corridor**

Temporarily drag an instance of `lamp.prefab` into a corridor module in any existing test scene (e.g. `Assets/_Project/Scenes/Levels/MonsterAI_TestScene.unity`), enter Play mode, and confirm the floor and nearby walls are visibly lit under the fixture. Remove the temporary instance afterward (do not commit a scene change unless the user asks for the fixture to be wired into a scene — that's out of scope per the spec).

- [ ] **Step 4: Check for any existing `LightZone` referencing this prefab**

Run: `grep -rl "1a71cd7f44cbd014bb5a86643288a841" /Users/lee/Desktop/gelistirjam/Assets --include="*.unity" --include="*.prefab"`

(`1a71cd7f44cbd014bb5a86643288a841` is `lamp.prefab`'s own guid — this checks whether any scene/prefab references *this* prefab, which would mean a `LightZone.lights` array might already point at the old root-level `Light` component and need re-pointing at the new `LightSource` child.)

Expected: no results (confirmed during brainstorming that this prefab isn't currently referenced anywhere) — if this now returns a match, tell the user before doing anything else, since it means some other change added a reference to this prefab since the spec was written.

- [ ] **Step 5: Report results to the user**

Summarize what was checked in Steps 1-4 and whether each looked correct, including any visual issues found in the Editor that require a follow-up tweak (e.g. emission looks too dim/bright, spot cone angle too narrow for the corridor width). Do not close out the task silently if something in the Editor looks wrong — flag it back to the user.
