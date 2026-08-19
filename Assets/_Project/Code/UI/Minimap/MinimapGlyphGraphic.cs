using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Minimap
{
    public enum MinimapGlyphKind : byte
    {
        Player = 0,
        DirectionArrow = 1
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MinimapGlyphGraphic : MaskableGraphic
    {
        [SerializeField] private MinimapGlyphKind glyphKind;
        [SerializeField, Range(0.03f, 0.4f)] private float strokeRatio = 0.16f;

        public override bool raycastTarget
        {
            get => false;
            set => base.raycastTarget = false;
        }

        public MinimapGlyphKind GlyphKind
        {
            get => glyphKind;
            set
            {
                glyphKind = value;
                SetVerticesDirty();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            Color32 color32 = color;
            if (glyphKind == MinimapGlyphKind.Player)
            {
                AddPlayer(vertexHelper, rect, color32);
            }
            else
            {
                AddArrow(vertexHelper, rect, color32);
            }
        }

        private static void AddPlayer(VertexHelper vertexHelper, Rect rect, Color32 color32)
        {
            Vector2 tip = new Vector2(rect.center.x, rect.yMax);
            Vector2 left = new Vector2(rect.xMin, rect.yMin);
            Vector2 notch = new Vector2(rect.center.x, rect.yMin + rect.height * 0.27f);
            Vector2 right = new Vector2(rect.xMax, rect.yMin);
            int start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(CreateVertex(tip, color32));
            vertexHelper.AddVert(CreateVertex(left, color32));
            vertexHelper.AddVert(CreateVertex(notch, color32));
            vertexHelper.AddVert(CreateVertex(right, color32));
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }

        private void AddArrow(VertexHelper vertexHelper, Rect rect, Color32 color32)
        {
            float thickness = Mathf.Min(rect.width, rect.height) * strokeRatio;
            Vector2 tip = new Vector2(rect.center.x, rect.yMax);
            Vector2 left = new Vector2(rect.xMin, rect.yMin + rect.height * 0.18f);
            Vector2 right = new Vector2(rect.xMax, rect.yMin + rect.height * 0.18f);
            AddLine(vertexHelper, left, tip, thickness, color32);
            AddLine(vertexHelper, tip, right, thickness, color32);
        }

        private static void AddLine(
            VertexHelper vertexHelper,
            Vector2 start,
            Vector2 end,
            float thickness,
            Color32 color32)
        {
            Vector2 delta = end - start;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (thickness * 0.5f);
            int first = vertexHelper.currentVertCount;
            vertexHelper.AddVert(CreateVertex(start - normal, color32));
            vertexHelper.AddVert(CreateVertex(end - normal, color32));
            vertexHelper.AddVert(CreateVertex(end + normal, color32));
            vertexHelper.AddVert(CreateVertex(start + normal, color32));
            vertexHelper.AddTriangle(first, first + 1, first + 2);
            vertexHelper.AddTriangle(first + 2, first + 3, first);
        }

        private static UIVertex CreateVertex(Vector2 position, Color32 color32)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = new Vector3(position.x, position.y, 0f);
            vertex.color = color32;
            return vertex;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            raycastTarget = false;
            strokeRatio = Mathf.Clamp(strokeRatio, 0.03f, 0.4f);
            SetVerticesDirty();
        }
#endif
    }
}
