using Game.Gameplay.Navigation;
using Gloomveil.MapGeneration;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Minimap
{
    /// <summary>
    /// Void Cartography: sprite-free cyan map geometry with amber objective routing.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MinimapTabletGraphic : MaskableGraphic
    {
        private static readonly Color DefaultBackground = new Color32(3, 13, 19, 222);
        private static readonly Color DefaultWallGlow = new Color32(45, 224, 226, 48);
        private static readonly Color DefaultWall = new Color32(121, 244, 241, 225);
        private static readonly Color DefaultRouteGlow = new Color32(255, 174, 65, 58);
        private static readonly Color DefaultRoute = new Color32(255, 190, 91, 240);
        private static readonly Color DefaultStair = new Color32(164, 198, 205, 220);

        [SerializeField, HideInInspector, Min(0.5f)] private float visibleDiameterUnits = 28f;
        [SerializeField, Range(0.002f, 0.04f)] private float wallThicknessRatio = 0.008f;
        [SerializeField, Range(0.002f, 0.05f)] private float routeThicknessRatio = 0.012f;
        [SerializeField] private Color backgroundColor = DefaultBackground;
        [SerializeField] private Color wallGlowColor = DefaultWallGlow;
        [SerializeField] private Color wallColor = DefaultWall;
        [SerializeField] private Color routeGlowColor = DefaultRouteGlow;
        [SerializeField] private Color routeColor = DefaultRoute;
        [SerializeField] private Color stairColor = DefaultStair;

        private MinimapRuntimeSnapshot snapshot = MinimapRuntimeSnapshot.Empty;
        private float chargeNormalized = 1f;
        private float signalAlpha = 1f;
        private bool chargeLow;

        public override bool raycastTarget
        {
            get => false;
            set => base.raycastTarget = false;
        }

        public void SetSnapshot(MinimapRuntimeSnapshot value)
        {
            snapshot = value ?? MinimapRuntimeSnapshot.Empty;
            SetVerticesDirty();
        }

        public void SetView(float visibleMapUnits, float zoomLevel)
        {
            visibleDiameterUnits = MinimapViewportProjection.EffectiveVisibleDiameter(
                visibleMapUnits,
                zoomLevel);
            SetVerticesDirty();
        }

        public void SetChargeState(float normalized, bool isLow)
        {
            chargeNormalized = Mathf.Clamp01(normalized);
            chargeLow = isLow;
            SetVerticesDirty();
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

            signalAlpha = chargeLow
                ? Mathf.Lerp(0.52f, 1f, Mathf.PerlinNoise(Time.unscaledTime * 22f, 0.37f))
                : 1f;
            AddQuad(vertexHelper, rect, Tint(backgroundColor));
            AddFrame(vertexHelper, rect);
            if (!snapshot.IsReady)
            {
                DrawChargeIndicator(vertexHelper, rect);
                return;
            }

            float shortSide = Mathf.Min(rect.width, rect.height);
            float wallThickness = shortSide * wallThicknessRatio;
            float routeThickness = shortSide * routeThicknessRatio;
            DrawWalls(vertexHelper, rect, wallThickness);
            DrawExitRoute(vertexHelper, rect, routeThickness);
            DrawStairs(vertexHelper, rect, shortSide * 0.022f, wallThickness);
            DrawChargeIndicator(vertexHelper, rect);
        }

        private void DrawChargeIndicator(VertexHelper vertexHelper, Rect rect)
        {
            const int segmentCount = 3;
            float shortSide = Mathf.Min(rect.width, rect.height);
            float margin = shortSide * 0.035f;
            float segmentWidth = shortSide * 0.038f;
            float segmentHeight = shortSide * 0.018f;
            float gap = shortSide * 0.012f;
            float totalWidth = segmentCount * segmentWidth + (segmentCount - 1) * gap;
            float x = rect.xMax - margin - totalWidth;
            float y = rect.yMax - margin - segmentHeight;
            int activeSegments = chargeNormalized <= 0f
                ? 0
                : Mathf.CeilToInt(chargeNormalized * segmentCount);
            Color activeColor = chargeLow ? routeColor : wallColor;
            Color inactiveColor = wallGlowColor;
            inactiveColor.a = Mathf.Min(inactiveColor.a, 0.16f);

            for (int index = 0; index < segmentCount; index++)
            {
                var segment = new Rect(
                    x + index * (segmentWidth + gap),
                    y,
                    segmentWidth,
                    segmentHeight);
                AddQuad(
                    vertexHelper,
                    segment,
                    Tint(index < activeSegments ? activeColor : inactiveColor));
            }
        }

        private void DrawWalls(VertexHelper vertexHelper, Rect rect, float thickness)
        {
            foreach (MinimapWallSegment wall in snapshot.Topology.Walls)
            {
                if (wall.Floor != snapshot.CurrentFloor)
                {
                    continue;
                }

                Vector2 start = Project(wall.Start, rect);
                Vector2 end = Project(wall.End, rect);
                AddLine(vertexHelper, start, end, thickness * 2.8f, Tint(wallGlowColor));
                AddLine(vertexHelper, start, end, thickness, Tint(wallColor));
            }
        }

        private void DrawExitRoute(VertexHelper vertexHelper, Rect rect, float thickness)
        {
            for (int index = 1; index < snapshot.ExitRoute.Count; index++)
            {
                MinimapRoutePoint previous = snapshot.ExitRoute[index - 1];
                MinimapRoutePoint current = snapshot.ExitRoute[index];
                if (previous.Floor != snapshot.CurrentFloor ||
                    current.Floor != snapshot.CurrentFloor)
                {
                    continue;
                }

                Vector2 start = Project(previous.Position, rect);
                Vector2 end = Project(current.Position, rect);
                AddLine(vertexHelper, start, end, thickness * 2.8f, Tint(routeGlowColor));
                AddLine(vertexHelper, start, end, thickness, Tint(routeColor));
            }
        }

        private void DrawStairs(
            VertexHelper vertexHelper,
            Rect rect,
            float radius,
            float thickness)
        {
            foreach (MinimapStairMarker stair in snapshot.Topology.Stairs)
            {
                if (stair.Floor != snapshot.CurrentFloor)
                {
                    continue;
                }

                Vector2 centre = Project(stair.Position, rect);
                float direction = stair.Direction == MinimapStairDirection.Up ? 1f : -1f;
                Color32 color32 = Tint(stairColor);
                Vector2 left = centre + new Vector2(-radius, -direction * radius * 0.45f);
                Vector2 tip = centre + new Vector2(0f, direction * radius);
                Vector2 right = centre + new Vector2(radius, -direction * radius * 0.45f);
                AddLine(vertexHelper, left, tip, thickness, color32);
                AddLine(vertexHelper, tip, right, thickness, color32);
            }
        }

        private Vector2 Project(MinimapPoint2 point, Rect rect) =>
            Project(new Vector2(point.X2 * 0.5f, point.Z2 * 0.5f), rect);

        private Vector2 Project(Vector2 point, Rect rect) =>
            MinimapViewportProjection.ProjectMapPoint(
                point,
                snapshot.PlayerMapPosition,
                rect,
                visibleDiameterUnits);

        private void AddFrame(VertexHelper vertexHelper, Rect rect)
        {
            float thickness = Mathf.Min(rect.width, rect.height) * 0.008f;
            Color32 frame = Tint(wallGlowColor);
            AddLine(vertexHelper, new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMax, rect.yMin), thickness, frame);
            AddLine(vertexHelper, new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax), thickness, frame);
            AddLine(vertexHelper, new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMin, rect.yMax), thickness, frame);
            AddLine(vertexHelper, new Vector2(rect.xMin, rect.yMax),
                new Vector2(rect.xMin, rect.yMin), thickness, frame);
        }

        private static void AddLine(
            VertexHelper vertexHelper,
            Vector2 start,
            Vector2 end,
            float thickness,
            Color32 color)
        {
            Vector2 delta = end - start;
            if (delta.sqrMagnitude <= 0.000001f || thickness <= 0f)
            {
                return;
            }

            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (thickness * 0.5f);
            AddQuad(
                vertexHelper,
                start - normal,
                end - normal,
                end + normal,
                start + normal,
                color);
        }

        private static void AddQuad(VertexHelper vertexHelper, Rect rect, Color32 color) =>
            AddQuad(
                vertexHelper,
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMin, rect.yMax),
                color);

        private static void AddQuad(
            VertexHelper vertexHelper,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Vector2 d,
            Color32 color)
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

        private Color32 Tint(Color source)
        {
            Color tinted = source * color;
            tinted.a *= signalAlpha;
            return tinted;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            raycastTarget = false;
            visibleDiameterUnits = Mathf.Max(0.5f, visibleDiameterUnits);
            wallThicknessRatio = Mathf.Clamp(wallThicknessRatio, 0.002f, 0.04f);
            routeThicknessRatio = Mathf.Clamp(routeThicknessRatio, 0.002f, 0.05f);
            SetVerticesDirty();
        }
#endif
    }
}
