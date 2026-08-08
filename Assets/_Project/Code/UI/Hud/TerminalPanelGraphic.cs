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
