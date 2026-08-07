using System;

namespace Gloomveil.MapGeneration
{
    public sealed class PlacedStair
    {
        public int PlacementId { get; }
        public string FootprintId { get; }
        public int LowerFloor { get; }
        public int UpperFloor => LowerFloor + 1;
        public GridCoord3 SouthwestOrigin { get; }
        public GridSize2 CanonicalSize { get; }
        public int QuarterTurns { get; }
        public GridRect RotatedRect { get; }
        public MapPort? BottomPort { get; private set; }
        public MapPort? TopPort { get; private set; }
        public int BottomGraphNodeId { get; private set; } = -1;
        public int TopGraphNodeId { get; private set; } = -1;

        public PlacedStair(
            int placementId,
            string footprintId,
            int lowerFloor,
            int x,
            int z,
            GridSize2 canonicalSize,
            int quarterTurns)
        {
            if (placementId < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(placementId),
                    placementId,
                    "Placement ID must be positive.");
            }

            PlacementId = placementId;
            FootprintId = footprintId ?? throw new ArgumentNullException(nameof(footprintId));
            LowerFloor = lowerFloor;
            SouthwestOrigin = new GridCoord3(x, z, lowerFloor);
            CanonicalSize = canonicalSize;
            QuarterTurns = ((quarterTurns % 4) + 4) % 4;
            GridSize2 rotatedSize = canonicalSize.Rotate(QuarterTurns);
            RotatedRect = new GridRect(x, z, rotatedSize.Width, rotatedSize.Depth);
        }

        internal void SetPorts(MapPort bottomPort, MapPort topPort)
        {
            BottomPort = bottomPort;
            TopPort = topPort;
        }

        internal void SetGraphNodeIds(int bottomGraphNodeId, int topGraphNodeId)
        {
            BottomGraphNodeId = bottomGraphNodeId;
            TopGraphNodeId = topGraphNodeId;
        }
    }
}
