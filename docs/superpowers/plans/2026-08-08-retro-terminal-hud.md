# Retro Terminal HUD Art Pass Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle the existing four-corner HUD as a readable “Arızalı Tesis Terminali” while preserving all gameplay bindings, keeping the crosshair pixel-locked, and keeping stamina as a continuously filling/depleting bar beneath a 12-cell terminal overlay.

**Architecture:** The existing `Game.UI` behavior layer remains a read-only view over `Game.Gameplay`. Text fields migrate to TextMeshPro, one reusable `TerminalPanelGraphic` draws each cut-corner panel without sprites or shaders, and an editor-only `HudTerminalFactory` owns font-asset creation plus authored UI primitives. `HudSceneBuilder` remains the only scene-construction entry point and validates the terminal font before replacing the existing canvas.

**Tech Stack:** Unity 6000.3.13f1, URP 17.3.0, uGUI 2.0.0, TextMeshPro from `com.unity.ugui`, `MaskableGraphic`, Unity assembly definitions, Unity Editor menu tooling.

**Spec:** `docs/superpowers/specs/2026-08-08-retro-terminal-hud-design.md`

## Global Constraints

- **Do not write or run PlayMode or EditMode tests.** The project owner explicitly reserves runtime testing for manual Play Mode. Each task uses Unity compilation, hierarchy inspection, serialized-reference inspection, and visual Game View evidence instead.
- Scope is the main HUD only. Do not change the fuse minigame, menus, gameplay rules, inputs, stamina math, or flashlight mechanics.
- Stamina remains a real continuous `Image.Type.Filled` bar. Its `Image` must have a persistent built-in sprite so uGUI actually executes `GenerateFilledSprite`. Set `fill.fillAmount = Clamp01(CurrentStamina / MaxStamina)` every refresh; the 11 divider Images are a non-interactive overlay only and align to the fill's inset bounds.
- Palette is exactly: base `#06100C`, phosphor `#79FF9B`, danger `#FF5A36`. Muted states use alpha variants, not additional colors.
- All HUD text is `TextMeshProUGUI` using the repo-owned Cascadia Mono font asset. Do not use `UnityEngine.UI.Text`, Arial, or LiberationSans for the new HUD.
- `Game.UI` may reference `Unity.TextMeshPro`; `Game.Gameplay` must never reference `Game.UI`.
- Crosshair remains under `FixedRoot`, never receives `HudSway`, and never changes its root anchored position. Only tick spread/alpha and part color may change.
- Existing `HudCameraMotion`, `HudSpring`, `HudRootSway`, `HudSway`, `CrosshairPulse`, and `HudVisibility` math/behavior stays unchanged.
- `HudCanvas` gets no `GraphicRaycaster`. Every HUD `Graphic.raycastTarget` is `false`; every HUD `CanvasGroup.blocksRaycasts` is `false`.
- `TerminalPanelGraphic` uses stock uGUI geometry and `CanvasRenderer.SetAlpha`; do not add a custom shader, post-process, texture, DOTween, or third-party UI package.
- Panel flicker affects only the panel graphic, never the text or crosshair. Its serialized amplitude is at most `0.025`.
- Missing gameplay sources log once, disable their widget behavior, and hide that widget instead of leaving an empty panel. The base crosshair remains visible if only target highlighting lacks `PlayerInteraction`.
- All objective event subscriptions remain matched `OnEnable`/`OnDisable` pairs.
- Builder validation is atomic with respect to the current canvas: load or create the Cascadia TMP asset first, self-clean any partially constructed candidate, and wrap candidate registration plus old-root destruction in one reverted-on-failure Undo group.
- The existing untracked `Assets/TextMesh Pro/` tree is Unity’s standard TMP Essential Resources import. Task 1 adopts it without modifying its generated contents; do not stage any other unrelated file.
- Active implementation branch is `hud`. Active scene is `Assets/Scenes/MainGame.unity` in Unity 6000.3.13f1.
- After each C# task: refresh/compile Unity and read a fresh console snapshot. “Clean” means zero `Error` entries.

---

## File Map

| File | Change | Responsibility |
|---|---|---|
| `Assets/TextMesh Pro/**` | Add existing generated assets | Standard TMP settings, shaders, fallback resources, and their metas. |
| `Assets/_Project/Art/UI/Fonts/CascadiaMono.ttf` | Add binary asset | Repo-owned monospace source font copied from the verified Windows installation. |
| `Assets/_Project/Art/UI/Fonts/CascadiaMono-OFL.txt` | Add text asset | Official Microsoft Cascadia Code SIL Open Font License. |
| `Assets/_Project/Art/UI/Fonts/CascadiaMono SDF.asset` | Generate in Unity | Deterministic static 1024×1024 SDFAA asset with a subtle outline material. |
| `Assets/_Project/Code/UI/Game.UI.asmdef` | Modify | Add `Unity.TextMeshPro` while preserving one-way UI → gameplay dependency. |
| `Assets/_Project/Code/Editor/Game.Editor.asmdef` | Modify | Let editor HUD tooling use TMP APIs. |
| `Assets/_Project/Code/UI/Hud/TerminalPanelGraphic.cs` | Create | Draw cut-corner panel fill, border, registration marks, scanlines, and bounded flicker. |
| `Assets/_Project/Code/UI/Hud/ObjectiveWidget.cs` | Modify | Drive a TMP objective value and hide the complete panel when no objective data exists. |
| `Assets/_Project/Code/UI/Hud/InteractionPromptWidget.cs` | Modify | Drive a TMP prompt using `> {0}` without altering source copy. |
| `Assets/_Project/Code/UI/Hud/FlashlightWidget.cs` | Modify | Drive explicit `AÇIK`/`KAPALI` TMP state and phosphor alpha. |
| `Assets/_Project/Code/UI/Hud/StaminaWidget.cs` | Modify | Apply terminal palette and hide invalid widget while retaining continuous fill math. |
| `Assets/_Project/Code/UI/Hud/CrosshairWidget.cs` | Modify | Apply phosphor idle/target alpha states. |
| `Assets/_Project/Code/Editor/HudTerminalFactory.cs` | Create | Own palette, font-asset generation, UI primitives, and serialized assignment helpers. |
| `Assets/_Project/Code/Editor/HudSceneBuilder.cs` | Modify | Build and wire the complete styled HUD hierarchy. |
| `Assets/Scenes/MainGame.unity` | Modify through Unity | Persist the rebuilt retro-terminal HUD canvas. |

---

### Task 1: Adopt TMP essentials, Cascadia Mono, and assembly references

**Files:**
- Add: `Assets/TextMesh Pro.meta`
- Add: `Assets/TextMesh Pro/**`
- Create: `Assets/_Project/Art/UI/Fonts/CascadiaMono.ttf`
- Create: `Assets/_Project/Art/UI/Fonts/CascadiaMono-OFL.txt`
- Modify: `Assets/_Project/Code/UI/Game.UI.asmdef`
- Modify: `Assets/_Project/Code/Editor/Game.Editor.asmdef`

**Interfaces:**
- Consumes: Unity’s existing `com.unity.ugui@2.0.0` package and the verified local font `C:\Windows\Fonts\CascadiaMono.ttf` (SHA-256 `0E141CB99609F6F10AD05313FD1807D5CC9E28658DCBB35AB162E52FF67DC718`).
- Produces: `Unity.TextMeshPro` visibility in `Game.UI` and `Game.Editor`, TMP Essential Resources, and the source font path consumed by `HudTerminalFactory.EnsureFontAsset()` in Task 4.

- [ ] **Step 1: Capture the baseline without touching the existing TMP import**

Run:

```powershell
git status --short --branch
Get-FileHash -Algorithm SHA256 -LiteralPath 'C:\Windows\Fonts\CascadiaMono.ttf'
Get-ChildItem -LiteralPath 'Assets\TextMesh Pro' -Recurse -File | Measure-Object
```

Expected:

- Branch is `hud`.
- Only `Assets/TextMesh Pro.meta` and `Assets/TextMesh Pro/` are untracked.
- Font hash equals `0E141CB99609F6F10AD05313FD1807D5CC9E28658DCBB35AB162E52FF67DC718`.

- [ ] **Step 2: Add the TMP assembly references**

Change `Assets/_Project/Code/UI/Game.UI.asmdef` references to exactly:

```json
"references": [
    "Game.Core",
    "Game.Gameplay",
    "UnityEngine.UI",
    "Unity.TextMeshPro"
]
```

Change `Assets/_Project/Code/Editor/Game.Editor.asmdef` references to exactly:

```json
"references": [
    "Game.Core",
    "Game.Gameplay",
    "Game.UI",
    "Game.DebugTools",
    "Unity.AI.Navigation",
    "Unity.InputSystem",
    "UnityEngine.UI",
    "Unity.TextMeshPro"
]
```

Do not alter any other asmdef property.

- [ ] **Step 3: Vendor the verified font and official license**

Create the folder, copy the binary font, and fetch the license from Microsoft’s official Cascadia repository:

```powershell
New-Item -ItemType Directory -Force -Path 'Assets\_Project\Art\UI\Fonts'
Copy-Item -LiteralPath 'C:\Windows\Fonts\CascadiaMono.ttf' -Destination 'Assets\_Project\Art\UI\Fonts\CascadiaMono.ttf'
Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/microsoft/cascadia-code/main/LICENSE' -OutFile 'Assets\_Project\Art\UI\Fonts\CascadiaMono-OFL.txt'
Get-FileHash -Algorithm SHA256 -LiteralPath 'Assets\_Project\Art\UI\Fonts\CascadiaMono.ttf'
```

Expected copied-font hash: `0E141CB99609F6F10AD05313FD1807D5CC9E28658DCBB35AB162E52FF67DC718`.

- [ ] **Step 4: Import and compile in the active Unity editor**

Use Unity MCP to refresh all assets and request compilation. Wait for the editor to return to idle, then read a fresh console snapshot.

Expected:

- `TMP Settings.asset` resolves from `Assets/TextMesh Pro/Resources/`.
- `CascadiaMono.ttf` imports as `UnityEngine.Font` with font data included.
- Zero console errors.

- [ ] **Step 5: Verify dependency direction and commit**

Run:

```powershell
$ui = Get-Content -Raw 'Assets\_Project\Code\UI\Game.UI.asmdef' | ConvertFrom-Json
$gameplay = Get-Content -Raw 'Assets\_Project\Code\Gameplay\Game.Gameplay.asmdef' | ConvertFrom-Json
if ('Unity.TextMeshPro' -notin $ui.references) { throw 'Game.UI is missing Unity.TextMeshPro' }
if ('Game.UI' -in $gameplay.references) { throw 'Game.Gameplay must not reference Game.UI' }
git diff --check -- . `
    ':(exclude)Assets/TextMesh Pro.meta' `
    ':(exclude)Assets/TextMesh Pro/**' `
    ':(exclude)Assets/_Project/Art/UI.meta' `
    ':(exclude)Assets/_Project/Art/UI/**'
git add -- 'Assets/TextMesh Pro.meta' 'Assets/TextMesh Pro' 'Assets/_Project/Art/UI/Fonts' 'Assets/_Project/Code/UI/Game.UI.asmdef' 'Assets/_Project/Code/Editor/Game.Editor.asmdef'
git diff --cached --check -- . `
    ':(exclude)Assets/TextMesh Pro.meta' `
    ':(exclude)Assets/TextMesh Pro/**' `
    ':(exclude)Assets/_Project/Art/UI.meta' `
    ':(exclude)Assets/_Project/Art/UI/**'
git commit -m "feat(ui): add terminal HUD typography"
```

Expected: one focused commit; no scene or runtime widget is staged.

---

### Task 2: Add the reusable terminal panel graphic

**Files:**
- Create: `Assets/_Project/Code/UI/Hud/TerminalPanelGraphic.cs`

**Interfaces:**
- Consumes: stock `UnityEngine.UI.MaskableGraphic`, the three-color palette, parent `RectTransform` dimensions, and `Time.unscaledTime`.
- Produces: `class Game.UI.TerminalPanelGraphic : MaskableGraphic`; no public gameplay API. `HudTerminalFactory.CreatePanel(RectTransform)` adds it as a full-stretch decorative child.

- [ ] **Step 1: Create the panel graphic**

Create `Assets/_Project/Code/UI/Hud/TerminalPanelGraphic.cs` with exactly:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Arızalı Tesis Terminali: draws one sprite-free cut-corner HUD plate.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TerminalPanelGraphic : MaskableGraphic
    {
        private static readonly Color DefaultPanel = new Color32(6, 16, 12, 190);
        private static readonly Color DefaultAccent = new Color32(121, 255, 155, 148);
        private static readonly Color DefaultRegistration = new Color32(121, 255, 155, 230);

        [SerializeField] private Color panelColor = DefaultPanel;
        [SerializeField] private Color accentColor = DefaultAccent;
        [SerializeField] private Color registrationColor = DefaultRegistration;
        [SerializeField, Min(0.5f)] private float borderThickness = 1.5f;
        [SerializeField, Min(0f)] private float cornerCut = 10f;
        [SerializeField, Min(2f)] private float scanlineSpacing = 4f;
        [SerializeField, Min(0.25f)] private float scanlineThickness = 0.75f;
        [SerializeField, Range(0f, 0.1f)] private float scanlineAlpha = 0.035f;
        [SerializeField, Range(0f, 0.025f)] private float flickerAmplitude = 0.02f;
        private const float PrimaryFlickerHz = 0.71f;
        private const float SecondaryFlickerHz = 1.37f;

        private float phase;

        public override bool raycastTarget
        {
            get => false;
            set => base.raycastTarget = false;
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            phase = Mathf.Abs(GetInstanceID() % 997) * 0.017f;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        protected override void OnDisable()
        {
            canvasRenderer.SetAlpha(1f);
            base.OnDisable();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                canvasRenderer.SetAlpha(1f);
                return;
            }

            float time = Time.unscaledTime + phase;
            float primary = 0.5f + 0.5f * Mathf.Sin(
                time * PrimaryFlickerHz * Mathf.PI * 2f);
            float secondary = 0.5f + 0.5f * Mathf.Sin(
                time * SecondaryFlickerHz * Mathf.PI * 2f);
            float loss = flickerAmplitude * (primary * 0.65f + secondary * 0.35f);
            canvasRenderer.SetAlpha(1f - loss);
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;

            float cut = Mathf.Min(cornerCut, Mathf.Min(rect.width, rect.height) * 0.45f);
            var points = new[]
            {
                new Vector2(rect.xMin + cut, rect.yMin),
                new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax - cut),
                new Vector2(rect.xMax - cut, rect.yMax),
                new Vector2(rect.xMin, rect.yMax),
                new Vector2(rect.xMin, rect.yMin + cut),
            };

            AddPolygon(vertexHelper, points, Tint(panelColor));
            AddBorder(vertexHelper, points, rect.center,
                Mathf.Min(borderThickness, Mathf.Min(rect.width, rect.height) * 0.2f),
                Tint(accentColor));
            AddScanlines(vertexHelper, rect, cut);
            AddRegistrationMarks(vertexHelper, rect);
        }

        private void AddScanlines(VertexHelper vertexHelper, Rect rect, float cut)
        {
            Color scanline = accentColor;
            scanline.a = scanlineAlpha;
            Color32 color32 = Tint(scanline);
            float thickness = Mathf.Min(scanlineThickness, scanlineSpacing);

            for (float y = rect.yMin + scanlineSpacing;
                 y < rect.yMax - borderThickness;
                 y += scanlineSpacing)
            {
                float left = y < rect.yMin + cut
                    ? rect.xMin + cut - (y - rect.yMin)
                    : rect.xMin;
                float right = y > rect.yMax - cut
                    ? rect.xMax - (y - (rect.yMax - cut))
                    : rect.xMax;

                left += borderThickness;
                right -= borderThickness;
                if (right <= left) continue;

                AddQuad(vertexHelper,
                    new Vector2(left, y),
                    new Vector2(right, y),
                    new Vector2(right, y + thickness),
                    new Vector2(left, y + thickness),
                    color32);
            }
        }

        private void AddRegistrationMarks(VertexHelper vertexHelper, Rect rect)
        {
            float markLength = Mathf.Min(40f, rect.width * 0.22f);
            float markThickness = Mathf.Max(2f, borderThickness);
            Color32 color32 = Tint(registrationColor);

            AddQuad(vertexHelper,
                new Vector2(rect.xMin + 12f, rect.yMax - markThickness),
                new Vector2(rect.xMin + 12f + markLength, rect.yMax - markThickness),
                new Vector2(rect.xMin + 12f + markLength, rect.yMax),
                new Vector2(rect.xMin + 12f, rect.yMax),
                color32);

            AddQuad(vertexHelper,
                new Vector2(rect.xMax - 12f - markLength, rect.yMin),
                new Vector2(rect.xMax - 12f, rect.yMin),
                new Vector2(rect.xMax - 12f, rect.yMin + markThickness),
                new Vector2(rect.xMax - 12f - markLength, rect.yMin + markThickness),
                color32);
        }

        private static void AddPolygon(VertexHelper vertexHelper, Vector2[] points, Color32 color)
        {
            Vector2 center = Vector2.zero;
            for (int i = 0; i < points.Length; i++) center += points[i];
            center /= points.Length;

            int centerIndex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(CreateVertex(center, color));
            for (int i = 0; i < points.Length; i++)
            {
                vertexHelper.AddVert(CreateVertex(points[i], color));
            }

            for (int i = 0; i < points.Length; i++)
            {
                int next = (i + 1) % points.Length;
                vertexHelper.AddTriangle(centerIndex, centerIndex + 1 + i,
                    centerIndex + 1 + next);
            }
        }

        private static void AddBorder(VertexHelper vertexHelper, Vector2[] points,
            Vector2 center, float thickness, Color32 color)
        {
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Length];
                Vector2 direction = (b - a).normalized;
                Vector2 inward = new Vector2(-direction.y, direction.x);
                if (Vector2.Dot(inward, center - (a + b) * 0.5f) < 0f)
                {
                    inward = -inward;
                }

                Vector2 innerA = a + inward * thickness;
                Vector2 innerB = b + inward * thickness;
                AddQuad(vertexHelper, a, b, innerB, innerA, color);
            }
        }

        private static void AddQuad(VertexHelper vertexHelper, Vector2 a, Vector2 b,
            Vector2 c, Vector2 d, Color32 color)
        {
            int start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(CreateVertex(a, color));
            vertexHelper.AddVert(CreateVertex(b, color));
            vertexHelper.AddVert(CreateVertex(c, color));
            vertexHelper.AddVert(CreateVertex(d, color));
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start + 2, start + 3, start);
        }

        private static UIVertex CreateVertex(Vector2 position, Color32 color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = new Vector3(position.x, position.y, 0f);
            vertex.color = color;
            return vertex;
        }

        private Color32 Tint(Color source) => source * color;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            raycastTarget = false;
            borderThickness = Mathf.Max(0.5f, borderThickness);
            cornerCut = Mathf.Max(0f, cornerCut);
            scanlineSpacing = Mathf.Max(2f, scanlineSpacing);
            scanlineThickness = Mathf.Clamp(scanlineThickness, 0.25f, scanlineSpacing);
            flickerAmplitude = Mathf.Clamp(flickerAmplitude, 0f, 0.025f);
            SetVerticesDirty();
        }
#endif
    }
}
```

- [ ] **Step 2: Refresh Unity and inspect the component contract**

Refresh/compile through Unity MCP, then read a fresh console snapshot.

Run the static checks:

```powershell
rg -n 'class TerminalPanelGraphic : MaskableGraphic|raycastTarget = false|SetAlpha|OnPopulateMesh' 'Assets\_Project\Code\UI\Hud\TerminalPanelGraphic.cs'
rg -n 'Shader|Texture2D|Sprite|GraphicRaycaster' 'Assets\_Project\Code\UI\Hud\TerminalPanelGraphic.cs'
```

Expected:

- First command finds all four required contracts.
- Second command returns no matches.
- Unity reports zero errors.

- [ ] **Step 3: Commit the reusable panel**

```powershell
git add -- 'Assets/_Project/Code/UI/Hud/TerminalPanelGraphic.cs' 'Assets/_Project/Code/UI/Hud/TerminalPanelGraphic.cs.meta'
git diff --cached --check
git commit -m "feat(ui): add retro terminal panel graphic"
```

---

### Task 3: Migrate HUD widget contracts to TMP and terminal state styling

**Files:**
- Modify: `Assets/_Project/Code/UI/Hud/ObjectiveWidget.cs`
- Modify: `Assets/_Project/Code/UI/Hud/InteractionPromptWidget.cs`
- Modify: `Assets/_Project/Code/UI/Hud/FlashlightWidget.cs`
- Modify: `Assets/_Project/Code/UI/Hud/StaminaWidget.cs`
- Modify: `Assets/_Project/Code/UI/Hud/CrosshairWidget.cs`

**Interfaces:**
- Consumes: existing gameplay sources and their unchanged properties/events; `TMPro.TMP_Text`; `CanvasGroup`; existing `Image.fillAmount` behavior.
- Produces: serialized fields `ObjectiveWidget.label : TMP_Text`, `ObjectiveWidget.group : CanvasGroup`, `InteractionPromptWidget.label : TMP_Text`, `FlashlightWidget.stateLabel : TMP_Text`, `FlashlightWidget.visualParts : Graphic[]`, and `FlashlightWidget.group : CanvasGroup` for Task 5’s builder wiring.

- [ ] **Step 1: Replace `ObjectiveWidget.cs`**

Use exactly:

```csharp
using Game.Gameplay.Objectives;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Displays fuse progress without owning any objective or exit gameplay logic.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjectiveWidget : MonoBehaviour
    {
        [SerializeField] private ObjectiveManager objectives;
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup group;

        [SerializeField, Tooltip("{0} = completed, {1} = required.")]
        private string format = "SİGORTA // {0:00}/{1:00}";

        [SerializeField] private string allCompleteText = "ÇIKIŞ // AÇIK";
        [SerializeField] private Color normalColor = new Color32(121, 255, 155, 235);
        [SerializeField] private Color flashColor = new Color32(121, 255, 155, 255);
        [SerializeField] private float flashSeconds = 0.6f;

        private bool allComplete;
        private float flashTimer;

        public void Bind(ObjectiveManager objectiveManager) => objectives = objectiveManager;

        private void Awake()
        {
            if (objectives == null || label == null || group == null)
            {
                Debug.LogWarning(
                    $"[HUD] {name}: ObjectiveManager/label/group not assigned — fuse counter disabled.",
                    this);
                if (group != null) group.alpha = 0f;
                if (label != null) label.enabled = false;
                enabled = false;
                return;
            }

            label.color = normalColor;
        }

        private void OnEnable()
        {
            if (objectives == null) return;

            objectives.OnAnyFuseCompleted += HandleFuseCompleted;
            objectives.OnAllComplete += HandleAllComplete;
            Refresh();
        }

        private void OnDisable()
        {
            if (objectives == null) return;

            objectives.OnAnyFuseCompleted -= HandleFuseCompleted;
            objectives.OnAllComplete -= HandleAllComplete;
        }

        private void HandleFuseCompleted() => flashTimer = flashSeconds;

        private void HandleAllComplete()
        {
            allComplete = true;
            Refresh();
        }

        private void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                float t = flashSeconds > 0f
                    ? Mathf.Clamp01(flashTimer / flashSeconds)
                    : 0f;
                label.color = Color.Lerp(normalColor, flashColor, t);
            }
            else if (label.color != normalColor)
            {
                label.color = normalColor;
            }

            Refresh();
        }

        private void Refresh()
        {
            int required = objectives.RequiredCount > 0
                ? objectives.RequiredCount
                : objectives.RegisteredCount;

            if (allComplete ||
                (required > 0 && objectives.CompletedCount >= required))
            {
                group.alpha = 1f;
                label.enabled = true;
                label.text = allCompleteText;
                return;
            }

            if (required <= 0)
            {
                group.alpha = 0f;
                label.enabled = false;
                return;
            }

            group.alpha = 1f;
            label.enabled = true;
            label.text = string.Format(format, objectives.CompletedCount, required);
        }
    }
}
```

- [ ] **Step 2: Replace `InteractionPromptWidget.cs`**

Use exactly:

```csharp
using Game.Gameplay.Interaction;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Draws and fades PlayerInteraction.CurrentPrompt as a terminal command.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionPromptWidget : MonoBehaviour
    {
        [SerializeField] private PlayerInteraction interaction;
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private string promptFormat = "> {0}";
        [SerializeField] private float fadeSeconds = 0.12f;

        private string lastPrompt;

        public void Bind(PlayerInteraction playerInteraction) => interaction = playerInteraction;

        private void Awake()
        {
            if (interaction == null || label == null || group == null)
            {
                Debug.LogWarning(
                    $"[HUD] {name}: interaction/label/group not fully assigned — interaction prompt disabled.",
                    this);
                if (group != null) group.alpha = 0f;
                enabled = false;
                return;
            }

            group.alpha = 0f;
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            string prompt = interaction.CurrentPrompt;
            bool show = !string.IsNullOrEmpty(prompt);

            if (show && prompt != lastPrompt)
            {
                lastPrompt = prompt;
                label.text = string.Format(promptFormat, prompt);
            }
            else if (!show)
            {
                lastPrompt = null;
            }

            float target = show ? 1f : 0f;
            group.alpha = fadeSeconds <= 0f
                ? target
                : Mathf.MoveTowards(group.alpha, target, Time.deltaTime / fadeSeconds);
        }
    }
}
```

- [ ] **Step 3: Replace `FlashlightWidget.cs`**

Use exactly:

```csharp
using Game.Gameplay.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Displays explicit flashlight state. No battery mechanic is implied.
    /// </summary>
    [DisallowMultipleComponent]
    public class FlashlightWidget : MonoBehaviour
    {
        [SerializeField] private FlashlightController flashlight;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private Graphic[] visualParts;
        [SerializeField] private CanvasGroup group;

        [SerializeField] private string onText = "AÇIK";
        [SerializeField] private string offText = "KAPALI";
        [SerializeField] private Color onColor = new Color32(121, 255, 155, 242);
        [SerializeField] private Color offColor = new Color32(121, 255, 155, 105);

        private int lastState = -1;

        public void Bind(FlashlightController controller) => flashlight = controller;

        private void Awake()
        {
            if (flashlight == null || stateLabel == null || group == null ||
                visualParts == null || visualParts.Length == 0)
            {
                Debug.LogWarning(
                    $"[HUD] {name}: flashlight terminal references incomplete — widget disabled.",
                    this);
                if (group != null) group.alpha = 0f;
                enabled = false;
                return;
            }

            group.alpha = 1f;
            Refresh();
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            int state = flashlight.IsOn ? 1 : 0;
            if (state == lastState) return;

            lastState = state;
            stateLabel.text = state == 1 ? onText : offText;
            Color color = state == 1 ? onColor : offColor;

            for (int i = 0; i < visualParts.Length; i++)
            {
                if (visualParts[i] != null) visualParts[i].color = color;
            }
        }
    }
}
```

- [ ] **Step 4: Keep stamina continuously filled while applying terminal colors**

In `StaminaWidget.cs`, replace the color defaults with:

```csharp
[SerializeField] private Color normalColor = new Color32(121, 255, 155, 230);
[SerializeField] private Color exhaustedColor = new Color32(255, 90, 54, 242);
```

Replace `Awake()` with:

```csharp
private void Awake()
{
    if (player == null || fill == null || group == null)
    {
        Debug.LogWarning(
            $"[HUD] {name}: player/fill/group not assigned — stamina bar disabled.", this);
        if (group != null) group.alpha = 0f;
        enabled = false;
        return;
    }

    group.alpha = 1f;
}
```

Do not change this existing fill calculation:

```csharp
float max = player.MaxStamina;
fill.fillAmount = max > 0f
    ? Mathf.Clamp01(player.CurrentStamina / max)
    : 0f;
```

- [ ] **Step 5: Apply the phosphor crosshair palette**

In `CrosshairWidget.cs`, replace only the two default colors:

```csharp
[SerializeField] private Color idleColor = new Color32(121, 255, 155, 150);
[SerializeField] private Color targetedColor = new Color32(121, 255, 155, 255);
```

Do not alter `Refresh()`, `Apply()`, the root transform, or `CrosshairPulse`.

- [ ] **Step 6: Compile and verify widget contracts**

Refresh/compile through Unity MCP and read a fresh console snapshot.

Run:

```powershell
rg -n 'UnityEngine\.UI\.Text|\bText label' 'Assets\_Project\Code\UI\Hud'
rg -n 'fill\.fillAmount.*|CurrentStamina / max|Image\.Type\.Filled' 'Assets\_Project\Code\UI\Hud\StaminaWidget.cs' 'Assets\_Project\Code\Editor\HudSceneBuilder.cs'
rg -n 'OnAnyFuseCompleted \+=|OnAnyFuseCompleted -=|OnAllComplete \+=|OnAllComplete -=' 'Assets\_Project\Code\UI\Hud\ObjectiveWidget.cs'
```

Expected:

- No legacy HUD `Text` field remains.
- `StaminaWidget` still computes continuous `fillAmount`; Task 5 authors `Image.Type.Filled` with a persistent built-in sprite so uGUI renders partial values.
- All four objective subscription lines are present.
- Unity reports zero errors.

- [ ] **Step 7: Commit the widget migration**

```powershell
git add -- 'Assets/_Project/Code/UI/Hud/ObjectiveWidget.cs' 'Assets/_Project/Code/UI/Hud/InteractionPromptWidget.cs' 'Assets/_Project/Code/UI/Hud/FlashlightWidget.cs' 'Assets/_Project/Code/UI/Hud/StaminaWidget.cs' 'Assets/_Project/Code/UI/Hud/CrosshairWidget.cs'
git diff --cached --check
git commit -m "feat(ui): style HUD widgets as terminal readouts"
```

---

### Task 4: Add the editor terminal factory and deterministic font asset

**Files:**
- Create: `Assets/_Project/Code/Editor/HudTerminalFactory.cs`
- Generate later: `Assets/_Project/Art/UI/Fonts/CascadiaMono SDF.asset`

**Interfaces:**
- Consumes: `CascadiaMono.ttf`, TMP Essential Resources, `TerminalPanelGraphic`, and Unity editor asset APIs.
- Produces: `TMP_FontAsset HudTerminalFactory.EnsureFontAsset()`, palette values, layout primitives, `TerminalPanelGraphic CreatePanel(RectTransform)`, and throwing `AssignSerialized(Object, string, object)` used by Task 5.

- [ ] **Step 1: Create `HudTerminalFactory.cs`**

Use exactly:

```csharp
using System;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Editor-only primitives and assets for the Arızalı Tesis Terminali HUD.
    /// </summary>
    internal static class HudTerminalFactory
    {
        internal const string FontSourcePath =
            "Assets/_Project/Art/UI/Fonts/CascadiaMono.ttf";
        internal const string FontAssetPath =
            "Assets/_Project/Art/UI/Fonts/CascadiaMono SDF.asset";

        private const string FilledImageSpritePath = "UI/Skin/UISprite.psd";
        private const string FontShaderName = "TextMeshPro/Mobile/Distance Field";
        private const int FontPointSize = 90;
        private const int FontPadding = 9;
        private const int FontAtlasSize = 1024;
        private const float FontOutlineWidth = 0.08f;

        private const string RequiredGlyphs =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
            "ÇĞİÖŞÜçğıöşü" +
            " .,:;!?/\\|[](){}<>+-=_@#$%^&*'\"`~";

        internal static readonly Color Base = new Color32(6, 16, 12, 235);
        internal static readonly Color PanelBackground = new Color32(6, 16, 12, 190);
        internal static readonly Color Phosphor = new Color32(121, 255, 155, 235);
        internal static readonly Color MutedPhosphor = new Color32(121, 255, 155, 112);
        internal static readonly Color Danger = new Color32(255, 90, 54, 242);
        private static readonly Color FontOutlineColor = new Color32(6, 16, 12, 220);

        internal static TMP_FontAsset EnsureFontAsset()
        {
            if (TMP_Settings.instance == null)
            {
                Debug.LogError(
                    "[HUD Builder] TMP Essential Resources are missing; existing HUD was not changed.");
                return null;
            }

            TMP_FontAsset existing =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                if (!ValidateFontAsset(existing, out string validationError))
                {
                    Debug.LogError(
                        $"[HUD Builder] Cascadia font asset is invalid: {validationError}. " +
                        "Existing HUD was not changed.", existing);
                    return null;
                }

                return existing;
            }

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
            if (sourceFont == null)
            {
                Debug.LogError(
                    $"[HUD Builder] Missing source font at '{FontSourcePath}'; " +
                    "existing HUD was not changed.");
                return null;
            }

            TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                FontPointSize,
                FontPadding,
                GlyphRenderMode.SDFAA,
                FontAtlasSize,
                FontAtlasSize,
                AtlasPopulationMode.Dynamic,
                false);

            if (created == null)
            {
                Debug.LogError(
                    "[HUD Builder] TMP could not create Cascadia Mono SDF; " +
                    "existing HUD was not changed.", sourceFont);
                return null;
            }

            if (!created.TryAddCharacters(RequiredGlyphs, out string missingCharacters, false) ||
                !string.IsNullOrEmpty(missingCharacters))
            {
                Debug.LogError(
                    $"[HUD Builder] Cascadia Mono is missing required glyphs: " +
                    $"'{missingCharacters}'. Existing HUD was not changed.", sourceFont);
                UnityEngine.Object.DestroyImmediate(created.material);
                UnityEngine.Object.DestroyImmediate(created.atlasTexture);
                UnityEngine.Object.DestroyImmediate(created);
                return null;
            }

            created.name = "CascadiaMono SDF";
            created.atlasPopulationMode = AtlasPopulationMode.Static;
            created.creationSettings = new FontAssetCreationSettings
            {
                sourceFontFileName = "CascadiaMono.ttf",
                sourceFontFileGUID = AssetDatabase.AssetPathToGUID(FontSourcePath),
                faceIndex = 0,
                pointSizeSamplingMode = 0,
                pointSize = FontPointSize,
                padding = FontPadding,
                paddingMode = 2,
                packingMode = 0,
                atlasWidth = FontAtlasSize,
                atlasHeight = FontAtlasSize,
                characterSetSelectionMode = 7,
                characterSequence = RequiredGlyphs,
                referencedFontAssetGUID = string.Empty,
                referencedTextAssetGUID = string.Empty,
                fontStyle = 0,
                fontStyleModifier = 0f,
                renderMode = (int)GlyphRenderMode.SDFAA,
                includeFontFeatures = false
            };
            created.material.name = "CascadiaMono SDF Material";
            created.material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            created.material.SetColor(ShaderUtilities.ID_OutlineColor, FontOutlineColor);
            created.material.SetFloat(ShaderUtilities.ID_OutlineWidth, FontOutlineWidth);

            AssetDatabase.CreateAsset(created, FontAssetPath);
            AssetDatabase.AddObjectToAsset(created.atlasTexture, created);
            AssetDatabase.AddObjectToAsset(created.material, created);
            EditorUtility.SetDirty(created);
            EditorUtility.SetDirty(created.material);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);

            TMP_FontAsset saved =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            string savedError = "asset could not be loaded";
            if (saved == null || !ValidateFontAsset(saved, out savedError))
            {
                bool removed = AssetDatabase.DeleteAsset(FontAssetPath);
                AssetDatabase.SaveAssets();
                Debug.LogError(
                    $"[HUD Builder] Saved Cascadia font asset failed validation: {savedError}. " +
                    $"The invalid asset was {(removed ? "removed" : "not removed")}; " +
                    "existing HUD was not changed.");
                return null;
            }

            return saved;
        }

        internal static RectTransform CreateFullScreenChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        internal static RectTransform CreateChild(Transform parent, string name,
            Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        internal static RectTransform CreateStretchChild(RectTransform parent, string name,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = CreateChild(parent, name, Vector2.zero,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        internal static TerminalPanelGraphic CreatePanel(RectTransform parent)
        {
            RectTransform rect = CreateStretchChild(parent, "Panel", Vector2.zero, Vector2.zero);
            rect.SetAsFirstSibling();
            TerminalPanelGraphic panel = rect.gameObject.AddComponent<TerminalPanelGraphic>();
            panel.raycastTarget = false;
            return panel;
        }

        internal static TextMeshProUGUI AddText(RectTransform rect, TMP_FontAsset font,
            float fontSize, TextAlignmentOptions alignment, Color color, string value)
        {
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.text = value;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        internal static Image CreateImage(RectTransform parent, string name, Vector2 anchor,
            Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            RectTransform rect = CreateChild(parent, name, anchor, pivot, anchoredPosition, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal static Sprite RequireFilledImageSprite()
        {
            Sprite sprite =
                AssetDatabase.GetBuiltinExtraResource<Sprite>(FilledImageSpritePath);
            if (sprite == null)
            {
                throw new InvalidOperationException(
                    $"Built-in UI sprite '{FilledImageSpritePath}' is unavailable.");
            }

            return sprite;
        }

        internal static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        internal static void AssignSerialized(UnityEngine.Object target, string fieldName,
            object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name} has no serialized field '{fieldName}'.");
            }

            switch (value)
            {
                case UnityEngine.Object unityObject:
                    property.objectReferenceValue = unityObject;
                    break;
                case Vector2 vector:
                    property.vector2Value = vector;
                    break;
                case UnityEngine.Object[] array:
                    property.arraySize = array.Length;
                    for (int i = 0; i < array.Length; i++)
                    {
                        property.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
                    }
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported serialized value type for '{fieldName}': " +
                        $"{value?.GetType().FullName ?? "null"}.");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool ValidateFontAsset(TMP_FontAsset fontAsset, out string error)
        {
            if (fontAsset.faceInfo.familyName != "Cascadia Mono")
            {
                error = $"family is '{fontAsset.faceInfo.familyName}'";
                return false;
            }

            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Static)
            {
                error = $"atlas mode is {fontAsset.atlasPopulationMode}, expected Static";
                return false;
            }

            var serialized = new SerializedObject(fontAsset);
            SerializedProperty sourceGuidProperty =
                serialized.FindProperty("m_SourceFontFileGUID");
            string expectedSourceGuid = AssetDatabase.AssetPathToGUID(FontSourcePath);
            if (sourceGuidProperty == null ||
                sourceGuidProperty.stringValue != expectedSourceGuid)
            {
                error = "source font GUID does not match the vendored CascadiaMono.ttf";
                return false;
            }

            FontAssetCreationSettings creationSettings = fontAsset.creationSettings;
            if (creationSettings.sourceFontFileGUID != expectedSourceGuid ||
                creationSettings.pointSize != FontPointSize ||
                creationSettings.padding != FontPadding ||
                creationSettings.atlasWidth != FontAtlasSize ||
                creationSettings.atlasHeight != FontAtlasSize ||
                creationSettings.renderMode != (int)GlyphRenderMode.SDFAA)
            {
                error = "saved font creation metadata does not match the generation contract";
                return false;
            }

            if (!Mathf.Approximately(fontAsset.faceInfo.pointSize, FontPointSize))
            {
                error = $"point size is {fontAsset.faceInfo.pointSize}, expected {FontPointSize}";
                return false;
            }

            if (fontAsset.atlasWidth != FontAtlasSize ||
                fontAsset.atlasHeight != FontAtlasSize ||
                fontAsset.atlasPadding != FontPadding ||
                fontAsset.atlasRenderMode != GlyphRenderMode.SDFAA)
            {
                error =
                    $"atlas contract is {fontAsset.atlasWidth}x{fontAsset.atlasHeight}, " +
                    $"padding {fontAsset.atlasPadding}, mode {fontAsset.atlasRenderMode}";
                return false;
            }

            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length != 1 ||
                fontAsset.atlasTexture == null || fontAsset.isMultiAtlasTexturesEnabled)
            {
                error = "atlas texture is missing or multi-atlas is enabled";
                return false;
            }

            if (fontAsset.atlasTexture.width != FontAtlasSize ||
                fontAsset.atlasTexture.height != FontAtlasSize)
            {
                error =
                    $"atlas texture is {fontAsset.atlasTexture.width}x" +
                    $"{fontAsset.atlasTexture.height}, expected {FontAtlasSize}x{FontAtlasSize}";
                return false;
            }

            Material material = fontAsset.material;
            if (material == null || material.shader == null ||
                material.shader.name != FontShaderName)
            {
                error = "material is missing or does not use the expected TMP shader";
                return false;
            }

            if (material.mainTexture != fontAsset.atlasTexture ||
                !material.IsKeywordEnabled(ShaderUtilities.Keyword_Outline) ||
                !Mathf.Approximately(
                    material.GetFloat(ShaderUtilities.ID_OutlineWidth), FontOutlineWidth) ||
                !Approximately(
                    material.GetColor(ShaderUtilities.ID_OutlineColor), FontOutlineColor))
            {
                error = "material atlas or outline settings do not match the HUD contract";
                return false;
            }

            if (!fontAsset.HasCharacters(RequiredGlyphs, out uint[] missing, false, false))
            {
                error = $"required glyphs are missing ({missing.Length})";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool Approximately(Color left, Color right)
        {
            const float tolerance = 0.001f;
            return Mathf.Abs(left.r - right.r) <= tolerance &&
                   Mathf.Abs(left.g - right.g) <= tolerance &&
                   Mathf.Abs(left.b - right.b) <= tolerance &&
                   Mathf.Abs(left.a - right.a) <= tolerance;
        }
    }
}
```

- [ ] **Step 2: Compile and verify the factory contract**

Refresh/compile through Unity MCP and read a fresh console snapshot.

Run:

```powershell
rg -n 'EnsureFontAsset|AtlasPopulationMode.Static|TryAddCharacters|HasCharacters|AssignSerialized|throw new InvalidOperationException' 'Assets\_Project\Code\Editor\HudTerminalFactory.cs'
rg -n 'GameObject\.Find|FindFirstObjectByType|GraphicRaycaster' 'Assets\_Project\Code\Editor\HudTerminalFactory.cs'
```

Expected:

- Font creation, source GUID/90 pt/9 px/1024²/SDFAA validation, static atlas conversion, outline validation, failed-asset cleanup, persistent fill-sprite lookup, and throwing serialized assignment are present.
- Factory does not search the scene and does not create a raycaster.
- Unity reports zero errors.

- [ ] **Step 3: Commit the editor factory**

```powershell
git add -- 'Assets/_Project/Code/Editor/HudTerminalFactory.cs' 'Assets/_Project/Code/Editor/HudTerminalFactory.cs.meta'
git diff --cached --check
git commit -m "feat(editor): add terminal HUD factory"
```

Do not stage `CascadiaMono SDF.asset` yet; Task 6 generates and validates it through the finished builder.

---

### Task 5: Rebuild `HudSceneBuilder` around the terminal hierarchy

**Files:**
- Modify: `Assets/_Project/Code/Editor/HudSceneBuilder.cs`

**Interfaces:**
- Consumes: every Task 2–4 interface, existing gameplay `Bind(...)` methods, and active-scene roots.
- Produces: `Tools/Gloomveil/UI/Build HUD`, an atomically swapped `HudCanvas`, four terminal panels, 11 stamina overlay separators, TMP visual references, and the unchanged `SwayRoot`/`FixedRoot` invariant.

- [ ] **Step 1: Replace `HudSceneBuilder.cs`**

Use exactly:

```csharp
using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Interaction;
using Game.Gameplay.Objectives;
using Game.Gameplay.Player;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Reproducibly builds the Arızalı Tesis Terminali HUD into the active scene.
    /// </summary>
    public static class HudSceneBuilder
    {
        private const string RootName = "HudCanvas";
        private const string CandidateName = "__HudCanvas_Building";
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float CornerMarginX = 48f;
        private const float CornerMarginY = 44f;
        private const float PromptBottomOffset = 190f;
        private const float CrosshairTickDistance = 10f;

        [MenuItem("Tools/Gloomveil/UI/Build HUD")]
        public static void BuildHud()
        {
            TMP_FontAsset font = HudTerminalFactory.EnsureFontAsset();
            if (font == null) return;

            Scene scene = SceneManager.GetActiveScene();
            GameObject candidate = null;

            try
            {
                candidate = BuildCandidate(font);
                ValidateCandidate(candidate);
            }
            catch (Exception exception)
            {
                if (candidate != null)
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                }

                Debug.LogException(new InvalidOperationException(
                    "[HUD Builder] Retro terminal construction failed; " +
                    "existing HUD was preserved.", exception));
                return;
            }

            int undoGroup = -1;
            try
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Build Retro Terminal HUD");
                Undo.RegisterCreatedObjectUndo(candidate, "Build Retro Terminal HUD");
                Undo.RegisterFullObjectHierarchyUndo(candidate, "Build Retro Terminal HUD");

                List<GameObject> existingRoots = FindNamedRoots(scene, RootName);
                for (int i = 0; i < existingRoots.Count; i++)
                {
                    Undo.DestroyObjectImmediate(existingRoots[i]);
                }

                candidate.name = RootName;
                candidate.SetActive(true);

                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log(
                    $"[HUD Builder] Built retro terminal '{RootName}' in scene '{scene.name}'. " +
                    "Save the scene to keep it.");
            }
            catch (Exception exception)
            {
                bool rollbackSucceeded = false;
                try
                {
                    if (undoGroup >= 0)
                    {
                        Undo.RevertAllDownToGroup(undoGroup);
                    }
                    else if (candidate != null)
                    {
                        UnityEngine.Object.DestroyImmediate(candidate);
                    }

                    rollbackSucceeded = true;
                }
                catch (Exception rollbackException)
                {
                    Debug.LogException(new InvalidOperationException(
                        "[HUD Builder] Retro terminal rollback also failed.",
                        rollbackException));
                }

                if (candidate != null)
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                }

                Debug.LogException(new InvalidOperationException(
                    rollbackSucceeded
                        ? "[HUD Builder] Retro terminal build failed; existing HUD was preserved."
                        : "[HUD Builder] Retro terminal build and rollback failed; inspect the scene.",
                    exception));
            }
        }

        private static GameObject BuildCandidate(TMP_FontAsset font)
        {
            var canvasObject = new GameObject(CandidateName, typeof(RectTransform));
            try
            {
                PopulateCandidate(canvasObject, font);
                return canvasObject;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
                throw;
            }
        }

        private static void PopulateCandidate(GameObject canvasObject, TMP_FontAsset font)
        {
            canvasObject.SetActive(false);
            canvasObject.AddComponent<Canvas>();
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<CanvasGroup>();
            canvasObject.AddComponent<HudCameraMotion>();
            canvasObject.AddComponent<HudVisibility>();

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            CanvasGroup canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            HudCameraMotion motion = canvasObject.GetComponent<HudCameraMotion>();
            Camera playerCamera = Camera.main;
            if (playerCamera != null)
            {
                motion.Bind(playerCamera.transform);
            }
            else
            {
                Debug.LogWarning(
                    "[HUD Builder] No MainCamera found — HUD sway will rest until bound.");
            }

            GameFlowController flow =
                UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
            if (flow != null)
            {
                canvasObject.GetComponent<HudVisibility>().Bind(flow, canvasGroup);
            }
            else
            {
                Debug.LogWarning(
                    "[HUD Builder] No GameFlowController found — HUD visibility stays disabled.");
            }

            RectTransform swayRoot =
                HudTerminalFactory.CreateFullScreenChild(canvasObject.transform, "SwayRoot");
            swayRoot.gameObject.AddComponent<HudRootSway>().Bind(motion);

            RectTransform fixedRoot =
                HudTerminalFactory.CreateFullScreenChild(canvasObject.transform, "FixedRoot");

            BuildObjective(swayRoot, motion, font);
            BuildStamina(swayRoot, motion, font);
            BuildFlashlight(swayRoot, motion, font);
            BuildPrompt(swayRoot, motion, font);
            BuildCrosshair(fixedRoot, motion);
        }

        private static void BuildObjective(RectTransform parent, HudCameraMotion motion,
            TMP_FontAsset font)
        {
            RectTransform rect = HudTerminalFactory.CreateChild(parent, "ObjectiveWidget",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(CornerMarginX, -CornerMarginY), new Vector2(460f, 66f));

            HudSway sway = rect.gameObject.AddComponent<HudSway>();
            sway.Bind(motion);
            HudTerminalFactory.AssignSerialized(sway, "pixelsPerDegreePerSecond",
                new Vector2(-0.055f, 0.038f));

            CanvasGroup group = AddPassiveCanvasGroup(rect, 1f);
            HudTerminalFactory.CreatePanel(rect);

            RectTransform headerRect = HudTerminalFactory.CreateChild(rect, "Header",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -7f), new Vector2(430f, 15f));
            HudTerminalFactory.AddText(headerRect, font, 11f,
                TextAlignmentOptions.TopLeft, HudTerminalFactory.MutedPhosphor,
                "GRID://POWER");

            RectTransform valueRect = HudTerminalFactory.CreateChild(rect, "Value",
                Vector2.zero, Vector2.zero, new Vector2(14f, 9f),
                new Vector2(430f, 31f));
            TextMeshProUGUI value = HudTerminalFactory.AddText(valueRect, font, 22f,
                TextAlignmentOptions.MidlineLeft, HudTerminalFactory.Phosphor, string.Empty);

            ObjectiveWidget widget = rect.gameObject.AddComponent<ObjectiveWidget>();
            ObjectiveManager objectives =
                UnityEngine.Object.FindFirstObjectByType<ObjectiveManager>();
            if (objectives != null)
            {
                widget.Bind(objectives);
            }
            else
            {
                group.alpha = 0f;
                Debug.Log(
                    "[HUD Builder] No ObjectiveManager — objective terminal is hidden.");
            }

            HudTerminalFactory.AssignSerialized(widget, "label", value);
            HudTerminalFactory.AssignSerialized(widget, "group", group);
        }

        private static void BuildStamina(RectTransform parent, HudCameraMotion motion,
            TMP_FontAsset font)
        {
            RectTransform rect = HudTerminalFactory.CreateChild(parent, "StaminaWidget",
                Vector2.zero, Vector2.zero,
                new Vector2(CornerMarginX, CornerMarginY), new Vector2(320f, 62f));

            HudSway sway = rect.gameObject.AddComponent<HudSway>();
            sway.Bind(motion);
            HudTerminalFactory.AssignSerialized(sway, "pixelsPerDegreePerSecond",
                new Vector2(-0.07f, 0.048f));

            CanvasGroup group = AddPassiveCanvasGroup(rect, 1f);
            HudTerminalFactory.CreatePanel(rect);

            RectTransform headerRect = HudTerminalFactory.CreateChild(rect, "Header",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -7f), new Vector2(292f, 14f));
            HudTerminalFactory.AddText(headerRect, font, 10f,
                TextAlignmentOptions.TopLeft, HudTerminalFactory.MutedPhosphor,
                "RUN://CAPACITY");

            RectTransform bar = HudTerminalFactory.CreateChild(rect, "Bar", Vector2.zero,
                Vector2.zero, new Vector2(14f, 10f), new Vector2(292f, 16f));

            Image background = HudTerminalFactory.CreateImage(bar, "Background",
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
                new Color32(121, 255, 155, 35));
            HudTerminalFactory.Stretch(background.rectTransform, Vector2.zero, Vector2.zero);

            Image fill = HudTerminalFactory.CreateImage(bar, "Fill", Vector2.zero,
                Vector2.zero, Vector2.zero, Vector2.zero, HudTerminalFactory.Phosphor);
            HudTerminalFactory.Stretch(fill.rectTransform, new Vector2(2f, 2f),
                new Vector2(-2f, -2f));
            fill.sprite = HudTerminalFactory.RequireFilledImageSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            for (int i = 1; i < 12; i++)
            {
                float normalized = i / 12f;
                float anchorX = Mathf.Lerp(2f / 292f, 290f / 292f, normalized);
                HudTerminalFactory.CreateImage(bar, $"Segment{i:00}",
                    new Vector2(anchorX, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(2f, 12f), HudTerminalFactory.Base);
            }

            StaminaWidget widget = rect.gameObject.AddComponent<StaminaWidget>();
            PlayerController player =
                UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                widget.Bind(player);
            }
            else
            {
                group.alpha = 0f;
                Debug.LogWarning(
                    "[HUD Builder] No PlayerController — stamina terminal is hidden.");
            }

            HudTerminalFactory.AssignSerialized(widget, "fill", fill);
            HudTerminalFactory.AssignSerialized(widget, "group", group);
        }

        private static void BuildFlashlight(RectTransform parent, HudCameraMotion motion,
            TMP_FontAsset font)
        {
            RectTransform rect = HudTerminalFactory.CreateChild(parent, "FlashlightWidget",
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-CornerMarginX, CornerMarginY), new Vector2(260f, 62f));

            HudSway sway = rect.gameObject.AddComponent<HudSway>();
            sway.Bind(motion);
            HudTerminalFactory.AssignSerialized(sway, "pixelsPerDegreePerSecond",
                new Vector2(-0.065f, 0.044f));

            CanvasGroup group = AddPassiveCanvasGroup(rect, 1f);
            HudTerminalFactory.CreatePanel(rect);

            RectTransform headerRect = HudTerminalFactory.CreateChild(rect, "Header",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -7f), new Vector2(232f, 14f));
            HudTerminalFactory.AddText(headerRect, font, 10f,
                TextAlignmentOptions.TopLeft, HudTerminalFactory.MutedPhosphor,
                "AUX://LIGHT");

            RectTransform keyRect = HudTerminalFactory.CreateChild(rect, "Key", Vector2.zero,
                Vector2.zero, new Vector2(14f, 8f), new Vector2(42f, 29f));
            TextMeshProUGUI key = HudTerminalFactory.AddText(keyRect, font, 18f,
                TextAlignmentOptions.Center, HudTerminalFactory.Phosphor, "[F]");

            RectTransform labelRect = HudTerminalFactory.CreateChild(rect, "Label", Vector2.zero,
                Vector2.zero, new Vector2(62f, 8f), new Vector2(90f, 29f));
            TextMeshProUGUI label = HudTerminalFactory.AddText(labelRect, font, 18f,
                TextAlignmentOptions.MidlineLeft, HudTerminalFactory.Phosphor, "FENER");

            RectTransform stateRect = HudTerminalFactory.CreateChild(rect, "State",
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-14f, 8f), new Vector2(84f, 29f));
            TextMeshProUGUI state = HudTerminalFactory.AddText(stateRect, font, 22f,
                TextAlignmentOptions.MidlineRight, HudTerminalFactory.MutedPhosphor,
                "KAPALI");

            FlashlightWidget widget = rect.gameObject.AddComponent<FlashlightWidget>();
            FlashlightController flashlight =
                UnityEngine.Object.FindFirstObjectByType<FlashlightController>();
            if (flashlight != null)
            {
                widget.Bind(flashlight);
            }
            else
            {
                group.alpha = 0f;
                Debug.LogWarning(
                    "[HUD Builder] No FlashlightController — flashlight terminal is hidden.");
            }

            HudTerminalFactory.AssignSerialized(widget, "stateLabel", state);
            HudTerminalFactory.AssignSerialized(widget, "visualParts",
                new Graphic[] { key, label, state });
            HudTerminalFactory.AssignSerialized(widget, "group", group);
        }

        private static void BuildPrompt(RectTransform parent, HudCameraMotion motion,
            TMP_FontAsset font)
        {
            RectTransform rect = HudTerminalFactory.CreateChild(parent,
                "InteractionPromptWidget", new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, PromptBottomOffset),
                new Vector2(720f, 52f));

            HudSway sway = rect.gameObject.AddComponent<HudSway>();
            sway.Bind(motion);
            HudTerminalFactory.AssignSerialized(sway, "pixelsPerDegreePerSecond",
                new Vector2(-0.03f, 0.02f));

            CanvasGroup group = AddPassiveCanvasGroup(rect, 0f);
            HudTerminalFactory.CreatePanel(rect);

            RectTransform headerRect = HudTerminalFactory.CreateChild(rect, "Header",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -6f), new Vector2(692f, 13f));
            HudTerminalFactory.AddText(headerRect, font, 9f,
                TextAlignmentOptions.TopLeft, HudTerminalFactory.MutedPhosphor,
                "INPUT://CONTEXT");

            RectTransform promptRect = HudTerminalFactory.CreateChild(rect, "Prompt",
                Vector2.zero, Vector2.zero, new Vector2(14f, 5f),
                new Vector2(692f, 29f));
            TextMeshProUGUI prompt = HudTerminalFactory.AddText(promptRect, font, 18f,
                TextAlignmentOptions.MidlineLeft, HudTerminalFactory.Phosphor, string.Empty);

            InteractionPromptWidget widget =
                rect.gameObject.AddComponent<InteractionPromptWidget>();
            PlayerInteraction interaction =
                UnityEngine.Object.FindFirstObjectByType<PlayerInteraction>();
            if (interaction != null)
            {
                widget.Bind(interaction);
            }
            else
            {
                Debug.LogWarning(
                    "[HUD Builder] No PlayerInteraction — prompt terminal is hidden.");
            }

            HudTerminalFactory.AssignSerialized(widget, "label", prompt);
            HudTerminalFactory.AssignSerialized(widget, "group", group);
        }

        private static void BuildCrosshair(RectTransform parent, HudCameraMotion motion)
        {
            RectTransform rect = HudTerminalFactory.CreateChild(parent, "CrosshairWidget",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(64f, 64f));

            CanvasGroup group = AddPassiveCanvasGroup(rect, 1f);

            Image dot = HudTerminalFactory.CreateImage(rect, "DotDiamond",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(3f, 3f), HudTerminalFactory.MutedPhosphor);
            dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var ticks = new RectTransform[4];
            var graphics = new Graphic[5];
            graphics[0] = dot;

            Vector2[] directions =
            {
                Vector2.up,
                Vector2.down,
                Vector2.left,
                Vector2.right,
            };
            string[] names = { "TickUp", "TickDown", "TickLeft", "TickRight" };

            for (int i = 0; i < directions.Length; i++)
            {
                bool vertical = i < 2;
                Vector2 size = vertical ? new Vector2(1.5f, 8f) : new Vector2(8f, 1.5f);
                Image tick = HudTerminalFactory.CreateImage(rect, names[i],
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    directions[i] * CrosshairTickDistance, size,
                    HudTerminalFactory.MutedPhosphor);
                ticks[i] = tick.rectTransform;
                graphics[i + 1] = tick;
            }

            CrosshairPulse pulse = rect.gameObject.AddComponent<CrosshairPulse>();
            pulse.Bind(motion);
            HudTerminalFactory.AssignSerialized(pulse, "ticks", ticks);
            HudTerminalFactory.AssignSerialized(pulse, "group", group);

            CrosshairWidget widget = rect.gameObject.AddComponent<CrosshairWidget>();
            PlayerInteraction interaction =
                UnityEngine.Object.FindFirstObjectByType<PlayerInteraction>();
            if (interaction != null)
            {
                widget.Bind(interaction);
            }

            HudTerminalFactory.AssignSerialized(widget, "parts", graphics);
        }

        private static CanvasGroup AddPassiveCanvasGroup(RectTransform rect, float alpha)
        {
            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = alpha;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        private static List<GameObject> FindNamedRoots(Scene scene, string rootName)
        {
            var results = new List<GameObject>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == rootName) results.Add(roots[i]);
            }

            return results;
        }

        private static void ValidateCandidate(GameObject candidate)
        {
            if (candidate.GetComponentsInChildren<TerminalPanelGraphic>(true).Length != 4)
            {
                throw new InvalidOperationException(
                    "Candidate must contain exactly four TerminalPanelGraphic components.");
            }

            int segmentCount = 0;
            RectTransform[] rects = candidate.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].name.StartsWith("Segment", StringComparison.Ordinal))
                {
                    segmentCount++;
                }
            }

            if (segmentCount != 11)
            {
                throw new InvalidOperationException(
                    $"Candidate stamina bar has {segmentCount} separators; expected 11.");
            }

            if (candidate.GetComponentInChildren<GraphicRaycaster>(true) != null)
            {
                throw new InvalidOperationException("HUD must not contain GraphicRaycaster.");
            }

            Graphic[] graphics = candidate.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i].raycastTarget)
                {
                    throw new InvalidOperationException(
                        $"Graphic '{graphics[i].name}' has raycastTarget enabled.");
                }
            }

            CanvasGroup[] groups = candidate.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].blocksRaycasts)
                {
                    throw new InvalidOperationException(
                        $"CanvasGroup '{groups[i].name}' blocks raycasts.");
                }
            }

            Transform fixedRoot = candidate.transform.Find("FixedRoot");
            Transform crosshair = fixedRoot != null
                ? fixedRoot.Find("CrosshairWidget")
                : null;
            if (crosshair == null || crosshair.GetComponent<HudSway>() != null)
            {
                throw new InvalidOperationException(
                    "Crosshair must be under FixedRoot and must not have HudSway.");
            }

            RequireReference(candidate.GetComponentInChildren<ObjectiveWidget>(true),
                "label", "group");
            RequireReference(candidate.GetComponentInChildren<StaminaWidget>(true),
                "fill", "group");
            Transform fillTransform =
                candidate.transform.Find("SwayRoot/StaminaWidget/Bar/Fill");
            Image staminaFill = fillTransform != null
                ? fillTransform.GetComponent<Image>()
                : null;
            if (staminaFill == null || staminaFill.sprite == null ||
                staminaFill.type != Image.Type.Filled ||
                staminaFill.fillMethod != Image.FillMethod.Horizontal ||
                staminaFill.fillOrigin != (int)Image.OriginHorizontal.Left)
            {
                throw new InvalidOperationException(
                    "Stamina fill must use a persistent sprite and fill horizontally from left.");
            }
            RequireReference(candidate.GetComponentInChildren<FlashlightWidget>(true),
                "stateLabel", "group");
            RequireReference(candidate.GetComponentInChildren<InteractionPromptWidget>(true),
                "label", "group");
            RequireArray(candidate.GetComponentInChildren<FlashlightWidget>(true),
                "visualParts", 3);
            RequireArray(candidate.GetComponentInChildren<CrosshairWidget>(true),
                "parts", 5);
            RequireArray(candidate.GetComponentInChildren<CrosshairPulse>(true),
                "ticks", 4);
        }

        private static void RequireReference(UnityEngine.Object target, params string[] fields)
        {
            if (target == null) throw new InvalidOperationException("Required widget is missing.");

            var serialized = new SerializedObject(target);
            for (int i = 0; i < fields.Length; i++)
            {
                SerializedProperty property = serialized.FindProperty(fields[i]);
                if (property == null || property.objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        $"{target.GetType().Name}.{fields[i]} is not assigned.");
                }
            }
        }

        private static void RequireArray(UnityEngine.Object target, string field, int length)
        {
            if (target == null) throw new InvalidOperationException("Required widget is missing.");

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || !property.isArray || property.arraySize != length)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name}.{field} must have {length} entries.");
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        $"{target.GetType().Name}.{field}[{i}] is not assigned.");
                }
            }
        }
    }
}
```

- [ ] **Step 2: Compile before touching the scene**

Refresh/compile through Unity MCP and read a fresh console snapshot.

Run:

```powershell
rg -n 'BuildCandidate|PopulateCandidate|ValidateCandidate|FindNamedRoots|RevertAllDownToGroup|RequireFilledImageSprite|Image.Type.Filled|Segment\{i:00\}' 'Assets\_Project\Code\Editor\HudSceneBuilder.cs'
rg -n 'new GameObject\(RootName|GameObject.Find\(RootName\)|AddComponent<GraphicRaycaster>|typeof\(GraphicRaycaster\)' 'Assets\_Project\Code\Editor\HudSceneBuilder.cs'
```

Expected:

- Candidate-build, validation, inactive-root discovery, continuous Filled Image, and separator creation are present.
- The second search returns no matches.
- Unity reports zero errors.

- [ ] **Step 3: Commit the builder code without generating the scene yet**

```powershell
git add -- 'Assets/_Project/Code/Editor/HudSceneBuilder.cs'
git diff --cached --check
git commit -m "feat(editor): build retro terminal HUD atomically"
```

---

### Task 6: Generate, inspect, and save the styled `MainGame` HUD

**Files:**
- Generate: `Assets/_Project/Art/UI/Fonts/CascadiaMono SDF.asset`
- Generate: `Assets/_Project/Art/UI/Fonts/CascadiaMono SDF.asset.meta`
- Modify through Unity: `Assets/Scenes/MainGame.unity`

**Interfaces:**
- Consumes: `Tools/Gloomveil/UI/Build HUD` and active Unity instance `gelistirjam@fcf7d29f`.
- Produces: persisted TMP font sub-assets, one validated `HudCanvas` in `MainGame`, and visual screenshot evidence under ignored `Library/HudCaptures/`.

- [ ] **Step 1: Pin the Unity editor and clear stale console data**

Read `mcpforunity://instances`, set the exact active instance, confirm the active scene is
`Assets/Scenes/MainGame.unity`, then clear the console.

Do not enter Play Mode.

- [ ] **Step 2: Refresh and execute the builder once**

Call:

```json
{"scope":"all","mode":"force","compile":"request","wait_for_ready":true}
```

through `mcp__unityMCP__refresh_unity`, read `mcpforunity://menu-items`, then call:

```json
{"menu_path":"Tools/Gloomveil/UI/Build HUD"}
```

through `mcp__unityMCP__execute_menu_item`.

Expected console evidence:

- Font asset is created without missing-glyph errors.
- Builder logs that retro terminal `HudCanvas` was built.
- The known `ObjectiveManager` absence may log once and hides the objective panel.
- Zero errors.

- [ ] **Step 3: Execute the builder a second time to prove idempotency**

Execute the same menu item again, then query the scene hierarchy with
`mcp__unityMCP__manage_scene(action="get_hierarchy", max_depth=6, page_size=50)`.

Expected:

- Exactly one root named `HudCanvas`.
- No root named `__HudCanvas_Building`.
- Second run succeeds with zero errors.
- One Editor Undo restores the previous single active `HudCanvas`; one Redo restores the rebuilt
  single active `HudCanvas` with no candidate root and no Inspector/serialization errors.

- [ ] **Step 4: Save and inspect the exact hierarchy**

Save `Assets/Scenes/MainGame.unity`, then verify:

```text
HudCanvas
├── SwayRoot
│   ├── ObjectiveWidget
│   │   ├── Panel
│   │   ├── Header
│   │   └── Value
│   ├── StaminaWidget
│   │   ├── Panel
│   │   ├── Header
│   │   └── Bar
│   │       ├── Background
│   │       ├── Fill
│   │       └── Segment01 through Segment11
│   ├── FlashlightWidget
│   │   ├── Panel
│   │   ├── Header
│   │   ├── Key
│   │   ├── Label
│   │   └── State
│   └── InteractionPromptWidget
│       ├── Panel
│       ├── Header
│       └── Prompt
└── FixedRoot
    └── CrosshairWidget
        ├── DotDiamond
        └── TickUp/Down/Left/Right
```

- [ ] **Step 5: Inspect components and serialized references**

Use paged component reads on the relevant objects. Confirm:

- `HudCanvas`: `Canvas`, `CanvasScaler`, `CanvasGroup`, `HudCameraMotion`, `HudVisibility`; no `GraphicRaycaster`.
- Four `Panel` children: each has `RectTransform`, `CanvasRenderer`, `TerminalPanelGraphic`.
- Every TMP object uses `CascadiaMono SDF`, has `raycastTarget=false`, and is not LiberationSans.
- `StaminaWidget.fill` points to `Bar/Fill`; `Fill.sprite` is the persistent built-in UI sprite, `Fill.type=Filled`, horizontal origin left, and `fillAmount=1` in edit state.
- Exactly 11 `Segment##` objects are siblings after `Fill`, not children of `Fill`.
- `FlashlightWidget.stateLabel`, three `visualParts`, and `group` are assigned.
- `ObjectiveWidget.label` and `group` are assigned even though `objectives` is null in this scene.
- `InteractionPromptWidget.label` and `group` are assigned.
- `CrosshairWidget.parts` has five entries; `CrosshairPulse.ticks` has four.
- `CrosshairWidget` is under `FixedRoot`, has no `HudSway`, and its root anchored position is `(0,0)`.
- All CanvasGroups have `blocksRaycasts=false`; all Graphics have `raycastTarget=false`.

- [ ] **Step 6: Capture visual evidence including Screen Space Overlay UI**

Call `mcp__unityMCP__manage_camera` with no camera argument so ScreenCapture includes overlay UI:

```json
{
  "action":"screenshot",
  "capture_source":"game_view",
  "include_image":true,
  "max_resolution":1280,
  "output_folder":"Library/HudCaptures",
  "screenshot_file_name":"retro-terminal-hud.png"
}
```

Inspect the returned image. Acceptance criteria:

- Stamina and flashlight terminal plates use near-black fill, phosphor border/scanlines, cut corners, and Cascadia Mono.
- Stamina shows one continuous full-width fill beneath 11 visible divider overlays.
- Crosshair is centered and panel-free.
- The middle gameplay view is unobstructed; no full-screen tint or CRT overlay exists.
- Objective and prompt may be hidden in this scene because their data/prompt is absent.

If any criterion fails, fix only the exact authored dimensions/colors in
`HudSceneBuilder.cs` or `TerminalPanelGraphic.cs`, repeat that file’s compile check and focused
commit, then rebuild and recapture before continuing.

- [ ] **Step 7: Commit generated font and scene artifacts**

Run:

```powershell
git status --short
git diff --check -- . `
    ':(exclude)Assets/_Project/Art/UI.meta' `
    ':(exclude)Assets/_Project/Art/UI/**'
git add -- 'Assets/_Project/Art/UI/Fonts/CascadiaMono SDF.asset' 'Assets/_Project/Art/UI/Fonts/CascadiaMono SDF.asset.meta' 'Assets/Scenes/MainGame.unity'
git diff --cached --check -- . `
    ':(exclude)Assets/_Project/Art/UI.meta' `
    ':(exclude)Assets/_Project/Art/UI/**'
git commit -m "feat(ui): apply retro terminal HUD to MainGame"
```

Do not add `Library/HudCaptures/`.

---

### Task 7: Final static/editor verification and manual runtime handoff

**Files:**
- Verify only; no planned file changes.

**Interfaces:**
- Consumes: all prior task commits and the saved `MainGame` scene.
- Produces: evidence-backed completion boundary plus the project owner’s Play Mode checklist.

- [ ] **Step 1: Run repository-level checks**

```powershell
git diff --check e484d84..HEAD -- . `
    ':(exclude)Assets/TextMesh Pro.meta' `
    ':(exclude)Assets/TextMesh Pro/**' `
    ':(exclude)Assets/_Project/Art/UI.meta' `
    ':(exclude)Assets/_Project/Art/UI/**'
$ui = Get-Content -Raw 'Assets\_Project\Code\UI\Game.UI.asmdef' | ConvertFrom-Json
$gameplay = Get-Content -Raw 'Assets\_Project\Code\Gameplay\Game.Gameplay.asmdef' | ConvertFrom-Json
if ('Unity.TextMeshPro' -notin $ui.references) { throw 'Game.UI TMP reference missing' }
if ('Game.UI' -in $gameplay.references) { throw 'Dependency direction broken' }
if (rg -n 'UnityEngine\.UI\.Text|\bText label' 'Assets\_Project\Code\UI\Hud') {
    throw 'Legacy HUD Text remains'
}
if (-not (Select-String -LiteralPath 'Assets\_Project\Code\UI\Hud\StaminaWidget.cs' -Pattern 'fill\.fillAmount' -Quiet)) {
    throw 'Continuous stamina fill assignment missing'
}
git status --short
```

Expected: all assertions pass and working tree is clean.

- [ ] **Step 2: Take a fresh Unity snapshot**

Refresh Unity with compilation requested and `wait_for_ready=true`, then read only fresh error
entries from the console.

Expected:

- Zero errors.
- `MainGame` is loaded and not dirty.
- Hierarchy still contains one `HudCanvas`, four terminal panels, 11 separators, and one fixed crosshair.

- [ ] **Step 3: Confirm explicitly excluded changes**

Run:

```powershell
git diff --name-only e484d84..HEAD
git diff e484d84..HEAD -- 'Assets/_Project/Code/Gameplay' 'Assets/_Project/Code/UI/Fuse*' 'Assets/Scenes/*'
```

Expected:

- No gameplay runtime file or fuse minigame UI file changed.
- `Assets/Scenes/MainGame.unity` is the only changed scene.
- No PlayMode/EditMode test file was created or changed.

- [ ] **Step 4: Hand the owner this Play Mode checklist without claiming runtime success**

```text
1. Shift ile koş: stamina soldan sağa sürekli dolup azalsın; 12 hücreye sıçramasın.
2. Stamina tükenince dolgu #FF5A36 rengine geçip yaklaşık 1 Hz nabız atsın.
3. F tuşu: fener terminalinde AÇIK/KAPALI aynı karede güncellensin.
4. Etkileşilebilir nesneye bak: prompt tek bir "> " ile panel ve metin birlikte fade olsun.
5. Kamerayı hızlı çevir: köşe panelleri sway yapsın; crosshair tam merkezde kalsın.
6. Objective bulunan sahnede SİGORTA // 01/03 ilerlesin ve sonunda ÇIKIŞ // AÇIK olsun.
7. Objective bulunmayan MainGame'de sol üstte boş panel görünmesin.
8. Minigame, ölüm ve zafer state'lerinde HUD eski görünürlük davranışını korusun.
```

Runtime behavior is complete only after the project owner reports this checklist passing.

---

## Implementation Notes

- The standard TMP Essentials folder contains LiberationSans fallback assets, but no HUD object may reference them. Cascadia Mono is explicitly serialized on every terminal text.
- The stamina separators deliberately remain visible over both filled and empty regions. They divide the rail visually; they never quantize `fillAmount`.
- `ObjectiveManager` is absent from current `MainGame`; a hidden objective panel and one warning are expected, not a defect.
- Font creation is dynamic only during editor generation. The saved font asset is switched to `AtlasPopulationMode.Static` so `TMP Settings.m_ClearDynamicDataOnBuild` cannot erase its glyph atlas.
- Full TMP glow is intentionally omitted because the generated Mobile Distance Field material supports the required thin outline without adding a full Distance Field material preset. Panel phosphor contrast supplies the restrained glow impression.
- The only screenshot retained for review lives under ignored `Library/HudCaptures/`; no evidence image becomes a shipped asset.
