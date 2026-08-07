using System;

namespace Gloomveil.MapGeneration
{
    public static class MapPortGeometry
    {
        public static MapPort Create(
            int ownerPlacementId,
            SocketKey localSocket,
            GridSize2 canonicalSize,
            int quarterTurns,
            GridRect rotatedRect,
            int floor)
        {
            SocketKey rotatedSocket = localSocket.Rotate(canonicalSize, quarterTurns);
            GridCoord3 outside;
            GridEdgeKey edge;
            switch (rotatedSocket.Side)
            {
                case CardinalDirection.North:
                    outside = new GridCoord3(
                        rotatedRect.X + rotatedSocket.Index,
                        rotatedRect.MaxZ,
                        floor);
                    edge = new GridEdgeKey(
                        floor,
                        GridEdgeAxis.AlongX,
                        rotatedRect.X + rotatedSocket.Index,
                        rotatedRect.MaxZ);
                    break;
                case CardinalDirection.East:
                    outside = new GridCoord3(
                        rotatedRect.MaxX,
                        rotatedRect.Z + rotatedSocket.Index,
                        floor);
                    edge = new GridEdgeKey(
                        floor,
                        GridEdgeAxis.AlongZ,
                        rotatedRect.MaxX,
                        rotatedRect.Z + rotatedSocket.Index);
                    break;
                case CardinalDirection.South:
                    outside = new GridCoord3(
                        rotatedRect.X + rotatedSocket.Index,
                        rotatedRect.Z - 1,
                        floor);
                    edge = new GridEdgeKey(
                        floor,
                        GridEdgeAxis.AlongX,
                        rotatedRect.X + rotatedSocket.Index,
                        rotatedRect.Z);
                    break;
                case CardinalDirection.West:
                    outside = new GridCoord3(
                        rotatedRect.X - 1,
                        rotatedRect.Z + rotatedSocket.Index,
                        floor);
                    edge = new GridEdgeKey(
                        floor,
                        GridEdgeAxis.AlongZ,
                        rotatedRect.X,
                        rotatedRect.Z + rotatedSocket.Index);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(localSocket));
            }

            return new MapPort(
                ownerPlacementId,
                localSocket,
                rotatedSocket,
                edge,
                outside,
                rotatedSocket.Side);
        }
    }
}
